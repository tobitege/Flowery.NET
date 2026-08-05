using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    internal static class LayoutTestAssertions
    {
        internal static double GetUnoButtonHeight(DaisySize size) => size switch
        {
            DaisySize.ExtraSmall => 24,
            DaisySize.Small => 28,
            DaisySize.Medium => 32,
            DaisySize.Large => 36,
            DaisySize.ExtraLarge => 40,
            _ => 32
        };

        internal static double GetUnoButtonFontSize(DaisySize size) => size switch
        {
            DaisySize.ExtraSmall => 8,
            DaisySize.Small => 10,
            DaisySize.Medium => 12,
            DaisySize.Large => 14,
            DaisySize.ExtraLarge => 16,
            _ => 12
        };

        internal static Thickness GetUnoButtonPadding(DaisySize size) => size switch
        {
            DaisySize.ExtraSmall => new Thickness(8, 0),
            DaisySize.Small => new Thickness(12, 0),
            DaisySize.Medium => new Thickness(12, 0),
            DaisySize.Large => new Thickness(16, 0),
            DaisySize.ExtraLarge => new Thickness(16, 0),
            _ => new Thickness(12, 0)
        };

        internal static double GetUnoButtonIconSize(DaisySize size) => size switch
        {
            DaisySize.ExtraSmall => 8,
            DaisySize.Small => 12,
            DaisySize.Medium => 16,
            DaisySize.Large => 20,
            DaisySize.ExtraLarge => 24,
            _ => 16
        };

        internal static double GetUnoButtonIconSpacing(DaisySize size) => size switch
        {
            DaisySize.ExtraSmall => 4,
            DaisySize.Small => 4,
            _ => 8
        };

        internal static void HasSize(Control control, double width, double height)
        {
            Assert.Equal(width, control.Bounds.Width, precision: 3);
            Assert.Equal(height, control.Bounds.Height, precision: 3);
        }

        internal static void IsCentered(Control container, Visual content, double tolerance = 0.5)
        {
            var position = GetPosition(content, container);
            var horizontalOffset = Math.Abs(
                container.Bounds.Width / 2 - (position.X + content.Bounds.Width / 2));
            var verticalOffset = Math.Abs(
                container.Bounds.Height / 2 - (position.Y + content.Bounds.Height / 2));

            Assert.InRange(horizontalOffset, 0, tolerance);
            Assert.InRange(verticalOffset, 0, tolerance);
        }

        internal static void HasHorizontalPadding(DaisyButton button)
        {
            var contentPanel = button.GetVisualDescendants()
                .OfType<StackPanel>()
                .Single(control => string.Equals(control.Name, "PART_ContentPanel", StringComparison.Ordinal));
            var expectedWidth = contentPanel.Bounds.Width + button.Padding.Left + button.Padding.Right;

            Assert.Equal(expectedWidth, button.Bounds.Width, precision: 3);
        }

        internal static Point GetPosition(Visual control, Visual relativeTo)
        {
            var transform = control.TransformToVisual(relativeTo);
            Assert.NotNull(transform);
            return transform.Value.Transform(default);
        }
    }
}
