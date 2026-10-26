using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
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

    [AvaloniaFact]
    public void APinchScalesTheZoomItStartedAt()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var body = BodyScroll(scope);
        var middle = new Point(body.Viewport.Width / 2, body.Viewport.Height / 2);

        body.RaiseEvent(new PinchEventArgs(2, middle) { RoutedEvent = InputElement.PinchEvent });
        Assert.Equal(2, scope.Scheduler.ColumnZoom);
        body.RaiseEvent(new PinchEventArgs(1.5, middle) { RoutedEvent = InputElement.PinchEvent });
        Assert.Equal(1.5, scope.Scheduler.ColumnZoom);
        body.RaiseEvent(new PinchEndedEventArgs { RoutedEvent = InputElement.PinchEndedEvent });

        body.RaiseEvent(new PinchEventArgs(2, middle) { RoutedEvent = InputElement.PinchEvent });
        Assert.Equal(3, scope.Scheduler.ColumnZoom);
    }

    [AvaloniaFact]
    public void TheSettingsMenuChoosesTheColumnZoom()
    {
        using var scope = new SchedulerScope(SchedulerView.Week);
        var builder = typeof(DaisyScheduler).GetMethod("BuildSettings", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var settings = Assert.IsType<ScrollViewer>(builder.Invoke(scope.Scheduler, null));
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
            // The changes of one turn are written once.
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

    // The scroll viewer of the view's body; the heading and the time or resource gutter follow it with hidden bars.
    private static ScrollViewer BodyScroll(SchedulerScope scope) => scope.Scheduler.GetVisualDescendants().OfType<ScrollViewer>()
        .First(static scroll => scroll.HorizontalScrollBarVisibility == ScrollBarVisibility.Auto &&
            scroll.VerticalScrollBarVisibility == ScrollBarVisibility.Auto);

    // The date and weekday of Monday above its Week column.
    private static StackPanel Heading(SchedulerScope scope) => Assert.IsType<StackPanel>(scope.Scheduler.GetVisualDescendants()
        .OfType<TextBlock>().First(text => text.Text == Monday.ToString("ddd, d MMM", scope.Scheduler.FormattingCulture)).Parent);
}
