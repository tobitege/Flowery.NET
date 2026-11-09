#if WINDOWS
using System.Runtime.InteropServices;
using Avalonia.Platform;
#endif
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Flowery.Enums;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

/// <summary>
/// A video over a still, which shows until the first frame: in the viewer with controls, or compact in a gallery
/// thumbnail. Windows plays it; in the viewer, other systems offer to open it in the default player.
/// </summary>
public sealed class VideoPlayer : Grid, IDisposable
{
    private readonly Image _frame = new() { Stretch = Stretch.Uniform };
    private readonly LayoutTransformControl _turned = new();
    private readonly DaisyButton _stop = Button("stop", DaisyIconSymbol.Stop);
    private readonly DaisyButton _play = Button("play", DaisyIconSymbol.Play);
    private readonly DaisyButton _mute = Button("mute", DaisyIconSymbol.Volume);
    private readonly DaisyButton _loop = Button("loop", null);
    private readonly DaisyButton _open = new() { Variant = DaisyButtonVariant.Primary, IconSymbol = DaisyIconSymbol.Video };
    private readonly DaisyRange _position = new() { Minimum = 0, Maximum = 1, VerticalAlignment = VerticalAlignment.Center };
    private readonly TextBlock _time = new() { VerticalAlignment = VerticalAlignment.Center, Margin = new Thickness(10, 0) };
    private readonly Border _controls;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Lock _sync = new();
    private WriteableBitmap? _bitmap;
    private bool _updating;
    private string? _path;
    // The player could not open the video, which then offers the default player instead.
    private bool _failed;
    private bool _muted;
    private readonly bool _compact;
#if WINDOWS
    private WindowsVideoPlayback? _playback;
    // Playing or not and the place in the video before the last TogglePlay, which a double click takes back.
    private (bool Playing, TimeSpan Position)? _beforeToggle;
    private bool _framePosted;
#endif

    public VideoPlayer()
    {
        IsVisible = false;
        AutomationProperties.SetAutomationId(this, "ViewerView.Video");
        // The turn assumes a child that fills its place; the frame centers itself, so a panel fills in between.
        _turned.Child = new Panel { Children = { _frame } };
        // The pointer passes through the picture to what lies below, which zooms, pans and plays; only the controls
        // take it. A canvas lets the picture grow past the player when zoomed in.
        Children.Add(new Canvas { IsHitTestVisible = false, Children = { _turned } });
        SizeChanged += (_, _) => { if (FrameBounds == null) PlaceFrame(); };

        _open.HorizontalAlignment = HorizontalAlignment.Center;
        _open.VerticalAlignment = VerticalAlignment.Center;
        AutomationProperties.SetAutomationId(_open, "ViewerView.Video.open");
        _open.Click += (_, _) => { if (_path != null) FileOperations.Open(_path); };
        Children.Add(_open);

        var bar = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto,Auto,Auto") };
        _stop.IsVisible = false;
        bar.Children.Add(_stop);
        Grid.SetColumn(_play, 1); bar.Children.Add(_play);
        Grid.SetColumn(_position, 2); bar.Children.Add(_position);
        Grid.SetColumn(_time, 3); bar.Children.Add(_time);
        _loop.IconData = StreamGeometry.Parse("M17,17H7V14L3,18L7,22V19H19V13H17M7,7H17V10L21,6L17,2V5H5V11H7V7Z");
        Grid.SetColumn(_loop, 4); bar.Children.Add(_loop);
        Grid.SetColumn(_mute, 5); bar.Children.Add(_mute);
        AutomationProperties.SetAutomationId(_position, "ViewerView.Video.Position");
        _controls = new Border
        {
            Child = bar, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(16), Padding = new Thickness(8, 4),
            CornerRadius = new CornerRadius(12), Background = new SolidColorBrush(Color.FromArgb(200, 0, 0, 0))
        };
        _time.Foreground = Brushes.White;
        Children.Add(_controls);
        // A press on the bar stays there, so a thumbnail below neither pauses nor opens.
        _controls.PointerPressed += (_, e) => e.Handled = true;

        _stop.Click += (_, _) => StopRequested?.Invoke();
        _play.Click += (_, _) => TogglePlay();
        _mute.Click += (_, _) => ToggleMute();
        _loop.Click += (_, _) => ToggleLoop();
        _position.PropertyChanged += (_, e) => { if (e.Property == Avalonia.Controls.Primitives.RangeBase.ValueProperty && !_updating) Seek(_position.Value); };
        _clock.Tick += (_, _) => RefreshControls();
        RefreshLabels();
    }

    /// <summary>Raised when the system cannot play the video.</summary>
#if WINDOWS
    public event Action? Failed;
#else
    // Nothing plays here, so nothing fails.
    public event Action? Failed { add { } remove { } }
#endif

    /// <summary>Raised by the stop button of a compact bar: the thumbnail ends the video and shows its still again.</summary>
    public event Action? StopRequested;

    /// <summary>The volume every player starts with, from 0 to 1; the tests set 0.</summary>
    public static double Volume { get; set; } = 1;

    /// <summary>True when this system plays videos in the app.</summary>
    public static bool CanPlay => OperatingSystem.IsWindows();

    /// <summary>Whether this player plays its video: on Windows, unless the video failed to open.</summary>
    internal bool IsPlayable => CanPlay && _path != null && !_failed;

    /// <summary>The height a compact bar takes at the bottom, with its gap.</summary>
    public const double CompactBarHeight = 40;

    /// <summary>
    /// A player inside a thumbnail: a small bar with play, position, time when there is room, and sound; the thumbnail
    /// takes the pointer everywhere else.
    /// </summary>
    public bool IsCompact
    {
        get => _compact;
        init
        {
            _compact = value;
            if (!value) return;
            _controls.Margin = new Thickness(4);
            _controls.Padding = new Thickness(2, 0);
            _controls.CornerRadius = new CornerRadius(8);
            _loop.IsVisible = false;
            _stop.IsVisible = true;
            _time.FontSize = 11;
            _time.Margin = new Thickness(4, 0);
            foreach (var button in new[] { _stop, _play, _mute }) button.Width = button.Height = CompactBarHeight - 8;
            SizeChanged += (_, _) => _time.IsVisible = Bounds.Width >= 240;
        }
    }

    /// <summary>Where the bar sits, for example above the name bar of a thumbnail.</summary>
    public Thickness ControlsMargin
    {
        get => _controls.Margin;
        set => _controls.Margin = value;
    }

    /// <summary>Silences the video, for example with the gallery's mute button.</summary>
    public bool IsMuted
    {
        get => _muted;
        set
        {
            _muted = value;
#if WINDOWS
            if (_playback is { } playback) playback.IsMuted = value;
#endif
            RefreshControls();
        }
    }

    /// <summary>The longest edge the frames are copied at; a thumbnail needs far fewer pixels than the viewer.</summary>
    public int MaxEdge { get; init; } = 1920;

    /// <summary>Clockwise quarter turns of the picture, for the view only.</summary>
    public int Turns
    {
        get;
        set
        {
            field = (value % 4 + 4) % 4;
            _turned.LayoutTransform = field == 0 ? null : new RotateTransform(90 * field);
        }
    }

    /// <summary>Where the picture shows, such as the place of the still at the viewer's zoom; null fills the player.</summary>
    public Rect? FrameBounds
    {
        get;
        set { field = value; PlaceFrame(); }
    }

    private void PlaceFrame()
    {
        var bounds = FrameBounds ?? new Rect(Bounds.Size);
        Canvas.SetLeft(_turned, bounds.X);
        Canvas.SetTop(_turned, bounds.Y);
        _turned.Width = bounds.Width;
        _turned.Height = bounds.Height;
    }

    /// <summary>True while the pointer is on the control bar.</summary>
    public bool IsPointerOverControls => _controls.IsPointerOver;

    /// <summary>Shows or hides the control bar, for example with the rest of the full-screen controls.</summary>
    public void SetControlsVisible(bool visible)
    {
        _controls.Opacity = visible ? 1 : 0;
        _controls.IsHitTestVisible = visible;
    }

    public void Open(string path)
    {
        Close();
        _path = path;
        _failed = false;
        IsVisible = true;
        _open.IsVisible = !CanPlay && !_compact;
        _controls.IsVisible = CanPlay;
#if WINDOWS
        _beforeToggle = null;
        var playback = new WindowsVideoPlayback(MaxEdge) { IsLooping = PhotosSettings.LoopVideos, Volume = Volume, IsMuted = _muted };
        playback.FrameReady += OnFrame;
        playback.Opened += error => Dispatcher.UIThread.Post(() =>
        {
            if (_playback != playback) return;
            // The bar follows the player while the video is open; right after Play the player does not report playing yet.
            if (error == null) { playback.Play(); _clock.Start(); }
            else ShowOpenInstead();
            RefreshControls();
        });
        playback.Ended += () => Dispatcher.UIThread.Post(RefreshControls);
        _playback = playback;
        _ = OpenPlaybackAsync(playback, path);
#endif
        RefreshControls();
    }

#if WINDOWS
    private async Task OpenPlaybackAsync(WindowsVideoPlayback playback, string path)
    {
        try { await playback.OpenAsync(path); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or COMException)
        {
            if (_playback == playback) ShowOpenInstead();
        }
    }

    // A video the system cannot play opens in the default player; a thumbnail keeps its still.
    private void ShowOpenInstead()
    {
        _failed = true;
        _controls.IsVisible = false;
        _open.IsVisible = !_compact;
        Failed?.Invoke();
    }
#endif

    private void ToggleLoop()
    {
        PhotosSettings.LoopVideos = !PhotosSettings.LoopVideos;
        PhotosSettings.SaveOptions();
#if WINDOWS
        if (_playback is { } playback) playback.IsLooping = PhotosSettings.LoopVideos;
#endif
        RefreshControls();
    }

    /// <summary>Stops the video; the player lets go of its file shortly after.</summary>
    public void Close() => _ = CloseAsync();

    /// <summary>Stops the video; completes once the player has let go of its file, for example before it is deleted.</summary>
    public Task CloseAsync()
    {
        _clock.Stop();
        var closing = Task.CompletedTask;
#if WINDOWS
        // The playback raises its last frame inside its own lock and none after it stops, so it stops before this
        // control's lock is taken.
        var playback = _playback;
        _playback = null;
        if (playback != null) closing = playback.DisposeAsync();
#endif
        lock (_sync)
        {
            _frame.Source = null;
            _bitmap?.Dispose(); _bitmap = null;
        }
        _path = null;
        IsVisible = false;
        return closing;
    }

    public void TogglePlay()
    {
#if WINDOWS
        if (_playback is not { } playback) return;
        _beforeToggle = (playback.IsPlaying, playback.Position);
        if (playback.IsPlaying) playback.Pause();
        else
        {
            // At the end, playing starts over.
            if (playback.Duration > TimeSpan.Zero && playback.Position >= playback.Duration - TimeSpan.FromMilliseconds(100))
                playback.Position = TimeSpan.Zero;
            playback.Play();
        }
        RefreshControls();
#endif
    }

    /// <summary>
    /// Takes the last <see cref="TogglePlay"/> back, the playing or resting and the place in the video: the first click
    /// of a double click came as a click, and at the end of a video it had started it over.
    /// </summary>
    internal void UndoTogglePlay()
    {
#if WINDOWS
        if (_playback is not { } playback || _beforeToggle is not var (playing, position)) return;
        _beforeToggle = null;
        if (playing) playback.Play(); else playback.Pause();
        playback.Position = position;
        RefreshControls();
#endif
    }

    public void ToggleMute() => IsMuted = !IsMuted;

    /// <summary>Jumps back or forward by the given time, no further than the start or the end.</summary>
    internal void Skip(TimeSpan by)
    {
#if WINDOWS
        if (_playback is not { } playback || playback.Duration <= TimeSpan.Zero) return;
        playback.Position = TimeSpan.FromTicks(Math.Clamp((playback.Position + by).Ticks, 0, playback.Duration.Ticks));
        RefreshControls();
#endif
    }

    private void Seek(double seconds)
    {
#if WINDOWS
        if (_playback is { } playback) playback.Position = TimeSpan.FromSeconds(seconds);
#endif
    }

    public void RefreshLabels()
    {
        var label = PhotosLocalization.GetString("Photos_OpenInPlayer");
        _open.Content = label;
        AutomationProperties.SetName(_open, label);
        AutomationProperties.SetName(_position, PhotosLocalization.GetString("Photos_VideoPosition"));
        SetName(_stop, "Photos_Stop");
        RefreshControls();
    }

    private void RefreshControls()
    {
#if WINDOWS
        var playing = _playback?.IsPlaying == true;
        var duration = _playback?.Duration ?? TimeSpan.Zero;
        var position = _playback?.Position ?? TimeSpan.Zero;
#else
        var playing = false;
        TimeSpan duration = TimeSpan.Zero, position = TimeSpan.Zero;
#endif
        _play.IconSymbol = playing ? DaisyIconSymbol.Pause : DaisyIconSymbol.Play;
        SetName(_play, playing ? "Photos_Pause" : "Photos_Play");
        _mute.IconSymbol = _muted ? DaisyIconSymbol.Mute : DaisyIconSymbol.Volume;
        SetName(_mute, _muted ? "Photos_Unmute" : "Photos_Mute");
        _loop.IsActive = PhotosSettings.LoopVideos;
        _loop.Variant = PhotosSettings.LoopVideos ? DaisyButtonVariant.Primary : DaisyButtonVariant.Ghost;
        SetName(_loop, "Photos_Loop");
        _updating = true;
        _position.Maximum = Math.Max(1, duration.TotalSeconds);
        _position.Value = Math.Min(position.TotalSeconds, _position.Maximum);
        _updating = false;
        _time.Text = $"{FileOperations.FormatDuration(position)} / {FileOperations.FormatDuration(duration)}";
    }

    private static void SetName(Control control, string key)
    {
        var name = PhotosLocalization.GetString(key);
        AutomationProperties.SetName(control, name);
        ToolTip.SetTip(control, name);
    }

    private static DaisyButton Button(string id, DaisyIconSymbol? symbol)
    {
        var button = new DaisyButton { IconSymbol = symbol, Variant = DaisyButtonVariant.Ghost, Foreground = Brushes.White };
        button.Classes.Add("toolbar-icon");
        AutomationProperties.SetAutomationId(button, "ViewerView.Video." + id);
        return button;
    }

#if WINDOWS
    // Raised on a worker thread: copies the frame into the bitmap and asks the UI thread once to show it.
    private void OnFrame(int width, int height, byte[] pixels)
    {
        lock (_sync)
        {
            if (_path == null) return;
            var bitmap = _bitmap;
            if (bitmap == null || bitmap.PixelSize.Width != width || bitmap.PixelSize.Height != height)
            {
                bitmap = new WriteableBitmap(new PixelSize(width, height), new Avalonia.Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
                var old = _bitmap;
                _bitmap = bitmap;
                Dispatcher.UIThread.Post(() => { if (_bitmap == bitmap) _frame.Source = bitmap; old?.Dispose(); });
            }
            using (var target = bitmap.Lock())
            {
                for (var row = 0; row < height; row++)
                    Marshal.Copy(pixels, row * width * 4, target.Address + row * target.RowBytes, width * 4);
            }
            if (_framePosted) return;
            _framePosted = true;
        }
        Dispatcher.UIThread.Post(() =>
        {
            lock (_sync) _framePosted = false;
            _frame.InvalidateVisual();
        }, DispatcherPriority.Render);
    }
#endif

    public void Dispose() => Close();
}
