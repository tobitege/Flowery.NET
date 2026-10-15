using System;
using Avalonia;
using Avalonia.Controls;

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

    private double TimelinePixelsPerMinute
    {
        get
        {
            var width = Token<double>("DaisySchedulerTimelineMinimumHourWidth") * Scale;
            for (var hour = 0; hour < 24; hour++)
            {
                var label = Label(FormatTime(DateTime.Today.AddHours(hour)), true);
                label.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width = Math.Max(width, label.DesiredSize.Width + SpacingSmall * 2);
            }
            return RoundToPixel(width) / 60;
        }
    }
}
