using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum CountdownClockUnit
    {
        None,
        Hours,
        Minutes,
        Seconds
    }

    /// <summary>
    /// A countdown control that displays a numeric value with optional countdown animation.
    /// Includes accessibility support for screen readers via the AccessibleText attached property.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyCountdown : TemplatedControl, IScalableControl
    {
        private const string DefaultAccessibleText = "Countdown";
        private const double BaseTextFontSize = 32.0;

        protected override Type StyleKeyOverride => typeof(DaisyCountdown);

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 20.0, scaleFactor);
        }

        private DispatcherTimer? _timer;

        static DaisyCountdown()
        {
            DaisyAccessibility.SetupAccessibility<DaisyCountdown>(DefaultAccessibleText);
            AutomationProperties.LiveSettingProperty.OverrideDefaultValue<DaisyCountdown>(AutomationLiveSetting.Polite);
        }

        public static readonly StyledProperty<int> ValueProperty =
            AvaloniaProperty.Register<DaisyCountdown, int>(nameof(Value), 0, coerce: CoerceValue);

        public static readonly StyledProperty<int> DigitsProperty =
            AvaloniaProperty.Register<DaisyCountdown, int>(nameof(Digits), 1, coerce: CoerceDigits);

        public static readonly StyledProperty<bool> IsCountingDownProperty =
            AvaloniaProperty.Register<DaisyCountdown, bool>(nameof(IsCountingDown), false);

        public static readonly StyledProperty<TimeSpan> IntervalProperty =
            AvaloniaProperty.Register<DaisyCountdown, TimeSpan>(nameof(Interval), TimeSpan.FromSeconds(1));

        public static readonly StyledProperty<bool> LoopProperty =
            AvaloniaProperty.Register<DaisyCountdown, bool>(nameof(Loop), false);

        public static readonly StyledProperty<int> LoopFromProperty =
            AvaloniaProperty.Register<DaisyCountdown, int>(nameof(LoopFrom), 59, coerce: CoerceValue);

        public static readonly StyledProperty<CountdownClockUnit> ClockUnitProperty =
            AvaloniaProperty.Register<DaisyCountdown, CountdownClockUnit>(nameof(ClockUnit), CountdownClockUnit.None);

        /// <summary>
        /// Gets or sets the size of the countdown display.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyCountdown, DaisySize>(nameof(Size), DaisySize.Medium);

        public static readonly DirectProperty<DaisyCountdown, string> DisplayValueProperty =
            AvaloniaProperty.RegisterDirect<DaisyCountdown, string>(
                nameof(DisplayValue),
                o => o.DisplayValue);

        private string _displayValue = "0";

        public int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public int Digits
        {
            get => GetValue(DigitsProperty);
            set => SetValue(DigitsProperty, value);
        }

        public bool IsCountingDown
        {
            get => GetValue(IsCountingDownProperty);
            set => SetValue(IsCountingDownProperty, value);
        }

        public TimeSpan Interval
        {
            get => GetValue(IntervalProperty);
            set => SetValue(IntervalProperty, value);
        }

        public bool Loop
        {
            get => GetValue(LoopProperty);
            set => SetValue(LoopProperty, value);
        }

        public int LoopFrom
        {
            get => GetValue(LoopFromProperty);
            set => SetValue(LoopFromProperty, value);
        }

        public CountdownClockUnit ClockUnit
        {
            get => GetValue(ClockUnitProperty);
            set => SetValue(ClockUnitProperty, value);
        }

        /// <summary>
        /// Gets or sets the display size.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public string DisplayValue
        {
            get => _displayValue;
            private set => SetAndRaise(DisplayValueProperty, ref _displayValue, value);
        }

        /// <summary>
        /// Gets or sets the accessible text announced by screen readers.
        /// Default is "Countdown". The current value and unit are automatically appended.
        /// </summary>
        public string? AccessibleText
        {
            get => DaisyAccessibility.GetAccessibleText(this);
            set => DaisyAccessibility.SetAccessibleText(this, value);
        }

        public event EventHandler? CountdownCompleted;

        private static int CoerceValue(AvaloniaObject obj, int value)
        {
            return Math.Max(0, Math.Min(999, value));
        }

        private static int CoerceDigits(AvaloniaObject obj, int value)
        {
            return Math.Max(1, Math.Min(3, value));
        }

        public DaisyCountdown()
        {
            UpdateDisplayValue();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty || change.Property == DigitsProperty)
            {
                UpdateDisplayValue();
            }
            else if (change.Property == IsCountingDownProperty)
            {
                UpdateTimerState();
            }
            else if (change.Property == ClockUnitProperty)
            {
                UpdateTimerState();
                if (ClockUnit != CountdownClockUnit.None)
                {
                    UpdateClockValue();
                }
            }
            else if (change.Property == IntervalProperty)
            {
                if (_timer != null)
                {
                    _timer.Interval = Interval;
                }
            }
        }

        private void UpdateDisplayValue()
        {
            var format = Digits switch
            {
                2 => "D2",
                3 => "D3",
                _ => "D1"
            };
            DisplayValue = Value.ToString(format);
        }

        private void UpdateTimerState()
        {
            bool needsTimer = IsCountingDown || ClockUnit != CountdownClockUnit.None;

            if (needsTimer && _timer == null)
            {
                StartTimer();
            }
            else if (!needsTimer && _timer != null)
            {
                StopTimer();
            }
        }

        private void StartTimer()
        {
            StopTimer();
            _timer = new DispatcherTimer
            {
                Interval = Interval
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void StopTimer()
        {
            if (_timer != null)
            {
                _timer.Tick -= OnTimerTick;
                _timer.Stop();
                _timer = null;
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (ClockUnit != CountdownClockUnit.None)
            {
                UpdateClockValue();
            }
            else if (IsCountingDown)
            {
                if (Value > 0)
                {
                    Value--;
                }
                else
                {
                    if (Loop)
                    {
                        Value = LoopFrom;
                    }
                    else
                    {
                        IsCountingDown = false;
                        CountdownCompleted?.Invoke(this, EventArgs.Empty);
                    }
                }
            }
        }

        private void UpdateClockValue()
        {
            var now = DateTime.Now;
            Value = ClockUnit switch
            {
                CountdownClockUnit.Hours => now.Hour,
                CountdownClockUnit.Minutes => now.Minute,
                CountdownClockUnit.Seconds => now.Second,
                _ => Value
            };
        }

        public void Start()
        {
            IsCountingDown = true;
        }

        public void Stop()
        {
            IsCountingDown = false;
        }

        public void Reset(int value = 59)
        {
            Value = value;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            StopTimer();
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DaisyCountdownAutomationPeer(this);
        }
    }

    /// <summary>
    /// AutomationPeer for DaisyCountdown that exposes it as a text element to assistive technologies.
    /// Uses live region to announce value changes.
    /// </summary>
    internal class DaisyCountdownAutomationPeer : ControlAutomationPeer
    {
        private const string DefaultAccessibleText = "Countdown";

        public DaisyCountdownAutomationPeer(DaisyCountdown owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Text;
        }

        protected override string GetClassNameCore()
        {
            return "DaisyCountdown";
        }

        protected override string? GetNameCore()
        {
            var countdown = (DaisyCountdown)Owner;
            var text = DaisyAccessibility.GetEffectiveAccessibleText(countdown, DefaultAccessibleText);
            var unitText = countdown.ClockUnit switch
            {
                CountdownClockUnit.Hours => "hours",
                CountdownClockUnit.Minutes => "minutes",
                CountdownClockUnit.Seconds => "seconds",
                _ => ""
            };

            if (!string.IsNullOrEmpty(unitText))
            {
                return $"{text}: {countdown.Value} {unitText}";
            }
            return $"{text}: {countdown.Value}";
        }

        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
    }
}
