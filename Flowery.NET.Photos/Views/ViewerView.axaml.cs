using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Automation;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.NET.Photos.Editing;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using SkiaSharp;

namespace Flowery.NET.Photos.Views;

public sealed class Adjustment(string key, Func<double> read, Action<double> write) : INotifyPropertyChanged
{
    public string Key => key;
    public string ResetHint => PhotosLocalization.GetString("Photos_DoubleClickReset");
    public string Label => PhotosLocalization.GetString("Photos_Adj_" + key);
    public double Minimum => key == "Fade" ? 0 : -100;
    public double Value
    {
        get => read();
        set { if (Math.Abs(read() - value) > 0.001) write(value); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
}

public sealed record AdjustmentGroup(string Key, bool Expanded, IReadOnlyList<Adjustment> Items)
{
    public string Label => PhotosLocalization.GetString("Photos_Section_" + Key);
}

public partial class ViewerView : UserControl, IDisposable
{
    private readonly EditHistory _history = new();
    private readonly List<Adjustment> _adjustments = [];
    private readonly List<Bitmap> _previews = [];
    private readonly List<Color> _recent = [];
    private readonly DispatcherTimer _slideshow = new();
    private readonly DispatcherTimer _toolbarTimer = new() { Interval = TimeSpan.FromSeconds(3) };
    private CancellationTokenSource? _load;
    private CancellationTokenSource? _filterLoad;
    private EditState? _previewBasis;
    private string? _previewPath;
    public Task FilterPreviewUpdate { get; private set; } = Task.CompletedTask;
    private IReadOnlyList<PhotoItem> _items = [];
    private PhotoItem? _item;
    private bool _refreshing;
    private bool _navigating;
    private bool _saving;
    private bool _disposed;
    private int _previewVersion;
    private int _lastCustomIndex = -1;
    private bool _fullscreen;
    private WindowState _previousState;
    public bool HasChanges => _history.Current.HasChanges;
    public PhotoItem? Current => _item;
    public bool IsBusy => _saving || _load != null;
    public Func<Task<bool>>? ConfirmLeave { get; set; }
    public Func<IReadOnlyList<PhotoItem>, Task>? DeletePhoto { get; set; }
    public Func<PhotoItem, MenuItem>? AlbumMenu { get; set; }
    public event Action<string, bool>? Notification;
    public event Action? BackRequested;
    public event Action? SettingsRequested;
    public event Action<bool>? ImmersiveChanged;
    private Window Owner => (Window)TopLevel.GetTopLevel(this)!;
    private static string L(string key) => PhotosLocalization.GetString("Photos_" + key);

    internal PhotoLibrary Library { get; set; }
    public ViewerView() : this(PhotoLibrary.Instance) { }
    public ViewerView(PhotoLibrary library)
    {
        Library = library;
        InitializeComponent();
        DataContext = PhotosLocalization.Instance;
        _history.Changed += (_, _) => RefreshState();
        Canvas.Edited += state =>
        {
            if (state.Strokes.Count > _history.Current.Strokes.Count) RememberColor(state.Strokes[^1].Color);
            _history.Apply(state);
        };
        Canvas.ColorPicked += (color, secondary) =>
        {
            if (secondary) SecondaryColorEditor.Color = color;
            else ColorEditor.Color = color;
        };
        Canvas.ViewChanged += RefreshZoom;
        Compare.IsCheckedChanged += (_, _) => { Canvas.Compare = Compare.IsChecked == true; Canvas.InvalidateVisual(); };
        ColorEditor.ColorChanged += (_, e) => Canvas.PrimaryColor = e.Color;
        SecondaryColorEditor.ColorChanged += (_, e) =>
        {
            Canvas.SecondaryColor = e.Color; PhotosSettings.SecondaryColor = e.Color; PhotosSettings.SaveOptions();
        };
        Highlighter.IsCheckedChanged += (_, _) => Canvas.Highlighter = Highlighter.IsChecked == true;
        PenSize.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) Canvas.PenWidth = PenSize.Value; };
        Straighten.PropertyChanged += (_, e) =>
        {
            if (!_refreshing && e.Property == Slider.ValueProperty)
                _history.Apply(_history.Current with { Straighten = Straighten.Value }, "Straighten");
        };
        FilterIntensity.PropertyChanged += (_, e) =>
        {
            if (!_refreshing && e.Property == Slider.ValueProperty)
                _history.Apply(_history.Current with { FilterIntensity = FilterIntensity.Value }, "FilterIntensity");
        };
        ZoomSlider.PropertyChanged += (_, e) => { if (!_refreshing && e.Property == Slider.ValueProperty) Canvas.SetZoom(ZoomSlider.Value); };
        Ratio.SelectionChanged += (_, _) => ApplyAspect();
        foreach (var option in new[] { Rulers, Gridlines, Navigator, StatusBar, AutoHide })
            option.IsCheckedChanged += (_, _) => ApplyViewOptions();
        _slideshow.Tick += async (_, _) => await NavigateAsync(1);
        InitializeChrome();
        Canvas.AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        BuildAdjustments();
        RefreshLabels();
        _refreshing = true;
        Rulers.IsChecked = PhotosSettings.ShowRulers; Gridlines.IsChecked = PhotosSettings.ShowGrid;
        Navigator.IsChecked = PhotosSettings.ShowNavigator; StatusBar.IsChecked = PhotosSettings.ShowStatusBar;
        AutoHide.IsChecked = PhotosSettings.AutoHideToolbar;
        _refreshing = false;
        Canvas.SecondaryColor = PhotosSettings.SecondaryColor;
        SecondaryColorEditor.Color = PhotosSettings.SecondaryColor;
        BuildPalette();
    }

    private void BuildAdjustments()
    {
        _adjustments.Add(new Adjustment("Brightness", () => _history.Current.Brightness, value => _history.Apply(_history.Current with { Brightness = value }, "Brightness")));
        _adjustments.Add(new Adjustment("Exposure", () => _history.Current.Exposure, value => _history.Apply(_history.Current with { Exposure = value }, "Exposure")));
        _adjustments.Add(new Adjustment("Contrast", () => _history.Current.Contrast, value => _history.Apply(_history.Current with { Contrast = value }, "Contrast")));
        _adjustments.Add(new Adjustment("Highlights", () => _history.Current.Highlights, value => _history.Apply(_history.Current with { Highlights = value }, "Highlights")));
        _adjustments.Add(new Adjustment("Shadows", () => _history.Current.Shadows, value => _history.Apply(_history.Current with { Shadows = value }, "Shadows")));
        _adjustments.Add(new Adjustment("Saturation", () => _history.Current.Saturation, value => _history.Apply(_history.Current with { Saturation = value }, "Saturation")));
        _adjustments.Add(new Adjustment("Warmth", () => _history.Current.Warmth, value => _history.Apply(_history.Current with { Warmth = value }, "Warmth")));
        _adjustments.Add(new Adjustment("Tint", () => _history.Current.Tint, value => _history.Apply(_history.Current with { Tint = value }, "Tint")));
        _adjustments.Add(new Adjustment("Sharpness", () => _history.Current.Sharpness, value => _history.Apply(_history.Current with { Sharpness = value }, "Sharpness")));
        _adjustments.Add(new Adjustment("Clarity", () => _history.Current.Clarity, value => _history.Apply(_history.Current with { Clarity = value }, "Clarity")));
        _adjustments.Add(new Adjustment("Vignette", () => _history.Current.Vignette, value => _history.Apply(_history.Current with { Vignette = value }, "Vignette")));
        _adjustments.Add(new Adjustment("Fade", () => _history.Current.Fade, value => _history.Apply(_history.Current with { Fade = value }, "Fade")));
        RefreshAdjustmentGroups();
    }
    private void RefreshAdjustmentGroups() => Adjustments.ItemsSource = new[]
    {
        new AdjustmentGroup("Light", true, _adjustments.Take(5).ToArray()),
        new AdjustmentGroup("Color", true, _adjustments.Skip(5).Take(3).ToArray()),
        new AdjustmentGroup("Detail", false, _adjustments.Skip(8).Take(2).ToArray()),
        new AdjustmentGroup("Effects", false, _adjustments.Skip(10).ToArray())
    };
    private void ResetAdjustment(object? sender, TappedEventArgs e)
    {
        if (sender is Control { DataContext: Adjustment adjustment }) adjustment.Value = 0;
    }
    public void RefreshLabels()
    {
        _refreshing = true;
        var selected = Ratio.SelectedIndex;
        Ratio.ItemsSource = new[] { L("AspectFree"), L("AspectOriginal"), "1:1", "4:3", "3:2", "16:9", "3:4", "2:3", "9:16" };
        Ratio.SelectedIndex = Math.Max(0, selected);
        Harmony.ItemsSource = new[] { "Complementary", "Triad", "Analogous", "Monochrome" }.Select(k => L("Harmony_" + k));
        Harmony.SelectedIndex = 0;
        _refreshing = false;
        foreach (var adjustment in _adjustments) adjustment.Refresh();
        RefreshAdjustmentGroups();
        for (var index = 0; index < Filters.Children.Count; index++)
        {
            if (Filters.Children[index] is not DaisyButton { Content: StackPanel panel } button) continue;
            var label = PhotosLocalization.GetString(FilterPresets.All[index].LocalizationKey);
            if (panel.Children.LastOrDefault() is TextBlock text) text.Text = label;
            AutomationProperties.SetName(button, label);
        }
        BuildPalette();
        BuildColorActions();
        RefreshFavorite();
    }
    public async Task OpenAsync(PhotoItem item, IReadOnlyList<PhotoItem> items)
    {
        _load?.Cancel();
        _filterLoad?.Cancel(); _previewVersion++;
        var load = new CancellationTokenSource();
        _load = load;
        IsEnabled = false;
        FileName.Text = item.FileName;
        ErrorText.IsVisible = false;
        if (_item != null) _item.PropertyChanged -= OnPhotoPropertyChanged;
        _item = item; _items = items;
        _item.PropertyChanged += OnPhotoPropertyChanged;
        try
        {
            var bitmap = await Task.Run(() => PhotoDecoder.Load(item.Path), load.Token);
            if (load.IsCancellationRequested || _disposed) { bitmap.Dispose(); return; }
            RefreshFavorite();
            Canvas.SetPhoto(bitmap, new Size(bitmap.Width, bitmap.Height));
            _history.Reset();
            ClosePanel();
            _previewPath = null;
            await UpdateFilterPreviews();
            load.Token.ThrowIfCancellationRequested();
            var metadata = await ImageMetadataReader.ReadDetailsAsync(item.Path);
            if (load.IsCancellationRequested || _disposed) return;
            if (metadata is { } probe)
            {
                item.DateTaken = probe.DateTaken ?? item.DateTaken;
                item.CameraModel = string.IsNullOrWhiteSpace(probe.CameraModel) ? item.CameraModel : probe.CameraModel;
                item.Details = probe.Details ?? item.Details;
            }
            RefreshInfo(); RefreshNavigation(); ApplyChromeLayout();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            if (load.IsCancellationRequested || _disposed) return;
            Canvas.Dispose(); _history.Reset(); ClosePanel(); RefreshNavigation(); RefreshInfo();
            ErrorText.Text = PhotosLocalization.Format("Photos_LoadFailed", item.FileName, ex.Message);
            ErrorText.IsVisible = true;
        }
        finally
        {
            if (_load == load)
            {
                _load = null; IsEnabled = !_saving && !_disposed;
                foreach (var button in Toolbar.GetVisualDescendants().OfType<DaisyButton>())
                    button.IsEnabled = Canvas.Bitmap != null || button.Tag as string is "back" or "info" or "delete" or "fullscreen";
                if (IsEnabled && Canvas.Bitmap != null) Canvas.Focus();
            }
            load.Dispose();
        }
    }
    private async Task BuildFilterPreviewsAsync(string path, CancellationToken token)
    {
        var version = ++_previewVersion;
        var state = _history.Current with { Strokes = [] };
        var bytes = await Task.Run(() =>
        {
            using var source = PhotoDecoder.Load(path, 160);
            var result = new List<(FilterPreset Preset, byte[] Bytes)>();
            foreach (var preset in FilterPresets.All)
            {
                token.ThrowIfCancellationRequested();
                using var rendered = EditRenderer.Render(source, state with { Filter = preset.Id, FilterIntensity = 100 });
                using var image = SKImage.FromBitmap(rendered);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 90);
                result.Add((preset, encoded.ToArray()));
            }
            return result;
        }, token);
        token.ThrowIfCancellationRequested();
        if (_disposed || version != _previewVersion) return;
        Filters.Children.Clear();
        foreach (var preview in _previews) preview.Dispose();
        _previews.Clear();
        foreach (var (preset, data) in bytes)
        {
            using var stream = new MemoryStream(data);
            var bitmap = new Bitmap(stream); _previews.Add(bitmap);
            var stack = new StackPanel { Width = 84, Spacing = 4 };
            stack.Children.Add(new Image { Source = bitmap, Width = 84, Height = 72, Stretch = Stretch.UniformToFill });
            stack.Children.Add(new TextBlock { Text = PhotosLocalization.GetString(preset.LocalizationKey), FontSize = 12, TextTrimming = TextTrimming.CharacterEllipsis });
            var button = new DaisyButton
            {
                Content = stack, Width = 96, Height = 112, Padding = new Thickness(2), Tag = preset.Id,
                BorderThickness = new Thickness(2), Variant = DaisyButtonVariant.Ghost
            };
            AutomationProperties.SetAutomationId(button, "Photos.Filter." + preset.Id);
            AutomationProperties.SetName(button, PhotosLocalization.GetString(preset.LocalizationKey));
            button.Click += (_, _) => _history.Apply(_history.Current with
            {
                Filter = preset.Id, FilterIntensity = _history.Current.Filter == preset.Id ? _history.Current.FilterIntensity : 100
            });
            Filters.Children.Add(button);
        }
        RefreshFilterSelection();
    }
    private Task UpdateFilterPreviews()
    {
        if (_item == null) return Task.CompletedTask;
        var basis = _history.Current with { Filter = FilterPresets.NoneId, FilterIntensity = 100, Strokes = [] };
        if (_previewPath == _item.Path && _previewBasis == basis) return FilterPreviewUpdate;
        _previewPath = _item.Path; _previewBasis = basis;
        _filterLoad?.Cancel();
        var load = new CancellationTokenSource();
        _filterLoad = load;
        return FilterPreviewUpdate = LoadFilterPreviewsAsync(_item.Path, load);
    }
    private async Task LoadFilterPreviewsAsync(string path, CancellationTokenSource load)
    {
        try { await BuildFilterPreviewsAsync(path, load.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_disposed && !load.IsCancellationRequested) Notification?.Invoke(ex.Message, true); }
        finally { if (_filterLoad == load) _filterLoad = null; load.Dispose(); }
    }
    private void RefreshFilterSelection()
    {
        var selected = this.FindResource("PhotosAccentBrush") as IBrush ?? Brushes.DodgerBlue;
        foreach (var button in Filters.Children.OfType<DaisyButton>())
        {
            button.IsActive = button.Tag as string == _history.Current.Filter;
            button.BorderBrush = button.IsActive ? selected : Brushes.Transparent;
        }
    }
    private void RefreshState()
    {
        _refreshing = true;
        Canvas.State = _history.Current;
        Straighten.Value = _history.Current.Straighten;
        FilterIntensity.Value = _history.Current.FilterIntensity;
        foreach (var adjustment in _adjustments) adjustment.Refresh();
        Undo.IsEnabled = _history.CanUndo; Redo.IsEnabled = _history.CanRedo;
        EditedBadge.IsVisible = HasChanges;
        _refreshing = false;
        RefreshZoom();
        RefreshImageStatus();
        RefreshFilterSelection();
        if (_activePanel == "filters" && _load == null) _ = UpdateFilterPreviews();
    }
    private void RefreshZoom()
    {
        ZoomText.Text = $"{Canvas.ActualZoom:0}%";
        _refreshing = true; ZoomSlider.Value = Canvas.ActualZoom; _refreshing = false;
    }
    private string _lastEditPanel = "adjust";
    private string? _activePanel;
    public void BeginEditing()
    {
        if (Canvas.Bitmap == null) return;
        if (SidePanel.IsVisible && TopSaveGroup.IsVisible) ClosePanel();
        else SelectPanel(_lastEditPanel);
    }
    private void ClosePanel()
    {
        SidePanel.IsVisible = TopSaveGroup.IsVisible = false;
        var cropping = Canvas.IsCropping;
        Canvas.IsCropping = Canvas.IsDrawing = false;
        _activePanel = null;
        ZoomSlider.IsEnabled = true;
        if (cropping) Canvas.Fit();
        Canvas.InvalidateVisual();
    }
    private void SelectPanel(string panel)
    {
        _activePanel = panel;
        SidePanel.IsVisible = true;
        TopSaveGroup.IsVisible = panel is "crop" or "adjust" or "filters" or "markup";
        if (TopSaveGroup.IsVisible) _lastEditPanel = panel;
        CropPanel.IsVisible = panel == "crop"; AdjustPanel.IsVisible = panel == "adjust";
        FilterPanel.IsVisible = panel == "filters"; MarkupPanel.IsVisible = panel == "markup";
        InfoPanel.IsVisible = panel == "info"; ViewPanel.IsVisible = panel == "view";
        if (panel == "info") RefreshInfo();
        Canvas.IsCropping = panel == "crop"; Canvas.IsDrawing = panel == "markup";
        ZoomSlider.IsEnabled = !Canvas.IsCropping;
        if (Canvas.IsCropping) Canvas.Fit();
        Canvas.InvalidateVisual();
    }
    private void ApplyAspect()
    {
        if (_refreshing || Ratio.SelectedIndex < 0 || Canvas.Bitmap == null) return;
        var size = EditRenderer.GetTransformedSize(new Size(Canvas.Bitmap.Width, Canvas.Bitmap.Height), _history.Current);
        double[] ratios = [0, size.Width / size.Height, 1, 4.0 / 3, 1.5, 16.0 / 9, .75, 2.0 / 3, 9.0 / 16];
        Canvas.AspectRatio = ratios[Ratio.SelectedIndex];
        if (Canvas.AspectRatio <= 0) return;
        _history.Apply(_history.Current with { Crop = Canvas.CreateAspectCrop(Canvas.AspectRatio) });
    }
    private void ApplyViewOptions()
    {
        if (_refreshing) return;
        PhotosSettings.ShowRulers = Rulers.IsChecked == true;
        PhotosSettings.ShowGrid = Gridlines.IsChecked == true;
        PhotosSettings.ShowNavigator = Navigator.IsChecked == true;
        PhotosSettings.ShowStatusBar = StatusBar.IsChecked == true;
        PhotosSettings.AutoHideToolbar = AutoHide.IsChecked == true;
        PhotosSettings.SaveOptions(); ApplyChromeLayout(); Canvas.InvalidateVisual();
    }
    private void OnPhotoPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(PhotoItem.IsFavorite)) RefreshFavorite();
    }

    private void RefreshFavorite()
    {
        var favorite = _item?.IsFavorite == true;
        FavoriteButton.IsActive = favorite;
        FavoriteButton.Variant = favorite ? DaisyButtonVariant.Primary : DaisyButtonVariant.Ghost;
        FavoriteButton.ButtonStyle = favorite ? DaisyButtonStyle.Soft : DaisyButtonStyle.Default;
        var name = L(favorite ? "Unfavorite" : "Favorite");
        AutomationProperties.SetName(FavoriteButton, name);
        ToolTip.SetTip(FavoriteButton, name);
    }

    private void RefreshInfo()
    {
        if (_item == null) return;
        var width = Canvas.Bitmap?.Width ?? _item.PixelWidth;
        var height = Canvas.Bitmap?.Height ?? _item.PixelHeight;
        InfoPanel.Children.Clear();
        InfoPanel.Children.Add(new SelectableTextBlock { Text = _item.FileName, FontSize = 16, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap });
        AddInfo("Folder", _item.FolderPath, "reveal");
        AddInfo("FileSize", FileOperations.FormatFileSize(_item.FileSize));
        AddInfo("Dimensions", $"{width} × {height}  ·  {(double)width * height / 1_000_000:0.0} MP");
        AddInfo("DateTaken", _item.DateTaken?.ToString("f", PhotosLocalization.CurrentCulture));
        AddInfo("Modified", _item.DateModified.ToString("f", PhotosLocalization.CurrentCulture));
        AddInfo("Camera", _item.CameraModel);
        AddInfo("Lens", _item.Details?.Lens);
        AddInfo("Exposure", _item.Details?.Exposure);
        if (_item.Details is { Latitude: { } latitude, Longitude: { } longitude })
            AddInfo("Location", string.Format(CultureInfo.InvariantCulture, "{0:0.00000}, {1:0.00000}", latitude, longitude), "map");
        AddInfo("Albums", string.Join(", ", Library.Albums.Where(album => album.Paths.Contains(_item.Path, StringComparer.OrdinalIgnoreCase)).Select(album => album.Name)));
        RefreshImageStatus();
    }
    private void AddInfo(string key, string? value, string? action = null)
    {
        if (string.IsNullOrWhiteSpace(value)) return;
        var row = new StackPanel { Spacing = 2 };
        row.Children.Add(new TextBlock { Text = L("Info_" + key), Opacity = .65 });
        Control content;
        if (action == null) content = new SelectableTextBlock { Text = value, TextWrapping = TextWrapping.Wrap };
        else
        {
            var link = new DaisyButton
            {
                Content = new TextBlock { Text = value, TextWrapping = TextWrapping.Wrap, TextDecorations = TextDecorations.Underline },
                Tag = action, Variant = DaisyButtonVariant.Ghost, Padding = new Thickness(0), Height = double.NaN,
                HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Left
            };
            link.Click += ActionClick;
            content = link;
        }
        AutomationProperties.SetAutomationId(content, "Viewer.Info." + key);
        AutomationProperties.SetName(content, L("Info_" + key) + ": " + value);
        row.Children.Add(content); InfoPanel.Children.Add(row);
    }
    private void RefreshImageStatus()
    {
        if (_item == null || Canvas.Bitmap == null) { ImageMetadata.Text = string.Empty; return; }
        var size = EditRenderer.GetOutputSize(new Size(Canvas.Bitmap.Width, Canvas.Bitmap.Height), _history.Current);
        var index = _items.ToList().FindIndex(item => item.Path == _item.Path);
        ImageMetadata.Text = $"{index + 1} / {_items.Count}  ·  {size.Width:0} × {size.Height:0}  ·  {FileOperations.FormatFileSize(_item.FileSize)}";
    }
    public async Task NavigateAsync(int offset)
    {
        if (_navigating || _item == null || _items.Count < 2) return;
        var index = _items.ToList().FindIndex(i => i.Path == _item.Path);
        var target = index + offset;
        if (_slideshow.IsEnabled) target = (target % _items.Count + _items.Count) % _items.Count;
        if (target < 0 || target >= _items.Count || target == index) return;
        _navigating = true;
        try
        {
            if (HasChanges && ConfirmLeave != null && !await ConfirmLeave()) { StopSlideshow(); return; }
            await OpenAsync(_items[target], _items);
        }
        finally { _navigating = false; }
    }
    public void Stop()
    {
        StopSlideshow(); _toolbarTimer.Stop(); _hintTimer.Stop();
        if (_fullscreen) ToggleFullScreen();
    }
    public async Task<bool> SaveAsync(bool replace)
    {
        if (_item == null || Canvas.Bitmap == null || _saving || !HasChanges) return false;
        _saving = true;
        IsEnabled = false;
        try
        {
            var target = _item.Path;
            var extension = Path.GetExtension(target);
            var writable = EditRenderer.CanEncode(extension);
            if (!writable) replace = false;
            if (replace)
            {
                if (await new PromptDialog(L("SaveOverwrite"), L("OverwriteWarning"), accept: L("SaveOverwrite"), destructive: true).ShowDialog<int>(Owner) != 1) return false;
            }
            else
            {
                target = FileOperations.GetUniquePath(Path.Combine(_item.FolderPath,
                    $"{Path.GetFileNameWithoutExtension(target)} ({L("EditedSuffix")}){(writable ? extension : ".jpg")}"));
            }
            await EditRenderer.SaveAsync(_item.Path, target, _history.Current, PhotosSettings.JpegQuality, replace);
            var refreshed = Library.Refresh(target);
            if (refreshed != null)
            {
                var items = _items.ToList();
                var index = items.FindIndex(item => item.Path == _item.Path);
                if (replace && index >= 0) items[index] = refreshed;
                else items.Insert(index + 1, refreshed);
                await OpenAsync(refreshed, items);
            }
            else _history.Reset();
            Notification?.Invoke(replace ? L("Saved") : PhotosLocalization.Format("Photos_SavedAs", Path.GetFileName(target)), false);
            return true;
        }
        catch (Exception ex) { Notification?.Invoke(PhotosLocalization.Format("Photos_SaveFailed", ex.Message), true); return false; }
        finally { _saving = false; IsEnabled = !_disposed; if (IsEnabled) Canvas.Focus(); }
    }
    private void BuildPalette()
    {
        Palette.Children.Clear();
        foreach (var (color, index) in PhotosSettings.CustomColors.Select((color, index) => (color, index)))
        {
            var button = new DaisyButton { Width = 28, Height = 28, MinWidth = 0, Padding = new Thickness(0), Margin = new Thickness(2), Background = new SolidColorBrush(color), Content = " " };
            AutomationProperties.SetAutomationId(button, "Photos.Palette.Custom." + index);
            AutomationProperties.SetName(button, L("CustomColors") + " " + (index + 1) + " " + color);
            button.Click += (_, _) => { _lastCustomIndex = index; ColorEditor.Color = color; };
            ToolTip.SetTip(button, L("CustomColorHint"));
            var store = new MenuItem { Header = L("StoreCurrentColor") };
            store.Click += (_, _) =>
            {
                StoreCustomColor(index, ColorEditor.Color);
            };
            var reset = new MenuItem { Header = L("ResetColor") };
            reset.Click += (_, _) =>
            {
                StoreCustomColor(index, Colors.White);
            };
            button.ContextMenu = new ContextMenu { ItemsSource = new[] { store, reset } };
            Palette.Children.Add(button);
        }
    }
    public void ApplyPalette(IEnumerable<Color> colors)
    {
        var values = colors.Take(PhotosSettings.CustomColors.Length).ToArray();
        for (var index = 0; index < PhotosSettings.CustomColors.Length; index++)
            PhotosSettings.CustomColors[index] = index < values.Length ? values[index] : Colors.White;
        _lastCustomIndex = -1;
        PhotosSettings.SaveOptions(); BuildPalette();
    }
    private void StoreCustomColor(int index, Color color)
    {
        PhotosSettings.CustomColors[index] = color;
        _lastCustomIndex = index;
        PhotosSettings.SaveOptions(); BuildPalette();
    }
    private int NextCustomSlot()
    {
        var free = Array.FindIndex(PhotosSettings.CustomColors, color => color == Colors.White);
        return free >= 0 ? free : Math.Max(0, _lastCustomIndex);
    }
    public IReadOnlyList<Color> ExportPaletteColors() =>
        new[] { ColorEditor.Color, SecondaryColorEditor.Color }.Concat(PhotosSettings.CustomColors).Concat(_recent).Distinct().ToArray();
    private void RememberColor(Color usedColor)
    {
        _recent.Remove(usedColor); _recent.Insert(0, usedColor);
        if (_recent.Count > 9) _recent.RemoveRange(9, _recent.Count - 9);
        RecentColors.Children.Clear();
        foreach (var color in _recent.Take(9))
        {
            var button = new DaisyButton { Width = 28, Height = 28, MinWidth = 0, Padding = new Thickness(0), Background = new SolidColorBrush(color), Content = " " };
            AutomationProperties.SetAutomationId(button, "Photos.Palette.Recent." + color);
            AutomationProperties.SetName(button, L("RecentColors") + " " + color);
            button.Click += (_, _) => ColorEditor.Color = color; RecentColors.Children.Add(button);
        }
    }
    private void BuildColorActions()
    {
        ColorActions.Children.Clear();
        (string Key, Func<Color, Color> Transform)[] actions =
        [
            ("Lighter", color => ColorTools.AdjustLightness(color, .1)),
            ("Darker", color => ColorTools.AdjustLightness(color, -.1)),
            ("MoreSaturation", color => ColorTools.AdjustSaturation(color, .1)),
            ("LessSaturation", color => ColorTools.AdjustSaturation(color, -.1)),
            ("Complement", color => ColorTools.RotateHue(color, 180)),
            ("Invert", ColorTools.Invert), ("Grayscale", ColorTools.Grayscale), ("Warmer", ColorTools.Warmer)
        ];
        foreach (var (key, transform) in actions)
        {
            var button = new DaisyButton { Content = L("Color_" + key), Margin = new Thickness(0, 0, 6, 6) };
            AutomationProperties.SetAutomationId(button, "Photos.Color." + key);
            AutomationProperties.SetName(button, L("Color_" + key));
            button.Click += (_, _) => ColorEditor.Color = transform(ColorEditor.Color);
            ColorActions.Children.Add(button);
        }
    }
    private async Task PaletteFileAsync(bool import)
    {
        if (import)
        {
            var files = await Owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                FileTypeFilter = [new FilePickerFileType("Palette") { Patterns = ["*.pal", "*.gpl"] }]
            });
            foreach (var file in files)
            {
                using (file)
                    if (file.TryGetLocalPath() is { } path)
                    {
                        var colors = ColorTools.ReadPalette(path);
                        if (colors.Count == 0) { Notification?.Invoke(L("ActionFailed"), true); continue; }
                        ApplyPalette(colors);
                        Notification?.Invoke(PhotosLocalization.Format("Photos_Palette_Imported", Math.Min(colors.Count, PhotosSettings.CustomColors.Length)), false);
                    }
            }
        }
        else
        {
            using var file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = "palette.pal", DefaultExtension = "pal",
                FileTypeChoices = [new FilePickerFileType("JASC-PAL") { Patterns = ["*.pal"] }, new FilePickerFileType("GIMP") { Patterns = ["*.gpl"] }]
            });
            if (file?.TryGetLocalPath() is not { } path) return;
            if (Path.GetExtension(path).Equals(".gpl", StringComparison.OrdinalIgnoreCase))
                await File.WriteAllLinesAsync(path, new[] { "GIMP Palette", "Name: Flowery Photos", "#" }.Concat(ExportPaletteColors().Select(c => $"{c.R} {c.G} {c.B}")));
            else ColorTools.WriteJascPalette(path, ExportPaletteColors());
            Notification?.Invoke(PhotosLocalization.Format("Photos_Palette_Exported", Path.GetFileName(path)), false);
        }
    }
    private async Task CopyEditedAsync()
    {
        if (_item == null || Canvas.Bitmap == null) return;
        if (Owner.Clipboard == null) throw new InvalidOperationException(L("ActionFailed"));
        if (!HasChanges)
        {
            var count = await FileOperations.CopyFilesAsync(Owner, [_item.Path]);
            Notification?.Invoke(PhotosLocalization.Format("Photos_CopiedCount", count), count != 1);
            return;
        }
        using var source = await Task.Run(() => PhotoDecoder.Load(_item.Path));
        using var rendered = EditRenderer.Render(source, _history.Current);
        using var image = SKImage.FromBitmap(rendered);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        using var bitmap = new Bitmap(stream);
        await Owner.Clipboard.SetBitmapAsync(bitmap);
        Notification?.Invoke(L("CopiedImage"), false);
    }
    private void MoreOpening(object? sender, EventArgs e)
    {
        if (sender is not MenuFlyout menu) return;
        menu.Items.Clear();
        if (_item == null) return;
        void Add(string key, string action)
        {
            var entry = new MenuItem { Header = L(key), Tag = action };
            AutomationProperties.SetAutomationId(entry, "Viewer.More." + action);
            AutomationProperties.SetName(entry, L(key));
            entry.Click += ActionClick;
            menu.Items.Add(entry);
        }
        Add("RotateLeft", "left"); Add("RotateRight", "right");
        if (AlbumMenu != null) menu.Items.Add(AlbumMenu(_item));
        Add("OpenWith", "openWith"); Add("ShowInExplorer", "reveal"); Add("CopyPath", "copyPath");
        if (OperatingSystem.IsWindows()) Add("SetAsBackground", "wallpaper");
    }
    private async Task ExecuteAsync(string action)
    {
        if (Canvas.Bitmap == null && action is not ("back" or "previous" or "next" or "info" or "fullscreen" or "delete" or "settings" or "closePanel")) return;
        switch (action)
        {
            case "back": BackRequested?.Invoke(); break;
            case "previous": await NavigateAsync(-1); break;
            case "next": await NavigateAsync(1); break;
            case "edit": BeginEditing(); break;
            case "favorite": if (_item != null) Library.ToggleFavorite(_item); break;
            case "slideshow": if (IsSlideshow) StopSlideshow(); else StartSlideshow(); break;
            case "fullscreen": ToggleFullScreen(); break;
            case "undo": _history.Undo(); break;
            case "redo": _history.Redo(); break;
            case "revert": _history.Apply(EditState.Original); break;
            case "info": case "view":
                if (SidePanel.IsVisible && _activePanel == action) ClosePanel(); else SelectPanel(action);
                break;
            case "crop": case "adjust": case "filters": case "markup":
                SelectPanel(action);
                if (action == "filters" && _item != null)
                    await UpdateFilterPreviews();
                break;
            case "closePanel": ClosePanel(); break;
            case "closeCompare": Compare.IsChecked = false; break;
            case "left": _history.Apply(_history.Current.Rotate(false)); break;
            case "right": _history.Apply(_history.Current.Rotate(true)); break;
            case "flipH": _history.Apply(_history.Current.Mirror(true)); break;
            case "flipV": _history.Apply(_history.Current.Mirror(false)); break;
            case "resetCrop": _history.Apply(_history.Current.ResetGeometry()); break;
            case "resetAdjust": _history.Apply(_history.Current.ResetAdjustments()); break;
            case "enhance": if (Canvas.Bitmap != null) _history.Apply(AutoEnhance.Apply(Canvas.Bitmap, _history.Current)); break;
            case "save": await SaveAsync(false); break;
            case "replace": await SaveAsync(true); break;
            case "copy": await CopyEditedAsync(); break;
            case "openWith": if (_item != null) FileOperations.OpenWith(_item.Path); break;
            case "reveal": if (_item != null) FileOperations.ShowInExplorer(_item.Path); break;
            case "copyPath":
                if (_item != null && Owner.Clipboard != null)
                { await Owner.Clipboard.SetTextAsync(_item.Path); Notification?.Invoke(L("PathCopied"), false); }
                break;
            case "wallpaper":
                if (_item != null)
                {
                    var success = FileOperations.SetAsWallpaper(_item.Path);
                    Notification?.Invoke(L(success ? "BackgroundSet" : "ActionFailed"), !success);
                }
                break;
            case "zoomIn": Canvas.ZoomStep(true); break;
            case "zoomOut": Canvas.ZoomStep(false); break;
            case "fit": Canvas.Fit(); break;
            case "actual": Canvas.SetZoom(100); break;
            case "swap":
                (Canvas.PrimaryColor, Canvas.SecondaryColor) = (Canvas.SecondaryColor, Canvas.PrimaryColor);
                ColorEditor.Color = Canvas.PrimaryColor;
                SecondaryColorEditor.Color = Canvas.SecondaryColor;
                PhotosSettings.SecondaryColor = Canvas.SecondaryColor; PhotosSettings.SaveOptions(); break;
            case "eyedropper": Canvas.Eyedropper = true; break;
            case "storeColor": StoreCustomColor(NextCustomSlot(), ColorEditor.Color); break;
            case "harmony": ApplyPalette(ColorTools.CreateHarmony(ColorEditor.Color, (ColorTools.Harmony)Math.Max(0, Harmony.SelectedIndex))); break;
            case "extract":
                if (Canvas.Bitmap != null)
                {
                    using var edited = EditRenderer.Render(Canvas.Bitmap, _history.Current with { Strokes = [] });
                    ApplyPalette(ColorTools.ExtractDominantColors(edited));
                }
                break;
            case "clearMarkup": _history.Apply(_history.Current with { Strokes = [] }); break;
            case "importPalette": await PaletteFileAsync(true); break;
            case "exportPalette": await PaletteFileAsync(false); break;
            case "settings": SettingsRequested?.Invoke(); break;
            case "delete":
                if (_item != null && DeletePhoto != null)
                {
                    await DeletePhoto([_item]);
                    if (!_item.Exists) { _history.Reset(); BackRequested?.Invoke(); }
                }
                break;
            case "map":
                if (_item?.Details is { Latitude: { } latitude, Longitude: { } longitude })
                    await Owner.Launcher.LaunchUriAsync(new Uri(string.Format(CultureInfo.InvariantCulture,
                        "https://www.openstreetmap.org/?mlat={0}&mlon={1}#map=14/{0}/{1}", latitude, longitude)));
                break;
        }
    }
    private async void ActionClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control { Tag: string action }) return;
        try { await ExecuteAsync(action); } catch (Exception ex) { Notification?.Invoke(ex.Message, true); }
    }
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!PhotosSettings.WheelNavigates || Canvas.IsCropping || Canvas.IsSlideshow || e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        e.Handled = true; _ = NavigateAsync(e.Delta.Y > 0 ? -1 : 1);
    }
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Handled) return;
        if (IsSlideshow)
        {
            e.Handled = true;
            if (e.Key is Key.Escape or Key.F5) StopSlideshow();
            else if (e.Key == Key.Left) await NavigateAsync(-1);
            else if (e.Key is Key.Right or Key.Space) await NavigateAsync(1);
            return;
        }
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        if (e.Source is Visual source)
        {
            var ancestors = source.GetSelfAndVisualAncestors().ToArray();
            if (ancestors.Any(visual => visual is TextBox)) return;
            if (!ctrl && e.Key is Key.Left or Key.Right or Key.Up or Key.Down or Key.Home or Key.End or Key.PageUp or Key.PageDown &&
                ancestors.Any(visual => visual is RangeBase or SelectingItemsControl)) return;
        }
        if (e.Key is Key.Home or Key.End && _item != null)
        {
            e.Handled = true;
            var index = _items.ToList().FindIndex(item => item.Path == _item.Path);
            await NavigateAsync((e.Key == Key.Home ? 0 : _items.Count - 1) - index);
            return;
        }
        var action = e.Key switch
        {
            Key.Left or Key.PageUp => "previous", Key.Right or Key.PageDown => "next", Key.F11 => "fullscreen", Key.F5 => "slideshow",
            Key.F when !ctrl => "favorite", Key.R => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "left" : "right",
            Key.X when Canvas.IsDrawing => "swap", Key.I when !ctrl => "info",
            Key.Add or Key.OemPlus => "zoomIn", Key.Subtract or Key.OemMinus => "zoomOut",
            Key.D0 or Key.NumPad0 => "fit", Key.D1 or Key.NumPad1 => "actual",
            Key.Z when ctrl => shift ? "redo" : "undo", Key.Y when ctrl => "redo",
            Key.S when ctrl => shift ? "replace" : "save", Key.E when ctrl => "edit", Key.C when ctrl => "copy",
            Key.Escape => _fullscreen ? "fullscreen" : Compare.IsChecked == true ? "closeCompare" : SidePanel.IsVisible ? "closePanel" : "back",
            Key.Delete => "delete",
            _ => null
        };
        if (action == null) return;
        e.Handled = true;
        try { await ExecuteAsync(action); } catch (Exception ex) { Notification?.Invoke(ex.Message, true); }
    }
    public void Dispose()
    {
        _disposed = true;
        _previewVersion++;
        Stop(); _load?.Cancel(); _filterLoad?.Cancel();
        if (_item != null) _item.PropertyChanged -= OnPhotoPropertyChanged;
        Canvas.Dispose();
        foreach (var preview in _previews) preview.Dispose();
        _previews.Clear();
    }
}
