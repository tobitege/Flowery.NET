#if WINDOWS
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.DirectX;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Core;
using SkiaSharp;
using Windows.Media.Playback;

namespace Flowery.NET.Photos.Services;

/// <summary>
/// Plays a video with the Windows media player, which brings its codecs and plays the sound. Frame server mode hands
/// each frame over as a GPU surface; the frames are copied into memory for an Avalonia bitmap, scaled down to at most
/// the given edge length.
/// </summary>
/// <remarks>
/// The UI thread never waits for the player: every call to it runs in order on a worker, and the state comes from values
/// its events keep. While the system reads stills from many videos, above all on a network drive, a call to the player
/// can wait for seconds, and on the UI thread the app would stop answering.
/// </remarks>
internal sealed class WindowsVideoPlayback : IDisposable
{
    private readonly int _maxEdge;
    private readonly Lock _sync = new();
    private readonly Lock _order = new();
    private Task _work = Task.CompletedTask;
    private MediaPlayer? _player;
    private VideoFrame? _surface, _memory;
    private byte[] _pixels = [];
    private int _copying;
    private bool _disposed;
    private volatile bool _playing;
    private long _position, _duration;
    private bool _muted, _looping;
    private double _volume = 1;

    /// <summary>A copied frame: width, height and its BGRA pixels, valid only during the call. Raised on a worker thread.</summary>
    public event Action<int, int, byte[]>? FrameReady;
    /// <summary>Raised on a worker thread with the error, or null once the video is ready.</summary>
    public event Action<string?>? Opened;
    public event Action? Ended;

    public WindowsVideoPlayback(int maxEdge)
    {
        _maxEdge = maxEdge;
        Run(player =>
        {
            player.MediaOpened += (sender, _) =>
            {
                Interlocked.Exchange(ref _duration, sender.PlaybackSession.NaturalDuration.Ticks);
                Opened?.Invoke(null);
            };
            player.MediaFailed += (_, e) => Opened?.Invoke(string.IsNullOrWhiteSpace(e.ErrorMessage) ? e.Error.ToString() : e.ErrorMessage);
            player.MediaEnded += (_, _) => { _playing = false; Ended?.Invoke(); };
            player.PlaybackSession.PlaybackStateChanged += (session, _) => _playing = session.PlaybackState == MediaPlaybackState.Playing;
            player.PlaybackSession.PositionChanged += (session, _) => Interlocked.Exchange(ref _position, session.Position.Ticks);
            player.PlaybackSession.NaturalDurationChanged += (session, _) => Interlocked.Exchange(ref _duration, session.NaturalDuration.Ticks);
            player.VideoFrameAvailable += OnFrame;
        });
    }

    /// <summary>
    /// Opens a video; <see cref="Opened"/> reports the result. The player reads it through a file URI, not a storage
    /// file: a storage file goes through the shell, which the folder dialog of Browse… kept busy for about twenty
    /// seconds after it closed on a network drive, and the video did not start until then.
    /// </summary>
    public Task OpenAsync(string path) => Run(player => player.Source = MediaSource.CreateFromUri(FileUri(path)));

    /// <summary>
    /// A frame of a video at the position <paramref name="at"/> picks from its length, at most <paramref name="maxEdge"/>
    /// pixels on its longest edge and turned upright, or null when no installed codec reads it in time. The player reads
    /// the file by its URI, so it does not wait for the shell.
    /// </summary>
    public static async Task<SKBitmap?> CaptureStillAsync(string path, int maxEdge, Func<TimeSpan, TimeSpan> at, CancellationToken token)
    {
        var playback = new WindowsVideoPlayback(maxEdge) { Volume = 0, IsMuted = true };
        var still = new TaskCompletionSource<SKBitmap?>(TaskCreationOptions.RunContinuationsAsynchronously);
        // No frame counts before the player has gone to the position.
        var target = long.MaxValue;
        playback.Opened += error =>
        {
            if (error != null) { still.TrySetResult(null); return; }
            var position = at(playback.Duration);
            Interlocked.Exchange(ref target, position.Ticks);
            playback.Position = position;
            playback.Play();
        };
        playback.FrameReady += (width, height, pixels) =>
        {
            if (playback.Position.Ticks < Interlocked.Read(ref target) - TimeSpan.TicksPerSecond / 10 || IsEmpty(pixels)) return;
            var bitmap = new SKBitmap(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
            Marshal.Copy(pixels, 0, bitmap.GetPixels(), width * height * 4);
            if (!still.TrySetResult(bitmap)) bitmap.Dispose();
        };
        playback.Ended += () => still.TrySetResult(null);
        try
        {
            await playback.OpenAsync(path).ConfigureAwait(false);
            return await still.Task.WaitAsync(TimeSpan.FromSeconds(15), token).ConfigureAwait(false);
        }
        catch (TimeoutException) { return null; }
        finally { await playback.DisposeAsync().ConfigureAwait(false); }
    }

    // The first frame after a jump is now and then a surface the player has not drawn yet: wholly transparent, which a
    // frame of a video never is. Every 97th pixel's alpha tells.
    private static bool IsEmpty(byte[] pixels)
    {
        for (var alpha = 3; alpha < pixels.Length; alpha += 4 * 97)
            if (pixels[alpha] != 0) return false;
        return true;
    }

    /// <summary>The file URI of a path, with characters such as # and % escaped, so they stay part of the name.</summary>
    internal static Uri FileUri(string path) => new(Path.GetFullPath(path));

    public bool IsPlaying => _playing;
    public TimeSpan Duration => TimeSpan.FromTicks(Interlocked.Read(ref _duration));

    public TimeSpan Position
    {
        get => TimeSpan.FromTicks(Interlocked.Read(ref _position));
        set
        {
            Interlocked.Exchange(ref _position, value.Ticks);
            Run(player => player.PlaybackSession.Position = value);
        }
    }

    public bool IsMuted
    {
        get => _muted;
        set { _muted = value; Run(player => player.IsMuted = value); }
    }

    /// <summary>The volume from 0 to 1.</summary>
    public double Volume
    {
        get => _volume;
        set { _volume = value; Run(player => player.Volume = value); }
    }

    /// <summary>Starts over at the end instead of stopping.</summary>
    public bool IsLooping
    {
        get => _looping;
        set { _looping = value; Run(player => player.IsLoopingEnabled = value); }
    }

    // The state follows at once; the player's own report confirms or corrects it.
    public void Play() { _playing = true; Run(player => player.Play()); }
    public void Pause() { _playing = false; Run(player => player.Pause()); }

    // Runs a call to the player after the ones before it, on a worker. The first call creates the player there, so
    // the UI thread does not even wait for that. Internal so tests can keep the player busy.
    internal Task Run(Action<MediaPlayer> call)
    {
        lock (_order)
        {
            var next = _work.ContinueWith(_ =>
            {
                MediaPlayer player;
                lock (_sync)
                {
                    if (_disposed) return;
                    player = _player ??= new MediaPlayer { IsVideoFrameServerEnabled = true, AutoPlay = false };
                }
                call(player);
            }, CancellationToken.None, TaskContinuationOptions.None, TaskScheduler.Default);
            // A failed call does not stop the ones after it; the task that Run returns still reports it.
            _work = next.ContinueWith(_ => { }, TaskScheduler.Default);
            return next;
        }
    }

    // A frame that arrives while the previous one is still being copied is dropped.
    private async void OnFrame(MediaPlayer sender, object args)
    {
        if (Interlocked.Exchange(ref _copying, 1) == 1) return;
        try
        {
            VideoFrame surface, memory;
            lock (_sync)
            {
                if (_disposed) return;
                var session = sender.PlaybackSession;
                Interlocked.Exchange(ref _position, session.Position.Ticks);
                var scale = Math.Min(1, (double)_maxEdge / Math.Max(1, Math.Max(session.NaturalVideoWidth, session.NaturalVideoHeight)));
                var width = Math.Max(1, (int)(session.NaturalVideoWidth * scale));
                var height = Math.Max(1, (int)(session.NaturalVideoHeight * scale));
                if (_memory == null || _memory.SoftwareBitmap.PixelWidth != width || _memory.SoftwareBitmap.PixelHeight != height)
                {
                    _surface?.Dispose(); _memory?.Dispose();
                    _surface = VideoFrame.CreateAsDirect3D11SurfaceBacked(DirectXPixelFormat.B8G8R8A8UIntNormalized, width, height);
                    _memory = new VideoFrame(BitmapPixelFormat.Bgra8, width, height, BitmapAlphaMode.Premultiplied);
                    _pixels = new byte[width * height * 4];
                }
                (surface, memory) = (_surface!, _memory);
            }
            sender.CopyFrameToVideoSurface(surface.Direct3DSurface);
            await surface.CopyToAsync(memory);
            lock (_sync)
            {
                if (_disposed) return;
                memory.SoftwareBitmap.CopyToBuffer(_pixels.AsBuffer());
                FrameReady?.Invoke(memory.SoftwareBitmap.PixelWidth, memory.SoftwareBitmap.PixelHeight, _pixels);
            }
        }
        // The player may stop or close between two frames.
        catch (Exception ex) when (ex is COMException or ObjectDisposedException or InvalidOperationException) { }
        finally { Interlocked.Exchange(ref _copying, 0); }
    }

    /// <summary>Stops at once: no frame or event follows. The player closes on its worker and lets go of the file.</summary>
    public void Dispose() => _ = DisposeAsync();

    /// <summary>Stops at once and completes once the player has let go of the file, for example before it is deleted.</summary>
    public Task DisposeAsync()
    {
        MediaPlayer? player;
        lock (_sync)
        {
            if (_disposed) return _work;
            _disposed = true;
            player = _player;
            _player = null;
        }
        lock (_order)
        {
            var closing = _work.ContinueWith(_ =>
            {
                if (player != null)
                {
                    player.VideoFrameAvailable -= OnFrame;
                    try { player.Pause(); player.Source = null; }
                    catch (Exception ex) when (ex is COMException or InvalidOperationException) { }
                    player.Dispose();
                }
                lock (_sync) { _surface?.Dispose(); _memory?.Dispose(); _surface = _memory = null; }
            }, TaskScheduler.Default);
            _work = closing;
            return closing;
        }
    }
}
#endif
