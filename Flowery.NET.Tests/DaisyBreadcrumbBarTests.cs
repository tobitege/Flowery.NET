using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyBreadcrumbBarTests
{
    [AvaloniaFact]
    public void Items_Render_With_Last_Item_As_Current()
    {
        var bar = new DaisyBreadcrumbBar { ItemsSource = new[] { "Home", "Docs", "Report" } };
        var window = Show(bar, 600);
        try
        {
            var items = GetItems(bar);

            Assert.Equal(new object?[] { "Home", "Docs", "Report" }, items.Select(item => item.Content));
            Assert.Equal(new[] { false, false, true }, items.Select(item => item.IsCurrent));
            Assert.Equal(0, GetPanel(bar).FirstVisibleIndex);
            Assert.False(GetEllipsis(bar).Focusable);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Clicking_An_Item_Raises_ItemClicked()
    {
        var bar = new DaisyBreadcrumbBar { ItemsSource = new[] { "Home", "Docs", "Report" } };
        var clicks = new List<DaisyBreadcrumbBarItemClickedEventArgs>();
        bar.ItemClicked += (_, e) => clicks.Add(e);
        var window = Show(bar, 600);
        try
        {
            GetItems(bar)[1].RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            var click = Assert.Single(clicks);
            Assert.Equal(1, click.Index);
            Assert.Equal("Docs", click.Item);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Leading_Items_Collapse_Into_Ellipsis_When_Narrow()
    {
        var bar = new DaisyBreadcrumbBar
        {
            ItemsSource = new[] { "Home", "Projects", "Flowery", "Controls", "Navigation", "Breadcrumb Bar" }
        };
        var window = Show(bar, 220);
        try
        {
            var panel = GetPanel(bar);
            var ellipsis = GetEllipsis(bar);
            var items = GetItems(bar);

            Assert.True(panel.FirstVisibleIndex > 0);
            Assert.True(ellipsis.Focusable);
            Assert.Equal(AccessibilityView.Default, AutomationProperties.GetAccessibilityView(ellipsis));
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(ellipsis)));
            Assert.True(items[^1].Focusable);
            Assert.False(items[0].Focusable);
            Assert.Equal(AccessibilityView.Raw, AutomationProperties.GetAccessibilityView(items[0]));
            Assert.True(panel.Bounds.Width <= 220);

            window.Width = 1200;
            Layout(window);

            Assert.Equal(0, panel.FirstVisibleIndex);
            Assert.False(ellipsis.Focusable);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Collection_Changes_Rebuild_The_Items()
    {
        var path = new ObservableCollection<string> { "Home", "Docs" };
        var bar = new DaisyBreadcrumbBar { ItemsSource = path };
        var window = Show(bar, 600);
        try
        {
            path.Add("Report");
            Layout(window);

            var items = GetItems(bar);
            Assert.Equal(3, items.Count);
            Assert.True(items[2].IsCurrent);
            Assert.False(items[1].IsCurrent);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window Show(DaisyBreadcrumbBar bar, double width)
    {
        var window = new Window { Width = width, Height = 120, Content = bar };
        window.Show();
        Layout(window);
        return window;
    }

    private static DaisyBreadcrumbBarPanel GetPanel(DaisyBreadcrumbBar bar) =>
        bar.GetVisualDescendants().OfType<DaisyBreadcrumbBarPanel>().Single();

    private static DaisyBreadcrumbBarItem GetEllipsis(DaisyBreadcrumbBar bar) =>
        GetPanel(bar).Children.OfType<DaisyBreadcrumbBarItem>().Single(item => item.IsEllipsis);

    private static List<DaisyBreadcrumbBarItem> GetItems(DaisyBreadcrumbBar bar) =>
        GetPanel(bar).Children.OfType<DaisyBreadcrumbBarItem>().Where(item => !item.IsEllipsis).ToList();

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
