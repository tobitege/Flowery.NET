using System.Runtime.Versioning;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Browser;
using Flowery.NET.Gallery;
using Flowery.NET.Gallery.Browser;
using Flowery.Services;

[assembly: SupportedOSPlatform("browser")]

internal sealed partial class Program
{
    private static async Task Main(string[] args)
    {
        StateStorageProvider.Configure(new BrowserStateStorage());

        await BuildAvaloniaApp()
            .WithInterFont()
            .WithNotoFonts()
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}
