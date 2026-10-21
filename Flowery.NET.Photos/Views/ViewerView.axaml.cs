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
    public string Label => PhotosLocalization.GetString("Photos_Adj_" + key);
    public double Value
    {
        get => read();
        set { if (Math.Abs(read() - value) > 0.001) write(value); }
    }
    public event PropertyChangedEventHandler? PropertyChanged;
    public void Refresh() => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
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
    private IReadOnlyList<PhotoItem> _items = [];
    private PhotoItem? _item;
    private bool _refreshing;
    private bool _navigating;
    private bool _saving;
    private bool _disposed;
    private int _previewVersion;
    private bool _fullscreen;
    private bool _pointerAtToolbar;
    private WindowState _previousState;
    public bool HasChanges => _history.Current.HasChanges;
    public bool IsBusy => _saving || _load != null;
    public Func<Task<bool>>? ConfirmLeave { get; set; }
    public Func<IReadOnlyList<PhotoItem>, Task>? DeletePhoto { get; set; }
    public event Action? BackRequested;
    public event Action? SettingsRequested;
    public event Action<bool>? ImmersiveChanged;
    private Window Owner => (Window)TopLevel.GetTopLevel(this)!;
    private static string L(string key) => PhotosLocalization.GetString("Photos_" + key);

    public ViewerView()
    {
        InitializeComponent();
        DataContext = PhotosLocalization.Instance;
        _history.Changed += (_, _) => RefreshState();
        Canvas.Edited += state => _history.Apply(state);
        Canvas.ColorPicked += color => ColorEditor.Color = color;
        Canvas.ViewChanged += RefreshZoom;
        Compare.IsCheckedChanged += (_, _) => { Canvas.Compare = Compare.IsChecked == true; Canvas.InvalidateVisual(); };
        ColorEditor.ColorChanged += (_, e) => { Canvas.PrimaryColor = e.Color; RememberColor(); };
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
        _toolbarTimer.Tick += (_, _) =>
        {
            if (!_pointerAtToolbar && (PhotosSettings.AutoHideToolbar || _fullscreen)) Toolbar.IsVisible = false;
            _toolbarTimer.Stop();
        };
        Canvas.AddHandler(PointerPressedEvent, (_, _) =>
        {
            if (_slideshow.IsEnabled) _slideshow.Stop();
            if (PhotosSettings.AutoHideToolbar || _fullscreen) Toolbar.IsVisible = false;
        }, RoutingStrategies.Tunnel, handledEventsToo: true);
        PointerMoved += (_, e) =>
        {
            var point = e.GetPosition(this);
            _pointerAtToolbar = Toolbar.IsVisible && point.Y < Toolbar.Bounds.Bottom + 12;
            if (point.Y < 20) { Toolbar.IsVisible = true; _toolbarTimer.Stop(); _toolbarTimer.Start(); }
            if (_fullscreen) StatusPanel.IsVisible = point.Y > Bounds.Height - 60;
        };
        AddHandler(PointerWheelChangedEvent, OnWheel, RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);
        BuildAdjustments();
        RefreshLabels();
        Rulers.IsChecked = PhotosSettings.ShowRulers; Gridlines.IsChecked = PhotosSettings.ShowGrid;
        Navigator.IsChecked = PhotosSettings.ShowNavigator; StatusBar.IsChecked = PhotosSettings.ShowStatusBar;
        AutoHide.IsChecked = PhotosSettings.AutoHideToolbar;
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
        Adjustments.ItemsSource = _adjustments;
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
        var load = new CancellationTokenSource();
        _load = load;
        IsEnabled = false;
        FileName.Text = item.FileName;
        try
        {
            var bitmap = await Task.Run(() => PhotoDecoder.Load(item.Path, 2560), load.Token);
            if (load.IsCancellationRequested || _disposed) { bitmap.Dispose(); return; }
            if (_item != null) _item.PropertyChanged -= OnPhotoPropertyChanged;
            _item = item; _items = items;
            _item.PropertyChanged += OnPhotoPropertyChanged;
            RefreshFavorite();
            Canvas.SetPhoto(bitmap, new Size(item.PixelWidth > 0 ? item.PixelWidth : bitmap.Width,
                item.PixelHeight > 0 ? item.PixelHeight : bitmap.Height));
            _history.Reset();
            SidePanel.IsVisible = TopSaveGroup.IsVisible = false; Canvas.IsCropping = Canvas.IsDrawing = false;
            await BuildFilterPreviewsAsync(item.Path, load.Token);
            RefreshInfo(); Canvas.Focus();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { FileName.Text = PhotosLocalization.Format("Photos_LoadFailed", item.FileName, ex.Message); }
        finally
        {
            if (_load == load) { _load = null; IsEnabled = !_saving && !_disposed; }
            load.Dispose();
        }
    }
    private async Task BuildFilterPreviewsAsync(string path, CancellationToken token)
    {
        var version = ++_previewVersion;
        var state = _history.Current;
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
            var stack = new StackPanel { Width = 112, Spacing = 4 };
            stack.Children.Add(new Image { Source = bitmap, Width = 112, Height = 75, Stretch = Stretch.UniformToFill });
            stack.Children.Add(new TextBlock { Text = PhotosLocalization.GetString(preset.LocalizationKey), TextWrapping = TextWrapping.Wrap });
            var button = new DaisyButton
            {
                Content = stack, Width = 136, Height = double.NaN, Padding = new Thickness(8),
                Margin = new Thickness(0, 0, 8, 8), Variant = DaisyButtonVariant.Neutral
            };
            AutomationProperties.SetAutomationId(button, "Photos.Filter." + preset.Id);
            AutomationProperties.SetName(button, PhotosLocalization.GetString(preset.LocalizationKey));
            button.Click += (_, _) => _history.Apply(_history.Current with { Filter = preset.Id });
            Filters.Children.Add(button);
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
    }
    private void RefreshZoom()
    {
        ZoomText.Text = $"{Canvas.ActualZoom:0}%";
        _refreshing = true; ZoomSlider.Value = Canvas.ActualZoom; _refreshing = false;
    }
    public void BeginEditing() { SidePanel.IsVisible = true; SelectPanel("crop"); }
    private void SelectPanel(string panel)
    {
        SidePanel.IsVisible = true;
        TopSaveGroup.IsVisible = panel is "crop" or "adjust" or "filters" or "markup";
        CropPanel.IsVisible = panel == "crop"; AdjustPanel.IsVisible = panel == "adjust";
        FilterPanel.IsVisible = panel == "filters"; MarkupPanel.IsVisible = panel == "markup";
        InfoText.IsVisible = panel == "info"; ViewPanel.IsVisible = panel == "view";
        MapButton.IsVisible = panel == "info" && _item?.Details is { Latitude: not null, Longitude: not null };
        Canvas.IsCropping = panel == "crop"; Canvas.IsDrawing = panel == "markup";
        Canvas.InvalidateVisual();
    }
    private void ApplyAspect()
    {
        if (_refreshing || Ratio.SelectedIndex < 0 || Canvas.Bitmap == null) return;
        var size = EditRenderer.GetTransformedSize(new Size(Canvas.Bitmap.Width, Canvas.Bitmap.Height), _history.Current);
        double[] ratios = [0, size.Width / size.Height, 1, 4.0 / 3, 1.5, 16.0 / 9, .75, 2.0 / 3, 9.0 / 16];
        Canvas.AspectRatio = ratios[Ratio.SelectedIndex];
        if (Canvas.AspectRatio <= 0) return;
        var width = Math.Min(1, Canvas.AspectRatio / (size.Width / size.Height));
        var height = width * size.Width / (Canvas.AspectRatio * size.Height);
        _history.Apply(_history.Current with { Crop = new Rect((1 - width) / 2, (1 - height) / 2, width, height) });
    }
    private void ApplyViewOptions()
    {
        PhotosSettings.ShowRulers = Rulers.IsChecked == true;
        PhotosSettings.ShowGrid = Gridlines.IsChecked == true;
        PhotosSettings.ShowNavigator = Navigator.IsChecked == true;
        PhotosSettings.ShowStatusBar = StatusBar.IsChecked == true;
        PhotosSettings.AutoHideToolbar = AutoHide.IsChecked == true;
        StatusPanel.IsVisible = PhotosSettings.ShowStatusBar; PhotosSettings.SaveOptions(); Canvas.InvalidateVisual();
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
        ImageMetadata.Text = $"{_item.PixelWidth} × {_item.PixelHeight} · {FileOperations.FormatFileSize(_item.FileSize)}";
        InfoText.Text = string.Join("\n\n", new[]
        {
            _item.FileName, L("Info_Folder") + ": " + _item.FolderPath,
            L("Info_FileSize") + ": " + FileOperations.FormatFileSize(_item.FileSize),
            L("Info_Dimensions") + $": {_item.PixelWidth} × {_item.PixelHeight}",
            L("Info_DateTaken") + ": " + _item.DateTaken?.ToString("g", PhotosLocalization.CurrentCulture),
            L("Info_Modified") + ": " + _item.DateModified.ToString("g", PhotosLocalization.CurrentCulture),
            L("Info_Camera") + ": " + _item.CameraModel,
            L("Info_Lens") + ": " + _item.Details?.Lens,
            L("Info_Exposure") + ": " + _item.Details?.Exposure,
            L("Info_Location") + ": " + (_item.Details?.Latitude.HasValue == true
                ? string.Format(CultureInfo.InvariantCulture, "{0:0.00000}, {1:0.00000}", _item.Details.Latitude, _item.Details.Longitude) : ""),
            L("Info_Albums") + ": " + string.Join(", ", PhotoLibrary.Instance.Albums.Where(a => a.Paths.Contains(_item.Path)).Select(a => a.Name))
        });
    }
    private async Task NavigateAsync(int offset)
    {
        if (_navigating || _item == null || _items.Count < 2) return;
        _navigating = true;
        try
        {
            if (HasChanges && ConfirmLeave != null && !await ConfirmLeave()) { _slideshow.Stop(); return; }
            var index = _items.ToList().FindIndex(i => i.Path == _item.Path);
            await OpenAsync(_items[(index + offset + _items.Count) % _items.Count], _items);
        }
        finally { _navigating = false; }
    }
    public void StartSlideshow()
    {
        _slideshow.Interval = TimeSpan.FromSeconds(PhotosSettings.SlideshowSeconds);
        _slideshow.Start();
    }
    private void ToggleFullScreen()
    {
        if (!_fullscreen) { _previousState = Owner.WindowState; Owner.WindowState = WindowState.FullScreen; }
        else Owner.WindowState = _previousState;
        _fullscreen = !_fullscreen;
        ImmersiveChanged?.Invoke(_fullscreen);
        if (_fullscreen) { SidePanel.IsVisible = false; _toolbarTimer.Start(); }
        else { Toolbar.IsVisible = true; StatusPanel.IsVisible = PhotosSettings.ShowStatusBar; }
    }
    public void Stop()
    {
        _slideshow.Stop(); _toolbarTimer.Stop();
        if (_fullscreen) ToggleFullScreen();
    }
    public async Task<bool> SaveAsync(bool replace)
    {
        if (_item == null || _saving) return false;
        _saving = true;
        IsEnabled = false;
        try
        {
            var target = _item.Path;
            if (replace && Path.GetExtension(target).ToLowerInvariant() is not (".jpg" or ".jpeg" or ".png")) replace = false;
            if (replace)
            {
                if (await new PromptDialog(L("SaveOverwrite"), L("OverwriteWarning")).ShowDialog<int>(Owner) != 1) return false;
            }
            else
            {
                using var file = await Owner.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = L("SaveCopy"), SuggestedFileName = Path.GetFileNameWithoutExtension(target) + "-" + L("EditedSuffix") + ".jpg",
                    FileTypeChoices = [new FilePickerFileType("JPEG") { Patterns = ["*.jpg"] }, new FilePickerFileType("PNG") { Patterns = ["*.png"] }],
                    DefaultExtension = "jpg", ShowOverwritePrompt = true
                });
                if (file?.TryGetLocalPath() is not { } localPath) return false;
                target = localPath;
            }
            await EditRenderer.SaveAsync(_item.Path, target, _history.Current, PhotosSettings.JpegQuality);
            var refreshed = PhotoLibrary.Instance.Refresh(target);
            if (refreshed != null) await OpenAsync(refreshed, _items);
            else _history.Reset();
            return true;
        }
        catch (Exception ex) { FileName.Text = PhotosLocalization.Format("Photos_SaveFailed", ex.Message); return false; }
        finally { _saving = false; IsEnabled = !_disposed; }
    }
    private void BuildPalette()
    {
        Palette.Children.Clear();
        foreach (var (color, index) in PhotosSettings.CustomColors.Select((color, index) => (color, index)))
        {
            var button = new DaisyButton { Width = 28, Height = 28, MinWidth = 0, Padding = new Thickness(0), Margin = new Thickness(2), Background = new SolidColorBrush(color), Content = " " };
            AutomationProperties.SetAutomationId(button, "Photos.Palette.Custom." + index);
            AutomationProperties.SetName(button, L("CustomColors") + " " + (index + 1) + " " + color);
            button.Click += (_, _) => ColorEditor.Color = color;
            ToolTip.SetTip(button, L("CustomColorHint"));
            var store = new MenuItem { Header = L("StoreCurrentColor") };
            store.Click += (_, _) =>
            {
                PhotosSettings.CustomColors[index] = ColorEditor.Color;
                PhotosSettings.SaveOptions(); BuildPalette();
            };
            var reset = new MenuItem { Header = L("ResetColor") };
            reset.Click += (_, _) =>
            {
                PhotosSettings.CustomColors[index] = Colors.White;
                PhotosSettings.SaveOptions(); BuildPalette();
            };
            button.ContextMenu = new ContextMenu { ItemsSource = new[] { store, reset } };
            Palette.Children.Add(button);
        }
    }
    private void SetPalette(IEnumerable<Color> colors)
    {
        var index = 0;
        foreach (var color in colors.Take(9).ToArray()) PhotosSettings.CustomColors[index++] = color;
        PhotosSettings.SaveOptions(); BuildPalette();
    }
    private void RememberColor()
    {
        _recent.Remove(ColorEditor.Color); _recent.Insert(0, ColorEditor.Color);
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
            foreach (var file in files) { using (file) { if (file.TryGetLocalPath() is { } path) SetPalette(ColorTools.ReadPalette(path)); } }
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
                await File.WriteAllLinesAsync(path, new[] { "GIMP Palette", "Name: Flowery Photos", "#" }.Concat(PhotosSettings.CustomColors.Select(c => $"{c.R} {c.G} {c.B}")));
            else ColorTools.WriteJascPalette(path, PhotosSettings.CustomColors);
        }
    }
    private async Task CopyEditedAsync()
    {
        if (_item == null || Owner.Clipboard == null) return;
        using var source = await Task.Run(() => PhotoDecoder.Load(_item.Path));
        using var rendered = EditRenderer.Render(source, _history.Current);
        using var image = SKImage.FromBitmap(rendered);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = new MemoryStream(data.ToArray());
        using var bitmap = new Bitmap(stream);
        await Owner.Clipboard.SetBitmapAsync(bitmap);
    }
    private async Task ExecuteAsync(string action)
    {
        switch (action)
        {
            case "back": BackRequested?.Invoke(); break;
            case "previous": await NavigateAsync(-1); break;
            case "next": await NavigateAsync(1); break;
            case "edit": BeginEditing(); break;
            case "favorite": if (_item != null) PhotoLibrary.Instance.ToggleFavorite(_item); break;
            case "slideshow": if (_slideshow.IsEnabled) _slideshow.Stop(); else StartSlideshow(); break;
            case "fullscreen": ToggleFullScreen(); break;
            case "undo": _history.Undo(); break;
            case "redo": _history.Redo(); break;
            case "revert": _history.Reset(); break;
            case "crop": case "adjust": case "filters": case "markup": case "info": case "view":
                SelectPanel(action);
                if (action == "filters" && _item != null)
                    await BuildFilterPreviewsAsync(_item.Path, CancellationToken.None);
                break;
            case "left": _history.Apply(_history.Current with { Rotation = (_history.Current.Rotation + 3) % 4, Crop = EditState.FullCrop }); break;
            case "right": _history.Apply(_history.Current with { Rotation = (_history.Current.Rotation + 1) % 4, Crop = EditState.FullCrop }); break;
            case "flipH": _history.Apply(_history.Current with { FlipH = !_history.Current.FlipH }); break;
            case "flipV": _history.Apply(_history.Current with { FlipV = !_history.Current.FlipV }); break;
            case "resetCrop": _history.Apply(_history.Current.ResetGeometry()); break;
            case "resetAdjust": _history.Apply(_history.Current.ResetAdjustments()); break;
            case "enhance": if (Canvas.Bitmap != null) _history.Apply(AutoEnhance.Apply(Canvas.Bitmap, _history.Current)); break;
            case "save": await SaveAsync(false); break;
            case "replace": await SaveAsync(true); break;
            case "copy": await CopyEditedAsync(); break;
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
            case "storeColor": SetPalette(new[] { ColorEditor.Color }.Concat(PhotosSettings.CustomColors)); RememberColor(); break;
            case "harmony": SetPalette(ColorTools.CreateHarmony(ColorEditor.Color, (ColorTools.Harmony)Math.Max(0, Harmony.SelectedIndex))); break;
            case "extract": if (Canvas.Bitmap != null) SetPalette(ColorTools.ExtractDominantColors(Canvas.Bitmap)); break;
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
        try { await ExecuteAsync(action); } catch (Exception ex) { FileName.Text = ex.Message; }
    }
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (!PhotosSettings.WheelNavigates || e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        e.Handled = true; _ = NavigateAsync(e.Delta.Y > 0 ? -1 : 1);
    }
    private async void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors()
            .Any(visual => visual is TextBox or RangeBase or SelectingItemsControl)) return;
        var ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        var action = e.Key switch
        {
            Key.Left => "previous", Key.Right => "next", Key.F11 => "fullscreen", Key.F5 => "slideshow",
            Key.F when !ctrl => "favorite", Key.R => e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? "left" : "right",
            Key.X => "swap", Key.Add or Key.OemPlus => "zoomIn", Key.Subtract or Key.OemMinus => "zoomOut",
            Key.D0 or Key.NumPad0 => "fit", Key.D1 or Key.NumPad1 => "actual",
            Key.Z when ctrl => "undo", Key.Y when ctrl => "redo", Key.S when ctrl => "save", Key.E when ctrl => "edit",
            Key.Escape => _fullscreen ? "fullscreen" : _slideshow.IsEnabled ? "slideshow" : "back",
            Key.Delete => "delete",
            _ => null
        };
        if (action == null) return;
        e.Handled = true;
        try { await ExecuteAsync(action); } catch (Exception ex) { FileName.Text = ex.Message; }
    }
    public void Dispose()
    {
        _disposed = true;
        _previewVersion++;
        Stop(); _load?.Cancel();
        if (_item != null) _item.PropertyChanged -= OnPhotoPropertyChanged;
        Canvas.Dispose();
        foreach (var preview in _previews) preview.Dispose();
        _previews.Clear();
    }
}
