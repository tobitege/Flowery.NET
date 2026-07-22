using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;

[assembly: AvaloniaTestApplication(typeof(Flowery.NET.Tests.TestAppBuilder))]

namespace Flowery.NET.Tests
{
    public class TestAppBuilder
    {
        public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions());
    }

    public class App : Application
    {
        public override void Initialize()
        {
            Styles.Add(new DaisyUITheme());
        }
    }
}
