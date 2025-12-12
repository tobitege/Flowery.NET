using Avalonia;
using System;

namespace Flowery.NET.Gallery;

class 
    Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        try
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var ex = e.ExceptionObject as Exception;
                Console.WriteLine("=== UNHANDLED EXCEPTION ===");
                Console.WriteLine(ex?.ToString() ?? "Unknown exception");
                Console.WriteLine("===========================");
            };

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }
        catch (Exception ex)
        {
            Console.WriteLine("=== STARTUP EXCEPTION ===");
            Console.WriteLine(ex.ToString());
            Console.WriteLine("=========================");
            throw;
        }
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
}
