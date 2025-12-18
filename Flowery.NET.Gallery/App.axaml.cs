using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Gallery.Localization;

namespace Flowery.NET.Gallery;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);

        // Force GalleryLocalization static constructor to run early,
        // which registers the CustomResolver with FloweryLocalization
        // so sidebar items display translated text instead of keys.
        _ = GalleryLocalization.Instance;
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // Restore saved app language (if any)
        // GalleryLocalization syncs automatically via FloweryLocalization.CultureChanged
        var savedLanguage = GallerySettings.LoadLanguage();
        if (!string.IsNullOrWhiteSpace(savedLanguage))
            FloweryLocalization.SetCulture(savedLanguage);

        // Save language whenever it changes
        FloweryLocalization.CultureChanged += (_, culture) => GallerySettings.SaveLanguage(culture.Name);

        // Restore saved theme or use Dark as default
        var savedTheme = GallerySettings.Load() ?? "Dark";
        DaisyThemeManager.ApplyTheme(savedTheme);

        // Save theme whenever it changes
        DaisyThemeManager.ThemeChanged += (_, name) => GallerySettings.Save(name);

        // Restore saved global size (if any). FlowerySizeManager defaults to Small.
        var savedGlobalSize = GallerySettings.LoadGlobalSize();
        if (savedGlobalSize != null)
            FlowerySizeManager.ApplySize(savedGlobalSize.Value);

        // Save global size whenever it changes
        FlowerySizeManager.SizeChanged += (_, size) => GallerySettings.SaveGlobalSize(size);

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            //DisableAvaloniaDataAnnotationValidation();
            desktop.MainWindow = new MainWindow();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleViewLifetime)
        {
            //DisableAvaloniaDataAnnotationValidation();
            var mainView = new MainView();
            singleViewLifetime.MainView = mainView;
            // HookThemeVariant(mainView.ViewModel);
        }

        base.OnFrameworkInitializationCompleted();
    }
}
