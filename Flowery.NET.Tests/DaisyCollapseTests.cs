using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyCollapseTests
    {
        [AvaloniaFact]
        public void When_HeaderIsPressed_ExpansionDoesNotWaitForPointerRelease()
        {
            var collapse = new DaisyCollapse
            {
                Header = "Kanban Features",
                Content = new TextBlock { Text = "Feature details" }
            };
            var window = new Window
            {
                Width = 400,
                Height = 200,
                Content = collapse
            };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();

                var header = collapse.GetVisualDescendants()
                    .OfType<ToggleButton>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_HeaderButton",
                        StringComparison.Ordinal));
                var headerPosition = LayoutTestAssertions.GetPosition(header, window);
                var pointerPosition = new Point(
                    headerPosition.X + header.Bounds.Width / 2,
                    headerPosition.Y + header.Bounds.Height / 2);

                window.MouseMove(pointerPosition, RawInputModifiers.None);
                window.MouseDown(pointerPosition, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();

                Assert.True(collapse.IsExpanded);

                window.MouseUp(pointerPosition, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.True(collapse.IsExpanded);

                window.MouseDown(pointerPosition, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(collapse.IsExpanded);

                window.MouseUp(pointerPosition, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                Assert.False(collapse.IsExpanded);
            }
            finally
            {
                window.Close();
            }
        }
    }
}
