using System;
using System.Collections.Generic;
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
}
