using Avalonia;
using Avalonia.Headless;
using Flowery;
using Flowery.NET.Photos.Services;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(Flowery.NET.Photos.Tests.TestAppBuilder))]
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Flowery.NET.Photos.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
}
public sealed class TestApp : Application
{
    public override void Initialize()
    {
        PhotosSettings.Initialize(Path.Combine(Path.GetTempPath(),
            "flowery-photos-tests", Guid.NewGuid().ToString("N")));
        Styles.Add(new Avalonia.Themes.Fluent.FluentTheme());
        Styles.Add(new DaisyUITheme());
    }
}
