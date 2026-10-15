using System;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;

namespace Flowery.Helpers;

internal sealed class DaisyActionAutomationPeer(Control owner, Action<Control> invoke) : ControlAutomationPeer(owner), IInvokeProvider
{
    public void Invoke()
    {
        if (!Owner.IsEffectivelyEnabled) throw new InvalidOperationException("The control is disabled.");
        invoke(Owner);
    }

    protected override string GetClassNameCore() => Owner.GetType().Name;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;
}
