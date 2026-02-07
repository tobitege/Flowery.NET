using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.NET.Gallery.Examples;

public partial class NavigationExamples : UserControl, IScrollableExample
{
    private Dictionary<string, Visual>? _sectionTargetsById;
    private int _photoTabSelectedIndex;
    private bool _photoTabPointerOver;
    private readonly bool _photoTabOverlayAlwaysVisible =
        OperatingSystem.IsBrowser() || OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

    public NavigationExamples()
    {
        InitializeComponent();
        InitializePhotoTab();
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

    private void OnPaginationPageChanged(object? sender, int page)
    {
        _ = sender;

        var statusText = this.FindControl<TextBlock>("PaginationStatusText");
        if (statusText != null)
            statusText.Text = $"Current page: {page}";
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

    #region PhotoTab Recipe (Gallery-only)

    private void InitializePhotoTab()
    {
        SetPhotoTabSelectedIndex(0);
        UpdatePhotoTabBarActiveState();
        UpdatePhotoTabOverlayVisibility();
    }

    private void UpdatePhotoTabOverlayVisibility()
    {
        var bar = this.FindControl<Border>("PhotoTabBar");
        if (bar == null) return;

        if (_photoTabOverlayAlwaysVisible)
        {
            bar.Opacity = 1;
            bar.IsHitTestVisible = true;
            if (bar.RenderTransform is TranslateTransform tt)
                tt.Y = 0;
        }
        else
        {
            bar.Opacity = 0;
            bar.IsHitTestVisible = false;
            if (bar.RenderTransform is TranslateTransform tt)
                tt.Y = 20;
        }
    }

    private void ShowPhotoTabOverlay()
    {
        if (_photoTabOverlayAlwaysVisible) return;

        var bar = this.FindControl<Border>("PhotoTabBar");
        if (bar == null) return;

        bar.IsHitTestVisible = true;
        bar.Opacity = 1;
        if (bar.RenderTransform is TranslateTransform tt)
            tt.Y = 0;
    }

    private void HidePhotoTabOverlay()
    {
        if (_photoTabOverlayAlwaysVisible) return;

        var bar = this.FindControl<Border>("PhotoTabBar");
        if (bar == null) return;

        bar.Opacity = 0;
        bar.IsHitTestVisible = false;
        if (bar.RenderTransform is TranslateTransform tt)
            tt.Y = 20;

        var indicator = this.FindControl<Border>("PhotoTabHoverIndicator");
        if (indicator != null)
            indicator.Opacity = 0;
    }

    private bool IsPhotoTabFocusWithin()
    {
        var card = this.FindControl<Border>("PhotoTabCard");
        if (card == null) return false;

        var topLevel = TopLevel.GetTopLevel(this);
        var focused = topLevel?.FocusManager?.GetFocusedElement();
        if (focused is not Visual focusedVisual) return false;

        // Walk parent chain to check if focusedVisual is inside card
        Visual? current = focusedVisual;
        while (current != null)
        {
            if (ReferenceEquals(current, card)) return true;
            current = current.GetVisualParent() as Visual;
        }
        return false;
    }

    private void SetPhotoTabSelectedIndex(int index)
    {
        _photoTabSelectedIndex = Math.Clamp(index, 0, 2);

        var img0 = this.FindControl<Image>("PhotoTabImage0");
        var img1 = this.FindControl<Image>("PhotoTabImage1");
        var img2 = this.FindControl<Image>("PhotoTabImage2");

        if (img0 != null) img0.Opacity = _photoTabSelectedIndex == 0 ? 1 : 0;
        if (img1 != null) img1.Opacity = _photoTabSelectedIndex == 1 ? 1 : 0;
        if (img2 != null) img2.Opacity = _photoTabSelectedIndex == 2 ? 1 : 0;
    }

    private void UpdatePhotoTabBarActiveState()
    {
        var btn0 = this.FindControl<DaisyButton>("PhotoTabAction0");
        var btn1 = this.FindControl<DaisyButton>("PhotoTabAction1");
        var btn2 = this.FindControl<DaisyButton>("PhotoTabAction2");

        IBrush base100Brush = Brushes.Transparent;
        IBrush baseContentBrush = Brushes.Black;
        IBrush primaryContentBrush = baseContentBrush;

        if (this.TryFindResource("DaisyBase100Brush", out var base100) && base100 is IBrush base100Resource)
            base100Brush = base100Resource;

        if (this.TryFindResource("DaisyBaseContentBrush", out var baseContent) && baseContent is IBrush baseContentResource)
            baseContentBrush = baseContentResource;

        if (this.TryFindResource("DaisyPrimaryContentBrush", out var primaryContent) && primaryContent is IBrush primaryContentResource)
            primaryContentBrush = primaryContentResource;

        ApplyPhotoTabButtonVisual(btn0, _photoTabSelectedIndex == 0, base100Brush, baseContentBrush, primaryContentBrush);
        ApplyPhotoTabButtonVisual(btn1, _photoTabSelectedIndex == 1, base100Brush, baseContentBrush, primaryContentBrush);
        ApplyPhotoTabButtonVisual(btn2, _photoTabSelectedIndex == 2, base100Brush, baseContentBrush, primaryContentBrush);
    }

    private static void ApplyPhotoTabButtonVisual(
        DaisyButton? button,
        bool isSelected,
        IBrush selectedBackground,
        IBrush selectedForeground,
        IBrush unselectedForeground)
    {
        if (button == null) return;

        button.Background = isSelected ? selectedBackground : Brushes.Transparent;
        button.Foreground = isSelected ? selectedForeground : unselectedForeground;
    }

    private void OnPhotoTabActionClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.Tag is not string tagText || !int.TryParse(tagText, out var index)) return;

        SetPhotoTabSelectedIndex(index);
        UpdatePhotoTabBarActiveState();

        var actionName = index switch
        {
            0 => "Profile",
            1 => "Pets",
            2 => "Explore",
            _ => "Action"
        };

        ShowToast($"PhotoTab action: {actionName}");
    }

    private void OnPhotoTabActionPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is not Control control) return;
        if (control.Tag is not string tagText || !int.TryParse(tagText, out var index)) return;

        var indicator = this.FindControl<Border>("PhotoTabHoverIndicator");
        if (indicator == null) return;

        const double itemSize = 32;
        const double spacing = 8;
        var x = index * (itemSize + spacing);

        if (indicator.RenderTransform is TranslateTransform tt)
            tt.X = x;

        indicator.Opacity = 1;
    }

    private void OnPhotoTabActionPointerExited(object? sender, PointerEventArgs e)
    {
        var indicator = this.FindControl<Border>("PhotoTabHoverIndicator");
        if (indicator != null)
            indicator.Opacity = 0;
    }

    private void OnPhotoTabCardPointerEntered(object? sender, PointerEventArgs e)
    {
        _photoTabPointerOver = true;
        ShowPhotoTabOverlay();
    }

    private void OnPhotoTabCardPointerExited(object? sender, PointerEventArgs e)
    {
        _photoTabPointerOver = false;

        if (_photoTabOverlayAlwaysVisible)
            return;

        if (!IsPhotoTabFocusWithin())
            HidePhotoTabOverlay();
    }

    private void OnPhotoTabActionGotFocus(object? sender, GotFocusEventArgs e)
    {
        ShowPhotoTabOverlay();
    }

    private void OnPhotoTabActionLostFocus(object? sender, RoutedEventArgs e)
    {
        if (_photoTabOverlayAlwaysVisible)
            return;

        // Defer to allow focus to move between PhotoTab buttons without flicker.
        DispatcherTimer.RunOnce(() =>
        {
            if (!_photoTabPointerOver && !IsPhotoTabFocusWithin())
                HidePhotoTabOverlay();
        }, TimeSpan.FromMilliseconds(1));
    }

    #endregion
}
