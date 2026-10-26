using System.Collections.Generic;
using System.Linq;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyResizableColumnsHostTests
{
    [AvaloniaFact]
    public void ColumnWidth_Stays_Within_Min_And_Max()
    {
        var host = new DaisyResizableColumnsHost { MinColumnWidth = 120, MaxColumnWidth = 400 };

        host.ColumnWidth = 50;
        Assert.Equal(120, host.ColumnWidth);

        host.ColumnWidth = 900;
        Assert.Equal(400, host.ColumnWidth);

        host.MaxColumnWidth = 300;
        Assert.Equal(300, host.ColumnWidth);
    }

    [AvaloniaFact]
    public void Keyboard_Resizes_The_Columns()
    {
        var host = new DaisyResizableColumnsHost { MinColumnWidth = 100, MaxColumnWidth = 500, ColumnWidth = 200 };
        var widths = new List<double>();
        host.ColumnWidthChanged += (_, width) => widths.Add(width);
        using var window = new TestWindow(host);

        PressOn(host, PhysicalKey.ArrowRight);
        Assert.Equal(208, host.ColumnWidth);

        PressOn(host, PhysicalKey.ArrowLeft);
        PressOn(host, PhysicalKey.ArrowLeft);
        Assert.Equal(192, host.ColumnWidth);

        PressOn(host, PhysicalKey.PageUp);
        Assert.Equal(224, host.ColumnWidth);

        PressOn(host, PhysicalKey.End);
        Assert.Equal(500, host.ColumnWidth);

        PressOn(host, PhysicalKey.Home);
        Assert.Equal(100, host.ColumnWidth);

        Assert.Equal(new[] { 208.0, 200.0, 192.0, 224.0, 500.0, 100.0 }, widths);
    }

    [AvaloniaFact]
    public void Disabled_Grip_Ignores_The_Keyboard()
    {
        var host = new DaisyResizableColumnsHost { ColumnWidth = 200 };
        using var window = new TestWindow(host);
        Assert.True(host.Focus());

        host.IsGripEnabled = false;
        Assert.False(host.Focusable);

        // A key still sent to the host must not resize it.
        host.Press(PhysicalKey.ArrowRight);
        Assert.Equal(200, host.ColumnWidth);
    }

    [AvaloniaFact]
    public void Items_Panel_Follows_Orientation_And_Spacing()
    {
        var host = new DaisyResizableColumnsHost
        {
            ColumnSpacing = 16,
            Items = { new Border { Width = 100 }, new Border { Width = 100 } }
        };
        var window = new Window { Width = 600, Height = 300, Content = host };
        try
        {
            window.Show();
            Layout(window);

            var panel = host.GetVisualDescendants().OfType<StackPanel>().First();
            Assert.Equal(Orientation.Horizontal, panel.Orientation);
            Assert.Equal(16, panel.Spacing);

            host.Orientation = Orientation.Vertical;
            host.ColumnSpacing = 8;
            Layout(window);

            Assert.Equal(Orientation.Vertical, panel.Orientation);
            Assert.Equal(8, panel.Spacing);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void AccessibleText_Sets_The_Automation_Name()
    {
        var host = new DaisyResizableColumnsHost();
        Assert.Equal("Resizable columns", AutomationProperties.GetName(host));

        host.AccessibleText = "Kanban columns";
        Assert.Equal("Kanban columns", AutomationProperties.GetName(host));
    }

    private static void PressOn(DaisyResizableColumnsHost host, PhysicalKey key)
    {
        Assert.True(host.Focus(), "The host cannot take the keyboard focus.");
        host.Press(key);
    }

    private static void Layout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
    }
}
