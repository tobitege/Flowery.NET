using Avalonia.Controls;

namespace Flowery.NET.Photos.Views;

/// <summary>
/// A status bar row: the first child gets the width the others leave and trims only there, so a long file name stays
/// whole while there is room. When it would get less than <see cref="MinFirstWidth"/>, the others move below it and
/// wrap onto as many lines as they need.
/// </summary>
public sealed class StatusLine : Panel
{
    public const double MinFirstWidth = 200;
    private bool _wrapped;

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Children.Count == 0) return default;
        double rest = 0, restHeight = 0;
        foreach (var child in Children.Skip(1))
        {
            child.Measure(Size.Infinity);
            rest += child.DesiredSize.Width;
            restHeight = Math.Max(restHeight, child.DesiredSize.Height);
        }
        _wrapped = availableSize.Width - rest < MinFirstWidth;
        var first = Children[0];
        first.Measure(availableSize.WithWidth(_wrapped ? availableSize.Width : availableSize.Width - rest));
        var size = first.DesiredSize;
        if (!_wrapped) return new Size(size.Width + rest, Math.Max(size.Height, restHeight));
        var placed = Flow(availableSize.Width, size.Height).ToList();
        return new Size(placed.Max(rect => rect.Right), placed.Max(rect => rect.Bottom));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (Children.Count == 0) return finalSize;
        var first = Children[0].DesiredSize;
        if (_wrapped)
        {
            foreach (var (child, place) in Children.Zip(Flow(finalSize.Width, first.Height))) child.Arrange(place);
            return finalSize;
        }
        Children[0].Arrange(new Rect(0, 0, first.Width, finalSize.Height));
        var x = first.Width;
        foreach (var child in Children.Skip(1))
        {
            child.Arrange(new Rect(x, 0, child.DesiredSize.Width, finalSize.Height));
            x += child.DesiredSize.Width;
        }
        return finalSize;
    }

    // The first child's place, then the others' from the given top, left to right, starting a line where one ends.
    private IEnumerable<Rect> Flow(double width, double top)
    {
        yield return new Rect(Children[0].DesiredSize);
        double x = 0, y = top, line = 0;
        foreach (var child in Children.Skip(1))
        {
            var size = child.DesiredSize;
            if (x > 0 && x + size.Width > width) { x = 0; y += line; line = 0; }
            yield return new Rect(new Point(x, y), size);
            x += size.Width;
            line = Math.Max(line, size.Height);
        }
    }
}
