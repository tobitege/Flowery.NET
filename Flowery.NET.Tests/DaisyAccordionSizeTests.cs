using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyAccordionSizeTests
{
    [AvaloniaFact]
    public void Accordion_Size_Reaches_Items_And_String_Headers()
    {
        var first = new DaisyAccordionItem { Header = "First", Content = new TextBlock { Text = "A" } };
        var second = new DaisyAccordionItem { Header = "Second", Content = new TextBlock { Text = "B" }, Size = DaisySize.ExtraSmall };
        var accordion = new DaisyAccordion { Items = { first, second } };
        var window = new Window { Width = 400, Height = 300, Content = accordion };
        try
        {
            window.Show();
            Layout(window);
            Assert.Equal(DaisySize.Medium, first.Size);
            Assert.Equal(DaisySize.ExtraSmall, second.Size);

            accordion.Size = DaisySize.ExtraLarge;
            Layout(window);
            Assert.Equal(DaisySize.ExtraLarge, first.Size);
            Assert.Equal(DaisySize.ExtraSmall, second.Size);
            Assert.True(first.TryFindResource("DaisySizeExtraLargeFontSize", out var fontSize));
            Assert.Equal(Assert.IsType<double>(fontSize), first.FontSize);
            Assert.Equal(first.FontSize, HeaderText(first).FontSize);
            Assert.True(first.TryFindResource("DaisyAccordionExtraLargeHeaderMinHeight", out var minHeight));
            Assert.Equal(Assert.IsType<double>(minHeight), HeaderButton(first).MinHeight);
            Assert.True(HeaderText(first).FontSize > HeaderText(second).FontSize);
        }
        finally
        {
            window.Close();
        }
    }

    private static ToggleButton HeaderButton(DaisyAccordionItem item) =>
        item.GetVisualDescendants().OfType<ToggleButton>().Single(button => button.Name == "PART_HeaderButton");

    private static TextBlock HeaderText(DaisyAccordionItem item) =>
        HeaderButton(item).GetVisualDescendants().OfType<TextBlock>().Single();

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
