using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos;

public partial class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        var configured = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        if (!string.IsNullOrWhiteSpace(configured) && !Path.IsPathFullyQualified(configured))
            throw new ArgumentException("FLOWERY_STATE_ROOT must be an absolute directory path.");
        PhotosSettings.Initialize(configured ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flowery.NET.Photos"));
        _ = PhotosLocalization.Instance;
        FloweryLocalization.SetCulture(PhotosSettings.LoadLanguage() ?? "en");
        DaisyThemeManager.ApplyTheme(PhotosSettings.LoadTheme() ?? "Dark");
        FlowerySizeManager.ApplySize(PhotosSettings.LoadGlobalSize() ?? DaisySize.Medium);
        FloweryLocalization.CultureChanged += (_, culture) => PhotosSettings.SaveLanguage(culture.Name);
        DaisyThemeManager.ThemeChanged += (_, name) => PhotosSettings.SaveTheme(name);
        FlowerySizeManager.SizeChanged += (_, size) => PhotosSettings.SaveGlobalSize(size);
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow(desktop.Args);
        base.OnFrameworkInitializationCompleted();
    }
}
