using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Flowery.Controls.Scheduler.Models;
using Flowery.Controls.Scheduler.Rendering;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    private sealed record TimeColumn(ColumnDefinition Header, ColumnDefinition Body, Canvas Surface,
        IReadOnlyList<(Border Frame, EventLayout Layout)> Blocks, IReadOnlyList<Border> Backgrounds, double MinimumWidth);

    private readonly List<TimeColumn> _timeColumns = new();
    private Grid? _monthGrid;

    private void UpdateViewGeometry()
    {
        if (_bodyScroll is not { Viewport.Width: > 0, Viewport.Height: > 0 } scroll)
            return;
        if (_monthGrid is { } month)
        {
            month.Width = Math.Max(month.MinWidth, scroll.Viewport.Width);
            month.Height = Math.Max(month.MinHeight, scroll.Viewport.Height);
        }
        RescaleHours(scroll.Viewport.Width);
        if (_timeColumns.Count == 0)
            return;
        var minimumWidth = 0d;
        foreach (var column in _timeColumns)
            minimumWidth += column.MinimumWidth;
        var extra = Math.Max(0, (scroll.Viewport.Width - minimumWidth) / _timeColumns.Count);
        foreach (var column in _timeColumns)
        {
            var width = column.MinimumWidth + extra;
            column.Header.Width = new GridLength(width);
            column.Body.Width = new GridLength(width);
            column.Surface.Width = width;
            foreach (var background in column.Backgrounds)
                background.Width = width;
            foreach (var (frame, layout) in column.Blocks)
            {
                var slotWidth = width / layout.ColumnCount;
                frame.Width = Math.Max(LineThickness, slotWidth - SpacingXS);
                Canvas.SetLeft(frame, layout.ColumnIndex * slotWidth + SpacingXS / 2);
            }
        }
    }

    private double TimedAppointmentMinimumWidth(AppointmentModel appointment)
    {
        var time = Label(FormatTime(appointment.Start) + " – " + FormatTime(appointment.End), true);
        time.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var flags = Label(AppointmentFlags(appointment), true);
        flags.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var statusWidth = flags.DesiredSize.Width;
        foreach (var indicator in appointment.Indicators)
        {
            var glyph = Label(indicator.Glyph, true);
            if (indicator.FontFamily is { } family)
                glyph.FontFamily = family;
            glyph.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            statusWidth = Math.Max(statusWidth, glyph.DesiredSize.Width);
        }
        // Account for the status gutter, accent, content margins, selection border and slot gap.
        var insets = statusWidth + SpacingXS * 3 + (SpacingXS + LineThickness) * 2 + SelectionThickness * 2;
        return Math.Max((ColumnMinWidth ?? Token<double>("DaisySchedulerColumnMinimumWidth")) * Scale,
            time.DesiredSize.Width + insets);
    }

    // Optional rows must fit in full; the subject and time take precedence over decoration and notes.
    private sealed class AppointmentTextPanel(double spacing) : Panel
    {
        private readonly List<int> _priorities = new();
        private bool[] _shown = Array.Empty<bool>();
        public Control? Bottom { get; set; }

        public void Add(Control child, int priority, bool first = false)
        {
            child.ClipToBounds = true;
            if (first)
            {
                Children.Insert(0, child);
                _priorities.Insert(0, priority);
            }
            else
            {
                Children.Add(child);
                _priorities.Add(priority);
            }
            _shown = new bool[Children.Count];
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children)
                child.Measure(new Size(availableSize.Width, double.PositiveInfinity));
            var height = ChooseRows(availableSize.Height);
            var width = 0d;
            for (var index = 0; index < Children.Count; index++)
                if (_shown[index])
                    width = Math.Max(width, Children[index].DesiredSize.Width);
            return new Size(width, Math.Min(height, availableSize.Height));
        }

        private double ChooseRows(double height)
        {
            Array.Clear(_shown);
            var used = 0d;
            var count = 0;
            for (var priority = 0; priority <= 4; priority++)
                for (var index = 0; index < Children.Count; index++)
                {
                    if (_priorities[index] != priority || Children[index].DesiredSize.Height <= 0)
                        continue;
                    var next = used + (count > 0 ? spacing : 0) + Children[index].DesiredSize.Height;
                    if (priority != 0 && next > height)
                        continue;
                    _shown[index] = true;
                    used = next;
                    count++;
                }
            return used;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            ChooseRows(finalSize.Height);
            var y = 0d;
            for (var index = 0; index < Children.Count; index++)
            {
                var child = Children[index];
                if (!_shown[index])
                {
                    child.Arrange(default);
                    continue;
                }
                var height = Math.Min(child.DesiredSize.Height, Math.Max(0, finalSize.Height - y));
                if (ReferenceEquals(child, Bottom))
                    y = Math.Max(y, finalSize.Height - height);
                child.Arrange(new Rect(0, y, finalSize.Width, height));
                y += height + spacing;
            }
            return finalSize;
        }
    }

    // One line of a chip or bar, left to right: status icons, left tiles, start time, subject, right tiles. The start
    // time always shows whole; Planner and Timeline bars are made wide enough for it. Status icons and then the tiles
    // show only when they fit beside it; the subject takes the rest and trims, or hides below its shortest useful width.
    private sealed class AppointmentLinePanel : Panel
    {
        private readonly double _spacing;
        private readonly Control? _start;
        private readonly Control _subject;
        private readonly double _subjectMinimum;
        private readonly Control[] _optional;
        private readonly HashSet<Control> _shown = new();
        private double _startWidth;
        private double _subjectWidth;

        /// <param name="spacing">The gap between two shown parts.</param>
        /// <param name="status">Status icons, or null.</param>
        /// <param name="leftTiles">Tiles before the start time, or null.</param>
        /// <param name="start">The start time, or null for an all-day appointment.</param>
        /// <param name="subject">The subject.</param>
        /// <param name="subjectMinimum">The narrowest subject worth showing, such as two characters and an ellipsis.</param>
        /// <param name="rightTiles">Tiles after the subject, or null.</param>
        public AppointmentLinePanel(double spacing, Control? status, Control? leftTiles, Control? start, Control subject,
            double subjectMinimum, Control? rightTiles)
        {
            _spacing = spacing;
            _start = start;
            _subject = subject;
            _subjectMinimum = subjectMinimum;
            _optional = new[] { status, leftTiles, rightTiles }.OfType<Control>().ToArray();
            foreach (var part in new[] { status, leftTiles, start, subject, rightTiles })
            {
                if (part is null)
                    continue;
                part.ClipToBounds = true;
                Children.Add(part);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children)
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            Choose(availableSize.Width);
            if (_shown.Contains(_subject))
                _subject.Measure(new Size(_subjectWidth, availableSize.Height));
            var width = 0d;
            var height = 0d;
            foreach (var child in Children)
            {
                if (!_shown.Contains(child))
                    continue;
                width += (width > 0 ? _spacing : 0) + PartWidth(child);
                height = Math.Max(height, child.DesiredSize.Height);
            }
            return new Size(width, height);
        }

        // Each shown part costs its width plus one spacing; the first spacing is given back. Without a width limit every
        // part shows at its natural width.
        private void Choose(double width)
        {
            _shown.Clear();
            if (double.IsInfinity(width))
                width = Children.Sum(static child => child.DesiredSize.Width) + _spacing * Math.Max(0, Children.Count - 1);
            var remaining = width + _spacing;
            if (_start is not null)
            {
                _startWidth = _start.DesiredSize.Width;
                _shown.Add(_start);
                remaining -= _startWidth + _spacing;
            }
            foreach (var part in _optional)
            {
                var cost = part.DesiredSize.Width + _spacing;
                if (cost > remaining)
                    continue;
                _shown.Add(part);
                remaining -= cost;
            }
            _subjectWidth = Math.Max(0, remaining - _spacing);
            if (_subjectWidth > 0 && _subjectWidth >= _subjectMinimum)
                _shown.Add(_subject);
        }

        private double PartWidth(Control child) =>
            ReferenceEquals(child, _start) ? _startWidth : ReferenceEquals(child, _subject) ? _subjectWidth : child.DesiredSize.Width;

        protected override Size ArrangeOverride(Size finalSize)
        {
            Choose(finalSize.Width);
            var x = 0d;
            foreach (var child in Children)
            {
                if (!_shown.Contains(child))
                {
                    child.Arrange(default);
                    continue;
                }
                var width = PartWidth(child);
                child.Arrange(new Rect(x, 0, width, finalSize.Height));
                x += width + _spacing;
            }
            return finalSize;
        }
    }

    /// <summary>The position of a Planner or Timeline element along the shown days, in minutes from the first day.
    /// Without <paramref name="Minutes"/> the element keeps its own width.</summary>
    private readonly record struct MinuteSpan(double Start, double Minutes, double Offset, double Trim, double MinimumWidth);

    // A Planner or Timeline surface that places its children by minutes along the shown days, at the scale it inherits
    // from the view. A new visible width only sets that scale on the view, and every surface arranges itself again.
    // Children without a span are placed like on a Canvas, such as the ghost of a running gesture.
    private sealed class MinuteCanvas(double totalMinutes) : Canvas
    {
        public static readonly AttachedProperty<double> PixelsPerMinuteProperty =
            AvaloniaProperty.RegisterAttached<MinuteCanvas, Control, double>("PixelsPerMinute", 1, inherits: true);

        private static readonly AttachedProperty<MinuteSpan?> SpanProperty =
            AvaloniaProperty.RegisterAttached<MinuteCanvas, Control, MinuteSpan?>("Span");

        static MinuteCanvas() => AffectsMeasure<MinuteCanvas>(PixelsPerMinuteProperty);

        public double PixelsPerMinute => GetValue(PixelsPerMinuteProperty);

        public static void Place(Control child, double start, double minutes = double.NaN, double offset = 0, double trim = 0,
            double minimumWidth = 0) => child.SetValue(SpanProperty, new MinuteSpan(start, minutes, offset, trim, minimumWidth));

        private static double SpanWidth(MinuteSpan span, double pixelsPerMinute) =>
            Math.Max(span.MinimumWidth, span.Minutes * pixelsPerMinute - span.Trim);

        protected override Size MeasureOverride(Size availableSize)
        {
            var pixelsPerMinute = PixelsPerMinute;
            foreach (var child in Children)
            {
                var width = child.GetValue(SpanProperty) is { Minutes: not double.NaN } span
                    ? SpanWidth(span, pixelsPerMinute) : double.PositiveInfinity;
                child.Measure(new Size(width, double.PositiveInfinity));
            }
            return new Size(totalMinutes * pixelsPerMinute, 0);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var pixelsPerMinute = PixelsPerMinute;
            foreach (var child in Children)
            {
                var top = GetTop(child);
                if (double.IsNaN(top))
                    top = 0;
                if (child.GetValue(SpanProperty) is not { } span)
                {
                    var left = GetLeft(child);
                    if (double.IsNaN(left))
                        left = 0;
                    child.Arrange(new Rect(new Point(left, top), child.DesiredSize));
                    continue;
                }
                var width = double.IsNaN(span.Minutes) ? child.DesiredSize.Width : SpanWidth(span, pixelsPerMinute);
                child.Arrange(new Rect(span.Start * pixelsPerMinute + span.Offset, top, width, child.DesiredSize.Height));
            }
            return finalSize;
        }
    }
}
