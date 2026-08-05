namespace Flowery.NET.Kanban.Controls;

internal sealed class FlowKanbanSelectOption<TValue>(string text, TValue value)
{
    internal string Text { get; } = text;

    internal TValue Value { get; } = value;

    public override string ToString()
    {
        return Text;
    }
}
