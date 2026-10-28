using Avalonia;
using Flowery.Skia;

namespace Flowery.NET.PackageTest;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Contains("--check"))
            return PackageCheck.Run();

        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // The setup of the README quick start; also used by the headless check and the visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .UseFlowerySkia()
            .LogToTrace();
}
