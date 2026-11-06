using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.HeadlessTestingFramework;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

public sealed partial class SchedulerTests
{
    [AvaloniaTheory]
    [InlineData(SchedulerView.Day)]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.Month)]
    [InlineData(SchedulerView.Planner)]
    [InlineData(SchedulerView.Timeline)]
    public void ColumnZoomWidensTheColumnsWithoutBuildingTheViewAgain(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        var body = BodyScroll(scope);
        var fitted = body.Extent.Width;
        var frame = scope.Frame(scope.Appointment);

        scope.Scheduler.ColumnZoom = 2;
        scope.Layout();

        // Layout rounds widths to device pixels.
        Assert.Equal(fitted * 2, body.Extent.Width, tolerance: 2);
        Assert.Same(frame, scope.Frame(scope.Appointment));
    }

    [AvaloniaFact]
    public void ColumnZoomStaysBetweenOneAndFour()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        scope.Scheduler.ColumnZoom = 10;
        Assert.Equal(4, scope.Scheduler.ColumnZoom);
        scope.Scheduler.ColumnZoom = 0.5;
        Assert.Equal(1, scope.Scheduler.ColumnZoom);
    }

    [AvaloniaFact]
    public void CtrlAndTheWheelZoomAndKeepTheDayUnderThePointer()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var body = BodyScroll(scope);
        var x = body.Viewport.Width * 3 / 4;
        var point = scope.Point(body, x, body.Viewport.Height / 2);

        scope.Window.MouseWheel(point, new Vector(0, 1));
        scope.Layout();
        Assert.Equal(1, scope.Scheduler.ColumnZoom);

        scope.Window.MouseWheel(point, new Vector(0, 1), RawInputModifiers.Control);
        scope.Layout();
        Assert.Equal(1.25, scope.Scheduler.ColumnZoom, 3);
        // What was at x is still at x.
        Assert.Equal(x * 0.25, body.Offset.X, tolerance: 2);
    }

    [AvaloniaFact]
    public void ADoubleTapOnAColumnHeadingSwitchesBetweenFittedAndWideColumnsAndSwipesDoNot()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var heading = Heading(scope);
        var middle = heading.Bounds.Center - heading.Bounds.Position;

        // A week does not scroll sideways, so the swipes start where the taps come.
        TouchDrag(scope, heading, middle, new Vector(1, 0), pixels: 40);
        TouchDrag(scope, heading, middle, new Vector(1, 0), pixels: 40);
        Assert.Equal(1, scope.Scheduler.ColumnZoom);
        TouchTap(scope, Heading(scope), middle);
        Assert.Equal(1, scope.Scheduler.ColumnZoom);
        TouchTap(scope, Heading(scope), middle);
        Assert.Equal(2, scope.Scheduler.ColumnZoom);
    }

    [AvaloniaFact]
    public void ADoubleClickOnAColumnHeadingSwitchesBetweenFittedAndWideColumns()
    {
        using var scope = new SchedulerScope(SchedulerView.Month);
        var heading = scope.Scheduler.GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Text == Monday.ToString("ddd", scope.Scheduler.FormattingCulture)).FindAncestorOfType<Border>()!;

        scope.Click(heading);
        Assert.Equal(1, scope.Scheduler.ColumnZoom);
        scope.Click(heading);
        Assert.Equal(2, scope.Scheduler.ColumnZoom);
    }

    [AvaloniaTheory]
    [InlineData(SchedulerView.Week)]
    [InlineData(SchedulerView.Month)]
    [InlineData(SchedulerView.Planner)]
    public void DraggingTheRightEdgeOfAColumnHeadingWithTheMouseResizesEveryColumn(SchedulerView view)
    {
        using var scope = new SchedulerScope(view);
        var body = BodyScroll(scope);
        var format = view == SchedulerView.Month ? "ddd" : "ddd, d MMM";
        var cell = scope.Scheduler.GetVisualDescendants().OfType<TextBlock>()
            .First(text => text.Text == Monday.ToString(format, scope.Scheduler.FormattingCulture)).FindAncestorOfType<Border>()!;
        // A Planner day is wider than the view: scroll its right edge into the middle. The heading follows the body.
        body.Offset = new Vector(Math.Max(0, cell.TranslatePoint(default, body)!.Value.X + body.Offset.X + cell.Bounds.Width - body.Viewport.Width / 2), 0);
        scope.Layout();
        var width = cell.Bounds.Width;
        var left = cell.TranslatePoint(default, body)!.Value.X;
        var drag = Math.Min(width / 2, 300);

        // A drag from inside the heading changes nothing.
        var inside = scope.Point(cell, width / 2, cell.Bounds.Height / 2);
        scope.Window.MouseDown(inside, MouseButton.Left);
        scope.Window.MouseMove(inside + new Vector(drag, 0));
        scope.Window.MouseUp(inside + new Vector(drag, 0), MouseButton.Left);
        scope.Layout();
        Assert.Equal(1, scope.Scheduler.ColumnZoom);

        var edge = scope.Point(cell, width - 2, cell.Bounds.Height / 2);
        scope.Window.MouseMove(edge);
        Assert.NotNull(cell.Cursor);
        scope.Window.MouseDown(edge, MouseButton.Left);
        scope.Window.MouseMove(edge + new Vector(drag, 0));
        scope.Window.MouseUp(edge + new Vector(drag, 0), MouseButton.Left);
        scope.Layout();

        Assert.Equal((width + drag) / width, scope.Scheduler.ColumnZoom, 2);
        Assert.Equal(width + drag, cell.Bounds.Width, tolerance: 2);
        // The left edge of the column stays where it was.
        Assert.Equal(left, cell.TranslatePoint(default, body)!.Value.X, tolerance: 2);
    }

    [AvaloniaFact]
    public void TwoFingersOnTheBodyZoomThroughThePinchRecognizer()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var body = BodyScroll(scope);
        var middle = new Point(body.Viewport.Width / 2, body.Viewport.Height / 2);

        MultiTouchTestHelperFactory.SimulatePinchZoomIn(body, middle, startDistance: 60, endDistance: 120);
        Assert.Equal(2, scope.Scheduler.ColumnZoom, 2);

        // Lifting both fingers ends the pinch, so the next one scales the zoom it starts at.
        MultiTouchTestHelperFactory.SimulatePinchZoomOut(body, middle, startDistance: 120, endDistance: 90);
        Assert.Equal(1.5, scope.Scheduler.ColumnZoom, 2);
        Assert.Null(scope.Scheduler.SelectedAppointment);
    }

    [AvaloniaFact]
    public void TheSettingsMenuChoosesTheColumnZoom()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var settings = Assert.IsType<ScrollViewer>(scope.Scheduler.BuildSettings());
        var popup = new Window { Width = 500, Height = 600, Content = settings };
        popup.Show();
        try
        {
            popup.UpdateLayout();
            var zoom = settings.GetVisualDescendants().OfType<DaisySelect>().Single(select => Equals(select.Tag, DaisyScheduler.ColumnZoomProperty));
            Assert.Equal(1d.ToString("P0", scope.Scheduler.FormattingCulture), zoom.SelectedItem?.ToString());
            zoom.SelectedIndex = 2;
            Assert.Equal(2, scope.Scheduler.ColumnZoom);
        }
        finally
        {
            popup.Close();
        }
    }

    [AvaloniaFact]
    public void WithAStateKeyTheZoomOfTheUserIsKeptForTheNextScheduler()
    {
        var previous = StateStorageProvider.Instance;
        var storage = new InMemoryStateStorage();
        StateStorageProvider.Configure(storage);
        try
        {
            using (var plain = new SchedulerScope(SchedulerView.Week))
            {
                plain.Scheduler.ColumnZoom = 3;
                plain.Layout();
            }
            Assert.Equal(0, storage.SaveCount);

            using (var first = new SchedulerScope(SchedulerView.Week))
            {
                first.Scheduler.StateKey = "zoom";
                first.Scheduler.ColumnZoom = 2;
                first.Scheduler.ColumnZoom = 2.5;
                first.Layout();
            }
            // The changes are written once, at the latest when the scheduler closes.
            Assert.Equal(1, storage.SaveCount);

            using var second = new SchedulerScope(SchedulerView.Week);
            Assert.Equal(1, second.Scheduler.ColumnZoom);
            second.Scheduler.StateKey = "zoom";
            Assert.Equal(2.5, second.Scheduler.ColumnZoom);
        }
        finally
        {
            StateStorageProvider.Configure(previous);
        }
    }

    [AvaloniaFact]
    public void AZoomGestureOverManyDispatcherTurnsIsWrittenOnceItRests()
    {
        var previous = StateStorageProvider.Instance;
        var storage = new InMemoryStateStorage();
        StateStorageProvider.Configure(storage);
        try
        {
            using var scope = new SchedulerScope(SchedulerView.Week);
            scope.Scheduler.StateKey = "zoom";
            // A pinch: one zoom per pointer event, each in a dispatcher turn of its own.
            foreach (var zoom in new[] { 1.2, 1.5, 1.9, 2.4 })
            {
                scope.Scheduler.ColumnZoom = zoom;
                scope.Layout();
            }
            Assert.Equal(0, storage.SaveCount);

            Thread.Sleep(DaisyScheduler.StateSaveDelay + TimeSpan.FromMilliseconds(150));
            // The dispatcher looks for due timers only after it ran a job.
            Dispatcher.UIThread.Post(static () => { });
            scope.Layout();
            Assert.Equal(1, storage.SaveCount);
            Assert.Equal(["columnZoom=2.4"], storage.LoadLines(Assert.Single(storage.GetKeys(string.Empty))));

            // A zoom right before the scheduler closes is not lost.
            scope.Scheduler.ColumnZoom = 3;
            scope.Window.Content = null;
            Assert.Equal(2, storage.SaveCount);
            Assert.Equal(["columnZoom=3"], storage.LoadLines(Assert.Single(storage.GetKeys(string.Empty))));
        }
        finally
        {
            StateStorageProvider.Configure(previous);
        }
    }

    [AvaloniaFact]
    public void AZoomWaitingForItsSaveIsWrittenUnderTheOldKeyWhenTheStateKeyChanges()
    {
        var previous = StateStorageProvider.Instance;
        var storage = new InMemoryStateStorage();
        StateStorageProvider.Configure(storage);
        try
        {
            using var scope = new SchedulerScope(SchedulerView.Week);
            scope.Scheduler.StateKey = "first";
            scope.Scheduler.ColumnZoom = 2.4;
            scope.Scheduler.StateKey = "second";

            var key = Assert.Single(storage.GetKeys(string.Empty));
            Assert.Contains("first", key);
            Assert.Equal(["columnZoom=2.4"], storage.LoadLines(key));
            // Nothing waits any more that could write the zoom of the old key under the new one.
            scope.Window.Content = null;
            Assert.Equal(1, storage.SaveCount);
        }
        finally
        {
            StateStorageProvider.Configure(previous);
        }
    }

    // The scroll viewer of the view's body; the heading and the time or resource gutter follow it with hidden bars.
    private static ScrollViewer BodyScroll(SchedulerScope scope) => scope.Scheduler.GetVisualDescendants().OfType<ScrollViewer>()
        .First(static scroll => scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto &&
            scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto);

    // The date and weekday of Monday above its Week column.
    private static StackPanel Heading(SchedulerScope scope) => Assert.IsType<StackPanel>(scope.Scheduler.GetVisualDescendants()
        .OfType<TextBlock>().First(text => text.Text == Monday.ToString("ddd, d MMM", scope.Scheduler.FormattingCulture)).Parent);
}
