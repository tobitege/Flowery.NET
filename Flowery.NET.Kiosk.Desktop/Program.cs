using Avalonia;
using Flowery.NET.Kiosk;
using Flowery.Skia;

namespace Flowery.NET.Kiosk.Desktop;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args) => BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect().WithSafeInterFont()
        .ConfigureFonts(manager => manager.AddFontCollection(new SafeEmbeddedFontCollection(
            new Uri("fonts:Flowery.NET.Kiosk"), new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts")))))
        .LogToTrace();
}
