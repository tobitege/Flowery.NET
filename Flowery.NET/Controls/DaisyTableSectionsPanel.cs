using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Flowery.Controls;

internal sealed class DaisyTableSectionsPanel : Panel
{
    private ScrollViewer? _viewer;
    private DaisyTable? _table;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _viewer = this.FindAncestorOfType<ScrollViewer>();
        _table = this.FindAncestorOfType<DaisyTable>();
        if (_viewer != null) _viewer.ScrollChanged += OnScrollChanged;
        if (_table != null) _table.PropertyChanged += OnTableChanged;
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_viewer != null) _viewer.ScrollChanged -= OnScrollChanged;
        if (_table != null) _table.PropertyChanged -= OnTableChanged;
        _viewer = null;
        _table = null;
        base.OnDetachedFromVisualTree(e);
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e) => InvalidatePinnedLayout();

    private void OnTableChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == DaisyTable.PinRowsProperty || e.Property == DaisyTable.PinColsProperty)
            InvalidatePinnedLayout();
    }

    private void InvalidatePinnedLayout()
    {
        InvalidateArrange();
        if (_table != null)
            foreach (var row in _table.GetVisualDescendants().OfType<DaisyTableRowPanel>()) row.InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double width = 0, height = 0;
        foreach (var child in Children)
        {
            child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            width = Math.Max(width, child.DesiredSize.Width);
            height += child.DesiredSize.Height;
        }
        return new Size(width, height);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var viewportTop = _viewer?.TranslatePoint(new Point(), this)?.Y ?? 0;
        var viewportHeight = _viewer?.Viewport.Height ?? finalSize.Height;
        double y = 0;
        foreach (var child in Children)
        {
            var height = child.DesiredSize.Height;
            var top = y;
            var pinned = _table?.PinRows == true && _viewer != null;
            if (pinned && child is DaisyTableHead)
                top = Math.Clamp(viewportTop, y, Math.Max(y, finalSize.Height - height));
            else if (pinned && child is DaisyTableFoot)
                top = Math.Clamp(viewportTop + viewportHeight - height, 0, y);
            child.ZIndex = pinned && child is DaisyTableHead or DaisyTableFoot ? 1 : 0;
            child.Arrange(new Rect(0, top, finalSize.Width, height));
            y += height;
        }
        return finalSize;
    }
}
