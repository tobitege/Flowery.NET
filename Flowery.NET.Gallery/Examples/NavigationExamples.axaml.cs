using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.NET.Gallery.Examples;

public partial class NavigationExamples : UserControl, IScrollableExample
{
    private Dictionary<string, Visual>? _sectionTargetsById;

    public NavigationExamples()
    {
        InitializeComponent();
    }

    public void ScrollToSection(string sectionName)
    {
        // Use a small delay to ensure the visual tree is fully realized
        // This is necessary because complex controls like DaisySteps take time to build
        DispatcherTimer.RunOnce(() => DoScrollToSection(sectionName), TimeSpan.FromMilliseconds(50));
    }

    private void DoScrollToSection(string sectionName)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (scrollViewer == null) return;

        var target = GetSectionTarget(sectionName);
        if (target == null) return;

        var transform = target.TransformToVisual(scrollViewer);
        if (transform.HasValue)
        {
            var point = transform.Value.Transform(new Point(0, 0));
            // Add current scroll offset to get absolute position in content
            scrollViewer.Offset = new Vector(0, point.Y + scrollViewer.Offset.Y);
        }
    }

    private Visual? GetSectionTarget(string sectionId)
    {
        if (_sectionTargetsById == null)
        {
            _sectionTargetsById = new Dictionary<string, Visual>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in this.GetVisualDescendants().OfType<SectionHeader>())
            {
                if (!string.IsNullOrWhiteSpace(header.SectionId))
                    _sectionTargetsById[header.SectionId] = header.Parent as Visual ?? header;
            }
        }

        return _sectionTargetsById.TryGetValue(sectionId, out var target) ? target : null;
    }

    private void OnDockItemSelected(object sender, DockItemSelectedEventArgs e)
    {
        if (e.Item is Button btn && btn.Tag is string tag)
        {
            ShowToast($"Dock item clicked: {tag}");
        }
    }

    private void ShowToast(string message)
    {
        var toast = this.FindControl<DaisyToast>("NavigationToast");
        if (toast != null)
        {
            var alert = new DaisyAlert
            {
                Content = message,
                Variant = DaisyAlertVariant.Info,
                Margin = new Thickness(0, 4)
            };

            toast.Items.Add(alert);

            // Auto remove after 3 seconds
            DispatcherTimer.RunOnce(() =>
            {
                toast.Items.Remove(alert);
            }, TimeSpan.FromSeconds(3));
        }
    }

    private void OnStepsPrevious(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var steps = this.FindControl<DaisySteps>("InteractiveSteps");
        if (steps != null && steps.SelectedIndex > 0)
        {
            steps.SelectedIndex--;
            UpdateStepColors(steps);
        }
    }

    private void OnStepsNext(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var steps = this.FindControl<DaisySteps>("InteractiveSteps");
        if (steps != null && steps.SelectedIndex < steps.ItemCount - 1)
        {
            steps.SelectedIndex++;
            UpdateStepColors(steps);
        }
    }

    private void OnStepsReset(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var steps = this.FindControl<DaisySteps>("InteractiveSteps");
        if (steps != null)
        {
            steps.SelectedIndex = 0;
            UpdateStepColors(steps);
        }
    }

    private void UpdateStepColors(DaisySteps steps)
    {
        for (int i = 0; i < steps.ItemCount; i++)
        {
            var container = steps.ContainerFromIndex(i);
            if (container is DaisyStepItem stepItem)
            {
                stepItem.Color = i <= steps.SelectedIndex ? DaisyStepColor.Primary : DaisyStepColor.Default;
            }
        }
    }

    #region Code Editor Tabs Example

    private void OnCodeEditorCloseTab(object? sender, DaisyTabEventArgs e)
    {
        var tabs = this.FindControl<DaisyTabs>("CodeEditorTabs");
        if (tabs == null) return;

        tabs.Items.Remove(e.TabItem);
        UpdateCodeEditorStatus();
        ShowToast($"Closed '{e.TabItem.Header}'");
    }

    private void OnCodeEditorCloseOtherTabs(object? sender, DaisyTabEventArgs e)
    {
        var tabs = this.FindControl<DaisyTabs>("CodeEditorTabs");
        if (tabs == null) return;

        var toRemove = tabs.Items.OfType<TabItem>()
            .Where(t => t != e.TabItem)
            .ToList();

        foreach (var tab in toRemove)
            tabs.Items.Remove(tab);

        tabs.SelectedItem = e.TabItem;
        UpdateCodeEditorStatus();
        ShowToast($"Closed {toRemove.Count} other tab(s)");
    }

    private void OnCodeEditorCloseTabsToRight(object? sender, DaisyTabEventArgs e)
    {
        var tabs = this.FindControl<DaisyTabs>("CodeEditorTabs");
        if (tabs == null) return;

        var allTabs = tabs.Items.OfType<TabItem>().ToList();
        var index = allTabs.IndexOf(e.TabItem);
        var toRemove = allTabs.Skip(index + 1).ToList();

        foreach (var tab in toRemove)
            tabs.Items.Remove(tab);

        UpdateCodeEditorStatus();
        if (toRemove.Count > 0)
            ShowToast($"Closed {toRemove.Count} tab(s) to the right");
    }

    private void OnCodeEditorTabPaletteColorChange(object? sender, DaisyTabPaletteColorChangedEventArgs e)
    {
        var colorName = e.NewColor == DaisyTabPaletteColor.Default ? "default" : e.NewColor.ToString();
        ShowToast($"Tab '{e.TabItem.Header}' color set to {colorName}");
    }

    private void UpdateCodeEditorStatus()
    {
        var tabs = this.FindControl<DaisyTabs>("CodeEditorTabs");
        var status = this.FindControl<TextBlock>("CodeEditorStatus");
        if (tabs != null && status != null)
        {
            var count = tabs.Items.Count;
            status.Text = $"Ready - {count} file{(count != 1 ? "s" : "")} open";
        }
    }

    #endregion
}
