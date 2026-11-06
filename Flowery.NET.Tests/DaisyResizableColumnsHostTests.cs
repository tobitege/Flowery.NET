using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyResizableColumnsHostTests
{
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

    private static void PressOn(DaisyResizableColumnsHost host, PhysicalKey key)
    {
        Assert.True(host.Focus(), "The host cannot take the keyboard focus.");
        host.Press(key);
    }
}
