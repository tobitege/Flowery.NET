using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls.Primitives;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A GitHub-style contribution heatmap graph.
    /// </summary>
    public class DaisyContributionGraph : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyContributionGraph);

        private int _weekCount = 53;
        public static readonly DirectProperty<DaisyContributionGraph, int> WeekCountProperty =
            AvaloniaProperty.RegisterDirect<DaisyContributionGraph, int>(nameof(WeekCount), o => o.WeekCount);

        /// <summary>Gets the number of week columns needed to display the complete year.</summary>
        public int WeekCount => _weekCount;
        private const int DaysInWeek = 7;
        private const double BaseTextFontSize = 12.0;

        private double _cellStepX;
        private double _cellStepY;
        private double _gridWidth;
        private double _gridHeight;

        public static readonly DirectProperty<DaisyContributionGraph, double> CellStepXProperty =
            AvaloniaProperty.RegisterDirect<DaisyContributionGraph, double>(nameof(CellStepX), o => o.CellStepX);

        public static readonly DirectProperty<DaisyContributionGraph, double> CellStepYProperty =
            AvaloniaProperty.RegisterDirect<DaisyContributionGraph, double>(nameof(CellStepY), o => o.CellStepY);

        public static readonly DirectProperty<DaisyContributionGraph, double> GridWidthProperty =
            AvaloniaProperty.RegisterDirect<DaisyContributionGraph, double>(nameof(GridWidth), o => o.GridWidth);

        public static readonly DirectProperty<DaisyContributionGraph, double> GridHeightProperty =
            AvaloniaProperty.RegisterDirect<DaisyContributionGraph, double>(nameof(GridHeight), o => o.GridHeight);

        /// <summary>
        /// Gets the horizontal distance between week columns (cell size + horizontal margins).
        /// </summary>
        public double CellStepX => _cellStepX;

        /// <summary>
        /// Gets the vertical distance between day rows (cell size + vertical margins).
        /// </summary>
        public double CellStepY => _cellStepY;

        /// <summary>
        /// Gets the total width of the year grid in pixels.
        /// </summary>
        public double GridWidth => _gridWidth;

        /// <summary>
        /// Gets the total height of the 7-day grid in pixels.
        /// </summary>
        public double GridHeight => _gridHeight;

        /// <summary>
        /// Defines the <see cref="Contributions"/> property.
        /// </summary>
        public static readonly StyledProperty<IEnumerable<DaisyContributionDay>?> ContributionsProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, IEnumerable<DaisyContributionDay>?>(nameof(Contributions));

        /// <summary>
        /// Gets or sets the contribution data used to populate the graph.
        /// </summary>
        public IEnumerable<DaisyContributionDay>? Contributions
        {
            get => GetValue(ContributionsProperty);
            set => SetValue(ContributionsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Year"/> property.
        /// </summary>
        public static readonly StyledProperty<int> YearProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, int>(nameof(Year), DateTime.Now.Year);

        /// <summary>
        /// Gets or sets the year displayed by the graph.
        /// </summary>
        public int Year
        {
            get => GetValue(YearProperty);
            set => SetValue(YearProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="StartDayOfWeek"/> property.
        /// </summary>
        public static readonly StyledProperty<DayOfWeek> StartDayOfWeekProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, DayOfWeek>(nameof(StartDayOfWeek), DayOfWeek.Sunday);

        /// <summary>
        /// Gets or sets which day is rendered as the first row of the graph.
        /// </summary>
        public DayOfWeek StartDayOfWeek
        {
            get => GetValue(StartDayOfWeekProperty);
            set => SetValue(StartDayOfWeekProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowLegend"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowLegendProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, bool>(nameof(ShowLegend), true);

        /// <summary>
        /// Gets or sets whether the legend is visible.
        /// </summary>
        public bool ShowLegend
        {
            get => GetValue(ShowLegendProperty);
            set => SetValue(ShowLegendProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowToolTips"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowToolTipsProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, bool>(nameof(ShowToolTips), true);

        /// <summary>
        /// Gets or sets whether tooltips are shown when hovering cells.
        /// </summary>
        public bool ShowToolTips
        {
            get => GetValue(ShowToolTipsProperty);
            set => SetValue(ShowToolTipsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowMonthLabels"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowMonthLabelsProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, bool>(nameof(ShowMonthLabels), true);

        /// <summary>
        /// Gets or sets whether month labels are shown.
        /// </summary>
        public bool ShowMonthLabels
        {
            get => GetValue(ShowMonthLabelsProperty);
            set => SetValue(ShowMonthLabelsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowDayLabels"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowDayLabelsProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, bool>(nameof(ShowDayLabels), true);

        /// <summary>
        /// Gets or sets whether day labels are shown.
        /// </summary>
        public bool ShowDayLabels
        {
            get => GetValue(ShowDayLabelsProperty);
            set => SetValue(ShowDayLabelsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="HighlightMonthStartBorders"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> HighlightMonthStartBordersProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, bool>(nameof(HighlightMonthStartBorders), false);

        /// <summary>
        /// Gets or sets whether the 1st of each month is highlighted with a secondary border.
        /// </summary>
        public bool HighlightMonthStartBorders
        {
            get => GetValue(HighlightMonthStartBordersProperty);
            set => SetValue(HighlightMonthStartBordersProperty, value);
        }

        /// <summary>
        /// Attached property used by the theme to style month-start cells.
        /// </summary>
        public static readonly AttachedProperty<bool> IsMonthStartCellProperty =
            AvaloniaProperty.RegisterAttached<DaisyContributionGraph, AvaloniaObject, bool>(
                "IsMonthStartCell",
                defaultValue: false);

        public static bool GetIsMonthStartCell(AvaloniaObject element) => element.GetValue(IsMonthStartCellProperty);
        public static void SetIsMonthStartCell(AvaloniaObject element, bool value) => element.SetValue(IsMonthStartCellProperty, value);

        /// <summary>
        /// Defines the <see cref="CellSize"/> property.
        /// </summary>
        public static readonly StyledProperty<double> CellSizeProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, double>(nameof(CellSize), 10.0);

        /// <summary>
        /// Gets or sets the size of each cell.
        /// </summary>
        public double CellSize
        {
            get => GetValue(CellSizeProperty);
            set => SetValue(CellSizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="CellMargin"/> property.
        /// </summary>
        public static readonly StyledProperty<Thickness> CellMarginProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, Thickness>(nameof(CellMargin), new Thickness(1));

        /// <summary>
        /// Gets or sets the margin around each cell.
        /// </summary>
        public Thickness CellMargin
        {
            get => GetValue(CellMarginProperty);
            set => SetValue(CellMarginProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="CellCornerRadius"/> property.
        /// </summary>
        public static readonly StyledProperty<CornerRadius> CellCornerRadiusProperty =
            AvaloniaProperty.Register<DaisyContributionGraph, CornerRadius>(nameof(CellCornerRadius), new CornerRadius(2));

        /// <summary>
        /// Gets or sets the corner radius for each cell.
        /// </summary>
        public CornerRadius CellCornerRadius
        {
            get => GetValue(CellCornerRadiusProperty);
            set => SetValue(CellCornerRadiusProperty, value);
        }

        /// <summary>
        /// Cells displayed by the graph (7 rows and enough columns for the complete year).
        /// </summary>
        public AvaloniaList<DaisyContributionGraphCell> Cells { get; } = new();

        /// <summary>
        /// Month labels positioned above the week columns.
        /// </summary>
        public AvaloniaList<DaisyContributionMonthHeader> MonthHeaders { get; } = new();

        /// <summary>
        /// Day labels positioned left of the day rows.
        /// </summary>
        public AvaloniaList<DaisyContributionDayLabel> DayLabels { get; } = new();

        static DaisyContributionGraph()
        {
            ContributionsProperty.Changed.AddClassHandler<DaisyContributionGraph>((x, _) => x.Rebuild());
            YearProperty.Changed.AddClassHandler<DaisyContributionGraph>((x, _) => x.Rebuild());
            StartDayOfWeekProperty.Changed.AddClassHandler<DaisyContributionGraph>((x, _) => x.Rebuild());
            ShowToolTipsProperty.Changed.AddClassHandler<DaisyContributionGraph>((x, _) => x.Rebuild());
            HighlightMonthStartBordersProperty.Changed.AddClassHandler<DaisyContributionGraph>((x, _) => x.Rebuild());
            CellSizeProperty.Changed.AddClassHandler<DaisyContributionGraph>((x, _) => x.Rebuild());
            CellMarginProperty.Changed.AddClassHandler<DaisyContributionGraph>((x, _) => x.Rebuild());
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        private void Rebuild()
        {
            var year = Year > 0 ? Year : DateTime.Today.Year;
            var first = GetFirstGridDate(new DateTime(year, 1, 1));
            var weeks = (new DateTime(year, 12, 31) - first).Days / DaysInWeek + 1;
            SetAndRaise(WeekCountProperty, ref _weekCount, weeks);
            UpdateCellSteps();
            RebuildDayLabels();
            RebuildMonthHeaders();
            RebuildCells();
        }

        private void UpdateCellSteps()
        {
            var margin = CellMargin;
            var stepX = CellSize + margin.Left + margin.Right;
            var stepY = CellSize + margin.Top + margin.Bottom;

            SetAndRaise(CellStepXProperty, ref _cellStepX, stepX);
            SetAndRaise(CellStepYProperty, ref _cellStepY, stepY);

            var gridWidth = WeekCount * stepX;
            var gridHeight = DaysInWeek * stepY;
            SetAndRaise(GridWidthProperty, ref _gridWidth, gridWidth);
            SetAndRaise(GridHeightProperty, ref _gridHeight, gridHeight);
        }

        private void RebuildDayLabels()
        {
            DayLabels.Clear();

            var format = CultureInfo.CurrentCulture.DateTimeFormat;

            for (int i = 0; i < DaysInWeek; i++)
            {
                var day = (DayOfWeek)(((int)StartDayOfWeek + i) % DaysInWeek);
                var label = i % 2 == 0 ? format.AbbreviatedDayNames[(int)day] : string.Empty;
                DayLabels.Add(new DaisyContributionDayLabel { Text = label });
            }
        }

        private void RebuildMonthHeaders()
        {
            MonthHeaders.Clear();

            var year = Year;
            var format = CultureInfo.CurrentCulture.DateTimeFormat;
            var startDate = new DateTime(year, 1, 1);
            var first = GetFirstGridDate(startDate);

            // Place each month label at the week-column that contains the 1st of that month.
            var lastWeekIndex = -1;

            for (int month = 1; month <= 12; month++)
            {
                var firstOfMonth = new DateTime(year, month, 1);
                var weekIndex = (int)((firstOfMonth.Date - first.Date).TotalDays / DaysInWeek);

                if (weekIndex < 0 || weekIndex >= WeekCount)
                    continue;

                if (weekIndex == lastWeekIndex)
                    continue;

                var text = format.AbbreviatedMonthNames[month - 1];
                if (string.IsNullOrWhiteSpace(text))
                    continue;

                MonthHeaders.Add(new DaisyContributionMonthHeader
                {
                    Text = text,
                    Left = weekIndex * CellStepX
                });

                lastWeekIndex = weekIndex;
            }
        }

        private void RebuildCells()
        {
            Cells.Clear();

            var year = Year;
            var startDate = new DateTime(year, 1, 1);
            var endDate = new DateTime(year, 12, 31);
            var first = GetFirstGridDate(startDate);

            var map = new Dictionary<DateTime, DaisyContributionDay>();
            if (Contributions != null)
            {
                foreach (var item in Contributions)
                {
                    map[item.Date.Date] = item;
                }
            }

            // IMPORTANT: UniformGrid lays out items row-major (left-to-right, top-to-bottom).
            // We want rows = day-of-week, columns = week-of-year, so generate in day-major order.
            for (int day = 0; day < DaysInWeek; day++)
            {
                for (int week = 0; week < WeekCount; week++)
                {
                    var currentDate = first.AddDays(week * DaysInWeek + day);

                    if (!IsDateInGraphRange(currentDate, startDate, endDate, year))
                    {
                        Cells.Add(new DaisyContributionGraphCell
                        {
                            Date = null,
                            Count = 0,
                            Level = -1,
                            ToolTipText = null
                        });
                        continue;
                    }

                    map.TryGetValue(currentDate.Date, out var data);

                    var count = data?.Count ?? 0;
                    var level = data?.Level ?? 0;
                    level = Math.Max(0, Math.Min(4, level));

                    Cells.Add(new DaisyContributionGraphCell
                    {
                        Date = currentDate,
                        Count = count,
                        Level = level,
                        ToolTipText = ShowToolTips ? FormatToolTip(currentDate, count) : null,
                        IsMonthStart = HighlightMonthStartBorders && currentDate.Day == 1
                    });
                }
            }
        }

        private DateTime GetFirstGridDate(DateTime startDate)
        {
            var diff = ((int)startDate.DayOfWeek - (int)StartDayOfWeek + DaysInWeek) % DaysInWeek;
            return startDate.AddDays(-diff);
        }

        private static bool IsDateInGraphRange(DateTime date, DateTime startDate, DateTime endDate, int targetYear)
        {
            if (date >= startDate && date <= endDate) return true;
            if (date.Year == targetYear - 1 && date.Month == 12) return true;
            if (date.Year == targetYear + 1 && date.Month == 1) return true;
            return false;
        }

        private static string FormatToolTip(DateTime date, int count)
        {
            var countText = count switch
            {
                0 => "No contributions",
                1 => "1 contribution",
                _ => $"{count} contributions"
            };

            return $"{countText} on {date:D}";
        }
    }

    /// <summary>
    /// Contribution data point used by <see cref="DaisyContributionGraph"/>.
    /// </summary>
    public class DaisyContributionDay
    {
        public DateTime Date { get; set; }
        public int Count { get; set; }
        public int Level { get; set; }
    }

    public class DaisyContributionGraphCell
    {
        public DateTime? Date { get; set; }
        public int Count { get; set; }
        public int Level { get; set; }
        public string? ToolTipText { get; set; }
        public bool IsMonthStart { get; set; }
    }

    public class DaisyContributionMonthHeader
    {
        public string Text { get; set; } = string.Empty;
        public double Left { get; set; }
    }

    public class DaisyContributionDayLabel
    {
        public string Text { get; set; } = string.Empty;
    }
}
