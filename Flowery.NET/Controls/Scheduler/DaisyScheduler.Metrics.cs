using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Utilities;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    private T Token<T>(string key) => this.TryFindResource(key, ActualThemeVariant, out var resource) && resource is T value
        ? value : throw new InvalidOperationException("Missing Flowery design token: " + key);

    private double SpacingXS => Token<double>("DaisySpacingXS");
    private double SpacingSmall => Token<double>("DaisySpacingSmall");
    private double SpacingMedium => Token<double>("DaisySpacingMedium");
    private double SpacingLarge => Token<double>("DaisySpacingLarge");
    private double LineThickness => Token<Thickness>("DaisyBorderThicknessThin").Left;
    private double SelectionThickness => Token<Thickness>("DaisyBorderThicknessMedium").Left;
    private double EventCornerRadius => AppointmentCornerRadius ?? Token<CornerRadius>($"DaisySize{Size}CornerRadius").TopLeft;
    private double RowHeight => RoundToPixel(Token<double>($"DaisySize{Size}Height") + SpacingSmall);
    private double RoundToPixel(double length)
    {
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        return Math.Ceiling(length * scaling) / scaling;
    }

    // Planner and Timeline hours fit their header label and a one-hour bar that shows its start time whole (the chrome
    // around the bar content, the gap between bars and one spacing of slack). The width follows the size, font, culture
    // and clock format; hours widen from it to fill the visible width. Hosts can raise it for the Timeline with
    // DaisySchedulerTimelineMinimumHourWidth. Text widths are rounded up to device pixels as layout rounds them.
    private double MinimumHourPixelsPerMinute(bool timeline)
    {
        var barChrome = LineChrome + BarGap + SpacingXS;
        var width = timeline ? Token<double>("DaisySchedulerTimelineMinimumHourWidth") * Scale : 0;
        for (var hour = 0; hour < 24; hour++)
        {
            var time = DateTime.Today.AddHours(hour);
            var header = Label(FormatTime(time), true);
            header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            // Bars also start on the half hour, where a compact 12-hour time is wider ("12:30p").
            width = Math.Max(width, Math.Max(RoundToPixel(header.DesiredSize.Width) + SpacingSmall * 2,
                StartWidth(time.AddMinutes(30)) + barChrome));
        }
        return RoundToPixel(width) / 60;
    }

    // The width of the start time of a single-line chip or bar, rounded up to device pixels as layout rounds it.
    private double StartWidth(DateTime start)
    {
        var label = Label(SchedulerTimeFormatter.CompactStart(start, TimePattern, Culture));
        label.FontWeight = FontWeight.SemiBold;
        label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return RoundToPixel(label.DesiredSize.Width);
    }

    // A Planner or Timeline bar is at least as wide as its whole start time, however short the appointment is.
    private double BarMinimumWidth(AppointmentModel appointment) =>
        Math.Max(ChipHeight, appointment.IsAllDay ? 0 : StartWidth(appointment.Start) + LineChrome);

}
