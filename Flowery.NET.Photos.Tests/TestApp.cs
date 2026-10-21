using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Fonts;
using Flowery.NET.Photos.Services;

[assembly: Avalonia.Headless.AvaloniaTestApplication(typeof(Flowery.NET.Photos.Tests.TestAppBuilder))]
[assembly: Xunit.CollectionBehavior(DisableTestParallelization = true)]

namespace Flowery.NET.Photos.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>()
        .UseSkia().UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
        .ConfigureFonts(manager => manager.AddFontCollection(new EmbeddedFontCollection(
            new Uri("fonts:Flowery.NET.Photos"), new Uri(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts")))));
}
public sealed class TestApp : App
{
    public override void Initialize()
    {
        PhotosSettings.Initialize(Path.Combine(Path.GetTempPath(),
            "flowery-photos-tests", Guid.NewGuid().ToString("N")));
        base.Initialize();
    }

    // Headless tests own their windows and isolated settings; do not start the desktop lifetime.
    public override void OnFrameworkInitializationCompleted() { }
}
