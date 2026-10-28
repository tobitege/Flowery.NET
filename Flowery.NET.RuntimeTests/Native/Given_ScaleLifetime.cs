using System.Collections;
using System.Reflection;
using Avalonia.Controls.Embedding;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

public class Given_ScaleLifetime
{
    public static bool IsLinux => OperatingSystem.IsLinux();

    [Fact(Skip = "Avalonia's X11 platform cannot create an EmbeddableControlRoot", SkipWhen = nameof(IsLinux))]
    public async Task When_A_NonWindow_TopLevel_Is_Disposed_The_Scaling_Tracker_Is_Released()
    {
        var button = new DaisyButton();
        FloweryScaleManager.SetEnableScaling(button, true);
        var root = new EmbeddableControlRoot { Content = button };
        var trackers = (IDictionary)typeof(FloweryScaleManager)
            .GetField("_topLevelTrackers", BindingFlags.Static | BindingFlags.NonPublic)!.GetValue(null)!;
        try
        {
            root.Prepare();
            root.UpdateLayout();
            Assert.True(trackers.Contains(root), "Embedded TopLevel was not registered for scaling.");
        }
        finally
        {
            root.Dispose();
        }

        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.False(trackers.Contains(root), "Disposed embedded TopLevel is retained by the scaling registry.");
    }
}
