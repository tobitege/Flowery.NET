using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyPopoverTests
{
    [AvaloniaFact]
    public void When_TriggerButtonIsInvoked_PopoverToggles()
    {
        var trigger = new DaisyButton { Content = "Open" };
        var popover = new DaisyPopover
        {
            TriggerContent = trigger,
            PopoverContent = new TextBlock { Text = "Popover content" }
        };
        var window = new Window { Content = popover };

        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var peer = Assert.IsAssignableFrom<AutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(trigger));
            var invokeProvider = Assert.IsAssignableFrom<IInvokeProvider>(peer);

            invokeProvider.Invoke();
            Dispatcher.UIThread.RunJobs();
            Assert.True(popover.IsOpen);

            invokeProvider.Invoke();
            Dispatcher.UIThread.RunJobs();
            Assert.False(popover.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }
}
