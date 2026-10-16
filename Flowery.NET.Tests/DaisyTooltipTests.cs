using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyTooltipTests
{
    [AvaloniaFact]
    public void Tip_Creates_A_Themed_ToolTip()
    {
        var tooltip = new DaisyTooltip { Content = new Button(), Tip = "Save" };

        var tip = Assert.IsType<ToolTip>(ToolTip.GetTip(tooltip));
        Assert.Equal("Save", tip.Content);
        Assert.Equal(PlacementMode.Top, ToolTip.GetPlacement(tooltip));

        tooltip.Tip = string.Empty;
        Assert.Null(ToolTip.GetTip(tooltip));
    }

    [AvaloniaFact]
    public void Variant_Maps_To_ToolTip_Class()
    {
        var tooltip = new DaisyTooltip { Tip = "Saved", Variant = DaisyColor.Success };
        var tip = Assert.IsType<ToolTip>(ToolTip.GetTip(tooltip));
        Assert.Contains("success", tip.Classes);

        tooltip.Variant = DaisyColor.Error;
        Assert.DoesNotContain("success", tip.Classes);
        Assert.Contains("error", tip.Classes);

        tooltip.Variant = DaisyColor.Default;
        Assert.DoesNotContain("error", tip.Classes);
    }

    [AvaloniaFact]
    public void Placement_And_IsOpen_Reach_The_ToolTip()
    {
        var tooltip = new DaisyTooltip { Tip = "Hint", Placement = PlacementMode.Bottom };
        var window = new Window { Width = 300, Height = 200, Content = tooltip };
        try
        {
            window.Show();
            Assert.Equal(PlacementMode.Bottom, ToolTip.GetPlacement(tooltip));

            tooltip.IsOpen = true;
            Assert.True(ToolTip.GetIsOpen(tooltip));

            ToolTip.SetIsOpen(tooltip, false);
            Assert.False(tooltip.IsOpen);
        }
        finally
        {
            window.Close();
        }
    }
}
