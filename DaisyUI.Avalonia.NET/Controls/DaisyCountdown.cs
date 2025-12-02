using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Avalonia.Threading;

namespace DaisyUI.Avalonia.Controls
{
    public enum CountdownClockUnit
    {
        None,
        Hours,
        Minutes,
        Seconds
    }

    public class DaisyCountdown : TemplatedControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyCountdown);

        private DispatcherTimer? _timer;

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

        public string DisplayValue
        {
            get => _displayValue;
            private set => SetAndRaise(DisplayValueProperty, ref _displayValue, value);
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
    }
}
