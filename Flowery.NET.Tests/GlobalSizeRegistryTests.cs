using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.Threading;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// Covers the central global-size registry: every control that declares a DaisySize
/// SizeProperty follows FlowerySizeManager without a per-control subscription.
/// </summary>
public class GlobalSizeRegistryTests
{
    [AvaloniaFact]
    public void Controls_Without_Subscription_Follow_Global_Size()
    {
        using var scope = new SizeScope();
        var button = new DaisyButton { Content = "Button" };
        var input = new DaisyInput();
        var select = new DaisySelect();
        var badge = new DaisyBadge { Content = "Badge" };
        var menu = new DaisyMenu();
        var kbd = new DaisyKbd { Content = "K" };
        var explicitButton = new DaisyButton { Content = "Fixed", Size = DaisySize.Large };
        var ignoredButton = new DaisyButton { Content = "Ignored" };
        var ignored = new StackPanel { Children = { ignoredButton } };
        FlowerySizeManager.SetIgnoreGlobalSize(ignored, true);
        scope.Show(new StackPanel { Children = { button, input, select, badge, menu, kbd, explicitButton, ignored } });

        foreach (var size in new[] { DaisySize.ExtraLarge, DaisySize.ExtraSmall, DaisySize.Large, DaisySize.Small })
        {
            FlowerySizeManager.ApplySize(size);
            scope.Layout();
            Assert.Equal(size, button.Size);
            Assert.Equal(size, input.Size);
            Assert.Equal(size, select.Size);
            Assert.Equal(size, badge.Size);
            Assert.Equal(size, menu.Size);
            Assert.Equal(size, kbd.Size);
            Assert.Equal(DaisySize.Large, explicitButton.Size);
            Assert.Equal(DaisySize.Medium, ignoredButton.Size);
        }
    }

    [AvaloniaFact]
    public void Late_Created_Controls_And_Second_Windows_Receive_The_Current_Size()
    {
        using var scope = new SizeScope();
        var panel = new StackPanel();
        scope.Show(panel);
        FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
        scope.Layout();

        var late = new DaisyButton { Content = "Late" };
        panel.Children.Add(late);
        scope.Layout();
        Assert.Equal(DaisySize.ExtraLarge, late.Size);

        var secondButton = new DaisyButton { Content = "Second window" };
        var second = new Window { Width = 200, Height = 100, Content = secondButton };
        second.Show();
        try
        {
            Dispatcher.UIThread.RunJobs();
            second.UpdateLayout();
            Assert.Equal(DaisySize.ExtraLarge, secondButton.Size);

            FlowerySizeManager.ApplySize(DaisySize.ExtraSmall);
            Dispatcher.UIThread.RunJobs();
            second.UpdateLayout();
            Assert.Equal(DaisySize.ExtraSmall, secondButton.Size);
            Assert.Equal(DaisySize.ExtraSmall, late.Size);
        }
        finally
        {
            second.Close();
        }
    }

    [AvaloniaFact]
    public void Style_Values_Win_And_Local_Values_Survive_Tier_Changes()
    {
        using var scope = new SizeScope();
        var styled = new DaisyButton { Content = "Styled" };
        var local = new DaisyButton { Content = "Local" };
        scope.Window.Styles.Add(new Style(static selector => selector.OfType<DaisyButton>().Name("styled"))
            { Setters = { new Setter(DaisyButton.SizeProperty, DaisySize.Small) } });
        styled.Name = "styled";
        scope.Show(new StackPanel { Children = { styled, local } });

        FlowerySizeManager.ApplySize(DaisySize.ExtraLarge);
        scope.Layout();
        Assert.Equal(DaisySize.Small, styled.Size);
        Assert.Equal(DaisySize.ExtraLarge, local.Size);

        local.Size = DaisySize.Large;
        FlowerySizeManager.ApplySize(DaisySize.ExtraSmall);
        scope.Layout();
        Assert.Equal(DaisySize.Large, local.Size);

        scope.Window.Styles.Clear();
        scope.Layout();
        Assert.Equal(DaisySize.ExtraSmall, styled.Size);

        local.ClearValue(DaisyButton.SizeProperty);
        scope.Layout();
        Assert.Equal(DaisySize.ExtraSmall, local.Size);
    }

    [AvaloniaFact]
    public void Lifecycle_Controls_Keep_Explicit_Size_Across_Tier_Changes()
    {
        using var scope = new SizeScope();
        var card = new DaisyCard();
        var fixedCard = new DaisyCard { Size = DaisySize.Small };
        var pagination = new DaisyPagination();
        var fixedPagination = new DaisyPagination { Size = DaisySize.Small };
        scope.Show(new StackPanel { Children = { card, fixedCard, pagination, fixedPagination } });

        foreach (var size in new[] { DaisySize.ExtraLarge, DaisySize.Large, DaisySize.ExtraSmall })
        {
            FlowerySizeManager.ApplySize(size);
            scope.Layout();
            Assert.Equal(size, card.Size);
            Assert.Equal(size, pagination.Size);
            Assert.Equal(DaisySize.Small, fixedCard.Size);
            Assert.Equal(DaisySize.Small, fixedPagination.Size);
        }
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
