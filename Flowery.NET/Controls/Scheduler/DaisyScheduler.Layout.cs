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

    // A single-line chip or bar keeps its start time whole. Status glyphs and tiles show only when they fit beside it,
    // in the order they were added; the subject takes the rest and trims, or hides below its shortest useful width.
    private sealed class AppointmentLinePanel(double spacing) : Panel
    {
        public const int Required = 0;
        public const int Optional = 1;
        public const int Fill = 2;

        private readonly List<int> _roles = new();
        private bool[] _shown = Array.Empty<bool>();
        private double _fillWidth;
        private double _fillMinimum;

        /// <param name="child">The part to add.</param>
        /// <param name="role"><see cref="Required"/>, <see cref="Optional"/> or <see cref="Fill"/>.</param>
        /// <param name="minimumWidth">For the Fill part: the narrowest width worth showing, such as the first characters
        /// and an ellipsis. Below it the part is hidden instead of showing fragments.</param>
        public void Add(Control child, int role, double minimumWidth = 0)
        {
            child.ClipToBounds = true;
            Children.Add(child);
            _roles.Add(role);
            if (role == Fill)
                _fillMinimum = minimumWidth;
            _shown = new bool[Children.Count];
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            foreach (var child in Children)
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
            Choose(availableSize.Width);
            var width = 0d;
            var height = 0d;
            var count = 0;
            for (var index = 0; index < Children.Count; index++)
            {
                if (!_shown[index])
                    continue;
                if (_roles[index] == Fill)
                    Children[index].Measure(new Size(_fillWidth, availableSize.Height));
                width += (count++ > 0 ? spacing : 0) + Children[index].DesiredSize.Width;
                height = Math.Max(height, Children[index].DesiredSize.Height);
            }
            return new Size(width, height);
        }

        // Each shown part costs its width plus one spacing; the first spacing is given back.
        private void Choose(double width)
        {
            Array.Clear(_shown);
            var remaining = width + spacing;
            foreach (var role in new[] { Required, Optional })
                for (var index = 0; index < Children.Count; index++)
                {
                    if (_roles[index] != role)
                        continue;
                    var cost = Children[index].DesiredSize.Width + spacing;
                    if (role == Optional && cost > remaining)
                        continue;
                    _shown[index] = true;
                    remaining -= cost;
                }
            _fillWidth = Math.Max(0, remaining - spacing);
            for (var index = 0; index < Children.Count; index++)
                if (_roles[index] == Fill)
                    _shown[index] = _fillWidth > 0 && _fillWidth >= _fillMinimum;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            Choose(finalSize.Width);
            var x = 0d;
            for (var index = 0; index < Children.Count; index++)
            {
                var child = Children[index];
                if (!_shown[index])
                {
                    child.Arrange(default);
                    continue;
                }
                var width = _roles[index] == Fill ? _fillWidth : child.DesiredSize.Width;
                child.Arrange(new Rect(x, 0, width, finalSize.Height));
                x += width + spacing;
            }
            return finalSize;
        }
    }
}
