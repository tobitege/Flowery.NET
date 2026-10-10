using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class MutedTextBrushTests
{
    [AvaloniaFact]
    public void Dimmed_Text_Uses_Muted_Brushes_Instead_Of_Opacity()
    {
        var stat = new DaisyStat { Title = "Title", Value = "1", Description = "Description" };
        var input = new DaisyInput { Watermark = "Search", HintText = "Hint" };
        var breadcrumbs = new DaisyBreadcrumbs
        {
            Items = { new DaisyBreadcrumbItem { Content = "Home" }, new DaisyBreadcrumbItem { Content = "Current" } }
        };
        var panel = new StackPanel { Children = { stat, input, breadcrumbs } };
        var window = new Window { Width = 400, Height = 400, Content = panel };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var muted = Brush(panel, "DaisyBaseContentMutedBrush");
            var subtle = Brush(panel, "DaisyBaseContentSubtleBrush");

            var title = stat.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Text == "Title");
            Assert.Equal(1.0, title.Opacity);
            Assert.Same(muted, title.Foreground);

            var watermark = input.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_Watermark");
            Assert.Equal(1.0, watermark.Opacity);
            Assert.Same(subtle, watermark.Foreground);

            var hint = input.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_HintText");
            Assert.Equal(1.0, hint.Opacity);
            Assert.Same(muted, hint.Foreground);

            var separator = breadcrumbs.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_Separator" && text.IsVisible);
            Assert.Equal(1.0, separator.Opacity);
            Assert.Same(subtle, separator.Foreground);

            var last = breadcrumbs.GetVisualDescendants().OfType<DaisyBreadcrumbItem>().Single(item => item.IsLast);
            Assert.Equal(1.0, last.Opacity);
            Assert.Same(muted, last.Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    private static object Brush(Control control, string key)
    {
        Assert.True(control.TryFindResource(key, control.ActualThemeVariant, out var brush));
        return brush!;
    }
}
