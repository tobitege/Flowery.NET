using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;

namespace Flowery.NET.Photos.Views;

/// <summary>A Daisy button whose active state is also exposed through UI Automation.</summary>
public sealed class PhotoFavoriteButton : DaisyButton
{
    protected override AutomationPeer OnCreateAutomationPeer() => new FavoritePeer(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == IsActiveProperty)
            ControlAutomationPeer.FromElement(this)?.RaisePropertyChangedEvent(
                TogglePatternIdentifiers.ToggleStateProperty,
                change.GetOldValue<bool>() ? ToggleState.On : ToggleState.Off,
                change.GetNewValue<bool>() ? ToggleState.On : ToggleState.Off);
    }

    private sealed class FavoritePeer(PhotoFavoriteButton owner) : ButtonAutomationPeer(owner), IToggleProvider
    {
        public ToggleState ToggleState => owner.IsActive ? ToggleState.On : ToggleState.Off;
        public void Toggle() => Invoke();
    }
}
