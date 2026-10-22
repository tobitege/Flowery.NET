using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Flowery.NET.Gallery;

namespace Flowery.NET.RuntimeTests;

/// <summary>
/// The Gallery application with its resources and styles, started with a test window instead of the main window.
/// </summary>
internal sealed class RuntimeTestApp(string reportPath) : App
{
    public override void OnFrameworkInitializationCompleted()
    {
        // Unlike the Gallery's own startup, the test run neither restores nor saves user settings.
        var lifetime = (IClassicDesktopStyleApplicationLifetime)ApplicationLifetime!;
        var window = new Window
        {
            Title = "Flowery.NET Runtime Tests",
            Width = 1280,
            Height = 900,
            ShowActivated = false
        };
        var region = RuntimeTestWindowPlacement.Prepare(window);
        window.Opened += async (_, _) => lifetime.Shutdown(await RunAsync(window, region));
        lifetime.MainWindow = window;
    }

    private async Task<int> RunAsync(Window window, PixelRect? region)
    {
        try
        {
            string? placement = null;
            if (region is { } bounds)
            {
                placement = await RuntimeTestWindowPlacement.FitAsync(window, bounds);
                Console.WriteLine($"Window placement: {placement}");
            }

            return await RuntimeTestRunner.RunAsync(window, reportPath, placement);
        }
        catch (Exception ex)
        {
            RuntimeTestReport.WriteStartupFailure(reportPath, ex);
            return 1;
        }
    }
}
