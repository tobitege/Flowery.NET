using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyStatSizeTests
{
    [AvaloniaFact]
    public void Stat_Text_Follows_Size_And_Can_Be_Styled()
    {
        var stat = new DaisyStat { Title = "Title", Value = "42", Description = "Description" };
        var window = new Window { Width = 400, Height = 300, Content = stat };
        try
        {
            window.Show();
            Layout(window);
            Assert.Equal(Token(stat, "DaisyStatMediumValueFontSize"), Text(stat, "ValueText").FontSize);
            Assert.Equal(Token(stat, "DaisySizeMediumSecondaryFontSize"), Text(stat, "DescriptionText").FontSize);

            stat.Size = DaisySize.ExtraLarge;
            Layout(window);
            Assert.Equal(Token(stat, "DaisyStatExtraLargeValueFontSize"), Text(stat, "ValueText").FontSize);
            Assert.Equal(Token(stat, "DaisySizeExtraLargeSecondaryFontSize"), Text(stat, "DescriptionText").FontSize);
            Assert.Equal(Token(stat, "DaisySizeExtraLargeFontSize"), stat.FontSize);

            stat.ValueFontSize = 40.0;
            Layout(window);
            Assert.Equal(40.0, Text(stat, "ValueText").FontSize);

            stat.ClearValue(DaisyStat.ValueFontSizeProperty);
            stat.Size = DaisySize.Medium;
            window.Styles.Add(new Style(static selector => selector.OfType<DaisyStat>())
                { Setters = { new Setter(DaisyStat.ValueFontSizeProperty, 36.0) } });
            Layout(window);
            Assert.Equal(36.0, Text(stat, "ValueText").FontSize);
        }
        finally
        {
            window.Close();
        }
    }

    private static double Token(Control control, string key)
    {
        Assert.True(control.TryFindResource(key, out var value));
        return Assert.IsType<double>(value);
    }

    private static TextBlock Text(DaisyStat stat, string name) =>
        stat.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == name);

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
