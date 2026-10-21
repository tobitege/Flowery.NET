using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Flowery.Controls;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed class CardTests
{
    [AvaloniaFact]
    public void CardKeyboardAndAutomationUseTheSameAction()
    {
        var card = new PhotoCard("test.card", "Sample collection") { Content = new TextBlock { Text = "Sample collection" } };
        var window = new Window { Content = card };
        var count = 0;
        card.Click += (_, _) => count++;
        try
        {
            window.Show(); card.Focus();
            var peer = ControlAutomationPeer.CreatePeerForElement(card)!;
            Assert.Equal("test.card", peer.GetAutomationId());
            Assert.Equal("Sample collection", peer.GetName());
            Assert.IsAssignableFrom<IInvokeProvider>(peer).Invoke();
            window.KeyPress(Key.Space, RawInputModifiers.None, PhysicalKey.Space, " ");
            Assert.Equal(2, count);
            Assert.IsAssignableFrom<DaisyCard>(card);
        }
        finally { window.Close(); }
    }

}
