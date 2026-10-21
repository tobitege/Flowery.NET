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
    private PhotoItem? _anchor;
    private bool _updating;
    private double _layoutWidth;
    private int _layoutVersion;
    private PhotoItem? _resizeAnchor;
    private double _resizeDelta;
    public IReadOnlyList<PhotoItem> Items => _items;
    public PhotoQuery Query => _query;
    public IReadOnlyList<PhotoItem> Selected => _items.Where(item => item.IsSelected).ToList();
    public event Action<PhotoItem>? OpenRequested;
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
            _resizeAnchor = null; _layoutVersion++;
            if (Scroller is { } scroller) scroller.Offset = new Avalonia.Vector(0, offset);
        };
        AttachedToVisualTree += (_, _) => FlowerySizeManager.SizeChanged += OnGlobalSizeChanged;
        DetachedFromVisualTree += (_, _) => FlowerySizeManager.SizeChanged -= OnGlobalSizeChanged;
        TileSize.Value = PhotosSettings.TileHeight;
        Square.IsChecked = PhotosSettings.SquareTiles;
        Names.IsChecked = PhotosSettings.ShowFileNames;
        RefreshLabels();
        Sort.SelectionChanged += (_, _) => ApplyOptions();
        Group.SelectionChanged += (_, _) => ApplyOptions();
        Type.SelectionChanged += (_, _) => ApplyOptions();
        Orientation.SelectionChanged += (_, _) => ApplyOptions();
        Square.IsCheckedChanged += (_, _) => ApplyOptions();
        Names.IsCheckedChanged += (_, _) => ApplyOptions();
        TileSize.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) ApplyOptions(); };
        PointerWheelChanged += (_, e) =>
        {
            if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
            ZoomTiles(e.Delta.Y > 0);
            e.Handled = true;
        };
    }
    public void RefreshLabels()
    {
        _updating = true;
        string L(string key) => PhotosLocalization.GetString(key);
        Sort.ItemsSource = new[] { "DateNewest", "DateOldest", "Modified", "NameAsc", "NameDesc", "Size" }.Select(k => L("Photos_Sort_" + k));
        Group.ItemsSource = new[] { "Day", "Month", "Year", "None" }.Select(k => L("Photos_Group_" + k));
        Type.ItemsSource = new[] { "AllTypes", "Jpeg", "Png", "OtherTypes" }.Select(k => L("Photos_Filter_" + k));
        Orientation.ItemsSource = new[] { "AnyOrientation", "Landscape", "Portrait", "Square" }.Select(k => L("Photos_Filter_" + k));
        Sort.SelectedIndex = (int)PhotosSettings.SortMode;
        Group.SelectedIndex = (int)PhotosSettings.GroupMode;
        Type.SelectedIndex = (int)_query.TypeFilter;
        Orientation.SelectedIndex = (int)_query.OrientationFilter;
        _updating = false;
    }
    public void Show(PhotoQuery query, string title)
    {
        ClearSelection();
        _query = query; _title = title;
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
        DateChipFrame.IsVisible = _items.Count > 0 && DateSorted;
        Back.IsVisible = _query.Source is PhotoSourceKind.Album or PhotoSourceKind.Folder;
        var emptyKind = !string.IsNullOrWhiteSpace(_query.SearchText) || _query.TypeFilter != PhotoTypeFilter.All ||
            _query.OrientationFilter != PhotoOrientationFilter.Any ? "NoResults" : _query.Source switch
            {
                PhotoSourceKind.Favorites => "Favorites",
                PhotoSourceKind.Album => "Album",
                PhotoSourceKind.Folder => "Folder",
                _ => "Library"
            };
        EmptyTitle.Text = PhotosLocalization.GetString("Photos_Empty_" + emptyKind + "Title");
        EmptyText.Text = PhotosLocalization.GetString("Photos_Empty_" + (emptyKind == "Folder" ? "Library" : emptyKind) + "Text");
        ResetFilters.IsVisible = emptyKind == "NoResults";
        RebuildRows();
    }
    private void ApplyOptions()
    {
        if (_updating) return;
        PhotosSettings.SortMode = (PhotoSortMode)Math.Max(0, Sort.SelectedIndex);
        PhotosSettings.GroupMode = (PhotoGroupMode)Math.Max(0, Group.SelectedIndex);
        PhotosSettings.SquareTiles = Square.IsChecked == true;
        PhotosSettings.ShowFileNames = Names.IsChecked == true;
        PhotosSettings.TileHeight = TileSize.Value;
        _query = _query with { TypeFilter = (PhotoTypeFilter)Math.Max(0, Type.SelectedIndex), OrientationFilter = (PhotoOrientationFilter)Math.Max(0, Orientation.SelectedIndex) };
        PhotosSettings.SaveOptions();
        Refresh();
    }
    private void RebuildRows()
    {
        var scroller = Scroller;
        var offset = scroller?.Offset.Y ?? 0;
        var oldIndex = GalleryLayout.FindRowAt(_rows, offset);
        var oldRow = oldIndex >= 0 ? _rows[oldIndex] : null;
        if (_resizeAnchor == null)
        {
            _resizeAnchor = oldRow is GalleryPhotoRow photos ? photos.Tiles[0].Item : (oldRow as GalleryHeaderRow)?.Items.FirstOrDefault();
            _resizeDelta = offset - (oldRow?.Top ?? 0);
        }
        _layoutWidth = Bounds.Width;
        var factor = FlowerySizeManager.CurrentSize switch
        {
            DaisySize.ExtraSmall => .85, DaisySize.Small => 1, DaisySize.Medium => 1.12,
            DaisySize.Large => 1.25, DaisySize.ExtraLarge => 1.4, _ => 1
        };
        Timeline.IsVisible = DateSorted && _items.Count > 0;
        _rows = GalleryLayout.Build(_items, Math.Max(100, Bounds.Width - (Timeline.IsVisible ? 90 : 25)), PhotosSettings.TileHeight * factor, 6,
            DateSorted ? PhotosSettings.GroupMode : PhotoGroupMode.None, PhotosSettings.SquareTiles);
        Rows.ItemsSource = _rows;
        Timeline.Update(_rows, offset, scroller?.Viewport.Height ?? 0);
        var version = ++_layoutVersion;
        Dispatcher.UIThread.Post(() =>
        {
            if (version != _layoutVersion || Scroller is not { } current) return;
            var row = _rows.OfType<GalleryPhotoRow>().FirstOrDefault(row => row.Tiles.Any(tile => tile.Item == _resizeAnchor));
            if (row != null) current.Offset = new Avalonia.Vector(0, row.Top + Math.Clamp(_resizeDelta, 0, row.Height));
            _resizeAnchor = null;
            Timeline.Update(_rows, current.Offset.Y, current.Viewport.Height);
        }, DispatcherPriority.Loaded);
        DateChip.Text = _rows.Count == 0 ? "" : GalleryLayout.FormatLongDate(_rows[0].Date, PhotosLocalization.CurrentCulture);
    }
    private ScrollViewer? Scroller => Rows.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
    private void OnGlobalSizeChanged(object? sender, DaisySize size) => RebuildRows();
    public void ZoomTiles(bool larger) => TileSize.Value = Math.Clamp(TileSize.Value * (larger ? 1.12 : 1 / 1.12), 90, 420);
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
        var panel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 6, Height = row.Height };
        foreach (var (item, width) in photos.Tiles)
        {
            var tile = new PhotoTile(item, width, photos.TileHeight, PhotosSettings.ShowFileNames);
            tile.Activated += Activate;
            tile.SelectionChanged += () => SelectionChanged?.Invoke();
            ConfigureTile?.Invoke(tile);
            panel.Children.Add(tile);
        }
        return panel;
    }
    private void Activate(PhotoItem item, KeyModifiers modifiers, bool checkbox)
    {
        if (modifiers.HasFlag(KeyModifiers.Shift) && _anchor != null)
        {
            var first = _items.IndexOf(_anchor); var last = _items.IndexOf(item);
            if (first >= 0 && last >= 0)
                foreach (var photo in _items.Skip(Math.Min(first, last)).Take(Math.Abs(first - last) + 1))
                    photo.IsSelected = true;
        }
        else if (checkbox || modifiers.HasFlag(KeyModifiers.Control) || _items.Any(photo => photo.IsSelected)) item.IsSelected = !item.IsSelected;
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
    public void FocusPhoto(PhotoItem? item)
    {
        if (item == null) return;
        var row = _rows.OfType<GalleryPhotoRow>().FirstOrDefault(row => row.Tiles.Any(tile => tile.Item.Path == item.Path));
        if (row == null) return;
        _resizeAnchor = null; _layoutVersion++;
        Rows.ScrollIntoView(row);
        Rows.UpdateLayout();
        Rows.GetVisualDescendants().OfType<PhotoTile>().FirstOrDefault(tile => tile.Item.Path == item.Path)?.Focus();
    }
    private void OnScroll(object? sender, ScrollChangedEventArgs e)
    {
        if (e.Source is not ScrollViewer scroller) return;
        Timeline.Update(_rows, scroller.Offset.Y, scroller.Viewport.Height);
        var index = GalleryLayout.FindRowAt(_rows, scroller.Offset.Y + 4);
        if (index >= 0) DateChip.Text = PhotosSettings.GroupMode == PhotoGroupMode.Year
            ? GalleryLayout.FormatGroupTitle(_rows[index].Date, PhotoGroupMode.Month, PhotosLocalization.CurrentCulture)
            : GalleryLayout.FormatLongDate(_rows[index].Date, PhotosLocalization.CurrentCulture);
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
