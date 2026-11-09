using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class GalleryView : UserControl
{
    private List<PhotoItem> _items = [];
    private List<GalleryRow> _rows = [];
    private PhotoQuery _query = new();
    private string _title = "";
    // The space between two thumbnails of a row, which the layout, the rows and the keyboard share.
    private const double TileGap = 6;
    private PhotoItem? _anchor;
    private bool _updating;
    private double _layoutWidth;
    private int _layoutVersion;
    // The topmost visible photo before the rows were rebuilt, followed by the photos after it, and its top in the
    // scroller. A rescan creates new items, so they go by path.
    private List<string>? _anchorPaths;
    private double _anchorTop;
    // The query of the rows shown; another source, search or filter starts at the top instead of keeping the place.
    private PhotoQuery? _shownQuery;
    public IReadOnlyList<PhotoItem> Items => _items;
    public PhotoQuery Query => _query;
    public IReadOnlyList<PhotoItem> Selected => _items.Where(item => item.IsSelected).ToList();
    public event Action<PhotoItem>? OpenRequested;
    public event Action<PhotoItem>? FullScreenRequested;
    public event Action? SelectionChanged;
    public event Action<PhotoTile>? ConfigureTile;
    public event Action? ImportRequested;
    public event Action? ImportFolderRequested;
    public event Action? RescanRequested;
    public event Action? SlideshowRequested;
    public event Action? BackRequested;
    public event Action? ResetSearchRequested;

    internal PhotoLibrary Library { get; set; }
    public GalleryView() : this(PhotoLibrary.Instance) { }
    public GalleryView(PhotoLibrary library)
    {
        Library = library;
        InitializeComponent();
        DataContext = PhotosLocalization.Instance;
        Rows.ItemTemplate = new FuncDataTemplate<GalleryRow>((row, _) => BuildRow(row));
        SizeChanged += (_, _) =>
        {
            if (Math.Abs(_layoutWidth - Bounds.Width) > 0.5) RebuildRows();
        };
        Rows.AddHandler(ScrollViewer.ScrollChangedEvent, OnScroll);
        Timeline.ScrollRequested += offset =>
        {
            _anchorPaths = null; _layoutVersion++;
            if (Scroller is { } scroller) ScrollToLayoutOffset(scroller, offset);
        };
        AttachedToVisualTree += (_, _) => { FlowerySizeManager.SizeChanged += OnGlobalSizeChanged; AttachKeys(); };
        DetachedFromVisualTree += (_, _) => { FlowerySizeManager.SizeChanged -= OnGlobalSizeChanged; DetachKeys(); };
        // A saved size above the default maximum stays until the window height is known.
        TileSize.Maximum = Math.Max(TileSize.Maximum, PhotosSettings.TileHeight);
        TileSize.Value = PhotosSettings.TileHeight;
        Square.IsChecked = PhotosSettings.SquareTiles;
        Names.IsChecked = PhotosSettings.ShowFileNames;
        ShowTimeline.IsChecked = PhotosSettings.ShowTimeline;
        ShowLetters.IsChecked = PhotosSettings.ShowLetters;
        LetterDigits.IsChecked = PhotosSettings.LetterDigits;
        LetterUmlauts.IsChecked = PhotosSettings.LetterUmlauts;
        RefreshLabels();
        Sort.SelectionChanged += (_, _) => ApplyOptions();
        Group.SelectionChanged += (_, _) => ApplyOptions();
        Type.SelectionChanged += (_, _) => ApplyOptions();
        Orientation.SelectionChanged += (_, _) => ApplyOptions();
        Square.IsCheckedChanged += (_, _) => ApplyOptions();
        Names.IsCheckedChanged += (_, _) => ApplyOptions();
        ShowTimeline.IsCheckedChanged += (_, _) => ApplyOptions();
        ShowLetters.IsCheckedChanged += (_, _) => ApplyOptions();
        LetterDigits.IsCheckedChanged += (_, _) => ApplyOptions();
        LetterUmlauts.IsCheckedChanged += (_, _) => ApplyOptions();
        Letters.LetterChosen += GoToLetter;
        TileSize.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) ApplyOptions(); };
        // A video playing in a thumbnail stops when the gallery hides, for the viewer or another page.
        PropertyChanged += (_, e) => { if (e.Property == IsVisibleProperty && !IsVisible) PhotoTile.StopPlayback(); };
        // The thumbnail size reaches up to the window height.
        SizeChanged += (_, _) =>
        {
            if (TopLevel.GetTopLevel(this) is { } top) TileSize.Maximum = Math.Max(TileSize.Minimum, top.ClientSize.Height);
        };
    }
    public void RefreshLabels()
    {
        _updating = true;
        string L(string key) => PhotosLocalization.GetString(key);
        Sort.ItemsSource = new[] { "DateNewest", "DateOldest", "Modified", "NameAsc", "NameDesc", "Size" }.Select(k => L("Photos_Sort_" + k));
        Group.ItemsSource = new[] { "Day", "Month", "Year", "None" }.Select(k => L("Photos_Group_" + k));
        Type.ItemsSource = new[] { "AllTypes", "Jpeg", "Png", "OtherTypes", "Videos" }.Select(k => L("Photos_Filter_" + k));
        Orientation.ItemsSource = new[] { "AnyOrientation", "Landscape", "Portrait", "Square" }.Select(k => L("Photos_Filter_" + k));
        Sort.SelectedIndex = (int)PhotosSettings.SortMode;
        Group.SelectedIndex = (int)PhotosSettings.GroupMode;
        Type.SelectedIndex = (int)_query.TypeFilter;
        Orientation.SelectedIndex = (int)_query.OrientationFilter;
        _updating = false;
        RefreshMuteVideos();
    }

    // The mute button silences every video that plays in a thumbnail, and the ones that start later.
    private void RefreshMuteVideos()
    {
        var muted = PhotosSettings.MuteGalleryVideos;
        MuteVideos.IconSymbol = muted ? Flowery.Enums.DaisyIconSymbol.Mute : Flowery.Enums.DaisyIconSymbol.Volume;
        MuteVideos.IsActive = muted;
        var name = PhotosLocalization.GetString(muted ? "Photos_UnmuteVideos" : "Photos_MuteVideos");
        Avalonia.Automation.AutomationProperties.SetName(MuteVideos, name);
        ToolTip.SetTip(MuteVideos, name);
    }

    private void MuteVideosClick(object? sender, RoutedEventArgs e)
    {
        PhotosSettings.MuteGalleryVideos = !PhotosSettings.MuteGalleryVideos;
        PhotosSettings.SaveOptions();
        PhotoTile.MuteAll(PhotosSettings.MuteGalleryVideos);
        RefreshMuteVideos();
    }

    public void Show(PhotoQuery query, string title)
    {
        ClearSelection();
        _query = query; _title = title; _shownQuery = null;
        RefreshLabels();
        Refresh();
    }
    public void Search(string? text) { _query = _query with { SearchText = text }; Refresh(); }
    public void Refresh()
    {
        var visible = Library.Query(_query with { Sort = PhotosSettings.SortMode });
        var visibleSet = visible.ToHashSet();
        foreach (var item in _items.Where(item => !visibleSet.Contains(item))) item.IsSelected = false;
        _items = visible;
        if (_anchor != null && !visibleSet.Contains(_anchor)) _anchor = null;
        SelectionChanged?.Invoke();
        PageTitle.Text = _title;
        CountLabel.Text = PhotosLocalization.Format(_items.Count == 1 ? "Photos_ItemCountOne" : "Photos_ItemCount", _items.Count);
        Empty.IsVisible = _items.Count == 0;
        // Only where videos play in their thumbnails.
        MuteVideos.IsVisible = VideoPlayer.CanPlay && _items.Any(item => item.IsVideo);
        DateChipFrame.IsVisible = _items.Count > 0 && DateSorted;
        Back.IsVisible = _query.Source is PhotoSourceKind.Album or PhotoSourceKind.Folder or PhotoSourceKind.Browsed;
        var emptyKind = !string.IsNullOrWhiteSpace(_query.SearchText) || _query.TypeFilter != PhotoTypeFilter.All ||
            _query.OrientationFilter != PhotoOrientationFilter.Any ? "NoResults" : _query.Source switch
            {
                PhotoSourceKind.Favorites => "Favorites",
                PhotoSourceKind.Album => "Album",
                PhotoSourceKind.Folder or PhotoSourceKind.Browsed => "Folder",
                _ => "Library"
            };
        EmptyTitle.Text = PhotosLocalization.GetString("Photos_Empty_" + emptyKind + "Title");
        EmptyText.Text = PhotosLocalization.GetString("Photos_Empty_" + (emptyKind == "Folder" ? "Library" : emptyKind) + "Text");
        ResetFilters.IsVisible = emptyKind == "NoResults";
        var newSet = _query != _shownQuery;
        _shownQuery = _query;
        RebuildRows(newSet);
    }
    private void ApplyOptions()
    {
        if (_updating) return;
        PhotosSettings.SortMode = (PhotoSortMode)Math.Max(0, Sort.SelectedIndex);
        PhotosSettings.GroupMode = (PhotoGroupMode)Math.Max(0, Group.SelectedIndex);
        PhotosSettings.SquareTiles = Square.IsChecked == true;
        PhotosSettings.ShowFileNames = Names.IsChecked == true;
        PhotosSettings.ShowTimeline = ShowTimeline.IsChecked == true;
        PhotosSettings.ShowLetters = ShowLetters.IsChecked == true;
        PhotosSettings.LetterDigits = LetterDigits.IsChecked == true;
        PhotosSettings.LetterUmlauts = LetterUmlauts.IsChecked == true;
        PhotosSettings.TileHeight = TileSize.Value;
        _query = _query with { TypeFilter = (PhotoTypeFilter)Math.Max(0, Type.SelectedIndex), OrientationFilter = (PhotoOrientationFilter)Math.Max(0, Orientation.SelectedIndex) };
        PhotosSettings.SaveOptions();
        Refresh();
    }
    // A change in place (removal, rescan, resize, grouping) keeps the topmost photo where it is; a new set starts at the top.
    private void RebuildRows(bool fromTop = false)
    {
        var scroller = Scroller;
        if (fromTop)
        {
            _anchorPaths = null;
            if (scroller != null) scroller.Offset = default;
        }
        else if (_anchorPaths == null && scroller != null) RememberTopPhoto(scroller);
        var offset = scroller?.Offset.Y ?? 0;
        _layoutWidth = Bounds.Width;
        var factor = FlowerySizeManager.CurrentSize switch
        {
            DaisySize.ExtraSmall => .85, DaisySize.Small => 1, DaisySize.Medium => 1.12,
            DaisySize.Large => 1.25, DaisySize.ExtraLarge => 1.4, _ => 1
        };
        // Beside the rows, the timeline for a date order and the letters for a name order, each when chosen.
        Timeline.IsVisible = DateSorted && _items.Count > 0 && PhotosSettings.ShowTimeline;
        Letters.IsVisible = NameSorted && _items.Count > 0 && PhotosSettings.ShowLetters;
        if (Letters.IsVisible) Letters.Update(_items.Select(item => item.FileName), PhotosSettings.LetterDigits, PhotosSettings.LetterUmlauts);
        _rows = GalleryLayout.Build(_items, Math.Max(100, Bounds.Width - (Timeline.IsVisible ? 90 : Letters.IsVisible ? 65 : 25)), PhotosSettings.TileHeight * factor, TileGap,
            DateSorted ? PhotosSettings.GroupMode : PhotoGroupMode.None, PhotosSettings.SquareTiles);
        Rows.ItemsSource = _rows;
        Timeline.Update(_rows, offset, scroller?.Viewport.Height ?? 0);
        var version = ++_layoutVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (version != _layoutVersion || Scroller is not { } current) return;
            if (_anchorPaths is { } paths) KeepPhotoInPlace(current, paths, _anchorTop);
            _anchorPaths = null;
            Timeline.Update(_rows, current.Offset.Y, current.Viewport.Height);
        }, DispatcherPriority.Loaded);
        DateChip.Text = _rows.Count == 0 ? "" : MonthOf(_rows[0]);
    }
    // Of the tiles in view, the topmost row and its first photo; the photos after it stand in if it goes.
    private void RememberTopPhoto(ScrollViewer scroller)
    {
        GalleryPhotoRow? top = null;
        foreach (var tile in Rows.GetVisualDescendants().OfType<PhotoTile>())
        {
            if (!tile.IsEffectivelyVisible || tile.DataContext is not GalleryPhotoRow row || tile.TranslatePoint(default, scroller) is not { } point ||
                point.Y + tile.Bounds.Height <= 0 || (top != null && point.Y >= _anchorTop)) continue;
            top = row; _anchorTop = point.Y;
        }
        _anchorPaths = top == null ? null
            : [.. _rows.SkipWhile(row => row != top).OfType<GalleryPhotoRow>().SelectMany(row => row.Tiles).Select(tile => tile.Item.Path)];
    }
    // After an items reset the panel only estimates where rows lie, so the measured place of the photo corrects the
    // offset.
    private void KeepPhotoInPlace(ScrollViewer scroller, List<string> paths, double top)
    {
        var rowOf = new Dictionary<string, int>(PhotoLibrary.PathComparer);
        for (var index = 0; index < _rows.Count; index++)
            if (_rows[index] is GalleryPhotoRow photos)
                foreach (var (item, _) in photos.Tiles) rowOf[item.Path] = index;
        var path = paths.FirstOrDefault(rowOf.ContainsKey);
        if (path == null) return;
        var tile = BringRowIntoView(rowOf[path])?.GetVisualDescendants().OfType<PhotoTile>()
            .FirstOrDefault(candidate => PhotoLibrary.PathComparer.Equals(candidate.Item.Path, path));
        if (tile != null) PageScroll.MoveTo(scroller, tile, top);
    }
    // The timeline names an offset in the rows' own layout; the row there takes the same place in the view.
    private void ScrollToLayoutOffset(ScrollViewer scroller, double offset)
    {
        var index = GalleryLayout.FindRowAt(_rows, offset);
        if (index >= 0 && BringRowIntoView(index) is { } row) PageScroll.MoveTo(scroller, row, _rows[index].Top - offset);
    }
    private Control? BringRowIntoView(int index)
    {
        Rows.ScrollIntoView(index);
        Rows.UpdateLayout();
        return Rows.ContainerFromIndex(index);
    }
    private ScrollViewer? Scroller => Rows.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    private void OnGlobalSizeChanged(object? sender, DaisySize size) => RebuildRows();
    /// <summary>Sets the thumbnail size through the size slider, which saves it and lays the rows out again.</summary>
    public void SetTileHeight(double height)
    {
        TileSize.Maximum = Math.Max(TileSize.Maximum, height);
        TileSize.Value = height;
    }
    private Control? BuildRow(GalleryRow? row)
    {
        if (row == null) return null;
        if (row is GalleryHeaderRow header)
        {
            var button = new DaisyButton { Content = header.Title, Height = header.Height, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left, Variant = DaisyButtonVariant.Ghost };
            button.Click += (_, _) =>
            {
                var select = header.Items.Any(i => !i.IsSelected);
                foreach (var item in header.Items) item.IsSelected = select;
                SelectionChanged?.Invoke();
            };
            return button;
        }
        var photos = (GalleryPhotoRow)row;
        var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = TileGap, Height = row.Height };
        foreach (var (item, width) in photos.Tiles)
        {
            var tile = new PhotoTile(item, width, photos.TileHeight, PhotosSettings.ShowFileNames);
            tile.Activated += (photo, modifiers, checkbox) => Activate(tile, photo, modifiers, checkbox);
            tile.OpenRequested += (photo, fullScreen) => (fullScreen ? FullScreenRequested : OpenRequested)?.Invoke(photo);
            tile.SelectionChanged += () => SelectionChanged?.Invoke();
            ConfigureTile?.Invoke(tile);
            panel.Children.Add(tile);
        }
        return panel;
    }
    private void Activate(PhotoTile tile, PhotoItem item, KeyModifiers modifiers, bool checkbox)
    {
        if (modifiers.HasFlag(KeyModifiers.Shift) && _anchor != null)
        {
            var first = _items.IndexOf(_anchor); var last = _items.IndexOf(item);
            if (first >= 0 && last >= 0)
                foreach (var photo in _items.Skip(Math.Min(first, last)).Take(Math.Abs(first - last) + 1))
                    photo.IsSelected = true;
        }
        else if (checkbox || modifiers.HasFlag(KeyModifiers.Control) || _items.Any(photo => photo.IsSelected)) item.IsSelected = !item.IsSelected;
        // A click plays a video in place where the system can play it.
        else if (item.IsVideo && VideoPlayer.CanPlay) { tile.TogglePlayback(); return; }
        else { OpenRequested?.Invoke(item); return; }
        _anchor = item;
        SelectionChanged?.Invoke();
    }
    public void SelectAll(bool select = true)
    {
        foreach (var item in _items) item.IsSelected = select;
        SelectionChanged?.Invoke();
    }
    public void ClearSelection()
    {
        SelectAll(false);
        _anchor = null;
    }
    public IReadOnlyList<PhotoItem> ContextItems(PhotoItem item) => item.IsSelected ? Selected : [item];
    private bool DateSorted => PhotosSettings.SortMode is PhotoSortMode.DateTakenNewest or PhotoSortMode.DateTakenOldest;
    private static bool NameSorted => PhotosSettings.SortMode is PhotoSortMode.NameAscending or PhotoSortMode.NameDescending;
    // The month and year of a row, which the header shows for the topmost one on the screen.
    private static string MonthOf(GalleryRow row) => GalleryLayout.FormatGroupTitle(row.Date, PhotoGroupMode.Month, PhotosLocalization.CurrentCulture);
    /// <summary>Scrolls to the photo and focuses it; false when the gallery does not show it.</summary>
    public bool FocusPhoto(PhotoItem? item)
    {
        if (item == null) return false;
        var row = _rows.OfType<GalleryPhotoRow>().FirstOrDefault(row => row.Tiles.Any(tile => tile.Item.Path == item.Path));
        if (row == null) return false;
        _anchorPaths = null; _layoutVersion++;
        Rows.ScrollIntoView(row);
        Rows.UpdateLayout();
        Rows.GetVisualDescendants().OfType<PhotoTile>().FirstOrDefault(tile => tile.Item.Path == item.Path)?.Focus();
        return true;
    }
    private void OnScroll(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is not ScrollViewer scroller) return;
        Timeline.Update(_rows, scroller.Offset.Y, scroller.Viewport.Height);
        var index = GalleryLayout.FindRowAt(_rows, scroller.Offset.Y + 4);
        if (index >= 0) DateChip.Text = MonthOf(_rows[index]);
    }
    // Puts the row of the first file with the letter at the top and focuses the file.
    private void GoToLetter(string letter)
    {
        if (Scroller is not { } scroller) return;
        for (var row = 0; row < _rows.Count; row++)
        {
            if (_rows[row] is not GalleryPhotoRow photos) continue;
            for (var column = 0; column < photos.Tiles.Count; column++)
            {
                if (Letters.LetterFor(photos.Tiles[column].Item.FileName) != letter) continue;
                _anchorPaths = null; _layoutVersion++;
                PageScroll.Show<PhotoTile>(scroller, Rows, row, column, 0, 0);
                return;
            }
        }
    }
    private void ResetFiltersClick(object? sender, RoutedEventArgs e)
    {
        _query = _query with { SearchText = null, TypeFilter = PhotoTypeFilter.All, OrientationFilter = PhotoOrientationFilter.Any };
        RefreshLabels(); ResetSearchRequested?.Invoke(); Refresh();
    }
    private void ImportClick(object? sender, RoutedEventArgs e) => ImportRequested?.Invoke();
    private void ImportFolderClick(object? sender, RoutedEventArgs e) => ImportFolderRequested?.Invoke();
    private void RescanClick(object? sender, RoutedEventArgs e) => RescanRequested?.Invoke();
    private void SlideshowClick(object? sender, RoutedEventArgs e) => SlideshowRequested?.Invoke();
    private void BackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke();
    private void SelectAllClick(object? sender, RoutedEventArgs e) => SelectAll();
}
