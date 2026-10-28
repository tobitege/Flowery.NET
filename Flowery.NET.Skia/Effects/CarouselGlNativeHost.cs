using System.Reflection;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using Flowery.Controls;

namespace Flowery.Effects
{
    /// <summary>Owns a GPU canvas that draws SkSL runtime shaders with the Skia renderer of Avalonia.</summary>
    public abstract class CarouselGlNativeHost : ContentControl
    {
        private CarouselGlSession? _session;
        private Task? _updateTask;
        private int _revision;
        private bool _inViewport = true;
        private bool _isEffectivelyVisible = true;

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

        // A derived host has no control theme of its own; the ContentControl theme presents the canvas.
        protected override Type StyleKeyOverride => typeof(ContentControl);

        public Exception? LastRenderError { get; private set; }
        public bool IsRendererInitialized => _session?.IsInitialized == true;
        public event EventHandler<Exception>? RenderFailed;

        /// <summary>
        /// Creates the engine of a renderer session; called once per session. It replaces the WinUI
        /// <c>EngineScript</c>, the JS engine that the WebView2 document ran.
        /// </summary>
        protected abstract CarouselGlEngine CreateEngine();
        protected virtual IReadOnlyDictionary<string, string>? ShaderSources => null;
        protected abstract CarouselGlConfiguration? CreateConfiguration();

        protected void RefreshRenderer()
        {
            _revision++;
            if (!IsLoaded || _updateTask is { IsCompleted: false }) return;
            _updateTask = UpdateRendererAsync();
        }

        public async Task<CarouselGlFrame> ReadFrameAsync(double? elapsedSeconds = null)
        {
            if (!IsLoaded) throw new InvalidOperationException("The GL host must be loaded in an Avalonia window.");
            if (_updateTask is not null) await _updateTask;
            if (LastRenderError is not null) throw new InvalidOperationException("The native GL renderer failed.", LastRenderError);
            var session = _session ?? throw new InvalidOperationException("No GL effect is selected.");
            return await session.SampleAsync(elapsedSeconds);
        }

        public async Task<IReadOnlyList<string>> CompileShadersAsync()
        {
            await ReadFrameAsync();
            // The read frame resumes in a later dispatcher job; an unload before it closed the session.
            var session = _session ?? throw new InvalidOperationException("No GL effect is selected.");
            return await session.CompileAsync();
        }

        protected static string ReadEmbeddedText(Assembly assembly, string resourceName)
        {
            using var stream = assembly.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException("GL resource not found.", resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // ReadBridgeScript: no counterpart; the session is the bridge.

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
                        await session.InitializeAsync();
                        var assets = new Dictionary<string, string>(StringComparer.Ordinal);
                        foreach (var asset in configuration.Assets)
                            assets.Add(asset.Key, await ResolveAssetAsync(asset.Value));
                        configuration = configuration with { Assets = assets };
                        await session.ConfigureAsync(configuration);
                        await session.SetActiveAsync(IsActive);
                    }
                    // An unload closed this session meanwhile. A reload that came first found this update running and
                    // left the new session to it (WinUI gave up here, and the reloaded host had no renderer); after an
                    // unload alone the loop ends.
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
                // A failed session reports its error once: its Failed event may have closed it already, and the
                // rejected request then carries the same error (WinUI reported it twice).
                if (IsLoaded && (session is null || ReferenceEquals(session, _session))) OnRendererFailed(this, error);
            }
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

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            DaisyAccessibility.PreferencesChanged += OnMotionChanged;
            LastRenderError = null;
            RefreshRenderer();
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            base.OnUnloaded(e);
            DaisyAccessibility.PreferencesChanged -= OnMotionChanged;
            _revision++;
            CloseRenderer();
        }

        private async void OnEffectiveViewportChanged(object? sender, EffectiveViewportChangedEventArgs args)
        {
            var viewport = args.EffectiveViewport;
            _inViewport = viewport.Width > 0 && viewport.Height > 0
                && viewport.X < Bounds.Width && viewport.Right > 0
                && viewport.Y < Bounds.Height && viewport.Bottom > 0;
            await UpdateActivityAsync();
        }

        private async void OnMotionChanged(object? sender, EventArgs args) => await UpdateActivityAsync();

        private async void OnLayoutUpdated(object? sender, EventArgs args)
        {
            if (_isEffectivelyVisible == IsEffectivelyVisible) return;
            _isEffectivelyVisible = IsEffectivelyVisible;
            await UpdateActivityAsync();
        }

        // !document.hidden: WebView2 hid the document of a collapsed host, which paused its draw loop. A hidden
        // Avalonia control still gets animation frames, so the host pauses while it or a parent is not visible.
        private bool IsActive => _inViewport && IsEffectivelyVisible && !DaisyAccessibility.IsReducedMotion;

        private async Task UpdateActivityAsync()
        {
            var session = _session;
            if (session?.IsInitialized != true) return;
            try { await session.SetActiveAsync(IsActive); }
            catch (OperationCanceledException) when (!ReferenceEquals(session, _session)) { }
            catch (ObjectDisposedException) when (!ReferenceEquals(session, _session)) { }
            catch (Exception error) { if (ReferenceEquals(session, _session)) OnRendererFailed(this, error); }
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

    public sealed record CarouselGlFrame(string Renderer, int Frames, byte[] Pixel, int Width, int Height);

    public sealed record CarouselGlConfiguration(
        string Api,
        string Mode,
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
    /// The renderer session of a host: the port of the WebView2 session and its JS bridge (flowery-native-gl.js).
    /// The bridge's commands are typed methods; each runs as a request with the timeout of the WebView2 messages.
    /// </summary>
    internal sealed class CarouselGlSession : IDisposable
    {
        private readonly CarouselGlEngine _engine;
        private readonly Dictionary<int, TaskCompletionSource<object?>> _requests = new();
        private Task? _initialization;
        private CarouselGlConfiguration? _configuration;
        private bool _requestedActive = true;
        private int _nextRequest;
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

        public Task InitializeAsync() => _initialization ??= InitializeCoreAsync();

        // No WebView2 and no document to load: the bridge's start() subscribes its events, and the session is ready.
        private Task InitializeCoreAsync()
        {
            if (_disposed) return Task.FromException(new ObjectDisposedException(nameof(CarouselGlSession)));
            View.Failed += OnCanvasFailed;
            // The ResizeObserver of the document: the canvas fills the host.
            View.SizeChanged += OnViewSizeChanged;
            IsInitialized = true;
            return Task.CompletedTask;
        }

        /// <summary>Configures the engine and completes with its next frame (bridge <c>configure</c>).</summary>
        public Task<CarouselGlFrame> ConfigureAsync(CarouselGlConfiguration configuration) =>
            RequestAsync(() => Configure(configuration));

        /// <summary>Draws and reads a frame (bridge <c>sample</c>).</summary>
        public Task<CarouselGlFrame> SampleAsync(double? elapsedSeconds) => RequestAsync(() => Sample(elapsedSeconds));

        /// <summary>Compiles the engine's programs (bridge <c>compile</c>).</summary>
        public Task<IReadOnlyList<string>> CompileAsync() => RequestAsync(_engine.CompileAsync);

        /// <summary>Runs or pauses the engine (bridge <c>active</c>).</summary>
        public Task<bool> SetActiveAsync(bool active) => RequestAsync(() => SetActive(active));

        private async Task<T> RequestAsync<T>(Func<Task<T>> command)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            await InitializeAsync();
            var id = ++_nextRequest;
            var completion = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
            _requests.Add(id, completion);
            try
            {
                _ = HandleAsync(completion, command);
                return (T)(await completion.Task.WaitAsync(TimeSpan.FromSeconds(30)))!;
            }
            finally { _requests.Remove(id); }
        }

        // The bridge's message listener: post({ id, result }) or post({ id, error }). The answer is a message as well, so
        // it reaches the host after the errors that the command posted, as WebView2 delivers its messages in order.
        private static async Task HandleAsync<T>(TaskCompletionSource<object?> completion, Func<Task<T>> command)
        {
            try
            {
                var result = await command();
                Dispatcher.UIThread.Post(() => completion.TrySetResult(result));
            }
            catch (Exception error)
            {
                Dispatcher.UIThread.Post(() => completion.TrySetException(new InvalidOperationException(error.Message, error)));
            }
        }

        private async Task<CarouselGlFrame> Configure(CarouselGlConfiguration value)
        {
            _configuration = value;
            _engine.AttachOverlay(View, value);
            _engine.SetOverlayTiming(value.IntervalSeconds);
            _engine.SetOverlayTransitionDuration(value.TransitionSeconds);
            Resize();
            // A paused draw loop draws no next frame, so draw this one now, as sample does; WinUI waited for the
            // frame until its request timed out and failed the renderer.
            if (DrawPausedFrame()) return View.ReadFrame();
            return await View.WaitFrameAsync();
        }

        // Draws a frame of a paused draw loop at the current time, as sample does; returns whether it drew. A paused
        // loop draws only where its frame changes: a request that needs a frame, a new size and a new program.
        private bool DrawPausedFrame()
        {
            if (View.State is not { Running: false, Render: { } render }) return false;
            render(CarouselGlCanvas.Now);
            return true;
        }

        private Task<CarouselGlFrame> Sample(double? elapsedSeconds)
        {
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

        private Task<bool> SetActive(bool active)
        {
            _requestedActive = active;
            // !document.hidden: Avalonia has no page visibility; TopLevel.RequestAnimationFrame does not call back
            // while the window does not render, as requestAnimationFrame in a hidden page.
            if (_configuration is not null) _engine.SetOverlayActive(_requestedActive);
            // A request that waits for the next frame of the running loop (configure, sample) would wait for good once
            // the pause cancelled that frame, until its timeout failed the renderer: the paused loop draws it.
            if (!active && View.HasFrameWaiters) DrawPausedFrame();
            return Task.FromResult(true);
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
                if (_configuration is not null) DrawPausedFrame();
            }
            catch (Exception error) { Post(error); }
        }

        private void OnCanvasFailed(object? sender, Exception error) => Post(error);

        // post({ error }): the message reaches the host after the current work, as a WebView2 message does.
        private void Post(Exception error) => Dispatcher.UIThread.Post(() => Fail(error));

        private void Fail(Exception error)
        {
            // A closed WebView2 sends no more messages.
            if (_disposed) return;
            foreach (var request in _requests.Values) request.TrySetException(error);
            Failed?.Invoke(this, error);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            IsInitialized = false;
            foreach (var request in _requests.Values) request.TrySetCanceled();
            View.Failed -= OnCanvasFailed;
            View.SizeChanged -= OnViewSizeChanged;
            // View.Close(): the page's pagehide detaches the overlays, and the closed WebView2 drops the GL context.
            if (_configuration is not null) _engine.DetachAllOverlays();
            View.Clear();
        }
    }
}
