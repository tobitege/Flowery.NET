using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using SkiaSharp;

namespace Flowery.Effects
{
    /// <summary>
    /// A carousel GL engine: the API that the renderer session of a <see cref="CarouselGlNativeHost"/> calls, as the
    /// WinUI bridge (flowery-native-gl.js) calls the engine objects <c>FloweryCarouselGL</c> and
    /// <c>FloweryCarouselGLTransitions</c>. An engine draws on a <see cref="CarouselGlCanvas"/> with SkSL runtime shaders.
    /// A host creates one engine per session in <see cref="CarouselGlNativeHost.CreateEngine"/>; the engine keeps the
    /// single overlay entry of that session (the bridge's key <c>"native-host"</c>). Only the session calls the
    /// members; a derived engine in another assembly overrides them as <c>protected</c>.
    /// </summary>
    public abstract class CarouselGlEngine
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyShaders = new Dictionary<string, string>();
        private static readonly HttpClient Http = new();

        /// <summary>
        /// The shader sources of the host (<see cref="CarouselGlNativeHost.ShaderSources"/>; the bridge's
        /// <c>window.FloweryNativeShaders</c>), by name. Empty when the host has none.
        /// </summary>
        protected internal IReadOnlyDictionary<string, string> NativeShaders { get; internal set; } = EmptyShaders;

        /// <summary>
        /// Attaches the overlay entry to <paramref name="canvas"/>, the canvas of the session, and draws
        /// <paramref name="configuration"/> on it (JS <c>attachOverlay</c>). A new mode, variant or asset set starts
        /// the effect again; other changes update the running one.
        /// </summary>
        /// <param name="canvas">The canvas of the session; the same canvas on every call.</param>
        /// <param name="configuration">The effect, with its assets resolved to data or http(s) URIs.</param>
        protected internal abstract void AttachOverlay(CarouselGlCanvas canvas, CarouselGlConfiguration configuration);

        /// <summary>Sets the hold time between transitions, in seconds (JS <c>setOverlayTiming</c>).</summary>
        /// <param name="intervalSec">The hold time; anything but a positive number means none.</param>
        protected internal abstract void SetOverlayTiming(double intervalSec);

        /// <summary>Sets the transition duration, in seconds (JS <c>setOverlayTransitionDuration</c>).</summary>
        /// <param name="transitionSec">The duration; anything but a positive number selects the effect's default.</param>
        protected internal abstract void SetOverlayTransitionDuration(double transitionSec);

        /// <summary>Resumes or pauses the draw loop of the overlay and shows or hides its canvas (JS <c>setOverlayActive</c>).</summary>
        /// <param name="isActive">Whether the effect runs.</param>
        protected internal abstract void SetOverlayActive(bool isActive);

        /// <summary>Resizes the drawing of the overlay on <paramref name="canvas"/> to its display size (JS <c>resize</c>).</summary>
        /// <param name="canvas">The canvas whose size changed.</param>
        protected internal abstract void Resize(CarouselGlCanvas canvas);

        /// <summary>
        /// Compiles and draws every program of the engine once on canvases of its own and returns their names
        /// (JS <c>compile</c>); fails with the first error.
        /// </summary>
        protected internal abstract Task<IReadOnlyList<string>> CompileAsync();

        /// <summary>Stops every overlay of the engine and releases what it holds (JS <c>detachAllOverlays</c>).</summary>
        protected internal abstract void DetachAllOverlays();

        /// <summary>
        /// Compiles an SkSL shader program (the role of JS <c>createProgram</c>). A compile error throws an
        /// <see cref="InvalidOperationException"/> with the Skia error text, which a host keeps visible.
        /// </summary>
        /// <param name="source">The SkSL source of a shader with <c>half4 main(float2 p)</c>.</param>
        protected static SKRuntimeEffect CreateProgram(string source) =>
            SKRuntimeEffect.CreateShader(source, out var error)
            ?? throw new InvalidOperationException(string.IsNullOrEmpty(error) ? "Shader compile failed" : error);

        /// <summary>
        /// Decodes the image of a data URI, or downloads an http(s) or protocol-relative URL (the image loading of
        /// JS <c>loadImage</c>). Throws "Failed to load image: " and <paramref name="source"/> when it is no image.
        /// </summary>
        /// <param name="source">A <c>data:</c> URI, an <c>http(s)://</c> URL or a URL that starts with <c>//</c>.</param>
        protected static async Task<SKImage> DecodeImageAsync(string source)
        {
            byte[]? bytes = null;
            try
            {
                if (source.StartsWith("data:", StringComparison.Ordinal))
                    bytes = DecodeDataUri(source);
                else if (source.StartsWith("//", StringComparison.Ordinal))
                    bytes = await Http.GetByteArrayAsync("https:" + source);
                else
                    bytes = await Http.GetByteArrayAsync(source);
            }
            catch (Exception error) when (error is FormatException or HttpRequestException or ArgumentException or UriFormatException)
            {
                throw new InvalidOperationException("Failed to load image: " + source, error);
            }
            var encoded = (bytes is null ? null : SKImage.FromEncodedData(bytes))
                ?? throw new InvalidOperationException("Failed to load image: " + source);
            // Decoded once, as a loaded Image element is; a lazy image could decode again on every software draw.
            var raster = encoded.ToRasterImage(true);
            if (!ReferenceEquals(raster, encoded))
                encoded.Dispose();
            return raster ?? throw new InvalidOperationException("Failed to load image: " + source);
        }

        /// <summary>
        /// Creates the child shader that samples <paramref name="image"/> like a GL texture with linear filtering and
        /// clamped edges: it takes normalized coordinates from the top left. GL uploads images flipped, so
        /// <c>texture2D(t, uv)</c> is <c>unpremul(t.eval(float2(uv.x, 1.0 - uv.y)))</c> in SkSL.
        /// </summary>
        /// <param name="image">The image; the shader keeps it alive natively.</param>
        protected static SKShader CreateTextureShader(SKImage image) =>
            image.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp, new SKSamplingOptions(SKFilterMode.Linear),
                SKMatrix.CreateScale(1f / image.Width, 1f / image.Height));

        /// <summary>
        /// Draws one frame (the role of <c>gl.drawArrays</c>): makes <paramref name="program"/> a shader with
        /// <paramref name="uniforms"/> and a child shader of each texture (<see cref="CreateTextureShader"/>),
        /// releases those children and presents the frame on <paramref name="canvas"/>, which takes it over.
        /// </summary>
        /// <param name="canvas">The canvas that presents the frame.</param>
        /// <param name="program">The compiled program.</param>
        /// <param name="uniforms">The uniforms of the frame; the caller disposes them.</param>
        /// <param name="textures">The child shader names of the program and their images.</param>
        protected static void DrawArrays(CarouselGlCanvas canvas, SKRuntimeEffect program, SKRuntimeEffectUniforms uniforms,
            params (string Name, SKImage Image)[] textures)
        {
            var shaders = new SKShader[textures.Length];
            try
            {
                using var children = new SKRuntimeEffectChildren(program);
                for (var i = 0; i < textures.Length; i++)
                {
                    shaders[i] = CreateTextureShader(textures[i].Image);
                    children[textures[i].Name] = shaders[i];
                }
                // The frame shader refers to the native children; their wrappers are released below.
                canvas.Present(program.ToShader(uniforms, children)
                    ?? throw new InvalidOperationException("Skia could not create the frame shader."));
            }
            finally
            {
                foreach (var shader in shaders)
                    shader?.Dispose();
            }
        }

        private static byte[] DecodeDataUri(string source)
        {
            var comma = source.IndexOf(',');
            if (comma < 0)
                throw new FormatException("The data URI has no data.");
            var header = source[5..comma];
            var data = source[(comma + 1)..];
            return header.EndsWith(";base64", StringComparison.OrdinalIgnoreCase)
                ? Convert.FromBase64String(Uri.UnescapeDataString(data))
                : System.Text.Encoding.UTF8.GetBytes(Uri.UnescapeDataString(data));
        }
    }

    /// <summary>
    /// What the session reads of the engine state that draws on a <see cref="CarouselGlCanvas"/> (JS
    /// <c>canvas._floweryGl</c> or <c>canvas._floweryGlTransitions</c>) to sample a frame.
    /// </summary>
    public interface ICarouselGlState
    {
        /// <summary>
        /// Draws a frame at a time in milliseconds of <see cref="CarouselGlCanvas.Now"/> (JS <c>state._render</c>);
        /// null until the images loaded and after the state stopped.
        /// </summary>
        Action<double>? Render { get; }

        /// <summary>Whether the draw loop runs (JS <c>state.running</c>).</summary>
        bool Running { get; }

        /// <summary>The time the effect started, in milliseconds of <see cref="CarouselGlCanvas.Now"/> (JS <c>state.nativeStart</c>).</summary>
        double? NativeStart { get; }

        /// <summary>The start of the transition cycle, in milliseconds of <see cref="CarouselGlCanvas.Now"/> (JS <c>state.startTime</c>).</summary>
        double? StartTime { get; }
    }

    /// <summary>
    /// The GL canvas of a carousel effect (the bridge's <c>&lt;canvas&gt;</c> and its helpers): it shows the frame
    /// shader an engine presents, stretched over its bounds, and reads frames back. A frame shader draws over
    /// (0, 0) to <see cref="Resolution"/> in local coordinates; pass that size as <c>u_resolution</c>.
    /// </summary>
    public sealed class CarouselGlCanvas : Control
    {
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private readonly Dictionary<int, FrameRequest> _frameRequests = new();
        private List<TaskCompletionSource<CarouselGlFrame>> _nativeWaiters = [];
        private CarouselGlShaderHolder? _current;
        private string? _nativeError;
        private string _renderer = "Skia";
        private int _nextFrameRequest;

        /// <summary>The monotonic clock of the draw loops, in milliseconds (JS <c>performance.now()</c>).</summary>
        public static double Now => Clock.Elapsed.TotalMilliseconds;

        /// <summary>The width of the drawing buffer in device pixels (JS <c>canvas.width</c>).</summary>
        public int PixelWidth { get; private set; } = 1;

        /// <summary>The height of the drawing buffer in device pixels (JS <c>canvas.height</c>).</summary>
        public int PixelHeight { get; private set; } = 1;

        /// <summary>
        /// The size of the drawing buffer in local units: <see cref="PixelWidth"/> and <see cref="PixelHeight"/> divided
        /// by the render scaling. Frame shaders draw over (0, 0) to this size.
        /// </summary>
        public Size Resolution { get; private set; } = new(1, 1);

        /// <summary>The number of frames drawn (JS <c>state.nativeFrames</c>).</summary>
        public int Frames { get; private set; }

        /// <summary>
        /// The Skia backend of the last frame drawn on screen ("Skia OpenGL", "Skia Direct3D", "Skia Vulkan",
        /// "Skia Metal", or "Skia CPU" without a GPU context); "Skia" before the first one.
        /// </summary>
        public string Renderer => Volatile.Read(ref _renderer);

        /// <summary>The error that stopped this canvas, or null (JS <c>canvas._nativeError</c>).</summary>
        public string? Error => _nativeError;

        /// <summary>
        /// The engine state that draws on this canvas (JS <c>canvas._floweryGl</c>): an engine sets it when it starts
        /// an effect and clears it when it stops the effect.
        /// </summary>
        public ICarouselGlState? State { get; set; }

        /// <summary>Raised when the canvas fails (the bridge's <c>post({ error })</c>).</summary>
        public event EventHandler<Exception>? Failed;

        /// <summary>Whether <see cref="WaitFrameAsync"/> calls wait for the next frame.</summary>
        internal bool HasFrameWaiters => _nativeWaiters.Count > 0;

        /// <summary>
        /// Returns the display size of the canvas in local units: the larger of its bounds and a finite
        /// <see cref="Layoutable.Width"/> and <see cref="Layoutable.Height"/>, so that a detached canvas with a size
        /// draws at that size (JS <c>getCanvasDisplaySize</c>).
        /// </summary>
        public Size GetCanvasDisplaySize()
        {
            var width = Math.Max(Bounds.Width, double.IsFinite(Width) ? Width : 0);
            var height = Math.Max(Bounds.Height, double.IsFinite(Height) ? Height : 0);
            return new Size(width, height);
        }

        /// <summary>
        /// Sizes the drawing buffer to the display size in device pixels, at least one pixel; returns whether it
        /// changed (JS <c>resizeCanvasToDisplaySize</c>).
        /// </summary>
        public bool ResizeCanvasToDisplaySize()
        {
            var dpr = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
            var size = GetCanvasDisplaySize();
            var width = (int)Math.Max(1, Math.Floor(size.Width * dpr));
            var height = (int)Math.Max(1, Math.Floor(size.Height * dpr));
            Resolution = new Size(width / dpr, height / dpr);
            if (PixelWidth != width || PixelHeight != height)
            {
                PixelWidth = width;
                PixelHeight = height;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Calls <paramref name="callback"/> once before the next frame with <see cref="Now"/> and returns a handle for
        /// <see cref="CancelAnimationFrame"/> (JS <c>requestAnimationFrame</c>). A detached canvas waits 16 ms instead.
        /// </summary>
        /// <param name="callback">The callback; it gets the time in milliseconds.</param>
        public int RequestAnimationFrame(Action<double> callback)
        {
            var handle = ++_nextFrameRequest;
            var request = new FrameRequest(this, handle, callback);
            _frameRequests[handle] = request;
            if (TopLevel.GetTopLevel(this) is { } topLevel)
                topLevel.RequestAnimationFrame(request.OnFrame);
            else
                request.StartTimer();
            return handle;
        }

        /// <summary>Cancels a callback of <see cref="RequestAnimationFrame"/> (JS <c>cancelAnimationFrame</c>).</summary>
        /// <param name="handle">The handle that <see cref="RequestAnimationFrame"/> returned.</param>
        public void CancelAnimationFrame(int handle)
        {
            if (_frameRequests.Remove(handle, out var request))
                request.StopTimer();
        }

        /// <summary>
        /// Shows <paramref name="shader"/> as the current frame, drawn over (0, 0) to <see cref="Resolution"/>. The
        /// canvas takes the shader over and disposes it once no draw uses it any more.
        /// </summary>
        /// <param name="shader">The frame shader.</param>
        public void Present(SKShader shader)
        {
            var previous = _current;
            _current = new CarouselGlShaderHolder(shader, Resolution);
            previous?.Release();
            InvalidateVisual();
        }

        /// <summary>
        /// Releases the current frame and cancels pending <see cref="WaitFrameAsync"/> calls; the canvas then shows
        /// nothing (JS <c>destroyContext</c>).
        /// </summary>
        public void Clear()
        {
            var previous = _current;
            _current = null;
            previous?.Release();
            var waiters = _nativeWaiters;
            _nativeWaiters = [];
            foreach (var waiter in waiters)
                waiter.TrySetCanceled();
            InvalidateVisual();
        }

        /// <summary>Counts a drawn frame and completes the waiting <see cref="WaitFrameAsync"/> calls with it (JS <c>frame</c>).</summary>
        public void Frame()
        {
            Frames++;
            var waiters = _nativeWaiters;
            if (waiters.Count == 0)
                return;
            try
            {
                var result = ReadFrame();
                _nativeWaiters = [];
                foreach (var waiter in waiters)
                    waiter.TrySetResult(result);
            }
            catch (Exception error) { Fail(error); }
        }

        /// <summary>Stops this canvas with <paramref name="error"/>: fails the waiting frames and raises <see cref="Failed"/> (JS <c>fail</c>).</summary>
        /// <param name="error">The error.</param>
        public void Fail(Exception error) => Fail(error.Message, error);

        /// <summary>Stops this canvas with an error <paramref name="message"/> (JS <c>fail</c>).</summary>
        /// <param name="message">The error message.</param>
        public void Fail(string message) => Fail(message, null);

        /// <summary>
        /// Stops this canvas with <paramref name="message"/> (JS <c>drawFallback</c>). The JS canvas holds its GL
        /// context then, so <c>getContext("2d")</c> returns null and no text is drawn; the host shows the message.
        /// </summary>
        /// <param name="message">The message.</param>
        public void DrawFallback(string message) => Fail(new InvalidOperationException(message));

        /// <summary>Completes with the next drawn frame, or fails at once when the canvas failed (JS <c>waitFrame</c>).</summary>
        public Task<CarouselGlFrame> WaitFrameAsync()
        {
            if (_nativeError is not null)
                return Task.FromException<CarouselGlFrame>(new InvalidOperationException(_nativeError));
            var waiter = new TaskCompletionSource<CarouselGlFrame>(TaskCreationOptions.RunContinuationsAsynchronously);
            _nativeWaiters.Add(waiter);
            return waiter.Task;
        }

        /// <summary>
        /// Reads the center pixel of the current frame as non-premultiplied RGBA (JS <c>readFrame</c>). GL reads
        /// (floor(w / 2), floor(h / 2)) from the bottom; the frame shader is drawn into a raster pixel at that place.
        /// </summary>
        public CarouselGlFrame ReadFrame() =>
            new(Renderer, Frames, ReadPixel(PixelWidth / 2, PixelHeight - 1 - PixelHeight / 2), PixelWidth, PixelHeight);

        /// <summary>
        /// Reads the pixel in column <paramref name="x"/> and row <paramref name="y"/> from the top of the current frame
        /// as non-premultiplied RGBA, transparent without a frame.
        /// </summary>
        internal byte[] ReadPixel(int x, int y)
        {
            var pixel = new byte[4];
            if (_current is { } holder)
            {
                var info = new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
                using var surface = SKSurface.Create(info)
                    ?? throw new InvalidOperationException("Skia draw/readback failed: no raster surface.");
                var canvas = surface.Canvas;
                canvas.Clear(SKColors.Transparent);
                canvas.Translate(-x, -y);
                canvas.Scale((float)(PixelWidth / holder.Resolution.Width), (float)(PixelHeight / holder.Resolution.Height));
                using (var paint = new SKPaint { Shader = holder.Shader, BlendMode = SKBlendMode.Src })
                    canvas.DrawRect(SKRect.Create((float)holder.Resolution.Width, (float)holder.Resolution.Height), paint);
                var handle = GCHandle.Alloc(pixel, GCHandleType.Pinned);
                try
                {
                    if (!surface.ReadPixels(info, handle.AddrOfPinnedObject(), 4, 0, 0))
                        throw new InvalidOperationException("Skia draw/readback failed.");
                }
                finally
                {
                    handle.Free();
                }
            }
            return pixel;
        }

        /// <inheritdoc/>
        public override void Render(DrawingContext context)
        {
            if (_current is { } holder && Bounds.Width > 0 && Bounds.Height > 0)
                context.Custom(new CarouselGlDrawOperation(this, holder, new Rect(Bounds.Size)));
        }

        internal void ReportRenderer(string renderer) => Volatile.Write(ref _renderer, renderer);

        private void Fail(string message, Exception? error)
        {
            _nativeError = message;
            var waiters = _nativeWaiters;
            _nativeWaiters = [];
            foreach (var waiter in waiters)
                waiter.TrySetException(new InvalidOperationException(_nativeError));
            Failed?.Invoke(this, new InvalidOperationException(_nativeError, error));
        }

        // One callback of RequestAnimationFrame. TopLevel.RequestAnimationFrame cannot be cancelled, so a cancelled
        // request is only removed from the canvas and does nothing when its frame comes.
        private sealed class FrameRequest(CarouselGlCanvas canvas, int handle, Action<double> callback)
        {
            private DispatcherTimer? _timer;

            public void OnFrame(TimeSpan time) => Run();

            public void StartTimer()
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
                _timer.Tick += OnTick;
                _timer.Start();
            }

            public void StopTimer()
            {
                if (_timer is null)
                    return;
                _timer.Stop();
                _timer.Tick -= OnTick;
                _timer = null;
            }

            private void OnTick(object? sender, EventArgs args) => Run();

            private void Run()
            {
                StopTimer();
                if (canvas._frameRequests.Remove(handle))
                    callback(Now);
            }
        }
    }

    /// <summary>
    /// A frame shader shared by the canvas and the draw operations that show it. A draw operation can outlive the frame,
    /// and several can draw the same one, so each holds a reference; the last release disposes the shader.
    /// </summary>
    internal sealed class CarouselGlShaderHolder(SKShader shader, Size resolution)
    {
        private int _references = 1;

        public SKShader Shader { get; } = shader;

        /// <summary>The local size the shader draws over.</summary>
        public Size Resolution { get; } = resolution;

        public void Retain() => Interlocked.Increment(ref _references);

        public void Release()
        {
            if (Interlocked.Decrement(ref _references) == 0)
                Shader.Dispose();
        }
    }

    /// <summary>
    /// Draws the frame shader of a <see cref="CarouselGlCanvas"/> over its bounds with SkiaSharp and reports the Skia
    /// backend that drew it.
    /// </summary>
    internal sealed class CarouselGlDrawOperation : ICustomDrawOperation
    {
        private readonly CarouselGlCanvas _canvas;
        private readonly CarouselGlShaderHolder _holder;
        private int _disposed;

        public CarouselGlDrawOperation(CarouselGlCanvas canvas, CarouselGlShaderHolder holder, Rect bounds)
        {
            _canvas = canvas;
            _holder = holder;
            Bounds = bounds;
            holder.Retain();
        }

        public Rect Bounds { get; }

        public bool HitTest(Point p) => false;

        public bool Equals(ICustomDrawOperation? other) =>
            other is CarouselGlDrawOperation op && ReferenceEquals(op._holder, _holder) && op.Bounds == Bounds;

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                _holder.Release();
        }

        public void Render(ImmediateDrawingContext context)
        {
            if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
                return;
            using var lease = feature.Lease();
            _canvas.ReportRenderer(lease.GrContext is { } gr ? "Skia " + gr.Backend : "Skia CPU");
            var canvas = lease.SkCanvas;
            var resolution = _holder.Resolution;
            canvas.Save();
            // The drawing buffer stretches over the bounds, as a GL canvas over its display size.
            canvas.Scale((float)(Bounds.Width / resolution.Width), (float)(Bounds.Height / resolution.Height));
            using (var paint = new SKPaint { Shader = _holder.Shader })
                canvas.DrawRect(SKRect.Create((float)resolution.Width, (float)resolution.Height), paint);
            canvas.Restore();
        }
    }
}
