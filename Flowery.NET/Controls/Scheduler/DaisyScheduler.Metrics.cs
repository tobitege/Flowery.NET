using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
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

    // Planner and Timeline hours fit their header label and a one-hour bar that shows its start time whole (frame and
    // accent borders, content margins, the gap between bars and one spacing of slack). The width follows the size,
    // font, culture and clock format; hours widen from it to fill the visible width. Hosts can raise it with
    // DaisySchedulerTimelineMinimumHourWidth. Text widths are rounded up to device pixels as layout rounds them.
    private double MinimumHourPixelsPerMinute()
    {
        var barChrome = SpacingXS * 4 + LineThickness * 4 + 2;
        var width = Token<double>("DaisySchedulerTimelineMinimumHourWidth") * Scale;
        for (var hour = 0; hour < 24; hour++)
        {
            var time = DateTime.Today.AddHours(hour);
            var header = Label(FormatTime(time), true);
            header.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            // Bars also start on the half hour, where a compact 12-hour time is widest ("12:30p").
            var start = Label(CompactTime(time.AddMinutes(30)));
            start.FontWeight = FontWeight.SemiBold;
            start.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            width = Math.Max(width, Math.Max(RoundToPixel(header.DesiredSize.Width) + SpacingSmall * 2,
                RoundToPixel(start.DesiredSize.Width) + barChrome));
        }
        return RoundToPixel(width) / 60;
    }

    // The start time of a single-line chip or bar, as AppointmentContext.CompactStart writes it.
    private string CompactTime(DateTime time) =>
        TimePattern.Contains('H') ? time.ToString(TimePattern, Culture) : SchedulerTimeFormatter.Compact(time);
}
