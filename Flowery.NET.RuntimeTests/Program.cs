using Avalonia;
using Avalonia.Controls;
using Flowery.NET.Gallery;

namespace Flowery.NET.RuntimeTests;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (!RuntimeTestArguments.TryGetReportPath(args, out var reportPath))
        {
            Console.Error.WriteLine("Usage: Flowery.NET.RuntimeTests --runtime-tests <report.xml>");
            return 2;
        }

        try
        {
            return BuildAvaloniaApp(reportPath).StartWithClassicDesktopLifetime(args, ShutdownMode.OnExplicitShutdown);
        }
        catch (Exception ex)
        {
            RuntimeTestReport.WriteStartupFailure(reportPath, ex);
            return 1;
        }
    }

    // Matches Flowery.NET.Gallery.Desktop, so the tests see the Gallery's fonts and styles.
    private static AppBuilder BuildAvaloniaApp(string reportPath)
        => AppBuilder.Configure(() => new RuntimeTestApp(reportPath))
            .UsePlatformDetect()
            .WithInterFont()
            .WithNotoFonts()
            .LogToTrace();
}
