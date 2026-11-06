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
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;
using static Flowery.NET.Tests.RealInput;
using static Flowery.NET.Tests.TestControls;

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
            buttons[2].PointerClick();
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
            buttons[1].PointerClick();
            group.SelectedIndex = 2;
            // A click on the selected segment is still a click, but no selection change.
            buttons[2].PointerClick();

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

            buttons[0].PointerClick();
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
    public void A_Bound_Collection_Of_A_Long_Lived_Model_Does_Not_Keep_A_Closed_Group_Alive()
    {
        var model = new SelectionModel { Indices = [1] };
        var group = ShowAndCloseBoundGroup(model);
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        Assert.False(group.TryGetTarget(out _), "The collection of the model keeps the group alive.");
        GC.KeepAlive(model);
    }

    [AvaloniaFact]
    public void A_Group_Attached_Again_Shows_And_Follows_The_Bound_Collection()
    {
        var group = Group(out var buttons);
        group.SelectionMode = DaisyButtonGroupSelectionMode.Multiple;
        var model = new SelectionModel { Indices = [1] };
        group.DataContext = model;
        group.Bind(DaisyButtonGroup.SelectedIndicesProperty, new Binding(nameof(SelectionModel.Indices)));
        var window = Show(group);
        try
        {
            window.Content = null;
            Dispatcher.UIThread.RunJobs();
            var changes = new List<int[]>();
            group.SelectionChanged += (_, e) => changes.Add([.. e.AddedIndices]);
            model.Indices.Add(2);

            window.Content = group;
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
            Assert.Contains("button-group-active", buttons[2].Classes);
            Assert.Equal([2], Assert.Single(changes));
            model.Indices.Remove(1);
            Assert.DoesNotContain("button-group-active", buttons[1].Classes);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Screen_Readers_Hear_The_Selection_Of_Plain_Button_Segments()
    {
        var buttons = new[] { new Button { Content = "Day" }, new Button { Content = "Week" } };
        var group = new DaisyButtonGroup { AutoSelect = true, SelectedIndex = 1 };
        foreach (var button in buttons)
            group.Items.Add(button);
        var window = Show(group);
        try
        {
            var selection = Assert.IsAssignableFrom<ISelectionProvider>(
                ControlAutomationPeer.CreatePeerForElement(group).GetProvider<ISelectionProvider>());
            Assert.Same(ControlAutomationPeer.CreatePeerForElement(buttons[1]), Assert.Single(selection.GetSelection()));
        }
        finally
        {
            window.Close();
        }
    }

    // Not inlined, so no local of the test keeps the group.
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference<DaisyButtonGroup> ShowAndCloseBoundGroup(SelectionModel model)
    {
        var group = Group(out _);
        group.SelectionMode = DaisyButtonGroupSelectionMode.Multiple;
        group.DataContext = model;
        group.Bind(DaisyButtonGroup.SelectedIndicesProperty, new Binding(nameof(SelectionModel.Indices)));
        var window = Show(group);
        Assert.Same(model.Indices, group.SelectedIndices);
        window.Content = null;
        Dispatcher.UIThread.RunJobs();
        window.Close();
        Dispatcher.UIThread.RunJobs();
        // Release the closed window's pending composition batch before the GC assertion.
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        return new WeakReference<DaisyButtonGroup>(group);
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
            Assert.Equal(AutomationControlType.Group, groupPeer.GetAutomationControlType());
            Assert.Equal(AutomationControlType.RadioButton, peer.GetAutomationControlType());
            var item = Assert.IsAssignableFrom<ISelectionItemProvider>(peer.GetProvider<ISelectionItemProvider>());
            Assert.Null(peer.GetProvider<IToggleProvider>());
            Assert.False(item.IsSelected);
            item.Select();
            Assert.Equal(0, group.SelectedIndex);

            // In multiple selection each segment is a toggle of its own, and the group a plain group.
            group.SelectionMode = DaisyButtonGroupSelectionMode.Multiple;
            Assert.Null(groupPeer.GetProvider<ISelectionProvider>());
            Assert.Null(peer.GetProvider<ISelectionItemProvider>());
            Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
            var toggle = Assert.IsAssignableFrom<IToggleProvider>(peer.GetProvider<IToggleProvider>());
            Assert.Equal(ToggleState.Off, toggle.ToggleState);
            toggle.Toggle();
            Assert.Equal(new[] { 0 }, group.SelectedIndices);
            Assert.Equal(ToggleState.On, toggle.ToggleState);

            // Without AutoSelect the segments are plain buttons.
            group.AutoSelect = false;
            Assert.Null(peer.GetProvider<IToggleProvider>());
            Assert.Null(groupPeer.GetProvider<ISelectionProvider>());
            Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());

            // A button outside a group is a plain button.
            var lone = ControlAutomationPeer.CreatePeerForElement(new DaisyButton { Content = "Alone" });
            Assert.Equal(AutomationControlType.Button, lone.GetAutomationControlType());
            Assert.Null(lone.GetProvider<ISelectionItemProvider>());
            Assert.Null(lone.GetProvider<IToggleProvider>());
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Glass_Track_Is_As_High_As_Its_Segments_And_Its_Padding()
    {
        // The top shine of the glass is 60 high; it must not make a lower track taller, which left the segments with
        // more room above and below than at the ends.
        var group = Group(out var buttons);
        group.Theme = DaisyGlassSurfaceTests.GlassTheme("DaisyGlassButtonGroupTheme");
        group.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        var window = Show(group);
        try
        {
            var inset = group.Padding.Top + group.BorderThickness.Top;
            Assert.Equal(buttons[0].Bounds.Height + 2 * inset, group.Bounds.Height);
            var track = group.GetVisualDescendants().OfType<Border>().Single(part => part.Name == "PART_Track");
            Assert.Equal(inset, buttons[0].TranslatePoint(default, track)!.Value.Y);
            Assert.Equal(inset, buttons[0].TranslatePoint(default, track)!.Value.X);
        }
        finally
        {
            window.Close();
        }
    }

    private static DaisyButtonGroup Group(out DaisyButton[] buttons)
    {
        buttons = [new DaisyButton { Content = "Day" }, new DaisyButton { Content = "Week" }, new DaisyButton { Content = "Month" }];
        var group = new DaisyButtonGroup { AutoSelect = true };
        foreach (var button in buttons)
            group.Items.Add(button);
        return group;
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
