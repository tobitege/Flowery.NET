namespace Flowery.NET.Kanban.Controls;

public sealed class FlowKanbanWrapPanel : WrapPanel
{
    private double _horizontalSpacing;
    private double _verticalSpacing;

    public FlowKanbanWrapPanel()
    {
        Children.CollectionChanged += OnChildrenChanged;
    }

    public double Spacing
    {
        get => Math.Max(HorizontalSpacing, VerticalSpacing);
        set
        {
            HorizontalSpacing = value;
            VerticalSpacing = value;
        }
    }

    public double HorizontalSpacing
    {
        get => _horizontalSpacing;
        set
        {
            if (Math.Abs(_horizontalSpacing - value) < double.Epsilon)
            {
                return;
            }

            _horizontalSpacing = Math.Max(0, value);
            ApplySpacing();
        }
    }

    public double VerticalSpacing
    {
        get => _verticalSpacing;
        set
        {
            if (Math.Abs(_verticalSpacing - value) < double.Epsilon)
            {
                return;
            }

            _verticalSpacing = Math.Max(0, value);
            ApplySpacing();
        }
    }

    private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        ApplySpacing();
    }

    private void ApplySpacing()
    {
        var margin = new Thickness(HorizontalSpacing / 2, VerticalSpacing / 2);
        foreach (var child in Children)
        {
            child.Margin = margin;
        }
    }
}
