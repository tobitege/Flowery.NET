using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Flowery.Controls;
using Flowery.Controls.Custom;

namespace Flowery.Helpers;

internal sealed class DaisyInputAutomationPeer(TextBox owner) : TextBoxAutomationPeer(owner)
{
    protected override string? GetNameCore()
    {
        var explicitName = base.GetNameCore();
        if (!string.IsNullOrWhiteSpace(explicitName)) return explicitName;
        return Owner switch { DaisyInput input => input.Label, DaisyMaskInput mask => mask.Label, _ => explicitName };
    }

    protected override string? GetHelpTextCore()
    {
        var explicitHelp = AutomationProperties.GetHelpText(Owner);
        if (!string.IsNullOrWhiteSpace(explicitHelp)) return explicitHelp;
        var (helper, hint) = Owner switch
        {
            DaisyInput input => (input.HelperText, input.HintText),
            DaisyMaskInput mask => (mask.HelperText, mask.HintText),
            _ => ((string?)null, (string?)null)
        };
        return !string.IsNullOrWhiteSpace(helper) ? helper : !string.IsNullOrWhiteSpace(hint) ? hint : base.GetHelpTextCore();
    }

    protected override void OwnerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        base.OwnerPropertyChanged(sender, e);
        if (e.Property.Name == nameof(DaisyInput.Label))
            RaisePropertyChangedEvent(AutomationElementIdentifiers.NameProperty, e.OldValue, GetName());
        else if (e.Property.Name is nameof(DaisyInput.HelperText) or nameof(DaisyInput.HintText))
            RaisePropertyChangedEvent(AutomationElementIdentifiers.HelpTextProperty, null, GetHelpText());
    }
}
