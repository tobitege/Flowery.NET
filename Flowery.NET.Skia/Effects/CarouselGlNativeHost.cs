using System.Reflection;
using System.Runtime.ExceptionServices;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Flowery.Controls;

namespace Flowery.Effects
{
    /// <summary>
    /// The base of the GL carousel hosts: while it is loaded, it runs a renderer session with the engine of
    /// <see cref="CreateEngine"/>, which draws SkSL runtime shaders on a canvas that fills the host, with the Skia
    /// renderer of Avalonia.
    /// </summary>
    public abstract class CarouselGlNativeHost : ContentControl
    {
        private CarouselGlSession? _session;
        private Task? _updateTask;
        private int _revision;
        private bool _inViewport = true;
        private bool _isEffectivelyVisible = true;

        // Each source is read and encoded once while the host is loaded: a property change, such as each tick of a
        // slider, configures the session again with the same images.
        private readonly Dictionary<string, string> _resolvedAssets = new(StringComparer.Ordinal);

        /// <summary>
        /// Initializes the host: the canvas fills it, and it takes no focus.
        /// </summary>
        protected CarouselGlNativeHost()
        {
            HorizontalContentAlignment = HorizontalAlignment.Stretch;
            VerticalContentAlignment = VerticalAlignment.Stretch;
            IsTabStop = false;
            // Loaded and Unloaded: the OnLoaded and OnUnloaded overrides.
            EffectiveViewportChanged += OnEffectiveViewportChanged;
            // Visual.IsEffectivelyVisibleChanged is internal; showing or hiding the host or a parent runs a layout pass.
            LayoutUpdated += OnLayoutUpdated;
        }

        /// <inheritdoc/>
        // A derived host has no control theme of its own; the ContentControl theme presents the canvas.
        protected override Type StyleKeyOverride => typeof(ContentControl);

        /// <summary>Gets the error that stopped the renderer last, or null.</summary>
        public Exception? LastRenderError { get; private set; }

        /// <summary>Gets whether the renderer session of the loaded host is initialized.</summary>
        public bool IsRendererInitialized => _session?.IsInitialized == true;

        /// <summary>Raised once for each error that stops the renderer; the host then shows the error message.</summary>
        public event EventHandler<Exception>? RenderFailed;

        /// <summary>
        /// Creates the engine of a renderer session; called once per session. It replaces the WinUI
        /// <c>EngineScript</c>, the JS engine that the WebView2 document ran.
        /// </summary>
        protected abstract CarouselGlEngine CreateEngine();

        /// <summary>
        /// Gets the shader sources by name that the engine reads as <see cref="CarouselGlEngine.NativeShaders"/>, or null
        /// for none.
        /// </summary>
        protected virtual IReadOnlyDictionary<string, string>? ShaderSources => null;

        /// <summary>
        /// Returns the effect to draw, or null for none; the host calls it again after <see cref="RefreshRenderer"/>.
        /// </summary>
        protected abstract CarouselGlConfiguration? CreateConfiguration();

        /// <summary>
        /// Configures the session again with the result of <see cref="CreateConfiguration"/>; call it when a property
        /// the configuration depends on changes.
        /// </summary>
        protected void RefreshRenderer()
        {
            _revision++;
            if (!IsLoaded || _updateTask is { IsCompleted: false }) return;
            _updateTask = UpdateRendererAsync();
        }

        /// <summary>
        /// Reads the frame at <paramref name="elapsedSeconds"/> since the start of the effect, or without a time the next
        /// frame of a running host and a fresh frame of a paused one.
        /// </summary>
        /// <param name="elapsedSeconds">The time in the animation, or null.</param>
        /// <exception cref="InvalidOperationException">The host is not loaded, has no effect, or its renderer failed. An error
        /// of the engine while it draws comes as the engine throws it.</exception>
        public async Task<CarouselGlFrame> ReadFrameAsync(double? elapsedSeconds = null)
        {
            if (!IsLoaded) throw new InvalidOperationException("The GL host must be loaded in an Avalonia window.");
            if (_updateTask is not null) await _updateTask;
            if (LastRenderError is not null) throw new InvalidOperationException("The native GL renderer failed.", LastRenderError);
            var session = _session ?? throw new InvalidOperationException("No GL effect is selected.");
            return await session.SampleAsync(elapsedSeconds);
        }

        /// <summary>
        /// Compiles and draws every program of the engine once and returns their names.
        /// </summary>
        /// <exception cref="InvalidOperationException">The host is not loaded, has no effect, or its renderer failed. An error
        /// of the engine while it draws comes as the engine throws it.</exception>
        public async Task<IReadOnlyList<string>> CompileShadersAsync()
        {
            await ReadFrameAsync();
            // The read frame resumes in a later dispatcher job; an unload before it closed the session.
            var session = _session ?? throw new InvalidOperationException("No GL effect is selected.");
            return await session.CompileAsync();
        }

        /// <summary>Reads an embedded text resource, such as a shader source.</summary>
        /// <param name="assembly">The assembly that embeds the resource.</param>
        /// <param name="resourceName">The full name of the resource.</param>
        /// <exception cref="FileNotFoundException">The assembly has no such resource.</exception>
        protected static string ReadEmbeddedText(Assembly assembly, string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException("GL resource not found.", resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private async Task UpdateRendererAsync()
        {
            CarouselGlSession? session = null;
            try
            {
                while (IsLoaded)
                {
                    var revision = _revision;
                    var configuration = CreateConfiguration();
                    if (configuration is null)
                    {
                        CloseRenderer();
                        return;
                    }
                    session = _session;
                    if (session is null)
                    {
                        session = new CarouselGlSession(CreateEngine(), ShaderSources);
                        _session = session;
                        session.Failed += OnRendererFailed;
                        Content = session.View;
                    }
                    try
                    {
                        session.Initialize();
                        var assets = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var asset in configuration.Assets)
                            assets.Add(asset.Key, await ResolveCachedAssetAsync(asset.Value));
                        // Only the images of the current effect stay cached, so switching through effects holds no others.
                        foreach (var path in _resolvedAssets.Keys.Except(configuration.Assets.Values).ToList())
                            _resolvedAssets.Remove(path);
                        configuration = configuration with { Assets = assets };
                        await session.ConfigureAsync(configuration);
                        session.SetActive(IsActive);
                    }
                    // An unload closed this session meanwhile, while an engine that still loaded its images waited for its
                    // first frame. A reload that came first found this update running and left the new session to it
                    // (WinUI gave up here, and the reloaded host had no renderer); after an unload alone the loop ends.
                    catch (Exception closed) when (closed is OperationCanceledException or ObjectDisposedException
                        && !ReferenceEquals(session, _session) && LastRenderError is null)
                    {
                        continue;
                    }
                    if (revision == _revision) return;
                }
            }
            catch (OperationCanceledException) when (!IsLoaded || _session is null) { }
            catch (ObjectDisposedException) when (!IsLoaded || _session is null) { }
            catch (Exception error)
            {
                // A failed session reports its error once: its Failed event may have closed it already.
                if (IsLoaded && (session is null || ReferenceEquals(session, _session))) OnRendererFailed(this, error);
            }
        }

        private async Task<string> ResolveCachedAssetAsync(string path)
        {
            if (_resolvedAssets.TryGetValue(path, out var resolved)) return resolved;
            resolved = await ResolveAssetAsync(path);
            // A read that ends after an unload keeps nothing: the unload cleared the cache for the next load.
            if (IsLoaded)
                _resolvedAssets[path] = resolved;
            return resolved;
        }

        private static async Task<string> ResolveAssetAsync(string path)
        {
            if (path.StartsWith("data:", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || path.StartsWith("http://", StringComparison.OrdinalIgnoreCase)) return path;
            string filePath;
            byte[] bytes;
            // Avalonia resources, for targets without application files such as Android and the browser.
            if (path.StartsWith("avares://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(path);
                await using var stream = AssetLoader.Open(uri);
                using var memory = new MemoryStream();
                await stream.CopyToAsync(memory);
                bytes = memory.ToArray();
                filePath = uri.AbsolutePath;
            }
            else
            {
                const string resourcePrefix = "ms-appx:///";
                var relativePath = path.StartsWith(resourcePrefix, StringComparison.OrdinalIgnoreCase)
                    ? Uri.UnescapeDataString(path[resourcePrefix.Length..])
                    : path;
                // A rooted path names its file: trimming its slash would make a Unix path relative.
                filePath = Path.IsPathFullyQualified(relativePath)
                    ? relativePath
                    : Path.Combine(AppContext.BaseDirectory, relativePath.TrimStart('/'));
                bytes = await File.ReadAllBytesAsync(filePath);
            }
            var mime = Path.GetExtension(filePath).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".svg" => "image/svg+xml",
                ".webp" => "image/webp",
                ".gif" => "image/gif",
                _ => "image/jpeg"
            };
            return "data:" + mime + ";base64," + Convert.ToBase64String(bytes);
        }

        /// <inheritdoc/>
        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            DaisyAccessibility.PreferencesChanged += OnMotionChanged;
            LastRenderError = null;
            RefreshRenderer();
        }

        /// <inheritdoc/>
        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            DaisyAccessibility.PreferencesChanged -= OnMotionChanged;
            _revision++;
            CloseRenderer();
            // An image file may change before the next load.
            _resolvedAssets.Clear();
        }

        private void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs args)
        {
            var viewport = args.EffectiveViewport;
            _inViewport = viewport.Width > 0 && viewport.Height > 0
                && viewport.X < Bounds.Width && viewport.Right > 0
                && viewport.Y < Bounds.Height && viewport.Bottom > 0;
            UpdateActivity();
        }

        private void OnMotionChanged(object? sender, EventArgs args) => UpdateActivity();

        private void OnLayoutUpdated(object? sender, EventArgs args)
        {
            if (_isEffectivelyVisible == IsEffectivelyVisible) return;
            _isEffectivelyVisible = IsEffectivelyVisible;
            UpdateActivity();
        }

        // !document.hidden: WebView2 hid the document of a collapsed host, which paused its draw loop. A hidden
        // Avalonia control still gets animation frames, so the host pauses while it or a parent is not visible.
        private bool IsActive => _inViewport && IsEffectivelyVisible && !DaisyAccessibility.IsReducedMotion;

        private void UpdateActivity()
        {
            var session = _session;
            if (session?.IsInitialized != true) return;
            try { session.SetActive(IsActive); }
            catch (Exception error) { OnRendererFailed(this, error); }
        }

        private void OnRendererFailed(object? sender, Exception error)
        {
            LastRenderError = error;
            CloseRenderer();
            RenderFailed?.Invoke(this, error);
            System.Diagnostics.Debug.WriteLine(error);
            Content = new TextBlock { Text = "GL renderer: " + error.Message, TextWrapping = TextWrapping.Wrap };
        }

        private void CloseRenderer()
        {
            var session = _session;
            _session = null;
            if (session is null) return;
            session.Failed -= OnRendererFailed;
            if (ReferenceEquals(Content, session.View)) Content = null;
            session.Dispose();
        }
    }

    /// <summary>
    /// A frame that a host read back.
    /// </summary>
    /// <param name="Renderer">The Skia backend of the last frame drawn on screen, such as "Skia OpenGL"; "Skia" before the first.</param>
    /// <param name="Frames">The number of frames drawn.</param>
    /// <param name="Pixel">The center pixel as non-premultiplied RGBA.</param>
    /// <param name="Width">The width of the drawing buffer in device pixels.</param>
    /// <param name="Height">The height of the drawing buffer in device pixels.</param>
    public sealed record CarouselGlFrame(string Renderer, int Frames, byte[] Pixel, int Width, int Height);

    /// <summary>
    /// The effect that a host draws, as its engine gets it in <see cref="CarouselGlEngine.AttachOverlay"/>.
    /// </summary>
    /// <param name="Mode">The program family of the effect.</param>
    /// <param name="Variant">The variant within the mode, such as <c>checkerboard</c>, or the name of a transition shader.</param>
    /// <param name="OverlayVariant">A continuous effect drawn into a mask, flip or transition, or null.</param>
    /// <param name="Assets">The images by asset key; the host passes them on resolved to data or http(s) URIs.</param>
    /// <param name="IntervalSeconds">The hold time between transitions; 0 holds none.</param>
    /// <param name="TransitionSeconds">The transition duration; 0 takes the default of the effect.</param>
    /// <param name="SliceCount">The tiles or slices of a mask; below 2 takes the default of the variant.</param>
    /// <param name="Stagger">Whether the slices of a mask start one after another.</param>
    /// <param name="StaggerMs">The delay between two slices in milliseconds.</param>
    /// <param name="PixelateSize">The block size of the pixelate mask in pixels.</param>
    /// <param name="DissolveDensity">The grain of the dissolve mask, from 0 to 1.</param>
    /// <param name="FlipAngle">The angle a flip turns, from 30 to 180 degrees.</param>
    public sealed record CarouselGlConfiguration(
        CarouselGlMode Mode,
        string? Variant,
        string? OverlayVariant,
        IReadOnlyDictionary<string, string> Assets,
        double IntervalSeconds,
        double TransitionSeconds,
        int SliceCount = 8,
        bool Stagger = true,
        double StaggerMs = 50,
        int PixelateSize = 20,
        double DissolveDensity = 0.5,
        double FlipAngle = 180);

    /// <summary>
    /// The renderer session of a host, the port of the WebView2 session and its JS bridge (flowery-native-gl.js). The
    /// engine runs in process, so the session calls it directly; only a frame of a running draw loop is waited for.
    /// </summary>
    internal sealed class CarouselGlSession : IDisposable
    {
        private readonly CarouselGlEngine _engine;
        private CarouselGlConfiguration? _configuration;
        private Exception? _failure;
        private bool _disposed;
        public CarouselGlCanvas View { get; } = new()
        {
            IsTabStop = false,
            IsHitTestVisible = false
        };
        public bool IsInitialized { get; private set; }
        public event EventHandler<Exception>? Failed;

        public CarouselGlSession(CarouselGlEngine engine, IReadOnlyDictionary<string, string>? shaders)
        {
            _engine = engine;
            // window.FloweryNativeShaders
            engine.NativeShaders = shaders ?? new Dictionary<string, string>();
        }

        // No WebView2 and no document to load: the bridge's start() subscribes its events, and the session is ready.
        public void Initialize()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (IsInitialized) return;
            View.Failed += OnCanvasFailed;
            // The ResizeObserver of the document: the canvas fills the host.
            View.SizeChanged += OnViewSizeChanged;
            IsInitialized = true;
        }

        /// <summary>
        /// Configures the engine and draws a frame at once (bridge <c>configure</c>). Only an engine that still loads its
        /// images is waited for; a window that draws no frames, such as a minimized one, does not hold the others.
        /// </summary>
        public async Task<CarouselGlFrame> ConfigureAsync(CarouselGlConfiguration value)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            _configuration = value;
            _engine.AttachOverlay(View, value);
            _engine.SetOverlayTiming(value.IntervalSeconds);
            _engine.SetOverlayTransitionDuration(value.TransitionSeconds);
            Resize();
            ThrowFailure();
            if (DrawFrame()) return View.ReadFrame();
            try { return await View.WaitFrameAsync(); }
            catch (InvalidOperationException) when (_failure is not null)
            {
                ThrowFailure();
                throw;
            }
        }

        // The first failure of the canvas, such as a missing shader, stands for the messages that follow it, such as the
        // fallback text of the engine.
        private void ThrowFailure()
        {
            if (_failure is not null) ExceptionDispatchInfo.Throw(_failure);
        }

        // Draws a frame at the current time, as sample does; returns whether the engine has one to draw.
        private bool DrawFrame()
        {
            if (View.State?.Render is not { } render) return false;
            render(CarouselGlCanvas.Now);
            return true;
        }

        /// <summary>
        /// Draws and reads a frame (bridge <c>sample</c>): at a time since the start of the effect, or without one the
        /// next frame of a running loop and a fresh frame of a paused one.
        /// </summary>
        public Task<CarouselGlFrame> SampleAsync(double? elapsedSeconds)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            var canvas = View;
            var state = canvas.State;
            if (state?.Render is not { } render) throw new InvalidOperationException("The GPU draw loop has not initialized.");
            if (elapsedSeconds is { } elapsed)
            {
                var origin = state.NativeStart ?? state.StartTime;
                if (origin is not { } start || !double.IsFinite(start)) throw new InvalidOperationException("The GPU animation clock is unavailable.");
                render(start + elapsed * 1000);
                return Task.FromResult(canvas.ReadFrame());
            }
            if (!state.Running)
            {
                render(CarouselGlCanvas.Now);
                return Task.FromResult(canvas.ReadFrame());
            }
            return canvas.WaitFrameAsync();
        }

        /// <summary>Compiles the engine's programs (bridge <c>compile</c>).</summary>
        public Task<IReadOnlyList<string>> CompileAsync()
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            return _engine.CompileAsync();
        }

        /// <summary>Runs or pauses the engine (bridge <c>active</c>).</summary>
        public void SetActive(bool active)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            // !document.hidden: Avalonia has no page visibility; TopLevel.RequestAnimationFrame does not call back
            // while the window does not render, as requestAnimationFrame in a hidden page.
            if (_configuration is not null) _engine.SetOverlayActive(active);
            // A request that waits for the next frame of the running loop (sample) would wait for good once the pause
            // cancelled that frame: the paused loop draws it.
            if (!active && View.HasFrameWaiters) DrawFrame();
        }

        private void Resize()
        {
            if (_configuration is null) return;
            // The canvas fills the host, so it needs no style size.
            _engine.Resize(View);
        }

        private void OnViewSizeChanged(object? sender, SizeChangedEventArgs args)
        {
            try
            {
                Resize();
                // The frame of a paused loop was drawn for the old size, and the canvas would stretch it over the new
                // one; a running loop draws the new size with its next frame.
                if (_configuration is not null && View.State is { Running: false }) DrawFrame();
            }
            catch (Exception error) { Post(error); }
        }

        private void OnCanvasFailed(object? sender, Exception error)
        {
            _failure ??= error;
            Post(error);
        }

        // post({ error }): the message reaches the host after the current work, as a WebView2 message does. A failure
        // during configure reaches the host through configure first, which closes the session.
        private void Post(Exception error) => Dispatcher.UIThread.Post(() => Fail(error));

        private void Fail(Exception error)
        {
            // A closed WebView2 sends no more messages.
            if (_disposed) return;
            Failed?.Invoke(this, error);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsInitialized = false;
            View.Failed -= OnCanvasFailed;
            View.SizeChanged -= OnViewSizeChanged;
            // View.Close(): the page's pagehide detaches the overlays, and the closed WebView2 drops the GL context.
            if (_configuration is not null) _engine.DetachAllOverlays();
            // Cancels the frames that requests still wait for.
            View.Clear();
        }
    }
}
