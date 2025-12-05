using Avalonia;
using Avalonia.Controls;

namespace Flowery.Controls
{
    /// <summary>
    /// A panel that stacks all children on top of each other at position (0,0).
    /// Creates a deck-of-cards effect with offset transforms.
    /// </summary>
    public class OverlayPanel : Panel
    {
        protected override Size MeasureOverride(Size availableSize)
        {
            var maxWidth = 0.0;
            var maxHeight = 0.0;

            foreach (var child in Children)
            {
                child.Measure(availableSize);
                maxWidth = System.Math.Max(maxWidth, child.DesiredSize.Width);
                maxHeight = System.Math.Max(maxHeight, child.DesiredSize.Height);
            }

            return new Size(maxWidth, maxHeight);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var arrangeRect = new Rect(0, 0, finalSize.Width, finalSize.Height);
            
            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                // Arrange ALL children at the exact same position (0,0)
                child.Arrange(arrangeRect);
            }

            return finalSize;
        }
    }
}
