using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
    public IReadOnlyList<PhotoItem> Items => _items;
    public PhotoQuery Query => _query;
    public event Action<PhotoItem>? OpenRequested;
    public event Action? SelectionChanged;
    public event Action<PhotoTile>? ConfigureTile;
    public event Action? ImportRequested;
    public event Action? SlideshowRequested;
    public event Action? BackRequested;

    public GalleryView()
    {
        InitializeComponent();
        DataContext = PhotosLocalization.Instance;
        Rows.ItemTemplate = new FuncDataTemplate<GalleryRow>((row, _) => BuildRow(row));
        SizeChanged += (_, _) =>
        {
            if (Math.Abs(_layoutWidth - Bounds.Width) > 0.5) RebuildRows();
        };
        Rows.AddHandler(ScrollViewer.ScrollChangedEvent, OnScroll);
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
            TileSize.Value = Math.Clamp(TileSize.Value + e.Delta.Y * 20, 90, 420);
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
        _query = query; _title = title;
        RefreshLabels();
        Refresh();
    }
    public void Search(string? text) { _query = _query with { SearchText = text }; Refresh(); }
    public void Refresh()
    {
        _items = PhotoLibrary.Instance.Query(_query with { Sort = PhotosSettings.SortMode });
        PageTitle.Text = _title;
        CountLabel.Text = PhotosLocalization.Format("Photos_ItemCount", _items.Count);
        Empty.IsVisible = _items.Count == 0;
        DateChipFrame.IsVisible = _items.Count > 0;
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
        _layoutWidth = Bounds.Width;
        _rows = GalleryLayout.Build(_items, Math.Max(100, Bounds.Width - 90), PhotosSettings.TileHeight, 6,
            PhotosSettings.GroupMode, PhotosSettings.SquareTiles);
        Rows.ItemsSource = _rows;
        Timeline.Children.Clear();
        foreach (var row in _rows.GroupBy(r => (r.Date.Year, r.Date.Month)).Select(g => g.First()))
        {
            var button = new DaisyButton { Content = row.Date.ToString("MMM\nyyyy", PhotosLocalization.CurrentCulture),
                Height = double.NaN, Padding = new Thickness(4), Variant = DaisyButtonVariant.Ghost };
            Avalonia.Automation.AutomationProperties.SetName(button, row.Date.ToString("Y", PhotosLocalization.CurrentCulture));
            Avalonia.Automation.AutomationProperties.SetAutomationId(button, "Photos.Timeline." + row.Date.ToString("yyyyMM"));
            button.Click += (_, _) => Rows.ScrollIntoView(row);
            Timeline.Children.Add(button);
        }
        DateChip.Text = _rows.Count == 0 ? "" : GalleryLayout.FormatLongDate(_rows[0].Date, PhotosLocalization.CurrentCulture);
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
        else if (checkbox || modifiers.HasFlag(KeyModifiers.Control)) item.IsSelected = !item.IsSelected;
        else { OpenRequested?.Invoke(item); return; }
        _anchor = item;
        SelectionChanged?.Invoke();
    }
    public void SelectAll(bool select = true)
    {
        foreach (var item in _items) item.IsSelected = select;
        SelectionChanged?.Invoke();
    }
    private void OnScroll(object? sender, ScrollChangedEventArgs e)
    {
        var index = GalleryLayout.FindRowAt(_rows, (e.Source as ScrollViewer)?.Offset.Y ?? 0);
        if (index >= 0) DateChip.Text = GalleryLayout.FormatLongDate(_rows[index].Date, PhotosLocalization.CurrentCulture);
    }
    private void ImportClick(object? sender, RoutedEventArgs e) => ImportRequested?.Invoke();
    private void SlideshowClick(object? sender, RoutedEventArgs e) => SlideshowRequested?.Invoke();
    private void BackClick(object? sender, RoutedEventArgs e) => BackRequested?.Invoke();
    private void SelectAllClick(object? sender, RoutedEventArgs e) => SelectAll();
}
