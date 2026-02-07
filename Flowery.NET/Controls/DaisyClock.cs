using System;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Avalonia.Threading;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Clock display mode.
    /// </summary>
    public enum ClockMode
    {
        /// <summary>Shows current time.</summary>
        Clock,
        /// <summary>Countdown timer mode.</summary>
        Timer,
        /// <summary>Stopwatch mode (elapsed time).</summary>
        Stopwatch
    }

    /// <summary>
    /// Label format for time units.
    /// </summary>
    public enum ClockLabelFormat
    {
        /// <summary>No labels, just numbers (04:53:16).</summary>
        None,
        /// <summary>Short labels (4h 53m 16s).</summary>
        Short,
        /// <summary>Long labels (4 hours 53 minutes 16 seconds).</summary>
        Long
    }

    /// <summary>
    /// Visual style for clock display.
    /// </summary>
    public enum ClockStyle
    {
        /// <summary>Uses DaisyCountdown controls for each time unit segment.</summary>
        Segmented,
        /// <summary>Flip-clock style using TextBlocks in DaisyJoin.</summary>
        Flip,
        /// <summary>Plain text display.</summary>
        Text
    }

    /// <summary>
    /// Time format (12h vs 24h).
    /// </summary>
    public enum ClockFormat
    {
        /// <summary>24-hour format (00:00 - 23:59).</summary>
        TwentyFourHour,
        /// <summary>12-hour format with AM/PM.</summary>
        TwelveHour
    }

    /// <summary>
    /// Represents a recorded lap time.
    /// </summary>
    public record LapTime(int Number, DateTimeOffset RecordedAt, TimeSpan TotalElapsed, TimeSpan LapDuration);

    /// <summary>
    /// A comprehensive clock control that displays time with optional labels, supports
    /// timer and stopwatch modes, and can manage alarms.
    /// </summary>
    public class DaisyClock : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyClock);

        private const double BaseFontSize = 24.0;
        private const double BaseSeparatorFontSize = 20.0;

        private DispatcherTimer? _timer;
        private DateTime _stopwatchStartTime;
        private TimeSpan _stopwatchPausedElapsed;
        private readonly ObservableCollection<LapTime> _laps = new();
        private TimeSpan _timerRemaining;
        private TimeSpan _lastLapTime;

        public DaisyClock()
        {
            Laps = new ReadOnlyObservableCollection<LapTime>(_laps);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            FloweryLocalization.CultureChanged += OnCultureChanged;
            UpdateTimerForMode();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            FloweryLocalization.CultureChanged -= OnCultureChanged;
            StopTimer();
        }

        private void OnCultureChanged(object? sender, System.Globalization.CultureInfo e)
        {
            UpdateDisplay();
        }

        #region Dependency Properties

        public static readonly StyledProperty<ClockMode> ModeProperty =
            AvaloniaProperty.Register<DaisyClock, ClockMode>(nameof(Mode), ClockMode.Clock);

        /// <summary>
        /// Gets or sets the clock mode.
        /// </summary>
        public ClockMode Mode
        {
            get => GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public static readonly StyledProperty<ClockStyle> DisplayStyleProperty =
            AvaloniaProperty.Register<DaisyClock, ClockStyle>(nameof(DisplayStyle), ClockStyle.Segmented);

        /// <summary>
        /// Gets or sets the visual style.
        /// </summary>
        public ClockStyle DisplayStyle
        {
            get => GetValue(DisplayStyleProperty);
            set => SetValue(DisplayStyleProperty, value);
        }

        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyClock, Orientation>(nameof(Orientation), Orientation.Horizontal);

        /// <summary>
        /// Gets or sets the layout orientation.
        /// </summary>
        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public static readonly StyledProperty<bool> ShowSecondsProperty =
            AvaloniaProperty.Register<DaisyClock, bool>(nameof(ShowSeconds), true);

        /// <summary>
        /// Gets or sets whether to display seconds.
        /// </summary>
        public bool ShowSeconds
        {
            get => GetValue(ShowSecondsProperty);
            set => SetValue(ShowSecondsProperty, value);
        }

        public static readonly StyledProperty<ClockLabelFormat> LabelFormatProperty =
            AvaloniaProperty.Register<DaisyClock, ClockLabelFormat>(nameof(LabelFormat), ClockLabelFormat.None);

        /// <summary>
        /// Gets or sets the label format.
        /// </summary>
        public ClockLabelFormat LabelFormat
        {
            get => GetValue(LabelFormatProperty);
            set => SetValue(LabelFormatProperty, value);
        }

        public static readonly StyledProperty<string> SeparatorProperty =
            AvaloniaProperty.Register<DaisyClock, string>(nameof(Separator), ":");

        /// <summary>
        /// Gets or sets the separator between time units.
        /// </summary>
        public string Separator
        {
            get => GetValue(SeparatorProperty);
            set => SetValue(SeparatorProperty, value);
        }

        public static readonly StyledProperty<ClockFormat> FormatProperty =
            AvaloniaProperty.Register<DaisyClock, ClockFormat>(nameof(Format), ClockFormat.TwentyFourHour);

        /// <summary>
        /// Gets or sets the clock format (12h or 24h).
        /// </summary>
        public ClockFormat Format
        {
            get => GetValue(FormatProperty);
            set => SetValue(FormatProperty, value);
        }

        public static readonly StyledProperty<bool> ShowAmPmProperty =
            AvaloniaProperty.Register<DaisyClock, bool>(nameof(ShowAmPm), false);

        /// <summary>
        /// Gets or sets whether to show AM/PM suffix.
        /// </summary>
        public bool ShowAmPm
        {
            get => GetValue(ShowAmPmProperty);
            set => SetValue(ShowAmPmProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyClock, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<double> SpacingProperty =
            AvaloniaProperty.Register<DaisyClock, double>(nameof(Spacing), 6.0);

        /// <summary>
        /// Gets or sets the spacing between time units.
        /// </summary>
        public double Spacing
        {
            get => GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        public static readonly StyledProperty<bool> UseJoinProperty =
            AvaloniaProperty.Register<DaisyClock, bool>(nameof(UseJoin), false);

        /// <summary>
        /// Gets or sets whether to use DaisyJoin for appearance.
        /// </summary>
        public bool UseJoin
        {
            get => GetValue(UseJoinProperty);
            set => SetValue(UseJoinProperty, value);
        }

        public static readonly StyledProperty<TimeSpan> TimerDurationProperty =
            AvaloniaProperty.Register<DaisyClock, TimeSpan>(nameof(TimerDuration), TimeSpan.FromMinutes(5));

        /// <summary>
        /// Gets or sets the timer duration.
        /// </summary>
        public TimeSpan TimerDuration
        {
            get => GetValue(TimerDurationProperty);
            set => SetValue(TimerDurationProperty, value);
        }

        #endregion

        #region Read-Only Properties

        public static readonly DirectProperty<DaisyClock, TimeSpan> TimerRemainingProperty =
            AvaloniaProperty.RegisterDirect<DaisyClock, TimeSpan>(
                nameof(TimerRemaining),
                o => o.TimerRemaining);

        /// <summary>
        /// Gets the remaining time in Timer mode.
        /// </summary>
        public TimeSpan TimerRemaining
        {
            get => _timerRemaining;
            private set => SetAndRaise(TimerRemainingProperty, ref _timerRemaining, value);
        }

        public static readonly DirectProperty<DaisyClock, TimeSpan> StopwatchElapsedProperty =
            AvaloniaProperty.RegisterDirect<DaisyClock, TimeSpan>(
                nameof(StopwatchElapsed),
                o => o.StopwatchElapsed);

        private TimeSpan _stopwatchElapsed;

        /// <summary>
        /// Gets the elapsed time in Stopwatch mode.
        /// </summary>
        public TimeSpan StopwatchElapsed
        {
            get => _stopwatchElapsed;
            private set => SetAndRaise(StopwatchElapsedProperty, ref _stopwatchElapsed, value);
        }

        public static readonly DirectProperty<DaisyClock, bool> IsRunningProperty =
            AvaloniaProperty.RegisterDirect<DaisyClock, bool>(
                nameof(IsRunning),
                o => o.IsRunning);

        private bool _isRunning;

        /// <summary>
        /// Gets whether the timer/stopwatch is running.
        /// </summary>
        public bool IsRunning
        {
            get => _isRunning;
            private set => SetAndRaise(IsRunningProperty, ref _isRunning, value);
        }

        public static readonly DirectProperty<DaisyClock, string> DisplayTextProperty =
            AvaloniaProperty.RegisterDirect<DaisyClock, string>(
                nameof(DisplayText),
                o => o.DisplayText);

        private string _displayText = "00:00:00";

        /// <summary>
        /// Gets the formatted display text.
        /// </summary>
        public string DisplayText
        {
            get => _displayText;
            private set => SetAndRaise(DisplayTextProperty, ref _displayText, value);
        }

        public static readonly DirectProperty<DaisyClock, int> HoursValueProperty =
            AvaloniaProperty.RegisterDirect<DaisyClock, int>(
                nameof(HoursValue),
                o => o.HoursValue);

        private int _hoursValue;

        /// <summary>
        /// Gets the hours component.
        /// </summary>
        public int HoursValue
        {
            get => _hoursValue;
            private set => SetAndRaise(HoursValueProperty, ref _hoursValue, value);
        }

        public static readonly DirectProperty<DaisyClock, int> MinutesValueProperty =
            AvaloniaProperty.RegisterDirect<DaisyClock, int>(
                nameof(MinutesValue),
                o => o.MinutesValue);

        private int _minutesValue;

        /// <summary>
        /// Gets the minutes component.
        /// </summary>
        public int MinutesValue
        {
            get => _minutesValue;
            private set => SetAndRaise(MinutesValueProperty, ref _minutesValue, value);
        }

        public static readonly DirectProperty<DaisyClock, int> SecondsValueProperty =
            AvaloniaProperty.RegisterDirect<DaisyClock, int>(
                nameof(SecondsValue),
                o => o.SecondsValue);

        private int _secondsValue;

        /// <summary>
        /// Gets the seconds component.
        /// </summary>
        public int SecondsValue
        {
            get => _secondsValue;
            private set => SetAndRaise(SecondsValueProperty, ref _secondsValue, value);
        }

        public static readonly DirectProperty<DaisyClock, string> AmPmTextProperty =
            AvaloniaProperty.RegisterDirect<DaisyClock, string>(
                nameof(AmPmText),
                o => o.AmPmText);

        private string _amPmText = "";

        /// <summary>
        /// Gets the AM/PM text.
        /// </summary>
        public string AmPmText
        {
            get => _amPmText;
            private set => SetAndRaise(AmPmTextProperty, ref _amPmText, value);
        }

        /// <summary>
        /// Gets the collection of recorded lap times.
        /// </summary>
        public ReadOnlyObservableCollection<LapTime> Laps { get; }

        #endregion

        #region Events

        /// <summary>
        /// Raised when the timer finishes.
        /// </summary>
        public event EventHandler? TimerFinished;

        /// <summary>
        /// Raised each second during timer countdown.
        /// </summary>
        public event EventHandler<TimeSpan>? TimerTick;

        /// <summary>
        /// Raised each second during stopwatch.
        /// </summary>
        public event EventHandler<TimeSpan>? StopwatchTick;

        /// <summary>
        /// Raised when a lap is recorded.
        /// </summary>
        public event EventHandler<LapTime>? LapRecorded;

        #endregion

        #region Public Methods

        /// <summary>
        /// Starts the timer or stopwatch.
        /// </summary>
        public void Start()
        {
            if (Mode == ClockMode.Clock) return;

            if (Mode == ClockMode.Timer)
            {
                StartTimer();
            }
            else if (Mode == ClockMode.Stopwatch)
            {
                StartStopwatch();
            }
        }

        /// <summary>
        /// Pauses the timer or stopwatch.
        /// </summary>
        public void Pause()
        {
            if (Mode == ClockMode.Timer || Mode == ClockMode.Stopwatch)
            {
                _timer?.Stop();
                if (Mode == ClockMode.Stopwatch)
                {
                    _stopwatchPausedElapsed = StopwatchElapsed;
                }
                IsRunning = false;
            }
        }

        /// <summary>
        /// Resets the timer or stopwatch.
        /// </summary>
        public void Reset()
        {
            if (Mode == ClockMode.Timer)
            {
                _timer?.Stop();
                _timerRemaining = TimerDuration;
                TimerRemaining = _timerRemaining;
                IsRunning = false;
                UpdateDisplay();
            }
            else if (Mode == ClockMode.Stopwatch)
            {
                _timer?.Stop();
                _stopwatchPausedElapsed = TimeSpan.Zero;
                StopwatchElapsed = TimeSpan.Zero;
                IsRunning = false;
                ClearLaps();
                UpdateDisplay();
            }
        }

        /// <summary>
        /// Records a lap.
        /// </summary>
        public void RecordLap()
        {
            if (Mode != ClockMode.Stopwatch || !IsRunning)
                return;

            var currentElapsed = StopwatchElapsed;
            var lapDuration = currentElapsed - _lastLapTime;
            var lapNumber = _laps.Count + 1;

            var lap = new LapTime(lapNumber, DateTimeOffset.Now, currentElapsed, lapDuration);
            _laps.Add(lap);
            _lastLapTime = currentElapsed;

            LapRecorded?.Invoke(this, lap);
        }

        /// <summary>
        /// Clears all recorded laps.
        /// </summary>
        public void ClearLaps()
        {
            _laps.Clear();
            _lastLapTime = TimeSpan.Zero;
        }

        #endregion

        #region Property Changed

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ModeProperty)
            {
                StopTimer();
                UpdateTimerForMode();
            }
            else if (change.Property == TimerDurationProperty && Mode == ClockMode.Timer && !IsRunning)
            {
                _timerRemaining = TimerDuration;
                TimerRemaining = _timerRemaining;
                UpdateDisplay();
            }
            else if (change.Property == FormatProperty ||
                     change.Property == ShowSecondsProperty ||
                     change.Property == ShowAmPmProperty ||
                     change.Property == SeparatorProperty ||
                     change.Property == LabelFormatProperty)
            {
                UpdateDisplay();
            }
            else if (change.Property == SizeProperty && FloweryScaleManager.GetEnableScaling(this))
            {
                ApplyScaleFactor(FloweryScaleManager.GetScaleFactor(this));
            }
        }

        #endregion

        #region Timer Management

        private void UpdateTimerForMode()
        {
            StopTimer();

            switch (Mode)
            {
                case ClockMode.Clock:
                    StartClockTimer();
                    break;
                case ClockMode.Timer:
                    _timerRemaining = TimerDuration;
                    TimerRemaining = _timerRemaining;
                    break;
                case ClockMode.Stopwatch:
                    _stopwatchPausedElapsed = TimeSpan.Zero;
                    StopwatchElapsed = TimeSpan.Zero;
                    break;
            }

            UpdateDisplay();
        }

        private void StartClockTimer()
        {
            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += OnClockTick;
            _timer.Start();
        }

        private void StartTimer()
        {
            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
                _timer.Tick += OnTimerTick;
            }
            _timer.Start();
            IsRunning = true;
        }

        private void StartStopwatch()
        {
            _stopwatchStartTime = DateTime.UtcNow.Subtract(_stopwatchPausedElapsed);

            if (_timer == null)
            {
                _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
                _timer.Tick += OnStopwatchTick;
            }
            _timer.Start();
            IsRunning = true;
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnClockTick;
                _timer.Tick -= OnTimerTick;
                _timer.Tick -= OnStopwatchTick;
                _timer = null;
            }
            IsRunning = false;
        }

        private void OnClockTick(object? sender, EventArgs e)
        {
            UpdateDisplay();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            _timerRemaining = _timerRemaining.Subtract(TimeSpan.FromSeconds(1));

            if (_timerRemaining <= TimeSpan.Zero)
            {
                _timerRemaining = TimeSpan.Zero;
                _timer?.Stop();
                IsRunning = false;
                TimerFinished?.Invoke(this, EventArgs.Empty);
            }

            TimerRemaining = _timerRemaining;
            TimerTick?.Invoke(this, _timerRemaining);
            UpdateDisplay();
        }

        private void OnStopwatchTick(object? sender, EventArgs e)
        {
            var elapsed = DateTime.UtcNow - _stopwatchStartTime;
            StopwatchElapsed = elapsed;
            StopwatchTick?.Invoke(this, elapsed);
            UpdateDisplay();
        }

        #endregion

        #region Display Update

        private void UpdateDisplay()
        {
            int hours, minutes, seconds;
            bool isPm = false;

            switch (Mode)
            {
                case ClockMode.Clock:
                    var now = DateTime.Now;
                    hours = now.Hour;
                    minutes = now.Minute;
                    seconds = now.Second;

                    if (Format == ClockFormat.TwelveHour)
                    {
                        isPm = hours >= 12;
                        hours = hours % 12;
                        if (hours == 0) hours = 12;
                    }
                    break;

                case ClockMode.Timer:
                    hours = (int)_timerRemaining.TotalHours;
                    minutes = _timerRemaining.Minutes;
                    seconds = _timerRemaining.Seconds;
                    break;

                case ClockMode.Stopwatch:
                    var elapsed = StopwatchElapsed;
                    hours = (int)elapsed.TotalHours;
                    minutes = elapsed.Minutes;
                    seconds = elapsed.Seconds;
                    break;

                default:
                    hours = minutes = seconds = 0;
                    break;
            }

            HoursValue = hours;
            MinutesValue = minutes;
            SecondsValue = seconds;
            AmPmText = ShowAmPm && Format == ClockFormat.TwelveHour
                ? (isPm ? FloweryLocalization.GetStringInternal("Clock_PM") : FloweryLocalization.GetStringInternal("Clock_AM"))
                : "";

            // Build display text
            var timeString = ShowSeconds
                ? $"{hours:D2}{Separator}{minutes:D2}{Separator}{seconds:D2}"
                : $"{hours:D2}{Separator}{minutes:D2}";

            if (LabelFormat == ClockLabelFormat.Short)
            {
                var hShort = FloweryLocalization.GetStringInternal("Clock_Hours_Short");
                var mShort = FloweryLocalization.GetStringInternal("Clock_Minutes_Short");
                var sShort = FloweryLocalization.GetStringInternal("Clock_Seconds_Short");
                timeString = ShowSeconds
                    ? $"{hours}{hShort} {minutes}{mShort} {seconds}{sShort}"
                    : $"{hours}{hShort} {minutes}{mShort}";
            }
            else if (LabelFormat == ClockLabelFormat.Long)
            {
                var hourLabel = hours == 1
                    ? FloweryLocalization.GetStringInternal("Clock_Hour")
                    : FloweryLocalization.GetStringInternal("Clock_Hours");
                var minuteLabel = minutes == 1
                    ? FloweryLocalization.GetStringInternal("Clock_Minute")
                    : FloweryLocalization.GetStringInternal("Clock_Minutes");
                var secondLabel = seconds == 1
                    ? FloweryLocalization.GetStringInternal("Clock_Second")
                    : FloweryLocalization.GetStringInternal("Clock_Seconds");
                timeString = ShowSeconds
                    ? $"{hours} {hourLabel} {minutes} {minuteLabel} {seconds} {secondLabel}"
                    : $"{hours} {hourLabel} {minutes} {minuteLabel}";
            }

            if (!string.IsNullOrEmpty(AmPmText))
            {
                timeString = $"{timeString} {AmPmText}";
            }

            DisplayText = timeString;
        }

        #endregion

        #region Scaling Properties

        public static readonly StyledProperty<double> ScaledFontSizeProperty =
            AvaloniaProperty.Register<DaisyClock, double>(nameof(ScaledFontSize), BaseFontSize);

        /// <summary>
        /// Gets the scaled font size for the clock digits. Automatically updated by FloweryScaleManager.
        /// </summary>
        public double ScaledFontSize
        {
            get => GetValue(ScaledFontSizeProperty);
            private set => SetValue(ScaledFontSizeProperty, value);
        }

        public static readonly StyledProperty<double> ScaledSeparatorFontSizeProperty =
            AvaloniaProperty.Register<DaisyClock, double>(nameof(ScaledSeparatorFontSize), BaseSeparatorFontSize);

        /// <summary>
        /// Gets the scaled font size for the separator. Automatically updated by FloweryScaleManager.
        /// </summary>
        public double ScaledSeparatorFontSize
        {
            get => GetValue(ScaledSeparatorFontSizeProperty);
            private set => SetValue(ScaledSeparatorFontSizeProperty, value);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            ScaledFontSize = FloweryScaleManager.ApplyScale(BaseFontSize, 16.0, scaleFactor);
            ScaledSeparatorFontSize = FloweryScaleManager.ApplyScale(BaseSeparatorFontSize, 12.0, scaleFactor);
        }

        #endregion
    }
}
