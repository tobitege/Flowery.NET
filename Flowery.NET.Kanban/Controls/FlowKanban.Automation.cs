namespace Flowery.NET.Kanban.Controls;

public partial class FlowKanban
{
    protected override AutomationPeer OnCreateAutomationPeer() => new FlowKanbanAutomationPeer(this);

    internal void SetTaskSelection(FlowTask task, bool selected, bool exclusive)
    {
        if (!IsEffectivelyEnabled) throw new InvalidOperationException("The Kanban board is disabled.");
        if (!Board.Columns.Any(column => column.Tasks.Contains(task)))
            throw new InvalidOperationException("The task no longer belongs to this board.");
        if (exclusive) DeselectAllTasks();
        task.IsSelected = selected;
    }
}

internal sealed class FlowKanbanAutomationPeer(FlowKanban owner) : ControlAutomationPeer(owner), ISelectionProvider
{
    private readonly ConditionalWeakTable<FlowTask, FlowTaskModelAutomationPeer> _modelPeers = new();
    public bool CanSelectMultiple => true;
    public bool IsSelectionRequired => false;

    public IReadOnlyList<AutomationPeer> GetSelection()
    {
        var realized = owner.GetVisualDescendants().OfType<FlowTaskCard>().Where(card => card.Task != null)
            .GroupBy(card => card.Task!).ToDictionary(group => group.Key, group => group.First());
        return owner.Board.Columns.SelectMany(column => column.Tasks).Where(task => task.IsSelected)
            .Select(task => realized.TryGetValue(task, out var card)
                ? CreatePeerForElement(card)
                : _modelPeers.GetValue(task, value => new FlowTaskModelAutomationPeer(owner, value)))
            .ToArray();
    }

    internal void SelectionChanged(FlowTask? task = null)
    {
        RaisePropertyChangedEvent(SelectionPatternIdentifiers.SelectionProperty, null, null);
        if (task != null && _modelPeers.TryGetValue(task, out var peer)) peer.SelectionChanged();
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.List;
}

internal sealed class FlowTaskModelAutomationPeer(FlowKanban board, FlowTask task)
    : AutomationPeer, ISelectionItemProvider
{
    public bool IsSelected => task.IsSelected;
    public ISelectionProvider SelectionContainer => (ISelectionProvider)ControlAutomationPeer.CreatePeerForElement(board);
    public void Select() => board.SetTaskSelection(task, true, true);
    public void AddToSelection() => board.SetTaskSelection(task, true, false);
    public void RemoveFromSelection() => board.SetTaskSelection(task, false, false);
    internal void SelectionChanged() => RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, !IsSelected, IsSelected);
    protected override string? GetNameCore() => task.Title;
    protected override string? GetAutomationIdCore() => "kanban-card-" + task.Id;
    protected override string GetClassNameCore() => nameof(FlowTask);
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ListItem;
    protected override AutomationPeer? GetParentCore() => ControlAutomationPeer.CreatePeerForElement(board);
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => Array.Empty<AutomationPeer>();
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override string? GetAcceleratorKeyCore() => null;
    protected override string? GetAccessKeyCore() => null;
    protected override bool HasKeyboardFocusCore() => false;
    protected override bool IsContentElementCore() => true;
    protected override bool IsControlElementCore() => true;
    protected override bool IsEnabledCore() => board.IsEffectivelyEnabled;
    protected override bool ShowContextMenuCore() => false;
    protected override bool TrySetParent(AutomationPeer? parent) => ReferenceEquals(parent, GetParentCore());
    protected override void BringIntoViewCore() => board.BringIntoView();
    protected override void SetFocusCore() => throw new InvalidOperationException("The task has no realized card.");
    protected override bool IsOffscreenCore() => true;
    protected override bool IsKeyboardFocusableCore() => false;
    protected override Rect GetBoundingRectangleCore() => default;
}
