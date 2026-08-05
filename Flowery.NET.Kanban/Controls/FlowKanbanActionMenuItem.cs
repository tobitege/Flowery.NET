namespace Flowery.NET.Kanban.Controls;

internal sealed class FlowKanbanActionMenuItem : ListBoxItem
{
    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<FlowKanbanActionMenuItem, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<FlowKanbanActionMenuItem, object?>(nameof(CommandParameter));

    public FlowKanbanActionMenuItem()
    {
        AddHandler(
            InputElement.TappedEvent,
            OnTapped,
            RoutingStrategies.Bubble,
            handledEventsToo: true);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    protected override AutomationPeer OnCreateAutomationPeer() =>
        new FlowKanbanActionMenuItemAutomationPeer(this);

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (!e.Handled
            && (e.Key == Key.Enter || e.Key == Key.Space)
            && TryExecuteCommand())
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    internal bool TryExecuteCommand()
    {
        if (!IsEffectivelyEnabled || Command is not { } command)
        {
            return false;
        }

        var parameter = CommandParameter;
        if (!command.CanExecute(parameter))
        {
            return false;
        }

        command.Execute(parameter);
        return true;
    }

    private void OnTapped(object? sender, TappedEventArgs e)
    {
        if (TryExecuteCommand())
        {
            e.Handled = true;
        }
    }
}

internal sealed class FlowKanbanActionMenuItemAutomationPeer : ControlAutomationPeer, IInvokeProvider
{
    public FlowKanbanActionMenuItemAutomationPeer(FlowKanbanActionMenuItem owner)
        : base(owner)
    {
    }

    private new FlowKanbanActionMenuItem Owner => (FlowKanbanActionMenuItem)base.Owner;

    public void Invoke()
    {
        if (!Owner.IsEffectivelyEnabled)
        {
            throw new InvalidOperationException("The Kanban menu action is disabled.");
        }

        if (!Owner.TryExecuteCommand())
        {
            throw new InvalidOperationException("The Kanban menu action is unavailable.");
        }
    }

    protected override AutomationControlType GetAutomationControlTypeCore() =>
        AutomationControlType.MenuItem;

    protected override string GetClassNameCore() => nameof(FlowKanbanActionMenuItem);

    protected override bool IsContentElementCore() =>
        FlowKanbanVisualTree.IsAutomationVisible(Owner);

    protected override bool IsControlElementCore() =>
        FlowKanbanVisualTree.IsAutomationVisible(Owner);
}
