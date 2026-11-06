using System.ComponentModel;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>DaisyExpander: the header colors, a bound IsExpanded and what screen readers hear.</summary>
public class DaisyExpanderTests
{
    [AvaloniaFact]
    public void The_Header_Foreground_Colors_The_Header_Text_And_Gives_Way_To_High_Contrast()
    {
        var previous = DaisyAccessibility.HighContrastOverride;
        var expander = new DaisyExpander { Header = "Section", HeaderForeground = Brushes.Orange };
        using var window = new TestWindow(expander);
        // Loaded, where the expander starts to follow the contrast setting.
        Dispatcher.UIThread.RunJobs();
        try
        {
            Assert.Same(Brushes.Orange, HeaderText(expander).Foreground);
            expander.HeaderForeground = Brushes.Purple;
            Assert.Same(Brushes.Purple, HeaderText(expander).Foreground);

            DaisyAccessibility.HighContrastOverride = true;
            Assert.NotSame(Brushes.Purple, HeaderText(expander).Foreground);
            DaisyAccessibility.HighContrastOverride = false;
            Assert.Same(Brushes.Purple, HeaderText(expander).Foreground);

            // A disabled header shows the disabled color of the button, and the chevron takes it too.
            var chevron = expander.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single();
            expander.IsEnabled = false;
            Assert.NotSame(Brushes.Purple, HeaderText(expander).Foreground);
            Assert.Same(HeaderText(expander).Foreground, chevron.Fill);
            expander.IsEnabled = true;
            Assert.Same(Brushes.Purple, HeaderText(expander).Foreground);
            Assert.Same(Brushes.Purple, chevron.Fill);

            expander.HeaderForeground = null;
            Assert.NotSame(Brushes.Purple, HeaderText(expander).Foreground);
        }
        finally
        {
            DaisyAccessibility.HighContrastOverride = previous;
        }
    }

    [AvaloniaFact]
    public void A_Header_Click_And_The_Screen_Reader_Keep_A_Binding_On_IsExpanded()
    {
        var model = new ExpanderModel { IsOpen = true };
        var expander = new DaisyExpander { Header = "Section", Content = new TextBlock { Text = "Body" }, DataContext = model };
        expander.Bind(DaisyExpander.IsExpandedProperty, new Binding(nameof(ExpanderModel.IsOpen)) { Mode = BindingMode.OneWay });
        using var window = new TestWindow(expander);
        var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(
            ControlAutomationPeer.CreatePeerForElement(HeaderButton(expander)).GetProvider<IExpandCollapseProvider>());

        HeaderButton(expander).PointerClick();
        Assert.False(expander.IsExpanded);
        model.IsOpen = false;
        model.IsOpen = true;
        Assert.True(expander.IsExpanded);

        provider.Collapse();
        Assert.False(expander.IsExpanded);
        provider.Expand();
        Assert.True(expander.IsExpanded);
        model.IsOpen = false;
        Assert.False(expander.IsExpanded);
    }

    [AvaloniaFact]
    public void Screen_Readers_Hear_The_Header_And_Whether_The_Section_Is_Expanded()
    {
        var expander = new DaisyExpander { Header = "Section", Content = new TextBlock { Text = "Body" } };
        using var window = new TestWindow(expander);
        var peer = ControlAutomationPeer.CreatePeerForElement(HeaderButton(expander));
        var provider = Assert.IsAssignableFrom<IExpandCollapseProvider>(peer.GetProvider<IExpandCollapseProvider>());
        var changes = new List<(object? Old, object? New)>();
        peer.PropertyChanged += (_, e) =>
        {
            if (e.Property == ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty)
                changes.Add((e.OldValue, e.NewValue));
        };

        Assert.Equal("Section", peer.GetName());
        Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
        Assert.Equal(ExpandCollapseState.Expanded, provider.ExpandCollapseState);

        provider.Collapse();
        Assert.False(expander.IsExpanded);
        Assert.Equal(ExpandCollapseState.Collapsed, provider.ExpandCollapseState);
        expander.IsExpanded = true;
        Assert.Equal([(ExpandCollapseState.Expanded, ExpandCollapseState.Collapsed), (ExpandCollapseState.Collapsed, ExpandCollapseState.Expanded)],
            changes.Select(static change => ((ExpandCollapseState)change.Old!, (ExpandCollapseState)change.New!)));

        // A header control names the section with its own text.
        expander.Header = new TextBlock { Text = "Details" };
        Assert.Equal("Details", peer.GetName());
    }

    private static Button HeaderButton(DaisyExpander expander) => expander.GetVisualDescendants().OfType<Button>().First();

    private static TextBlock HeaderText(DaisyExpander expander) =>
        HeaderButton(expander).GetVisualDescendants().OfType<ContentPresenter>()
            .Single(static presenter => presenter.Content is string).GetVisualDescendants().OfType<TextBlock>().Single();

    private sealed class ExpanderModel : INotifyPropertyChanged
    {
        public bool IsOpen { get; set { field = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsOpen))); } }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
