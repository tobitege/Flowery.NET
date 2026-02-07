using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Avalonia;

namespace Flowery.NET.Gallery.Desktop;

sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        // Configure crash logging before Avalonia starts
        ConfigureCrashLogging();

        try
        {
            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            LogFatal("Main.Unhandled", ex);
            throw;
        }
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .WithNotoFonts()
            .LogToTrace();

    private static void ConfigureCrashLogging()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            LogFatal("AppDomain.UnhandledException", e.ExceptionObject as Exception);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            LogFatal("TaskScheduler.UnobservedTaskException", e.Exception);
            e.SetObserved();
        };
    }

    internal static void LogFatal(string source, Exception? ex)
    {
        var message = $"[{DateTimeOffset.Now:O}] {source}: {ex}\n";
        Debug.WriteLine(message);
        Console.Error.WriteLine(message);

        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flowery.NET.Gallery");
            Directory.CreateDirectory(dir);
            var logPath = Path.Combine(dir, "crash.log");
            File.AppendAllText(logPath, message);
            Debug.WriteLine($"Crash log written to: {logPath}");
        }
        catch
        {
            // Ignore file IO failures; Debug output is still useful.
        }
    }
}
