using Avalonia;
using Avalonia.Media.Fonts;
using Flowery.NET.Photos;

namespace Flowery.NET.Photos.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        var builder = BuildAvaloniaApp();
#if DEBUG
        var enableCdp = args.Contains("--cdp");
        if (enableCdp)
            builder.AfterSetup(_ => Avalonia.Diagnostics.Cdp.CdpServer.Start(9222));
        try
        {
            builder.StartWithClassicDesktopLifetime(args);
        }
        finally
        {
            if (enableCdp) Avalonia.Diagnostics.Cdp.CdpServer.Stop();
        }
#else
        builder.StartWithClassicDesktopLifetime(args);
#endif
    }

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect().WithInterFont()
        .ConfigureFonts(manager => manager.AddFontCollection(new EmbeddedFontCollection(
            new Uri("fonts:Flowery.NET.Photos"), new Uri("avares://Flowery.NET.Photos/Assets/Fonts"))))
        .LogToTrace();
}
