using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.RuntimeTests.Native;

public class Given_NativePopup
{
    [Fact]
    public async Task When_The_Offset_Or_Window_Changes_The_Popup_Stays_On_Screen()
    {
        var window = RuntimeTestHelpers.GetWindow();
        var (width, height, position) = (window.Width, window.Height, window.Position);
        try
        {
            // The popup needs room around the window: at a screen edge it is held in place and offsets cannot move it.
            // Like the old 800x600 probe window, the window shrinks, at most to the placed size, and is centered
            // in the placed bounds, so it never leaves the requested monitor region.
            var probeWidth = Math.Min(800, width);
            var probeHeight = Math.Min(600, height);
            var scaling = window.DesktopScaling;
            window.Width = probeWidth;
            window.Height = probeHeight;
            window.Position = new PixelPoint(
                position.X + (int)((width - probeWidth) * scaling / 2),
                position.Y + (int)((height - probeHeight) * scaling / 2));
            var popover = await RuntimeTestHelpers.AttachAsync(new ProbePopover
            {
                TriggerContent = "Open",
                PopoverContent = new Border { Width = 240, Height = 160, Background = Brushes.Coral },
                HorizontalAlignment = HorizontalAlignment.Right,
                VerticalAlignment = VerticalAlignment.Bottom,
                IsLightDismissEnabled = false
            });
            popover.IsOpen = true;
            await RuntimeTestHelpers.WaitForRenderAsync();
            try
            {
                var root = TopLevel.GetTopLevel(popover.Popup.Child)!;
                Assert.True(root is not Window && root.TryGetPlatformHandle() is not null, "Expected a native popup root.");
                AssertInsideScreen(window, root);
                var before = root.PointToScreen(default);
                popover.HorizontalOffset = -80;
                await RuntimeTestHelpers.WaitForRenderAsync();
                var after = root.PointToScreen(default);
                Assert.True(after != before, "Changing the offset did not reposition the popup.");
                window.Width = Math.Min(650, probeWidth);
                window.Height = Math.Min(470, probeHeight);
                await RuntimeTestHelpers.WaitForRenderAsync();
                AssertInsideScreen(window, root);
            }
            finally
            {
                popover.IsOpen = false;
            }
        }
        finally
        {
            // Later tests use the placed window size.
            window.Width = width;
            window.Height = height;
            window.Position = position;
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }

    private static void AssertInsideScreen(Window window, TopLevel popup)
    {
        var area = window.Screens.ScreenFromWindow(window)!.WorkingArea;
        var origin = popup.PointToScreen(default);
        var right = origin.X + popup.Bounds.Width * popup.RenderScaling;
        var bottom = origin.Y + popup.Bounds.Height * popup.RenderScaling;
        Assert.True(origin.X >= area.X && origin.Y >= area.Y && right <= area.Right + 1 && bottom <= area.Bottom + 1,
            $"Popup {origin}, {popup.Bounds.Size} outside screen {area}.");
    }

    private sealed class ProbePopover : DaisyPopover
    {
        public Popup Popup { get; private set; } = null!;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            Popup = e.NameScope.Find<Popup>("PART_Popup")!;
        }
    }
}
