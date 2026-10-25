// Avalonia adaptation of Xtreme.Components Scheduler (MIT).
// See ThirdPartyNotices/Xtreme.Components.txt for attribution and license.
using System;
using System.Globalization;
using System.IO;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    private Border CreateAppointment(AppointmentModel appointment, AppointmentSurface surface, Lane? lane = null,
        AppointmentModel? displayed = null, object? resourceId = null)
    {
        var shown = displayed ?? appointment;
        if (_occurrenceSources.TryGetValue(appointment, out var source))
            appointment = source;
        var background = shown.BackColor is { } back ? new SolidColorBrush(back) : Brush("DaisySchedulerEventBackgroundBrush");
        var bar = shown.BarColor is { } accent ? new SolidColorBrush(accent) : Brush("DaisyPrimaryBrush");
        var foreground = shown.BackColor is { } color ? Contrast(color) : Brush("DaisyBaseContentBrush");
        var context = new AppointmentContext(shown, surface, background, bar, foreground,
            Brush("DaisyBase300Brush"), EventCornerRadius, ShowTiles, Culture, TimePattern);
        var template = surface switch
        {
            AppointmentSurface.Chip => AppointmentChipTemplate,
            AppointmentSurface.Bar => AppointmentBarTemplate,
            _ => AppointmentTemplate
        };
        var content = AppointmentRenderer?.Invoke(context) ?? template?.Build(context) ?? BuildAppointmentContent(context);
        content.DataContext = context;
        var frame = new Border
        {
            Background = background,
            BorderBrush = context.BorderBrush,
            BorderThickness = new Thickness(LineThickness),
            CornerRadius = new CornerRadius(EventCornerRadius),
            Child = content,
            MinHeight = ChipHeight,
            Tag = new AppointmentTarget(appointment, lane, shown, lane?.Resource?.Id ?? resourceId),
            DataContext = context,
            Focusable = true
        };
        AutomationProperties.SetName(frame, appointment.Subject + ", " + context.TimeRange);
        frame.PointerPressed += OnAppointmentPressed;
        frame.KeyDown += OnAppointmentKeyDown;
        var tooltip = AppointmentTooltipRenderer?.Invoke(context) ?? AppointmentTooltipTemplate?.Build(context) ?? BuildTooltip(context);
        tooltip.DataContext = context;
        SetSchedulerTooltip(frame, tooltip);
        _frames.Add((frame, appointment));
        return frame;
    }

    // The gap between two Planner or Timeline bars that touch.
    private const double BarGap = 2;

    // The horizontal space of a chip or bar around its content: both frame borders, the color bar and the content margins.
    private double LineChrome => LineThickness * 2 + SpacingXS + (SpacingXS + LineThickness) * 2;

    private Control BuildAppointmentContent(AppointmentContext context) =>
        WrapContent(context, context.Surface == AppointmentSurface.Timed ? BuildTimedContent(context) : BuildLineContent(context));

    // Day and Week blocks: subject, time and note in rows that give way from the bottom, status icons on the left and
    // tiles on every side.
    private Grid BuildTimedContent(AppointmentContext context)
    {
        var main = ContentGrid();
        var text = new AppointmentTextPanel(SpacingXS / 2) { ClipToBounds = true };
        text.Add(Title(context), 0);
        var time = Label(context.TimeRange, true);
        time.Foreground = context.Foreground;
        text.Add(time, 1);
        if (!string.IsNullOrWhiteSpace(context.Note))
        {
            var note = Label(context.Note, true);
            note.Foreground = context.Foreground;
            if (CurrentView == SchedulerView.Calendar)
            {
                note.TextWrapping = TextWrapping.Wrap;
                note.TextTrimming = TextTrimming.None;
            }
            text.Add(note, 4);
        }
        Grid.SetColumn(text, 2);
        Grid.SetRow(text, 1);
        main.Children.Add(text);
        var status = StatusIcons(context, Orientation.Vertical);
        status.Margin = new Thickness(0, 0, SpacingXS, 0);
        Grid.SetRow(status, 1);
        main.Children.Add(status);
        if (!ShowTiles)
            return main;
        foreach (var side in Enum.GetValues<TileSide>())
        {
            var strip = TileStrip(context, side, true);
            if (side is TileSide.Left or TileSide.Right)
            {
                AddToSide(main, strip, side);
                continue;
            }
            text.Add(strip, side == TileSide.Top ? 2 : 3, first: side == TileSide.Top);
            if (side == TileSide.Bottom)
                text.Bottom = strip;
        }
        return main;
    }

    // Month and Agenda chips, Planner and Timeline bars: one line that starts with the start time (see
    // AppointmentLinePanel), with top and bottom tiles above and below it.
    private Grid BuildLineContent(AppointmentContext context)
    {
        var main = ContentGrid();
        StackPanel? leftTiles = null;
        StackPanel? rightTiles = null;
        if (ShowTiles)
        {
            foreach (var side in Enum.GetValues<TileSide>())
            {
                var strip = TileStrip(context, side, false);
                if (side == TileSide.Left)
                    leftTiles = strip;
                else if (side == TileSide.Right)
                    rightTiles = strip;
                else
                    AddToSide(main, strip, side);
            }
        }
        TextBlock? start = null;
        if (!context.IsAllDay)
        {
            start = Label(context.CompactStart);
            start.Foreground = context.Foreground;
            start.FontWeight = FontWeight.SemiBold;
            start.TextTrimming = TextTrimming.None;
        }
        var status = StatusIcons(context, Orientation.Horizontal);
        var title = Title(context);
        var line = new AppointmentLinePanel(SpacingXS, status.Children.Count > 0 ? status : null, NonEmpty(leftTiles), start,
            title, ShortestSubjectWidth(title), NonEmpty(rightTiles)) { VerticalAlignment = VerticalAlignment.Center };
        Grid.SetRow(line, 1);
        Grid.SetColumnSpan(line, 4);
        main.Children.Add(line);
        return main;
    }

    private static StackPanel? NonEmpty(StackPanel? panel) => panel is { Children.Count: > 0 } ? panel : null;

    private Grid ContentGrid() => new()
    {
        ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,*,Auto"),
        Margin = new Thickness(SpacingXS + LineThickness, SpacingXS / 2)
    };

    private TextBlock Title(AppointmentContext context)
    {
        var title = Label(context.Subject);
        title.Foreground = context.Foreground;
        title.FontWeight = FontWeight.SemiBold;
        if (CurrentView == SchedulerView.Calendar)
        {
            title.TextWrapping = TextWrapping.Wrap;
            title.TextTrimming = TextTrimming.None;
        }
        return title;
    }

    // A subject is worth showing from its first two characters and an ellipsis on. Characters are text elements, so an
    // emoji is not split, and the width is rounded up to device pixels as layout rounds it.
    private double ShortestSubjectWidth(TextBlock title)
    {
        var subject = new StringInfo(title.Text ?? string.Empty);
        var shortest = Label(subject.LengthInTextElements > 2 ? subject.SubstringByTextElements(0, 2) + "…" : subject.String);
        shortest.FontWeight = title.FontWeight;
        shortest.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        return RoundToPixel(shortest.DesiredSize.Width);
    }

    private StackPanel StatusIcons(AppointmentContext context, Orientation orientation)
    {
        var status = new StackPanel { Spacing = SpacingXS / 2, Orientation = orientation };
        var flags = AppointmentFlags(context.Appointment);
        if (flags.Length > 0)
        {
            var label = Label(flags, true);
            label.Foreground = context.Foreground;
            status.Children.Add(label);
        }
        foreach (var indicator in context.Indicators)
        {
            var glyph = Label(indicator.Glyph, true);
            if (indicator.FontFamily is { } family)
                glyph.FontFamily = family;
            glyph.Foreground = indicator.Color is { } color ? new SolidColorBrush(color) : context.Foreground;
            SetSchedulerTooltip(glyph, indicator.Tooltip);
            status.Children.Add(glyph);
        }
        return status;
    }

    private StackPanel TileStrip(AppointmentContext context, TileSide side, bool multiline)
    {
        var strip = new StackPanel { Spacing = 3, Orientation = TileLayout.IsVertical(side, multiline) ? Orientation.Vertical : Orientation.Horizontal };
        foreach (var (alignment, tiles) in TileLayout.Groups(context.Tiles, side, multiline))
        {
            var group = new StackPanel { Spacing = 2, Orientation = strip.Orientation };
            group.HorizontalAlignment = alignment switch { DecorationAlignment.Middle => HorizontalAlignment.Center, DecorationAlignment.Far => HorizontalAlignment.Right, _ => HorizontalAlignment.Left };
            group.VerticalAlignment = alignment switch { DecorationAlignment.Middle => VerticalAlignment.Center, DecorationAlignment.Far => VerticalAlignment.Bottom, _ => VerticalAlignment.Top };
            foreach (var tile in tiles)
                group.Children.Add(CreateDecoration(tile, context, true));
            strip.Children.Add(group);
        }
        return strip;
    }

    private static void AddToSide(Grid main, Control strip, TileSide side)
    {
        Grid.SetColumn(strip, side switch { TileSide.Left => 1, TileSide.Right => 3, _ => 2 });
        Grid.SetRow(strip, side switch { TileSide.Top => 0, TileSide.Bottom => 2, _ => 1 });
        main.Children.Add(strip);
    }

    // The color bar on the left and the adornments over the content.
    private Grid WrapContent(AppointmentContext context, Control main)
    {
        var root = new Grid { ClipToBounds = true };
        // The bar follows the rounded corners inside the frame border instead of covering them.
        var inner = Math.Max(0, context.CornerRadius - LineThickness);
        root.Children.Add(new Border
        {
            BorderBrush = context.BarBrush, BorderThickness = new Thickness(SpacingXS, 0, 0, 0),
            CornerRadius = new CornerRadius(inner, 0, 0, inner), Child = main
        });
        foreach (var adornment in context.Adornments)
        {
            var decoration = CreateDecoration(adornment, context, false);
            decoration.HorizontalAlignment = adornment.Position switch
            {
                DecorationPosition.Left or DecorationPosition.FloatLeft => HorizontalAlignment.Left,
                DecorationPosition.Right or DecorationPosition.FloatRight => HorizontalAlignment.Right,
                _ => adornment.Alignment switch { DecorationAlignment.Near => HorizontalAlignment.Left, DecorationAlignment.Far => HorizontalAlignment.Right, _ => HorizontalAlignment.Center }
            };
            decoration.VerticalAlignment = adornment.Position switch
            {
                DecorationPosition.Bottom => VerticalAlignment.Bottom,
                DecorationPosition.Top or DecorationPosition.FloatLeft or DecorationPosition.FloatRight => VerticalAlignment.Top,
                _ => adornment.Alignment switch { DecorationAlignment.Near => VerticalAlignment.Top, DecorationAlignment.Far => VerticalAlignment.Bottom, _ => VerticalAlignment.Center }
            };
            root.Children.Add(decoration);
        }
        return root;
    }

    private static string AppointmentFlags(AppointmentModel appointment)
    {
        var flags = (appointment.IsLocked ? "🔒" : "") + (appointment.IsComplete ? "✓" : "") +
            (appointment.IsCancelled ? "×" : "") + (appointment.IsTentative ? "?" : "") +
            (appointment.IsConfirmed ? "✓" : "") + (appointment.IsRecurring ? "↻" : "");
        foreach (var indicator in appointment.Indicators)
            if (!string.IsNullOrEmpty(indicator.Glyph))
                flags = flags.Replace(indicator.Glyph, string.Empty, StringComparison.Ordinal);
        return flags;
    }

    private Border CreateDecoration(AppointmentDecoration decoration, AppointmentContext context, bool tile)
    {
        var background = decoration.BackColor is { } back ? new SolidColorBrush(back) : tile ? Brush("DaisyBase300Brush") : Brushes.Transparent;
        var foreground = context.Foreground;
        if (background is ISolidColorBrush { Color.A: > 0 } fill)
        {
            var color = fill.Color;
            if (color.A < 255 && context.BackBrush is ISolidColorBrush backdrop)
            {
                var opacity = color.A / 255d;
                color = Color.FromRgb(
                    (byte)Math.Round(color.R * opacity + backdrop.Color.R * (1 - opacity)),
                    (byte)Math.Round(color.G * opacity + backdrop.Color.G * (1 - opacity)),
                    (byte)Math.Round(color.B * opacity + backdrop.Color.B * (1 - opacity)));
            }
            foreground = Contrast(color);
        }
        Control content;
        if (decoration.Image is { Length: > 0 } bytes)
            content = new OwnedImage(bytes) { Width = ChipHeight - 6, Height = ChipHeight - 6 };
        else
        {
            var label = Label(string.Join(" ", new[] { decoration.Glyph, decoration.Text }).Trim(), true);
            if (decoration.FontSize is { } fontSize)
                label.FontSize = fontSize;
            label.Foreground = decoration.ForeColor is { } color ? new SolidColorBrush(color) : foreground;
            label.FontWeight = decoration.Bold ? FontWeight.Bold : FontWeight.Normal;
            if (decoration.GlyphFontFamily is { } family)
                label.FontFamily = family;
            content = label;
        }
        var border = new Border
        {
            Child = content,
            Padding = new Thickness(SpacingXS - LineThickness, LineThickness),
            CornerRadius = new CornerRadius(EventCornerRadius),
            Background = background,
            BorderBrush = decoration.BorderColor is { } outline ? new SolidColorBrush(outline) : Brushes.Transparent,
            BorderThickness = new Thickness(decoration.BorderColor is null ? 0 : LineThickness),
            RenderTransform = new TranslateTransform(decoration.OffsetX, decoration.OffsetY)
        };
        SetSchedulerTooltip(border, decoration.Tooltip);
        return border;
    }

    private Control BuildTooltip(AppointmentContext context)
    {
        var stack = new StackPanel { Spacing = SpacingXS + SpacingXS / 2, MaxWidth = Token<double>("DaisySchedulerTooltipMaximumWidth") };
        var title = TooltipLabel(context.Subject);
        title.FontWeight = FontWeight.SemiBold;
        stack.Children.Add(title);
        stack.Children.Add(TooltipLabel(context.Start.ToString("d", Culture) + "  " + context.TimeRange, true));
        foreach (var detail in context.Details)
            stack.Children.Add(TooltipLabel(detail.Label + ": " + detail.Value, true));
        if (context.Note is { Length: > 0 } note)
        {
            var label = TooltipLabel(note);
            label.TextWrapping = TextWrapping.Wrap;
            stack.Children.Add(label);
        }
        if (context.Memo is { Length: > 0 } memo)
            stack.Children.Add(TooltipLabel(memo, true));
        return stack;
    }

    private TextBlock TooltipLabel(string text, bool secondary = false)
    {
        var label = Label(text, secondary);
        // Inherit the scheduler tooltip palette, including for custom appointment fills.
        label.ClearValue(TextBlock.ForegroundProperty);
        return label;
    }

    private void SetSchedulerTooltip(Control target, object? content)
    {
        if (content is null)
            return;
        var tooltip = content as ToolTip ?? new ToolTip { Content = content };
        tooltip.Classes.Add("daisy-scheduler");
        tooltip.FontFamily = FontFamily;
        tooltip.FontSize = Token<double>($"DaisySize{Size}SecondaryFontSize");
        tooltip.MaxWidth = Token<double>("DaisySchedulerTooltipMaximumWidth");
        ToolTip.SetTip(target, tooltip);
    }

    private static IBrush Contrast(Color color)
    {
        static double Linear(byte value)
        {
            var channel = value / 255d;
            return channel <= 0.04045 ? channel / 12.92 : Math.Pow((channel + 0.055) / 1.055, 2.4);
        }
        var luminance = 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);
        return luminance > 0.179 ? Brushes.Black : Brushes.White;
    }

    private sealed class OwnedImage(byte[] bytes) : Image
    {
        private Bitmap? _bitmap;

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            using var stream = new MemoryStream(bytes, writable: false);
            _bitmap = new Bitmap(stream);
            Source = _bitmap;
            base.OnAttachedToVisualTree(e);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            Source = null;
            _bitmap?.Dispose();
            _bitmap = null;
            base.OnDetachedFromVisualTree(e);
        }
    }
}
