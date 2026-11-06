using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Theming;
using Xunit;
using Path = Avalonia.Controls.Shapes.Path;

namespace Flowery.NET.RuntimeTests.Controls;

public class Given_DaisyExpander
{
    [Fact]
    public async Task When_The_Expander_Has_A_Top_Border_It_Is_Drawn_Once()
    {
        var expander = new DaisyExpander
        {
            Header = "Section", ExpanderContent = new TextBlock { Text = "Body" },
            BorderThickness = new Thickness(0, 1, 0, 0), BorderBrush = Brushes.Red
        };
        await RuntimeTestHelpers.AttachAsync(expander);
        // The control template must not draw the border, background or padding of the expander a second time.
        var bordered = expander.GetVisualDescendants().Where(static visual => visual switch
        {
            Border border => border.BorderThickness != default,
            ContentPresenter presenter => presenter.BorderThickness != default,
            _ => false
        }).ToList();
        var single = Assert.Single(bordered);
        Assert.IsType<Border>(single);
        Assert.Equal(new Thickness(0, 1, 0, 0), ((Border)single).BorderThickness);
    }

    [Fact]
    public async Task When_High_Contrast_Ends_The_Chevron_No_Longer_Follows_The_Header_Text()
    {
        var previous = DaisyAccessibility.HighContrastOverride;
        var expander = new DaisyExpander { Header = "Section", ExpanderContent = new TextBlock { Text = "Body" } };
        try
        {
            await RuntimeTestHelpers.AttachAsync(expander);
            DaisyAccessibility.HighContrastOverride = true;
            await RuntimeTestHelpers.WaitForRenderAsync();
            DaisyAccessibility.HighContrastOverride = false;
            await RuntimeTestHelpers.WaitForRenderAsync();
            var chevron = (Path)Chevron(expander).Child!;
            var fill = chevron.Fill;
            HeaderPresenter(expander).Foreground = Brushes.Red;
            Assert.Same(fill, chevron.Fill);
        }
        finally
        {
            DaisyAccessibility.HighContrastOverride = previous;
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }

    [Fact]
    public async Task When_An_Ancestor_Is_Disabled_The_Contrast_Text_Takes_The_Disabled_Color()
    {
        var previous = DaisyAccessibility.HighContrastOverride;
        var content = new TextBlock { Text = "Body" };
        var expander = new DaisyExpander { Header = "Section", ExpanderContent = content };
        var host = new Border { Child = expander };
        try
        {
            await RuntimeTestHelpers.AttachAsync(host);
            DaisyAccessibility.HighContrastOverride = true;
            await RuntimeTestHelpers.WaitForRenderAsync();
            var presenter = (ContentPresenter)ContentBorder(expander, content).Child!;
            Assert.Same(DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"), presenter.Foreground);

            host.IsEnabled = false;
            Assert.True(expander.IsEnabled);
            Assert.Same(DaisyResourceLookup.GetBrush("DaisyBaseContentDisabledBrush"), presenter.Foreground);
            host.IsEnabled = true;
            Assert.Same(DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"), presenter.Foreground);
        }
        finally
        {
            DaisyAccessibility.HighContrastOverride = previous;
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }

    [Fact]
    public async Task When_High_Contrast_Is_On_The_Header_Uses_The_Contrast_Colors_And_Returns_Afterwards()
    {
        var previous = DaisyAccessibility.HighContrastOverride;
        var expander = new DaisyExpander
        {
            Header = "Section", ExpanderContent = new TextBlock { Text = "Body" }, HeaderBackground = Brushes.Blue,
            BorderBrush = Brushes.Red, BorderThickness = new Thickness(0, 1, 0, 0)
        };
        try
        {
            await RuntimeTestHelpers.AttachAsync(expander);
            DaisyAccessibility.HighContrastOverride = true;
            await RuntimeTestHelpers.WaitForRenderAsync();
            var border = HeaderBorder(expander);
            Assert.Same(DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"), border.BorderBrush);
            Assert.Equal(new Thickness(1), border.BorderThickness);
            Assert.Same(DaisyResourceLookup.GetBrush("DaisyBase100Brush"), HeaderButton(expander).Background);
            // In contrast mode the chevron takes the color of the header text.
            var chevron = (Path)Chevron(expander).Child!;
            HeaderPresenter(expander).Foreground = Brushes.Red;
            Assert.Same(Brushes.Red, chevron.Fill);

            DaisyAccessibility.HighContrastOverride = false;
            await RuntimeTestHelpers.WaitForRenderAsync();
            Assert.Same(Brushes.Red, border.BorderBrush);
            Assert.Equal(new Thickness(0, 1, 0, 0), border.BorderThickness);
            Assert.Same(Brushes.Blue, HeaderButton(expander).Background);
        }
        finally
        {
            DaisyAccessibility.HighContrastOverride = previous;
            await RuntimeTestHelpers.WaitForRenderAsync();
        }
    }

    [Fact]
    public async Task When_The_Expander_Unloads_It_Leaves_The_Theme_Events()
    {
        var expander = new DaisyExpander { Header = "Section" };
        await RuntimeTestHelpers.AttachAsync(expander);
        Assert.Equal(1, Subscriptions(typeof(DaisyThemeManager), "ThemeChanged", expander));
        Assert.Equal(1, Subscriptions(typeof(DaisyAccessibility), "PreferencesChanged", expander));

        RuntimeTestHelpers.GetHostPanel().Children.Remove(expander);
        await RuntimeTestHelpers.WaitForRenderAsync();
        Assert.Equal(0, Subscriptions(typeof(DaisyThemeManager), "ThemeChanged", expander));
        Assert.Equal(0, Subscriptions(typeof(DaisyAccessibility), "PreferencesChanged", expander));
    }

    private static Button HeaderButton(DaisyExpander expander) =>
        expander.GetVisualDescendants().OfType<Button>().First();

    private static ContentPresenter HeaderPresenter(DaisyExpander expander) =>
        HeaderButton(expander).GetVisualDescendants().OfType<ContentPresenter>().Single(presenter => presenter.Content is not Grid);

    private static Border HeaderBorder(DaisyExpander expander) =>
        expander.GetVisualDescendants().OfType<Border>().First(border => border.Child is Button);

    private static Viewbox Chevron(DaisyExpander expander) =>
        expander.GetVisualDescendants().OfType<Viewbox>().Single();

    private static Border ContentBorder(DaisyExpander expander, Control content) =>
        expander.GetVisualDescendants().OfType<Border>().Single(border => border.Child is ContentPresenter presenter && ReferenceEquals(presenter.Content, content));

    // Field-like static events keep their delegates in a private field of the same name.
    private static int Subscriptions(Type owner, string eventName, object subscriber) =>
        owner.GetField(eventName, BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null) is Delegate handlers
            ? handlers.GetInvocationList().Count(handler => ReferenceEquals(handler.Target, subscriber))
            : 0;
}
