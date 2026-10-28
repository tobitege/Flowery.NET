using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Interactivity;
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
    public async Task When_The_Header_Is_Clicked_The_Content_Toggles_And_The_Settings_Reach_Their_Parts()
    {
        var content = new TextBlock { Text = "Body" };
        var expander = new DaisyExpander
        {
            Header = new TextBlock { Text = "Header" }, ExpanderContent = content,
            HeaderPadding = new Thickness(1, 2, 3, 4), ContentPadding = new Thickness(5, 6, 7, 8), ChevronSize = 20
        };
        await RuntimeTestHelpers.AttachAsync(expander);
        var header = HeaderButton(expander);
        var body = ContentBorder(expander, content);
        var chevron = Chevron(expander);
        Assert.Equal(new Thickness(1, 2, 3, 4), header.Padding);
        Assert.Equal(new Thickness(5, 6, 7, 8), body.Padding);
        Assert.Equal(20, chevron.Width);
        Assert.Equal(20, chevron.Height);
        Assert.True(body.IsVisible);

        header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.False(expander.IsExpanded);
        Assert.False(body.IsVisible);
        header.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Assert.True(expander.IsExpanded);
        Assert.True(body.IsVisible);

        expander.ShowChevron = false;
        Assert.False(chevron.IsVisible);
        expander.ChevronSize = 10;
        Assert.Equal(10, chevron.Width);
    }

    [Fact]
    public async Task When_Settings_Are_Made_Before_The_Template_Is_Built_The_Parts_Show_Them()
    {
        var content = new TextBlock { Text = "Body" };
        var expander = new DaisyExpander { Header = "Section header", IsExpanded = false, ShowChevron = false, ExpanderContent = content };
        await RuntimeTestHelpers.AttachAsync(expander);
        Assert.Contains(HeaderButton(expander).GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Section header");
        Assert.False(ContentBorder(expander, content).IsVisible);
        Assert.False(Chevron(expander).IsVisible);
    }

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
    public async Task When_Content_Is_Set_It_Is_The_Expandable_Content_And_The_Header_Stays()
    {
        var first = new TextBlock { Text = "First" };
        var expander = new DaisyExpander { Header = "Section", Content = first };
        await RuntimeTestHelpers.AttachAsync(expander);
        Assert.Same(first, expander.ExpanderContent);
        Assert.Same(first, ((ContentPresenter)ContentBorder(expander, first).Child!).Child);
        Assert.Single(expander.GetVisualDescendants().OfType<Button>());

        var second = new TextBlock { Text = "Second" };
        expander.ExpanderContent = second;
        await RuntimeTestHelpers.WaitForRenderAsync();
        Assert.Same(second, expander.Content);
        Assert.Same(second, ((ContentPresenter)ContentBorder(expander, second).Child!).Child);
        Assert.Single(expander.GetVisualDescendants().OfType<Button>());
        Assert.Null(first.Parent);
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
    public async Task When_Header_Colors_Are_Set_Before_And_After_The_Template_Is_Built_The_Header_Shows_Them()
    {
        var expander = new DaisyExpander { Header = "Section", HeaderBackground = Brushes.Blue, HeaderForeground = Brushes.Orange };
        await RuntimeTestHelpers.AttachAsync(expander);
        var chevron = (Path)Chevron(expander).Child!;
        Assert.Same(Brushes.Blue, HeaderButton(expander).Background);
        Assert.Same(Brushes.Orange, chevron.Fill);

        expander.HeaderBackground = Brushes.Green;
        expander.HeaderForeground = Brushes.Purple;
        Assert.Same(Brushes.Green, HeaderButton(expander).Background);
        Assert.Same(Brushes.Purple, chevron.Fill);
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
