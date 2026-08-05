using Avalonia;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Markup.Xaml.Styling;
using Avalonia.Themes.Fluent;

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
            Styles.Add(new FluentTheme());
            Styles.Add(new DaisyUITheme());
            Styles.Add(new StyleInclude(new Uri("avares://Flowery.NET.Tests/"))
            {
                Source = new Uri("avares://Flowery.NET.Kanban/Themes/Generic.axaml")
            });
        }
    }
}
