using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// Covers the visual-tree propagation of FlowerySizeManager: repeated tier changes reach
/// every control again, and author values (local or style) are never overwritten.
/// </summary>
public class FlowerySizeManagerPropagationTests
{
    [AvaloniaFact]
    public void Repeated_Propagation_Updates_Controls_And_Keeps_Author_Values()
    {
        using var scope = new SizeScope();
        var button = new DaisyButton { Content = "Auto" };
        var input = new DaisyInput();
        var explicitButton = new DaisyButton { Content = "Fixed", Size = DaisySize.Large };
        var styledButton = new DaisyButton { Content = "Styled", Name = "styled" };
        scope.Window.Styles.Add(new Style(static selector => selector.OfType<DaisyButton>().Name("styled"))
            { Setters = { new Setter(DaisyButton.SizeProperty, DaisySize.ExtraSmall) } });
        scope.Show(new StackPanel { Children = { button, input, explicitButton, styledButton } });

        foreach (var size in new[] { DaisySize.Large, DaisySize.Small, DaisySize.ExtraLarge, DaisySize.Medium })
        {
            FlowerySizeManager.ApplySize(size);
            scope.Layout();
            Assert.Equal(size, button.Size);
            Assert.Equal(size, input.Size);
            Assert.Equal(DaisySize.Large, explicitButton.Size);
            Assert.Equal(DaisySize.ExtraSmall, styledButton.Size);
        }

        scope.Window.Styles.Clear();
        FlowerySizeManager.RefreshAllSizes();
        scope.Layout();
        Assert.Equal(DaisySize.Medium, styledButton.Size);

        button.Size = DaisySize.ExtraLarge;
        FlowerySizeManager.ApplySize(DaisySize.Small);
        scope.Layout();
        Assert.Equal(DaisySize.ExtraLarge, button.Size);
        Assert.Equal(DaisySize.Small, input.Size);
    }

    private sealed class SizeScope : IDisposable
    {
        private readonly Window? _previousWindow = FlowerySizeManager.MainWindow;
        private readonly DaisySize _previousSize = FlowerySizeManager.CurrentSize;
        private readonly bool _auto = FlowerySizeManager.EnableGlobalAutoSize;
        private readonly bool _useGlobal = FlowerySizeManager.UseGlobalSizeByDefault;
        public Window Window { get; } = new() { Width = 500, Height = 600 };

        public SizeScope()
        {
            FlowerySizeManager.MainWindow = null;
            FlowerySizeManager.EnableGlobalAutoSize = true;
            FlowerySizeManager.UseGlobalSizeByDefault = true;
            FlowerySizeManager.ApplySize(DaisySize.Medium);
        }

        public void Show(Control content)
        {
            Window.Content = content;
            Window.Show();
            FlowerySizeManager.MainWindow = Window;
            FlowerySizeManager.RefreshAllSizes();
            Layout();
        }

        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
        }

        public void Dispose()
        {
            Window.Close();
            FlowerySizeManager.MainWindow = null;
            FlowerySizeManager.ApplySize(_previousSize);
            FlowerySizeManager.EnableGlobalAutoSize = _auto;
            FlowerySizeManager.UseGlobalSizeByDefault = _useGlobal;
            FlowerySizeManager.MainWindow = _previousWindow;
        }
    }
}
