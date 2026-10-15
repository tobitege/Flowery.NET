using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Media;

namespace Flowery.Helpers;

internal sealed class DaisyColorAutomationPeer : ControlAutomationPeer, IValueProvider
{
    private readonly StyledProperty<Color> _property;

    public DaisyColorAutomationPeer(Control owner, StyledProperty<Color> property) : base(owner)
    {
        _property = property;
        owner.PropertyChanged += OnColorChanged;
    }

    public bool IsReadOnly => false;
    public string Value => Owner.GetValue(_property).ToString();

    public void SetValue(string? value)
    {
        if (!Owner.IsEffectivelyEnabled) throw new InvalidOperationException("The control is disabled.");
        if (!Color.TryParse(value, out var color)) throw new ArgumentException("Use a valid color value.", nameof(value));
        Owner.SetCurrentValue(_property, color);
    }

    private void OnColorChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == _property)
            RaisePropertyChangedEvent(ValuePatternIdentifiers.ValueProperty, e.OldValue?.ToString(), e.NewValue?.ToString());
    }

    protected override string GetClassNameCore() => Owner.GetType().Name;
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
}
