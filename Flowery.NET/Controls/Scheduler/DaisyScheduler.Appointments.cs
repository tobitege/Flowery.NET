// Avalonia adaptation of Xtreme.Components Scheduler (MIT).
// See ThirdPartyNotices/Xtreme.Components.txt for attribution and license.
using System;
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

    private Control BuildAppointmentContent(AppointmentContext context)
    {
        var main = new Grid { ColumnDefinitions = new ColumnDefinitions("Auto,Auto,*,Auto"), RowDefinitions = new RowDefinitions("Auto,*,Auto"), Margin = new Thickness(SpacingXS + LineThickness, SpacingXS / 2) };
        var multiline = context.Surface == AppointmentSurface.Timed;
        var text = new AppointmentTextPanel(SpacingXS / 2) { ClipToBounds = true };
        var title = Label(context.Subject);
        title.Foreground = context.Foreground;
        title.FontWeight = FontWeight.SemiBold;
        if (CurrentView == SchedulerView.Calendar)
        {
            title.TextWrapping = TextWrapping.Wrap;
            title.TextTrimming = TextTrimming.None;
        }
        text.Add(title, 0);
        if (multiline)
        {
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
        }
        else if (!context.IsAllDay)
            title.Text = context.CompactStart + "  " + context.Subject;
        Grid.SetColumn(text, 2);
        Grid.SetRow(text, 1);
        main.Children.Add(text);

        var status = new StackPanel { Spacing = SpacingXS / 2, Margin = new Thickness(0, 0, SpacingXS, 0), Orientation = multiline ? Orientation.Vertical : Orientation.Horizontal };
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
        Grid.SetRow(status, 1);
        main.Children.Add(status);

        if (ShowTiles)
        {
            foreach (var side in Enum.GetValues<TileSide>())
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
                if (multiline && side is TileSide.Top or TileSide.Bottom)
                {
                    text.Add(strip, side == TileSide.Top ? 2 : 3, first: side == TileSide.Top);
                    if (side == TileSide.Bottom)
                        text.Bottom = strip;
                }
                else
                {
                    Grid.SetColumn(strip, side switch { TileSide.Left => 1, TileSide.Right => 3, _ => 2 });
                    Grid.SetRow(strip, side switch { TileSide.Top => 0, TileSide.Bottom => 2, _ => 1 });
                    main.Children.Add(strip);
                }
            }
        }
        var root = new Grid { ClipToBounds = true };
        root.Children.Add(new Border { BorderBrush = context.BarBrush, BorderThickness = new Thickness(SpacingXS, 0, 0, 0), Child = main });
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
