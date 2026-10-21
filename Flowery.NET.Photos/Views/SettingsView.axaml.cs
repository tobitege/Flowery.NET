using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Templates;
using Avalonia.Automation;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DaisyIconSymbol = Flowery.Enums.DaisyIconSymbol;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class SettingsView : Window
{
    private readonly SettingsSnapshot _snapshot;
    private readonly PhotoLibrary _library;
    private bool _accepted;
    private readonly Avalonia.Threading.DispatcherTimer _noticeTimer = new() { Interval = TimeSpan.FromSeconds(4) };
    public SettingsView() : this(PhotoLibrary.Instance) { }
    public SettingsView(PhotoLibrary library)
    {
        _library = library;
        InitializeComponent();
        InitializeModalLayout();
        _noticeTimer.Tick += (_, _) => { Notifications.Items.Clear(); _noticeTimer.Stop(); };
        _snapshot = SettingsSnapshot.Capture(library);
        DataContext = PhotosLocalization.Instance;
        Title = PhotosLocalization.GetString("Photos_Nav_Settings");
        ThemeChoice.ItemsSource = DaisyThemeManager.AvailableThemes.Select(t => t.Name).ToList();
        ThemeChoice.SelectedItem = DaisyThemeManager.CurrentThemeName;
        GlobalSize.ItemsSource = Enum.GetValues<DaisySize>();
        GlobalSize.SelectedItem = FlowerySizeManager.CurrentSize;
        Language.ItemsSource = FloweryLocalization.SupportedLanguages;
        Language.ItemTemplate = new FuncDataTemplate<string>((code, _) => new TextBlock
        {
            Text = code != null && FloweryLocalization.LanguageDisplayNames.TryGetValue(code, out var name) ? name : code
        });
        Language.SelectedItem = FloweryLocalization.CurrentCulture.Name;
        Recursive.IsChecked = PhotosSettings.IncludeSubfolders;
        ConfirmDelete.IsChecked = PhotosSettings.ConfirmDelete;
        Wheel.IsChecked = PhotosSettings.WheelNavigates;
        Interval.Value = PhotosSettings.SlideshowSeconds;
        Quality.Value = PhotosSettings.JpegQuality;
        ThemeChoice.SelectionChanged += (_, _) => { if (ThemeChoice.SelectedItem is string theme) DaisyThemeManager.ApplyTheme(theme); };
        GlobalSize.SelectionChanged += (_, _) => { if (GlobalSize.SelectedItem is DaisySize size) FlowerySizeManager.ApplySize(size); };
        Language.SelectionChanged += (_, _) => { if (Language.SelectedItem is string language) FloweryLocalization.SetCulture(language); };
        Recursive.IsCheckedChanged += (_, _) => _library.ApplyIncludeSubfolders(Recursive.IsChecked == true);
        ConfirmDelete.IsCheckedChanged += (_, _) => PhotosSettings.ConfirmDelete = ConfirmDelete.IsChecked == true;
        Wheel.IsCheckedChanged += (_, _) => PhotosSettings.WheelNavigates = Wheel.IsChecked == true;
        Interval.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) PhotosSettings.SlideshowSeconds = Interval.Value; };
        Quality.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) PhotosSettings.JpegQuality = (int)Quality.Value; };
        Closing += (_, _) => { if (!_accepted) _snapshot.Restore(_library); };
        PhotosLocalization.CultureChanged += OnCultureChanged;
        _library.Changed += OnLibraryChanged;
        Closed += (_, _) => { PhotosLocalization.CultureChanged -= OnCultureChanged; _library.Changed -= OnLibraryChanged; _noticeTimer.Stop(); };
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        RefreshLanguage();
    }
    private void OnCultureChanged(object? sender, string culture) => RefreshLanguage();
    private void OnLibraryChanged(object? sender, EventArgs e) => RefreshFolders();
    private void RefreshLanguage()
    {
        Title = PhotosLocalization.GetString("Photos_Nav_Settings");
        GlobalSize.ItemTemplate = new FuncDataTemplate<DaisySize>((size, _) =>
            new TextBlock { Text = FloweryLocalization.GetString("Size_" + size) });
        VersionText.Text = typeof(App).Assembly.GetName().Version?.ToString(3);
        Shortcuts.Children.Clear();
        (string Keys, string Name)[] shortcuts =
        [
            ("Ctrl+F", "Search"), ("Ctrl+,", "Settings"), ("Ctrl+A", "SelectAll"),
            ("← / →", "Navigate"), ("+ / −", "Zoom"), ("0", "Fit"), ("F", "Favorite"),
            ("Ctrl+E", "Edit"), ("Ctrl+Z", "Undo"), ("Ctrl+Y", "Redo"), ("Ctrl+S", "Save"),
            ("Delete", "Delete"), ("F5", "Slideshow"), ("F11", "FullScreen"), ("Esc", "Back")
        ];
        foreach (var (keys, name) in shortcuts)
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("130,*") };
            row.Children.Add(new TextBlock { Text = keys, FontWeight = FontWeight.SemiBold });
            var label = new TextBlock { Text = PhotosLocalization.GetString("Photos_Shortcut_" + name), TextWrapping = TextWrapping.Wrap };
            Grid.SetColumn(label, 1); row.Children.Add(label); Shortcuts.Children.Add(row);
        }
        RefreshFolders();
    }
    private void RefreshFolders()
    {
        Folders.Children.Clear();
        LibraryStats.Text = PhotosLocalization.FormatLibraryStats(_library.Items.Count,
            _library.Folders.Count, FileOperations.FormatFileSize(_library.Items.Sum(i => i.FileSize)));
        if (_library.Folders.Count == 0)
            Folders.Children.Add(new TextBlock { Text = PhotosLocalization.GetString("Photos_Settings_NoFolders"), TextWrapping = TextWrapping.Wrap });
        foreach (var folder in _library.Folders.ToList())
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto,Auto") };
            var info = new StackPanel { Spacing = 2, Margin = new Thickness(0, 0, 8, 0) };
            info.Children.Add(new TextBlock { Text = Path.GetFileName(Path.TrimEndingDirectorySeparator(folder)), FontWeight = FontWeight.SemiBold });
            info.Children.Add(new SelectableTextBlock { Text = folder, TextWrapping = TextWrapping.Wrap, Opacity = .65 });
            var count = _library.Items.Count(item => PhotoLibrary.IsWithin(folder, item.Path));
            info.Children.Add(new TextBlock
            {
                Text = Directory.Exists(folder) ? count == 1 ? PhotosLocalization.Format("Photos_ItemCountOne", count)
                    : PhotosLocalization.Format("Photos_ItemCount", count) : PhotosLocalization.GetString("Photos_Settings_FolderMissing"),
                Opacity = .65
            });
            row.Children.Add(info);
            var open = new DaisyButton { IconSymbol = DaisyIconSymbol.Folder, Variant = DaisyButtonVariant.Ghost };
            AutomationProperties.SetName(open, PhotosLocalization.GetString("Photos_OpenInExplorer") + " " + folder);
            AutomationProperties.SetAutomationId(open, "SettingsView.OpenFolder." + folder);
            open.IsEnabled = Directory.Exists(folder);
            open.Click += (_, _) => OpenFolder(folder);
            Grid.SetColumn(open, 1); row.Children.Add(open);
            var remove = new DaisyButton { Content = PhotosLocalization.GetString("Photos_Remove"), Variant = DaisyButtonVariant.Neutral };
            AutomationProperties.SetName(remove, PhotosLocalization.GetString("Photos_Remove") + " " + folder);
            AutomationProperties.SetAutomationId(remove, "SettingsView.RemoveFolder." + folder);
            remove.Click += async (_, _) =>
            {
                var title = PhotosLocalization.GetString("Photos_RemoveFromLibrary");
                var message = PhotosLocalization.Format("Photos_RemoveFolderConfirm", folder);
                if (await new PromptDialog(title, message, accept: PhotosLocalization.GetString("Photos_Remove"), destructive: true).ShowDialog<int>(this) != 1) return;
                _library.RemoveFolder(folder); RefreshFolders();
            };
            Grid.SetColumn(remove, 2); row.Children.Add(remove); Folders.Children.Add(row);
        }
    }
    private async void AddFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = true });
        foreach (var folder in folders)
        {
            using (folder)
                if (folder.TryGetLocalPath() is { } path)
                    ShowNotice(_library.AddFolder(path) ? PhotosLocalization.Format("Photos_FolderAdded", path) : PhotosLocalization.GetString("Photos_FolderAlreadyAdded"));
        }
        RefreshFolders();
    }
    private void ShowNotice(string message, bool error = false)
    {
        Notifications.Items.Clear();
        var alert = new DaisyAlert { Content = message, Variant = error ? DaisyAlertVariant.Error : DaisyAlertVariant.Success };
        AutomationProperties.SetName(alert, message);
        Notifications.Items.Add(alert); _noticeTimer.Stop(); _noticeTimer.Start();
    }
    private void OpenFolder(string path)
    {
        try { FileOperations.OpenFolder(path); }
        catch (Exception ex) { ShowNotice(ex.Message, true); }
    }
    private async void ClearCacheClick(object? sender, RoutedEventArgs e)
    {
        try { _library.ClearCache(); ShowNotice(PhotosLocalization.GetString("Photos_CacheCleared")); await _library.RescanAsync(); }
        catch (Exception ex) { ShowNotice(ex.Message, true); }
    }
    private async void RescanClick(object? sender, RoutedEventArgs e)
    {
        try { await _library.RescanAsync(); RefreshFolders(); ShowNotice(PhotosLocalization.GetString("Photos_RescanDone")); }
        catch (Exception ex) { ShowNotice(ex.Message, true); }
    }
    private async void GitHubClick(object? sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(new Uri("https://github.com/tobitege/Flowery.NET"));
    private void DataFolderClick(object? sender, RoutedEventArgs e) => OpenFolder(PhotosSettings.StateDirectory);
    private void CancelClick(object? sender, RoutedEventArgs e) => Close();
    private void OkClick(object? sender, RoutedEventArgs e) { _accepted = true; PhotosSettings.SaveOptions(); Close(); }
}
