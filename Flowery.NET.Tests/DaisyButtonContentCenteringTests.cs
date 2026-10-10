using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyButtonContentCenteringTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Content_Is_Vertically_Centered_Without_Trailing_Row_Spacing(bool withIcon)
    {
        var button = new DaisyButton { Content = "Add", Size = DaisySize.Medium };
        if (withIcon)
            button.IconSymbol = Flowery.Enums.DaisyIconSymbol.Add;
        var window = new Window { Width = 300, Height = 200, Content = new StackPanel { Children = { button } } };
        try
        {
            window.Show();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();

            var grid = button.GetVisualDescendants().OfType<Grid>().Single(g => g.Name == "PART_UnifiedContentGrid");
            var presenter = button.GetVisualDescendants().OfType<ContentPresenter>().Single(p => p.Name == "PART_ContentPresenter");
            var contentHeight = grid.Children.Where(c => c.IsVisible).Max(c => c.Bounds.Height);
            Assert.Equal(contentHeight, grid.Bounds.Height, 1);

            var top = ((Visual)presenter).TranslatePoint(new Point(0, 0), button)!.Value.Y;
            var bottom = button.Bounds.Height - (top + presenter.Bounds.Height);
            Assert.Equal(top, bottom, 1);

            if (withIcon)
            {
                button.IconPlacement = IconPlacement.Top;
                Dispatcher.UIThread.RunJobs();
                window.UpdateLayout();
                var viewbox = grid.Children.OfType<Viewbox>().Single();
                Assert.Equal(new Thickness(0, 0, 0, button.EffectiveIconSpacing), button.EffectiveIconMargin);
                Assert.Equal(button.EffectiveIconMargin, viewbox.Margin);
                Assert.Equal(1, Grid.GetRow(presenter));
                var contentWidth = grid.Children.Where(c => c.IsVisible).Max(c => c.Bounds.Width);
                Assert.Equal(contentWidth, grid.Bounds.Width, 1);
                var iconBottom = ((Visual)viewbox).TranslatePoint(new Point(0, viewbox.Bounds.Height), grid)!.Value.Y;
                var textTop = ((Visual)presenter).TranslatePoint(new Point(0, 0), grid)!.Value.Y;
                Assert.Equal(button.EffectiveIconSpacing, textTop - iconBottom, 1);
            }
        }
        finally
        {
            window.Close();
        }
    }
}
