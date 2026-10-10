using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class XamlExplicitSizeTests
{
    [AvaloniaFact]
    public void Sizes_Set_In_Xaml_Survive_Global_Size_Changes()
    {
        var view = new ExplicitSizeView();
        var window = new Window { Width = 300, Height = 200, Content = view };
        var previous = FlowerySizeManager.CurrentSize;
        try
        {
            window.Show();
            Layout(window);
            var tiny = view.GetVisualDescendants().OfType<DaisyBadge>().Single(b => b.Name == "Tiny");
            var auto = view.GetVisualDescendants().OfType<DaisyBadge>().Single(b => b.Name == "Auto");
            var large = view.GetVisualDescendants().OfType<DaisyButton>().Single();
            Assert.Equal(DaisySize.ExtraSmall, tiny.Size);
            Assert.Equal(DaisySize.Large, large.Size);

            FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
            Layout(window);
            Assert.Equal(DaisySize.ExtraSmall, tiny.Size);
            Assert.Equal(DaisySize.Large, large.Size);
            Assert.Equal(DaisySize.ExtraLarge, auto.Size);
            Assert.True(tiny.Bounds.Height < auto.Bounds.Height);
        }
        finally
        {
            window.Close();
            FlowerySizeManager.ApplySize(previous);
        }
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
