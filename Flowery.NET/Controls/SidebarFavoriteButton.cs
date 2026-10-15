using System;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls.Primitives;

namespace Flowery.Controls;

internal sealed class SidebarFavoriteButton : ToggleButton
{
    protected override Type StyleKeyOverride => typeof(ToggleButton);
    protected override AutomationPeer OnCreateAutomationPeer() => new FavoritePeer(this);

    private sealed class FavoritePeer(SidebarFavoriteButton owner) : ToggleButtonAutomationPeer(owner), IToggleProvider
    {
        void IToggleProvider.Toggle()
        {
            if (!owner.IsEffectivelyEnabled) throw new InvalidOperationException("The control is disabled.");
            owner.OnClick();
        }
    }
}
