using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Flowery.NET.Kiosk.Slides;
using Xunit;

[assembly: AvaloniaTestApplication(typeof(Flowery.NET.Kiosk.Tests.TestAppBuilder))]

namespace Flowery.NET.Kiosk.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());
}

// The styles and resources of the kiosk, without its window and lifetime.
public sealed class TestApp : App
{
    public override void OnFrameworkInitializationCompleted() { }
}

public class LoginSlideTests
{
    // The first feature fades in 300 ms after the slide loaded; before that no fade runs that a cancellation could end.
    [AvaloniaFact]
    public void Leaving_The_Login_Slide_Before_Its_First_Feature_Cancels_The_Entrance()
    {
        var slide = new LoginSlide();
        var window = new Window { Content = slide };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            Assert.True(slide.IsFeatureTimerRunning);
            Assert.False(slide.FeaturesShown.IsCompleted);

            window.Content = null;
            Dispatcher.UIThread.RunJobs();

            Assert.False(slide.IsFeatureTimerRunning);
            Assert.True(slide.FeaturesShown.IsCanceled);
        }
        finally
        {
            window.Close();
        }
    }
}
