using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class PhotoTile : UserControl
{
    private CancellationTokenSource? _load;
    private Bitmap? _image;
    private PhotoItem? _item;
    private VideoPlayer? _player;
    private readonly VideoBadge? _badge;
    private readonly Grid? _hoverRow;
    // The height of the name bar, when it shows.
    private readonly double _bottomInset;
    // The loading hint: due once the thumbnail took a moment, and a spinner once its file is being read.
    private bool _hintDue, _reading;
    private static readonly TimeSpan LoadingHintDelay = TimeSpan.FromMilliseconds(400);
    /// <summary>How many thumbnails play at once; one more stops the one that started first.</summary>
    public const int MaxPlaying = 10;
    // Oldest first.
    private static readonly List<PhotoTile> PlayingTiles = [];
    // Playing thumbnails that left the tree, by file, until a new thumbnail of the same file takes their player over.
    private static readonly Dictionary<string, PhotoTile> HandedOver = new(StringComparer.OrdinalIgnoreCase);
    public PhotoItem Item => _item ?? throw new InvalidOperationException();
    public event Action<PhotoItem, KeyModifiers, bool>? Activated;
    public event Action? SelectionChanged;
    /// <summary>A video asks to open in the viewer; true asks for full screen.</summary>
    public event Action<PhotoItem, bool>? OpenRequested;

    public PhotoTile() { InitializeComponent(); }
    public PhotoTile(PhotoItem item, double width, double height, bool showName = true) : this()
    {
        _item = item;
        Width = width; Height = height;
        Focusable = true;
        NameLabel.Text = item.FileName;
        Queued.Text = Localization.PhotosLocalization.GetString("Photos_ThumbnailQueued");
        NameLabel.IsVisible = showName;
        NameLabel.Parent!.SetValue(IsVisibleProperty, showName);
        // Above the name bar when names show.
        _bottomInset = showName ? 30 : 0;
        if (Frame.Child is Panel layers)
        {
            if (item.IsVideo) layers.Children.Add(_badge = new VideoBadge(item) { Margin = new Thickness(6, 6, 6, _bottomInset + 6) });
            if (item.IsVideo || !showName) layers.Children.Add(_hoverRow = CreateHoverRow(item, showName));
            PlaceOverlays();
        }
        Avalonia.Automation.AutomationProperties.SetName(this, item.FileName);
        var identity = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(item.Path)))[..16];
        AutomationProperties.SetAutomationId(this, "Photos.Tile." + identity);
        AutomationProperties.SetAutomationId(Selection, "Photos.Tile.Select." + identity);
        AutomationProperties.SetName(Selection, Localization.PhotosLocalization.GetString("Photos_Select") + " " + item.FileName);
        Selection.IsCheckedChanged += (_, _) =>
        {
            var selected = Selection.IsChecked == true;
            if (item.IsSelected == selected) return;
            item.IsSelected = selected;
            SelectionChanged?.Invoke();
        };
        KeyDown += (_, e) =>
        {
            if (e.Key is not (Key.Enter or Key.Space)) return;
            // Enter opens a video in full screen; Space selects as for photos.
            if (item.IsVideo && e.Key == Key.Enter) OpenRequested?.Invoke(item, true);
            else Activated?.Invoke(item, e.KeyModifiers, e.Key == Key.Space);
            e.Handled = true;
        };
        AttachedToVisualTree += async (_, _) =>
        {
            _item.PropertyChanged += OnItemChanged;
            UpdateState();
            TakeOverPlayback();
            var load = new CancellationTokenSource();
            _load = load;
            HideLoadingHint();
            try
            {
                var loading = ThumbnailLoader.LoadAsync(item, height, load.Token, this, () =>
                {
                    if (_load != load) return;
                    _reading = true;
                    ShowLoadingHint();
                });
                // A thumbnail that takes a while, such as the first still the shell takes from a video, says so; a
                // quick one does not flash the hint.
                if (await Task.WhenAny(loading, Task.Delay(LoadingHintDelay)) != loading && _load == load)
                {
                    _hintDue = true;
                    ShowLoadingHint();
                }
                var image = await loading;
                if (_load != load) { image?.Dispose(); return; }
                _image = image; Photo.Source = image;
            }
            catch (OperationCanceledException) { }
            finally
            {
                load.Dispose();
                if (_load == load) { _load = null; HideLoadingHint(); }
            }
        };
        DetachedFromVisualTree += (_, _) =>
        {
            _item.PropertyChanged -= OnItemChanged;
            HandOverPlayback();
            _load?.Cancel(); _load = null; HideLoadingHint();
            Photo.Source = null; _image?.Dispose(); _image = null;
        };
        PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
            // The first click of a double click already started the video in place.
            if (item.IsVideo && e.ClickCount == 2 && e.KeyModifiers == KeyModifiers.None) OpenRequested?.Invoke(item, true);
            else Activated?.Invoke(item, e.KeyModifiers, e.GetPosition(this).Y < 45 && e.GetPosition(this).X > Bounds.Width - 45);
            e.Handled = true;
        };
    }
    private void OnItemChanged(object? sender, PropertyChangedEventArgs e) => UpdateState();

    // A thumbnail that waits for its turn says so in words; one whose file is being read shows the spinner.
    private void ShowLoadingHint()
    {
        Loading.IsVisible = _hintDue && _reading;
        Queued.IsVisible = _hintDue && !_reading;
    }

    private void HideLoadingHint()
    {
        _hintDue = _reading = false;
        Loading.IsVisible = Queued.IsVisible = false;
    }

    /// <summary>Plays the video in place, or pauses and resumes it, each thumbnail with its own player.</summary>
    public void TogglePlayback()
    {
        if (_player != null) { _player.TogglePlay(); return; }
        if (Frame.Child is not Panel layers) return;
        if (PlayingTiles.Count >= MaxPlaying) PlayingTiles[0].StopOwnPlayback();
        PlayingTiles.Add(this);
        // Frames at twice the thumbnail size stay sharp on a high-density display.
        _player = new VideoPlayer
        {
            IsCompact = true, MaxEdge = (int)Math.Ceiling(2 * Math.Max(Width, Height)), IsMuted = PhotosSettings.MuteGalleryVideos,
            ControlsMargin = new Thickness(4, 4, 4, _bottomInset + 4)
        };
        layers.Children.Insert(layers.Children.IndexOf(Photo) + 1, _player);
        _player.Failed += OnPlayerFailed;
        _player.StopRequested += StopOwnPlayback;
        _player.Open(Item.Path);
        PlaceOverlays();
    }

    // A video the system cannot play opens in the viewer, which offers the default player.
    private void OnPlayerFailed()
    {
        StopOwnPlayback();
        OpenRequested?.Invoke(Item, false);
    }

    // A gallery that builds its rows anew, for example when a scan of the library ends, replaces the thumbnail of a
    // playing video. The video plays on in the new thumbnail of its file, and stops when no thumbnail takes it over by
    // the time the gallery is idle.
    private void HandOverPlayback()
    {
        if (_player is not { } player) return;
        (player.Parent as Panel)?.Children.Remove(player);
        var path = Item.Path;
        HandedOver[path] = this;
        Dispatcher.UIThread.Post(() =>
        {
            if (HandedOver.TryGetValue(path, out var tile) && tile == this) StopOwnPlayback();
        }, DispatcherPriority.Background);
    }

    private void TakeOverPlayback()
    {
        if (!HandedOver.Remove(Item.Path, out var previous) || previous._player is not { } player || Frame.Child is not Panel layers) return;
        previous._player = null;
        player.Failed -= previous.OnPlayerFailed;
        player.Failed += OnPlayerFailed;
        player.StopRequested -= previous.StopOwnPlayback;
        player.StopRequested += StopOwnPlayback;
        player.ControlsMargin = new Thickness(4, 4, 4, _bottomInset + 4);
        PlayingTiles[PlayingTiles.IndexOf(previous)] = this;
        _player = player;
        layers.Children.Insert(layers.Children.IndexOf(Photo) + 1, player);
        PlaceOverlays();
    }

    /// <summary>Stops every video that plays in a thumbnail, for example when the gallery hides.</summary>
    public static void StopPlayback()
    {
        foreach (var tile in PlayingTiles.ToList()) tile.StopOwnPlayback();
    }

    /// <summary>Silences, or gives back the sound of, every video that plays in a thumbnail.</summary>
    public static void MuteAll(bool muted)
    {
        foreach (var tile in PlayingTiles)
            if (tile._player is { } player) player.IsMuted = muted;
    }

    /// <summary>
    /// Stops the videos of these files that play in a thumbnail; completes once their players have let go of the files,
    /// so they can be deleted.
    /// </summary>
    public static Task StopPlaybackAsync(IEnumerable<string> paths)
    {
        var files = paths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return Task.WhenAll(PlayingTiles.Where(tile => files.Contains(tile.Item.Path)).ToList().Select(tile => tile.Stop()));
    }

    private void StopOwnPlayback() => _ = Stop();

    private Task Stop()
    {
        PlayingTiles.Remove(this);
        if (HandedOver.TryGetValue(Item.Path, out var waiting) && waiting == this) HandedOver.Remove(Item.Path);
        if (_player is not { } player) return Task.CompletedTask;
        _player = null;
        var closing = player.CloseAsync();
        (player.Parent as Panel)?.Children.Remove(player);
        PlaceOverlays();
        return closing;
    }

    // While the video plays here, its bar takes the bottom: the length goes, since the bar shows the time, and the hover
    // row moves above the bar.
    private void PlaceOverlays()
    {
        var playing = _player != null;
        if (_badge != null) _badge.IsVisible = !playing;
        if (_hoverRow != null)
            _hoverRow.Margin = new Thickness(6, 6, _badge != null && !playing ? 76 : 6, _bottomInset + 6 + (playing ? VideoPlayer.CompactBarHeight : 0));
    }

    // The bottom row that shows while the pointer is over the thumbnail, instead of a tooltip: for a video two small
    // outlined buttons, the viewer and the viewer in full screen, then the file name unless the name bar shows it.
    // The length of a video keeps its corner.
    private Grid CreateHoverRow(PhotoItem item, bool showName)
    {
        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("Auto,*"), IsVisible = false, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Bottom
        };
        if (!showName)
        {
            var name = new Border
            {
                Child = new TextBlock { Text = item.FileName, Foreground = Brushes.White, FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis },
                Background = new SolidColorBrush(Color.FromArgb(179, 0, 0, 0)), CornerRadius = new CornerRadius(6), Padding = new Thickness(6, 3),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                IsHitTestVisible = false
            };
            Grid.SetColumn(name, 1);
            row.Children.Add(name);
        }
        PointerEntered += (_, _) => row.IsVisible = true;
        PointerExited += (_, _) => row.IsVisible = false;
        if (!item.IsVideo) return row;
        var buttons = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 4, Margin = new Thickness(0, 0, 6, 0) };
        foreach (var (key, fullScreen, data) in new[]
        {
            ("Open", false, "M10,21V19H6.41L10.91,14.5L9.5,13.09L5,17.59V14H3V21H10M14.5,10.91L19,6.41V10H21V3H14V5H17.59L13.09,9.5L14.5,10.91Z"),
            ("FullScreen", true, "M4,8V4H8V2H2V8M16,2V4H20V8H22V2M20,16V20H16V22H22V16M8,20H4V16H2V22H8Z")
        })
        {
            var button = new DaisyButton
            {
                IconData = StreamGeometry.Parse(data), Size = Flowery.Controls.DaisySize.ExtraSmall, ButtonStyle = DaisyButtonStyle.Outline,
                Shape = DaisyButtonShape.Square, Foreground = Brushes.White, BorderBrush = Brushes.White,
                Background = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0))
            };
            var name = Localization.PhotosLocalization.GetString("Photos_" + key);
            AutomationProperties.SetAutomationId(button, "Photos.Tile." + key);
            AutomationProperties.SetName(button, name + " " + item.FileName);
            ToolTip.SetTip(button, name);
            button.Click += (_, e) => { OpenRequested?.Invoke(item, fullScreen); e.Handled = true; };
            buttons.Children.Add(button);
        }
        row.Children.Add(buttons);
        return row;
    }
    private void UpdateState()
    {
        Selection.IsChecked = Item.IsSelected;
        Heart.IsVisible = Item.IsFavorite;
        Frame.BorderBrush = Item.IsSelected
            ? this.FindResource("PhotosAccentBrush") as IBrush ?? Brushes.DodgerBlue : Brushes.Transparent;
    }

    protected override AutomationPeer OnCreateAutomationPeer() => new PhotoTilePeer(this);

    private sealed class PhotoTilePeer(PhotoTile owner) : ControlAutomationPeer(owner), IInvokeProvider, IToggleProvider
    {
        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
        protected override string GetClassNameCore() => nameof(PhotoTile);
        protected override string? GetNameCore() => owner._item?.FileName;
        public ToggleState ToggleState => owner._item?.IsSelected == true ? ToggleState.On : ToggleState.Off;
        public void Invoke()
        {
            EnsureEnabled();
            if (owner._item is { } item) owner.Activated?.Invoke(item, KeyModifiers.None, false);
        }
        public void Toggle()
        {
            EnsureEnabled();
            if (owner._item is not { } item) return;
            item.IsSelected = !item.IsSelected;
            owner.SelectionChanged?.Invoke();
        }
    }
}
