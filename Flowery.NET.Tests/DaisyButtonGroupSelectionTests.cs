using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>
/// DaisyButtonGroup as a segmented control: selection, binding, keyboard, screen readers and the sliding pill.
/// </summary>
public class DaisyButtonGroupSelectionTests
{
    [AvaloniaFact]
    public void A_Click_Keeps_A_Binding_On_The_Selected_Index()
    {
        var model = new SelectionModel { Index = 0 };
        var group = Group(out var buttons);
        group.DataContext = model;
        group.Bind(DaisyButtonGroup.SelectedIndexProperty, new Binding(nameof(SelectionModel.Index)));
        var window = Show(group);
        try
        {
            Click(buttons[2]);
            Assert.Equal(2, model.Index);

            model.Index = 1;
            Assert.Equal(1, group.SelectedIndex);
            Assert.Contains("button-group-active", buttons[1].Classes);
            Assert.DoesNotContain("button-group-active", buttons[2].Classes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Selection_Change_Reports_The_Added_And_Removed_Indices()
    {
        var group = Group(out var buttons);
        var changes = new List<(int[] Removed, int[] Added)>();
        var clicks = 0;
        group.SelectionChanged += (_, e) => changes.Add(([.. e.RemovedIndices], [.. e.AddedIndices]));
        group.ItemSelected += (_, _) => clicks++;
        var window = Show(group);
        try
        {
            Click(buttons[1]);
            group.SelectedIndex = 2;
            // A click on the selected segment is still a click, but no selection change.
            Click(buttons[2]);

            Assert.Equal(2, clicks);
            Assert.Equal(2, changes.Count);
            Assert.Empty(changes[0].Removed);
            Assert.Equal([1], changes[0].Added);
            Assert.Equal([1], changes[1].Removed);
            Assert.Equal([2], changes[1].Added);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Bound_Collection_Carries_The_Multiple_Selection()
    {
        var group = Group(out var buttons);
        group.SelectionMode = DaisyButtonGroupSelectionMode.Multiple;
        var changes = new List<(int[] Removed, int[] Added)>();
        group.SelectionChanged += (_, e) => changes.Add(([.. e.RemovedIndices], [.. e.AddedIndices]));
        var model = new SelectionModel { Indices = [1] };
        group.DataContext = model;
        group.Bind(DaisyButtonGroup.SelectedIndicesProperty, new Binding(nameof(SelectionModel.Indices)));
        var window = Show(group);
        try
        {
            Assert.Same(model.Indices, group.SelectedIndices);
            Assert.Contains("button-group-active", buttons[1].Classes);

            Click(buttons[0]);
            Assert.Equal(new[] { 1, 0 }, model.Indices);
            Assert.Contains("button-group-active", buttons[0].Classes);

            model.Indices = [2];
            Assert.Contains("button-group-active", buttons[2].Classes);
            Assert.DoesNotContain("button-group-active", buttons[0].Classes);
            Assert.Equal([0, 1], changes[^1].Removed);
            Assert.Equal([2], changes[^1].Added);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Switching_The_Mode_Keeps_A_Bound_Multiple_Selection()
    {
        var group = Group(out var buttons);
        group.SelectionMode = DaisyButtonGroupSelectionMode.Multiple;
        var model = new SelectionModel { Indices = [0, 2] };
        group.DataContext = model;
        group.Bind(DaisyButtonGroup.SelectedIndicesProperty, new Binding(nameof(SelectionModel.Indices)));
        group.SelectedIndex = 1;
        var window = Show(group);
        try
        {
            group.SelectionMode = DaisyButtonGroupSelectionMode.Single;
            Assert.Equal(new[] { 0, 2 }, model.Indices);
            Assert.Contains("button-group-active", buttons[1].Classes);
            Assert.DoesNotContain("button-group-active", buttons[0].Classes);

            group.SelectionMode = DaisyButtonGroupSelectionMode.Multiple;
            Assert.Contains("button-group-active", buttons[0].Classes);
            Assert.Contains("button-group-active", buttons[2].Classes);
            Assert.DoesNotContain("button-group-active", buttons[1].Classes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Arrow_Keys_Move_The_Single_Selection()
    {
        var group = Group(out var buttons);
        group.SelectedIndex = 0;
        var window = Show(group);
        try
        {
            Assert.Equal(KeyboardNavigationMode.Once, KeyboardNavigation.GetTabNavigation(group));
            Assert.True(buttons[0].Focus());
            Press(window, Key.Right);
            Assert.Equal(1, group.SelectedIndex);
            Assert.Equal(1, group.SelectedIndex);
            Assert.True(buttons[1].IsFocused);

            Press(window, Key.End);
            Assert.Equal(2, group.SelectedIndex);
            Press(window, Key.Right);
            Assert.Equal(2, group.SelectedIndex);
            Press(window, Key.Home);
            Assert.Equal(0, group.SelectedIndex);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void In_Multiple_Selection_The_Arrow_Keys_Move_The_Focus_And_Space_Toggles()
    {
        var group = Group(out var buttons);
        group.SelectionMode = DaisyButtonGroupSelectionMode.Multiple;
        var window = Show(group);
        try
        {
            buttons[0].Focus();
            Press(window, Key.Right);
            Assert.True(buttons[1].IsFocused);
            Assert.Empty(group.SelectedIndices);

            Press(window, Key.Space);
            Assert.Equal(new[] { 1 }, group.SelectedIndices);
            Press(window, Key.Space);
            Assert.Empty(group.SelectedIndices);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Hear_The_Selected_Segment_Or_Each_Toggle()
    {
        var group = Group(out var buttons);
        group.SelectedIndex = 1;
        var window = Show(group);
        try
        {
            var groupPeer = ControlAutomationPeer.CreatePeerForElement(group);
            var selection = Assert.IsAssignableFrom<ISelectionProvider>(groupPeer.GetProvider<ISelectionProvider>());
            Assert.False(selection.CanSelectMultiple);
            Assert.Same(ControlAutomationPeer.CreatePeerForElement(buttons[1]), Assert.Single(selection.GetSelection()));

            var peer = ControlAutomationPeer.CreatePeerForElement(buttons[0]);
            var item = Assert.IsAssignableFrom<ISelectionItemProvider>(peer.GetProvider<ISelectionItemProvider>());
            Assert.Null(peer.GetProvider<IToggleProvider>());
            Assert.False(item.IsSelected);
            item.Select();
            Assert.Equal(0, group.SelectedIndex);

            // In multiple selection each segment is a toggle of its own, and the group a plain group.
            group.SelectionMode = DaisyButtonGroupSelectionMode.Multiple;
            Assert.Null(groupPeer.GetProvider<ISelectionProvider>());
            Assert.Null(peer.GetProvider<ISelectionItemProvider>());
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer.GetProvider<IToggleProvider>());
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
            toggle.Toggle();
            Assert.Equal(new[] { 0 }, group.SelectedIndices);
            Assert.Equal(ToggleState.On, toggle.ToggleState);

            // Without AutoSelect the segments are plain buttons.
            group.AutoSelect = false;
            Assert.Null(peer.GetProvider<IToggleProvider>());
            Assert.Null(groupPeer.GetProvider<ISelectionProvider>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Glass_Pill_Slides_Under_The_Selected_Segment_And_Gives_Way_In_Multiple_Selection()
    {
        var group = Group(out var buttons);
        group.Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassButtonGroupTheme");
        group.Width = 300;
        group.SelectedIndex = 0;
        var window = Show(group);
        try
        {
            var glass = group.GetVisualDescendants().OfType<DaisyGlass>().Single(part => part.Name == "PART_Glass");
            Assert.Equal(24, glass.GlassBlur);
            var pill = group.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "PART_Indicator");
            Assert.Equal(0, group.SelectedIndex);
            AssertPillUnder(pill, buttons[0]);

            Click(buttons[2]);
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            AssertPillUnder(pill, buttons[2]);

            group.SelectionMode = DaisyButtonGroupSelectionMode.Multiple;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.False(pill.IsVisible);
        }
        finally
        {
            window.Close();
        }
    }

    private static void AssertPillUnder(Border pill, Button segment)
    {
        var origin = segment.TranslatePoint(default, Assert.IsAssignableFrom<Visual>(pill.GetVisualParent()))!.Value;
        var transform = Assert.IsType<TransformOperations>(pill.GetBaseValue(Visual.RenderTransformProperty).GetValueOrDefault());
        Assert.True(pill.IsVisible);
        Assert.Equal(segment.Bounds.Width, pill.Width);
        var translate = transform.Operations.Single(operation => operation.Type == TransformOperation.OperationType.Translate);
        Assert.Equal(origin.X, translate.Data.Translate.X, 3);
        Assert.Equal(origin.Y, translate.Data.Translate.Y, 3);
    }

    private static DaisyButtonGroup Group(out DaisyButton[] buttons)
    {
        buttons = [new DaisyButton { Content = "Day" }, new DaisyButton { Content = "Week" }, new DaisyButton { Content = "Month" }];
        var group = new DaisyButtonGroup { AutoSelect = true };
        foreach (var button in buttons)
            group.Items.Add(button);
        return group;
    }

    private static void Click(Button button) => button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent, button));

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: key == Key.Space ? " " : null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 400, Height = 300, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private sealed class SelectionModel : INotifyPropertyChanged
    {
        private int _index = -1;
        private ObservableCollection<int> _indices = [];

        public int Index
        {
            get => _index;
            set => Set(ref _index, value);
        }

        public ObservableCollection<int> Indices
        {
            get => _indices;
            set => Set(ref _indices, value);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private void Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }
    }
}
