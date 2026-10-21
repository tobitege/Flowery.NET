using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Controls.Templates;
using Avalonia.Automation;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class SettingsView : Window
{
    private readonly SettingsSnapshot _snapshot;
    private bool _accepted;
    public SettingsView()
    {
        InitializeComponent();
        _snapshot = SettingsSnapshot.Capture();
        DataContext = PhotosLocalization.Instance;
        Title = PhotosLocalization.GetString("Photos_Nav_Settings");
        ThemeChoice.ItemsSource = DaisyThemeManager.AvailableThemes.Select(t => t.Name).ToList();
        ThemeChoice.SelectedItem = DaisyThemeManager.CurrentThemeName;
        GlobalSize.ItemsSource = Enum.GetValues<DaisySize>();
        GlobalSize.SelectedItem = FlowerySizeManager.CurrentSize;
        Language.ItemsSource = FloweryLocalization.SupportedLanguages;
        Language.SelectedItem = FloweryLocalization.CurrentCulture.Name;
        Recursive.IsChecked = PhotosSettings.IncludeSubfolders;
        ConfirmDelete.IsChecked = PhotosSettings.ConfirmDelete;
        Wheel.IsChecked = PhotosSettings.WheelNavigates;
        Interval.Value = PhotosSettings.SlideshowSeconds;
        Quality.Value = PhotosSettings.JpegQuality;
        ThemeChoice.SelectionChanged += (_, _) => { if (ThemeChoice.SelectedItem is string theme) DaisyThemeManager.ApplyTheme(theme); };
        GlobalSize.SelectionChanged += (_, _) => { if (GlobalSize.SelectedItem is DaisySize size) FlowerySizeManager.ApplySize(size); };
        Language.SelectionChanged += (_, _) => { if (Language.SelectedItem is string language) FloweryLocalization.SetCulture(language); };
        Recursive.IsCheckedChanged += (_, _) => PhotoLibrary.Instance.ApplyIncludeSubfolders(Recursive.IsChecked == true);
        ConfirmDelete.IsCheckedChanged += (_, _) => PhotosSettings.ConfirmDelete = ConfirmDelete.IsChecked == true;
        Wheel.IsCheckedChanged += (_, _) => PhotosSettings.WheelNavigates = Wheel.IsChecked == true;
        Interval.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) PhotosSettings.SlideshowSeconds = Interval.Value; };
        Quality.PropertyChanged += (_, e) => { if (e.Property == Slider.ValueProperty) PhotosSettings.JpegQuality = (int)Quality.Value; };
        Closing += (_, _) => { if (!_accepted) _snapshot.Restore(); };
        PhotosLocalization.CultureChanged += OnCultureChanged;
        Closed += (_, _) => PhotosLocalization.CultureChanged -= OnCultureChanged;
        KeyDown += (_, e) => { if (e.Key == Key.Escape) Close(); };
        RefreshLanguage();
    }
    private void OnCultureChanged(object? sender, string culture) => RefreshLanguage();
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
        LibraryStats.Text = PhotosLocalization.Format("Photos_Settings_LibraryStats", PhotoLibrary.Instance.Items.Count,
            PhotoLibrary.Instance.Folders.Count, FileOperations.FormatFileSize(PhotoLibrary.Instance.Items.Sum(i => i.FileSize)));
        if (PhotoLibrary.Instance.Folders.Count == 0)
            Folders.Children.Add(new TextBlock { Text = PhotosLocalization.GetString("Photos_Settings_NoFolders"), TextWrapping = TextWrapping.Wrap });
        foreach (var folder in PhotoLibrary.Instance.Folders.ToList())
        {
            var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto") };
            row.Children.Add(new TextBlock { Text = folder, TextWrapping = TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });
            var remove = new DaisyButton { Content = PhotosLocalization.GetString("Photos_Remove"), Variant = DaisyButtonVariant.Neutral };
            AutomationProperties.SetName(remove, PhotosLocalization.GetString("Photos_Remove") + " " + folder);
            AutomationProperties.SetAutomationId(remove, "SettingsView.RemoveFolder." + folder);
            remove.Click += (_, _) => { PhotoLibrary.Instance.RemoveFolder(folder); RefreshFolders(); };
            Grid.SetColumn(remove, 1); row.Children.Add(remove); Folders.Children.Add(row);
        }
    }
    private async void AddFolderClick(object? sender, RoutedEventArgs e)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { AllowMultiple = true });
        foreach (var folder in folders) { using (folder) { if (folder.TryGetLocalPath() is { } path) PhotoLibrary.Instance.AddFolder(path); } }
        RefreshFolders();
    }
    private async void ClearCacheClick(object? sender, RoutedEventArgs e) { PhotoLibrary.Instance.ClearCache(); await PhotoLibrary.Instance.RescanAsync(); }
    private async void RescanClick(object? sender, RoutedEventArgs e) { await PhotoLibrary.Instance.RescanAsync(); RefreshFolders(); }
    private async void GitHubClick(object? sender, RoutedEventArgs e) => await Launcher.LaunchUriAsync(new Uri("https://github.com/tobitege/Flowery.NET"));
    private void DataFolderClick(object? sender, RoutedEventArgs e) => FileOperations.ShowInExplorer(PhotosSettings.StateDirectory);
    private void CancelClick(object? sender, RoutedEventArgs e) => Close();
    private void OkClick(object? sender, RoutedEventArgs e) { _accepted = true; PhotosSettings.SaveOptions(); Close(); }
}
