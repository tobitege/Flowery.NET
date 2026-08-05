namespace Flowery.NET.Kanban.Controls;

internal static class FlowKanbanVisualTree
{
    internal static T? FindAncestor<T>(
        AvaloniaObject? element,
        bool includeSelf = true,
        Func<T, bool>? predicate = null)
        where T : AvaloniaObject
    {
        var current = element as Visual;
        if (!includeSelf)
        {
            current = current?.GetVisualParent();
        }

        while (current is not null)
        {
            if (current is T match && (predicate is null || predicate(match)))
            {
                return match;
            }

            current = current.GetVisualParent();
        }

        return null;
    }

    internal static T? FindDescendant<T>(
        AvaloniaObject? root,
        Func<T, bool>? predicate = null,
        int maxDepth = int.MaxValue)
        where T : AvaloniaObject
    {
        if (root is not Visual visual || maxDepth <= 0)
        {
            return null;
        }

        foreach (var child in visual.GetVisualChildren())
        {
            if (child is T match && (predicate is null || predicate(match)))
            {
                return match;
            }

            if (maxDepth > 1 && FindDescendant<T>(child, predicate, maxDepth - 1) is { } descendant)
            {
                return descendant;
            }
        }

        return null;
    }

    internal static T? FindNamedDescendant<T>(AvaloniaObject? root, string name)
        where T : Control
    {
        return FindDescendant<T>(
            root,
            element => string.Equals(element.Name, name, StringComparison.Ordinal));
    }

    internal static bool IsDescendantOrSelf(AvaloniaObject? element, AvaloniaObject? ancestor)
    {
        if (ancestor is null)
        {
            return false;
        }

        for (var current = element as Visual; current is not null; current = current.GetVisualParent())
        {
            if (ReferenceEquals(current, ancestor))
            {
                return true;
            }
        }

        return false;
    }

    internal static bool IsAutomationVisible(Control control)
    {
        return control.IsAttachedToVisualTree() && control.IsEffectivelyVisible;
    }

    internal static Point TransformPoint(Visual from, Visual to, Point point)
    {
        return from.TransformToVisual(to)?.Transform(point) ?? point;
    }

    internal static AvaloniaObject? GetFocusedElement(Visual visual)
    {
        return TopLevel.GetTopLevel(visual)?.FocusManager.GetFocusedElement() as AvaloniaObject;
    }

    internal static IEnumerable<AvaloniaObject> Enumerate(AvaloniaObject root)
    {
        yield return root;
        if (root is not Visual visual)
        {
            yield break;
        }

        foreach (var descendant in visual.GetVisualDescendants())
        {
            yield return descendant;
        }
    }
}
