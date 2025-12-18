using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Animation.Easings;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Services;
using Flowery.Effects;

namespace Flowery.Controls
{
    /// <summary>
    /// A control that displays a numeric value with a smooth scrolling animation for individual digits.
    /// Inspired by SmoothUI's Number Flow. Uses a two-element per-digit animation (prev/next) where
    /// the previous value slides out and the new value slides in from the opposite direction.
    /// </summary>
    /// <remarks>
    /// Set <see cref="ShowControls"/> to true for built-in +/- buttons (SmoothUI style).
    /// Set <see cref="ShowDigitBoxes"/> to true for individual digit box styling.
    /// </remarks>
    public class DaisyNumberFlow : TemplatedControl, IScalableControl
    {
        private const string DefaultAccessibleText = "Number Flow";
        /// <summary>
        /// Base font size matching DaisySizeLargeFontSize token (18).
        /// NumberFlow is a display component, so defaults are larger than typical inputs.
        /// </summary>
        private const double BaseTextFontSize = 18.0;

        /// <summary>
        /// Line height multiplier for digit cells (relative to FontSize).
        /// </summary>
        internal const double LineHeightMultiplier = 1.4;

        /// <summary>
        /// Multiplier for digit box height relative to FontSize.
        /// </summary>
        internal const double DigitBoxHeightMultiplier = 2.2;

        protected override Type StyleKeyOverride => typeof(DaisyNumberFlow);

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        static DaisyNumberFlow()
        {
            DaisyAccessibility.SetupAccessibility<DaisyNumberFlow>(DefaultAccessibleText);
            // Make control focusable by default for keyboard navigation
            FocusableProperty.OverrideDefaultValue<DaisyNumberFlow>(true);
        }

        public DaisyNumberFlow()
        {
            IncrementCommand = new SimpleCommand(() => Increment(), () => CanIncrement);
            DecrementCommand = new SimpleCommand(() => Decrement(), () => CanDecrement);
        }

        private DaisyButton? _incrementButton;
        private DaisyButton? _decrementButton;
        private ItemsControl? _partsItemsControl;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Detach old handlers
            if (_incrementButton != null)
            {
                _incrementButton.RemoveHandler(PointerPressedEvent, (EventHandler<PointerPressedEventArgs>)OnIncrementPointerPressed);
                _incrementButton.RemoveHandler(PointerReleasedEvent, (EventHandler<PointerReleasedEventArgs>)OnButtonPointerReleased);
                _incrementButton.RemoveHandler(PointerCaptureLostEvent, (EventHandler<PointerCaptureLostEventArgs>)OnButtonPointerCaptureLost);
                _incrementButton.RemoveHandler(KeyDownEvent, (EventHandler<KeyEventArgs>)OnIncrementButtonKeyDown);
            }
            if (_decrementButton != null)
            {
                _decrementButton.RemoveHandler(PointerPressedEvent, (EventHandler<PointerPressedEventArgs>)OnDecrementPointerPressed);
                _decrementButton.RemoveHandler(PointerReleasedEvent, (EventHandler<PointerReleasedEventArgs>)OnButtonPointerReleased);
                _decrementButton.RemoveHandler(PointerCaptureLostEvent, (EventHandler<PointerCaptureLostEventArgs>)OnButtonPointerCaptureLost);
                _decrementButton.RemoveHandler(KeyDownEvent, (EventHandler<KeyEventArgs>)OnDecrementButtonKeyDown);
            }

            // Get template parts
            _incrementButton = e.NameScope.Find<DaisyButton>("PART_IncrementButton");
            _decrementButton = e.NameScope.Find<DaisyButton>("PART_DecrementButton");
            _partsItemsControl = e.NameScope.Find<ItemsControl>("PART_ItemsControl");

            // Attach pointer handlers - use Tunnel to fire before button's click processing
            if (_incrementButton != null)
            {
                _incrementButton.AddHandler(PointerPressedEvent, OnIncrementPointerPressed, RoutingStrategies.Tunnel);
                _incrementButton.AddHandler(PointerReleasedEvent, OnButtonPointerReleased, RoutingStrategies.Tunnel);
                _incrementButton.AddHandler(PointerCaptureLostEvent, OnButtonPointerCaptureLost, RoutingStrategies.Tunnel);
                _incrementButton.AddHandler(KeyDownEvent, OnIncrementButtonKeyDown, RoutingStrategies.Tunnel);
            }
            if (_decrementButton != null)
            {
                _decrementButton.AddHandler(PointerPressedEvent, OnDecrementPointerPressed, RoutingStrategies.Tunnel);
                _decrementButton.AddHandler(PointerReleasedEvent, OnButtonPointerReleased, RoutingStrategies.Tunnel);
                _decrementButton.AddHandler(PointerCaptureLostEvent, OnButtonPointerCaptureLost, RoutingStrategies.Tunnel);
                _decrementButton.AddHandler(KeyDownEvent, OnDecrementButtonKeyDown, RoutingStrategies.Tunnel);
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            // Handle digit selection when AllowDigitSelection is true
            if (!AllowDigitSelection) return;
            if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

            // Walk up the visual tree from the source to find an element with NumberFlowPartViewModel DataContext
            var source = e.Source as Control;
            while (source != null && source != this)
            {
                // Check for the ViewModel in DataContext (works with DataTemplate items)
                if (source.DataContext is NumberFlowPartViewModel partVm && partVm.DigitIndex >= 0)
                {
                    SelectDigit(partVm.DigitIndex);
                    e.Handled = true;
                    return;
                }

                // Also check Tag as fallback (for named containers)
                if (source is Border border && border.Tag != null)
                {
                    int digitIndex = -1;
                    if (border.Tag is int i)
                        digitIndex = i;
                    else if (border.Tag is long l)
                        digitIndex = (int)l;
                    else if (int.TryParse(border.Tag.ToString(), out var parsed))
                        digitIndex = parsed;

                    if (digitIndex >= 0)
                    {
                        SelectDigit(digitIndex);
                        e.Handled = true;
                        return;
                    }
                }

                source = source.GetVisualParent() as Control;
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);
            if (e.Handled) return;

            // Only handle keys when AllowDigitSelection is enabled
            if (!AllowDigitSelection) return;

            switch (e.Key)
            {
                case Key.Left:
                    // Move selection to higher digit (left = higher position)
                    MoveDigitSelection(1);
                    e.Handled = true;
                    break;

                case Key.Right:
                    // Move selection to lower digit (right = lower position)
                    MoveDigitSelection(-1);
                    e.Handled = true;
                    break;

                case Key.Up:
                    // Increment the selected digit
                    if (SelectedDigitIndex.HasValue && CanIncrement)
                    {
                        IncrementDigitAtPosition(SelectedDigitIndex.Value);
                        e.Handled = true;
                    }
                    break;

                case Key.Down:
                    // Decrement the selected digit
                    if (SelectedDigitIndex.HasValue && CanDecrement)
                    {
                        DecrementDigitAtPosition(SelectedDigitIndex.Value);
                        e.Handled = true;
                    }
                    break;

                // Numeric keys 0-9
                case Key.D0:
                case Key.NumPad0:
                    SetSelectedDigitTo(0);
                    e.Handled = true;
                    break;
                case Key.D1:
                case Key.NumPad1:
                    SetSelectedDigitTo(1);
                    e.Handled = true;
                    break;
                case Key.D2:
                case Key.NumPad2:
                    SetSelectedDigitTo(2);
                    e.Handled = true;
                    break;
                case Key.D3:
                case Key.NumPad3:
                    SetSelectedDigitTo(3);
                    e.Handled = true;
                    break;
                case Key.D4:
                case Key.NumPad4:
                    SetSelectedDigitTo(4);
                    e.Handled = true;
                    break;
                case Key.D5:
                case Key.NumPad5:
                    SetSelectedDigitTo(5);
                    e.Handled = true;
                    break;
                case Key.D6:
                case Key.NumPad6:
                    SetSelectedDigitTo(6);
                    e.Handled = true;
                    break;
                case Key.D7:
                case Key.NumPad7:
                    SetSelectedDigitTo(7);
                    e.Handled = true;
                    break;
                case Key.D8:
                case Key.NumPad8:
                    SetSelectedDigitTo(8);
                    e.Handled = true;
                    break;
                case Key.D9:
                case Key.NumPad9:
                    SetSelectedDigitTo(9);
                    e.Handled = true;
                    break;
            }
        }

        /// <summary>
        /// Moves the digit selection by the specified offset.
        /// Positive offset moves left (higher digit), negative moves right (lower digit).
        /// </summary>
        private void MoveDigitSelection(int offset)
        {
            var digitCount = GetDigitCount();
            if (digitCount == 0) return;

            int currentIndex = SelectedDigitIndex ?? 0;
            int newIndex = currentIndex + offset;

            // Clamp to valid range
            if (newIndex < 0) newIndex = 0;
            if (newIndex >= digitCount) newIndex = digitCount - 1;

            SelectDigit(newIndex);
        }

        /// <summary>
        /// Gets the number of digits in the current formatted value.
        /// </summary>
        private int GetDigitCount()
        {
            return Parts.Count(p => p.IsDigit);
        }

        /// <summary>
        /// Sets the selected digit to a specific value (0-9).
        /// Calculates the delta needed and applies it with correct animation direction.
        /// </summary>
        private void SetSelectedDigitTo(int newDigitValue)
        {
            if (!SelectedDigitIndex.HasValue) return;
            if (newDigitValue < 0 || newDigitValue > 9) return;

            int position = SelectedDigitIndex.Value;
            int currentDigitValue = GetDigitValueAtPosition(position);

            if (currentDigitValue == newDigitValue) return;

            // Calculate delta to change just this digit
            int delta = newDigitValue - currentDigitValue;
            var multiplier = (decimal)Math.Pow(10, position);
            var newValue = (Value ?? 0) + (delta * multiplier);

            // Apply bounds (this respects the existing wrap logic)
            newValue = ApplyBoundsWithWrap(newValue, delta > 0);
            Value = newValue;
        }

        /// <summary>
        /// Gets the current digit value at the specified position (0 = rightmost).
        /// </summary>
        private int GetDigitValueAtPosition(int position)
        {
            var currentValue = Math.Abs(Value ?? 0);
            var divisor = (decimal)Math.Pow(10, position);
            // Extract the digit at this position
            return (int)(Math.Floor(currentValue / divisor) % 10);
        }

        private void OnIncrementPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Guard against double-firing from routing
            if (e.Handled) return;
            if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
            {
                StartIncrement();
                e.Handled = true;
            }
        }

        private void OnDecrementPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // Guard against double-firing from routing
            if (e.Handled) return;
            if (e.GetCurrentPoint(sender as Visual).Properties.IsLeftButtonPressed)
            {
                StartDecrement();
                e.Handled = true;
            }
        }

        private void OnButtonPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            StopRepeat();
        }

        private void OnButtonPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            StopRepeat();
        }

        private void OnIncrementButtonKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                if (CanIncrement)
                {
                    IncrementSelected();
                    e.Handled = true;
                }
            }
        }

        private void OnDecrementButtonKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space || e.Key == Key.Enter)
            {
                if (CanDecrement)
                {
                    DecrementSelected();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Called from the template when a digit container is tapped.
        /// </summary>
        internal void OnDigitTapped(int digitIndex)
        {
            if (AllowDigitSelection && digitIndex >= 0)
            {
                SelectDigit(digitIndex);
            }
        }

        /// <summary>
        /// Gets or sets the accessible text announced by screen readers.
        /// </summary>
        public string? AccessibleText
        {
            get => DaisyAccessibility.GetAccessibleText(this);
            set => DaisyAccessibility.SetAccessibleText(this, value);
        }

        /// <summary>
        /// Gets or sets the numeric value to display.
        /// </summary>
        public static readonly StyledProperty<decimal?> ValueProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, decimal?>(nameof(Value), 0m);

        /// <summary>
        /// Gets or sets the format string used to format the numeric value.
        /// </summary>
        public static readonly StyledProperty<string> FormatStringProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, string>(nameof(FormatString), "N0");

        /// <summary>
        /// Gets or sets the duration of the animation.
        /// </summary>
        public static readonly StyledProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(200));

        /// <summary>
        /// Gets or sets the easing function for the animation.
        /// </summary>
        public static readonly StyledProperty<Easing> EasingProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, Easing>(nameof(Easing), new CubicEaseOut());

        /// <summary>
        /// Gets or sets the prefix text displayed before the number.
        /// </summary>
        public static readonly StyledProperty<string?> PrefixProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, string?>(nameof(Prefix));

        /// <summary>
        /// Gets or sets the suffix text displayed after the number.
        /// </summary>
        public static readonly StyledProperty<string?> SuffixProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, string?>(nameof(Suffix));

        /// <summary>
        /// Gets or sets the culture used for formatting.
        /// </summary>
        public static readonly StyledProperty<CultureInfo> CultureProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, CultureInfo>(nameof(Culture), CultureInfo.InvariantCulture);

        /// <summary>
        /// Gets or sets whether to show individual boxes around each digit.
        /// </summary>
        public static readonly StyledProperty<bool> ShowDigitBoxesProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, bool>(nameof(ShowDigitBoxes), false);

        /// <summary>
        /// Gets or sets whether to show the built-in +/- control buttons.
        /// </summary>
        public static readonly StyledProperty<bool> ShowControlsProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, bool>(nameof(ShowControls), false);

        /// <summary>
        /// Gets or sets the minimum allowed value.
        /// </summary>
        public static readonly StyledProperty<decimal?> MinimumProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, decimal?>(nameof(Minimum));

        /// <summary>
        /// Gets or sets the maximum allowed value.
        /// </summary>
        public static readonly StyledProperty<decimal?> MaximumProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, decimal?>(nameof(Maximum));

        /// <summary>
        /// Gets or sets the step value for increment/decrement operations.
        /// </summary>
        public static readonly StyledProperty<decimal> StepProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, decimal>(nameof(Step), 1m);

        /// <summary>
        /// Gets or sets whether the value wraps around when reaching Minimum/Maximum.
        /// When true, incrementing past Maximum wraps to Minimum, and decrementing below Minimum wraps to Maximum.
        /// </summary>
        public static readonly StyledProperty<bool> WrapAroundProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, bool>(nameof(WrapAround), false);

        /// <summary>
        /// Gets or sets the repeat interval for held buttons. Set to zero to disable repeat.
        /// Should be >= Duration to allow animations to complete before the next value change.
        /// </summary>
        public static readonly StyledProperty<TimeSpan> RepeatIntervalProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, TimeSpan>(nameof(RepeatInterval), TimeSpan.FromMilliseconds(200));

        /// <summary>
        /// Gets or sets the initial delay before repeat starts when a button is held.
        /// </summary>
        public static readonly StyledProperty<TimeSpan> RepeatDelayProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, TimeSpan>(nameof(RepeatDelay), TimeSpan.FromMilliseconds(400));

        /// <summary>
        /// Gets or sets whether digit selection is enabled (tap digits to select which one to control).
        /// </summary>
        public static readonly StyledProperty<bool> AllowDigitSelectionProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, bool>(nameof(AllowDigitSelection), false);

        /// <summary>
        /// Gets or sets the currently selected digit index (0 = rightmost digit).
        /// Null means default behavior (rightmost digit).
        /// </summary>
        public static readonly StyledProperty<int?> SelectedDigitIndexProperty =
            AvaloniaProperty.Register<DaisyNumberFlow, int?>(nameof(SelectedDigitIndex), null);

        public decimal? Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public string FormatString
        {
            get => GetValue(FormatStringProperty);
            set => SetValue(FormatStringProperty, value);
        }

        public TimeSpan Duration
        {
            get => GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        public Easing Easing
        {
            get => GetValue(EasingProperty);
            set => SetValue(EasingProperty, value);
        }

        public string? Prefix
        {
            get => GetValue(PrefixProperty);
            set => SetValue(PrefixProperty, value);
        }

        public string? Suffix
        {
            get => GetValue(SuffixProperty);
            set => SetValue(SuffixProperty, value);
        }

        public CultureInfo Culture
        {
            get => GetValue(CultureProperty);
            set => SetValue(CultureProperty, value);
        }

        public bool ShowDigitBoxes
        {
            get => GetValue(ShowDigitBoxesProperty);
            set => SetValue(ShowDigitBoxesProperty, value);
        }

        public bool ShowControls
        {
            get => GetValue(ShowControlsProperty);
            set => SetValue(ShowControlsProperty, value);
        }

        public decimal? Minimum
        {
            get => GetValue(MinimumProperty);
            set => SetValue(MinimumProperty, value);
        }

        public decimal? Maximum
        {
            get => GetValue(MaximumProperty);
            set => SetValue(MaximumProperty, value);
        }

        public decimal Step
        {
            get => GetValue(StepProperty);
            set => SetValue(StepProperty, value);
        }

        public bool WrapAround
        {
            get => GetValue(WrapAroundProperty);
            set => SetValue(WrapAroundProperty, value);
        }

        public TimeSpan RepeatInterval
        {
            get => GetValue(RepeatIntervalProperty);
            set => SetValue(RepeatIntervalProperty, value);
        }

        public TimeSpan RepeatDelay
        {
            get => GetValue(RepeatDelayProperty);
            set => SetValue(RepeatDelayProperty, value);
        }

        public bool AllowDigitSelection
        {
            get => GetValue(AllowDigitSelectionProperty);
            set => SetValue(AllowDigitSelectionProperty, value);
        }

        public int? SelectedDigitIndex
        {
            get => GetValue(SelectedDigitIndexProperty);
            set => SetValue(SelectedDigitIndexProperty, value);
        }

        /// <summary>
        /// Command to increment the value.
        /// </summary>
        public ICommand IncrementCommand { get; }

        /// <summary>
        /// Command to decrement the value.
        /// </summary>
        public ICommand DecrementCommand { get; }

        // With WrapAround, buttons are always enabled when both Min and Max are set
        private bool CanIncrement => WrapAround ? (Minimum.HasValue && Maximum.HasValue) || !Maximum.HasValue || (Value ?? 0) < Maximum.Value
                                                : !Maximum.HasValue || (Value ?? 0) < Maximum.Value;
        private bool CanDecrement => WrapAround ? (Minimum.HasValue && Maximum.HasValue) || !Minimum.HasValue || (Value ?? 0) > Minimum.Value
                                                : !Minimum.HasValue || (Value ?? 0) > Minimum.Value;

        // Public bindable properties for button IsEnabled binding
        public static readonly DirectProperty<DaisyNumberFlow, bool> CanIncrementValueProperty =
            AvaloniaProperty.RegisterDirect<DaisyNumberFlow, bool>(nameof(CanIncrementValue), o => o.CanIncrementValue);

        public static readonly DirectProperty<DaisyNumberFlow, bool> CanDecrementValueProperty =
            AvaloniaProperty.RegisterDirect<DaisyNumberFlow, bool>(nameof(CanDecrementValue), o => o.CanDecrementValue);

        private bool _canIncrementValue = true;
        private bool _canDecrementValue = true;

        /// <summary>
        /// Gets whether the value can be incremented (for button enabled state binding).
        /// </summary>
        public bool CanIncrementValue
        {
            get => _canIncrementValue;
            private set => SetAndRaise(CanIncrementValueProperty, ref _canIncrementValue, value);
        }

        /// <summary>
        /// Gets whether the value can be decremented (for button enabled state binding).
        /// </summary>
        public bool CanDecrementValue
        {
            get => _canDecrementValue;
            private set => SetAndRaise(CanDecrementValueProperty, ref _canDecrementValue, value);
        }

        private void UpdateCanExecuteStates()
        {
            CanIncrementValue = CanIncrement;
            CanDecrementValue = CanDecrement;
            (IncrementCommand as SimpleCommand)?.RaiseCanExecuteChanged();
            (DecrementCommand as SimpleCommand)?.RaiseCanExecuteChanged();
        }

        private DispatcherTimer? _repeatTimer;
        private bool _isIncrementing;
        private bool _isInitialRepeat;

        /// <summary>
        /// Starts the increment action on pointer pressed with repeat support.
        /// </summary>
        public void StartIncrement()
        {
            if (!CanIncrement) return;
            _isIncrementing = true;
            IncrementSelected();
            StartRepeatTimer();
        }

        /// <summary>
        /// Starts the decrement action on pointer pressed with repeat support.
        /// </summary>
        public void StartDecrement()
        {
            if (!CanDecrement) return;
            _isIncrementing = false;
            DecrementSelected();
            StartRepeatTimer();
        }

        /// <summary>
        /// Stops the repeat timer when pointer is released.
        /// </summary>
        public void StopRepeat()
        {
            _repeatTimer?.Stop();
            _repeatTimer = null;
        }

        private void StartRepeatTimer()
        {
            if (RepeatInterval <= TimeSpan.Zero) return;

            _isInitialRepeat = true;
            _repeatTimer?.Stop();
            _repeatTimer = new DispatcherTimer
            {
                Interval = RepeatDelay
            };
            _repeatTimer.Tick += OnRepeatTimerTick;
            _repeatTimer.Start();
        }

        private void OnRepeatTimerTick(object? sender, EventArgs e)
        {
            if (_repeatTimer == null) return;

            // After initial delay, switch to faster repeat interval
            if (_isInitialRepeat)
            {
                _isInitialRepeat = false;
                _repeatTimer.Interval = RepeatInterval;
            }

            if (_isIncrementing)
            {
                if (CanIncrement)
                    IncrementSelected();
                else
                    StopRepeat();
            }
            else
            {
                if (CanDecrement)
                    DecrementSelected();
                else
                    StopRepeat();
            }
        }

        /// <summary>
        /// Selects a digit at the given index (0 = rightmost).
        /// </summary>
        public void SelectDigit(int index)
        {
            if (!AllowDigitSelection) return;
            SelectedDigitIndex = index;
            UpdateSelectedDigitVisuals();
        }

        protected override void OnGotFocus(GotFocusEventArgs e)
        {
            base.OnGotFocus(e);

            // Auto-select the rightmost digit when control receives focus (if digit selection is enabled)
            if (AllowDigitSelection && !SelectedDigitIndex.HasValue)
            {
                SelectDigit(0);
            }
        }

        /// <summary>
        /// Increments the selected digit (or the whole value if no digit is selected).
        /// </summary>
        private void IncrementSelected()
        {
            if (AllowDigitSelection && SelectedDigitIndex.HasValue)
            {
                IncrementDigitAtPosition(SelectedDigitIndex.Value);
            }
            else
            {
                Increment();
            }
        }

        /// <summary>
        /// Decrements the selected digit (or the whole value if no digit is selected).
        /// </summary>
        private void DecrementSelected()
        {
            if (AllowDigitSelection && SelectedDigitIndex.HasValue)
            {
                DecrementDigitAtPosition(SelectedDigitIndex.Value);
            }
            else
            {
                Decrement();
            }
        }

        /// <summary>
        /// Increments the digit at the given position (0 = rightmost/ones place).
        /// Always adds 10^position - when digit is 9, this naturally wraps to 0 and carries.
        /// </summary>
        private void IncrementDigitAtPosition(int position)
        {
            var currentValue = Value ?? 0;
            var divisor = (decimal)Math.Pow(10, position);
            // Always increment by divisor - this handles both normal case and 9→0 wrap with carry
            var newValue = currentValue + divisor;

            newValue = ApplyBoundsWithWrap(newValue, isIncrement: true);
            Value = newValue;
        }

        /// <summary>
        /// Decrements the digit at the given position (0 = rightmost/ones place).
        /// Always subtracts 10^position - when digit is 0, this naturally wraps to 9 and borrows from higher digit.
        /// </summary>
        private void DecrementDigitAtPosition(int position)
        {
            var currentValue = Value ?? 0;
            var divisor = (decimal)Math.Pow(10, position);
            // Always decrement by divisor - this handles both normal case and 0→9 wrap with borrow
            var newValue = currentValue - divisor;

            newValue = ApplyBoundsWithWrap(newValue, isIncrement: false);
            Value = newValue;
        }

        /// <summary>
        /// Applies Minimum/Maximum bounds, with optional wrap-around behavior.
        /// </summary>
        private decimal ApplyBoundsWithWrap(decimal newValue, bool isIncrement)
        {
            if (isIncrement && Maximum.HasValue && newValue > Maximum.Value)
            {
                // Exceeded maximum
                if (WrapAround && Minimum.HasValue)
                    return Minimum.Value;
                return Maximum.Value;
            }

            if (!isIncrement && Minimum.HasValue && newValue < Minimum.Value)
            {
                // Went below minimum
                if (WrapAround && Maximum.HasValue)
                    return Maximum.Value;
                return Minimum.Value;
            }

            return newValue;
        }

        /// <summary>
        /// Increments the value by Step.
        /// </summary>
        public void Increment()
        {
            var newValue = (Value ?? 0) + Step;
            newValue = ApplyBoundsWithWrap(newValue, isIncrement: true);
            Value = newValue;
        }

        /// <summary>
        /// Decrements the value by Step.
        /// </summary>
        public void Decrement()
        {
            var newValue = (Value ?? 0) - Step;
            newValue = ApplyBoundsWithWrap(newValue, isIncrement: false);
            Value = newValue;
        }

        // Internal property to hold the formatted parts for the template
        public static readonly DirectProperty<DaisyNumberFlow, ObservableCollection<NumberFlowPartViewModel>> PartsProperty =
            AvaloniaProperty.RegisterDirect<DaisyNumberFlow, ObservableCollection<NumberFlowPartViewModel>>(
                nameof(Parts),
                o => o.Parts);

        private ObservableCollection<NumberFlowPartViewModel> _parts = new();
        private string _previousFormatted = "";
        private decimal? _previousValue;

        public ObservableCollection<NumberFlowPartViewModel> Parts
        {
            get => _parts;
            private set => SetAndRaise(PartsProperty, ref _parts, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty)
            {
                UpdateParts(false);
                UpdateCanExecuteStates();
            }
            else if (change.Property == FormatStringProperty || change.Property == CultureProperty)
            {
                UpdateParts(true);
            }
            else if (change.Property == MinimumProperty || change.Property == MaximumProperty)
            {
                UpdateCanExecuteStates();
            }
            else if (change.Property == SelectedDigitIndexProperty || change.Property == AllowDigitSelectionProperty)
            {
                UpdateSelectedDigitVisuals();
            }
        }

        private void UpdateSelectedDigitVisuals()
        {
            if (!AllowDigitSelection)
            {
                // Clear all selection visuals
                foreach (var part in Parts)
                {
                    part.IsSelected = false;
                }
                return;
            }

            // Calculate digit indices from right (0 = rightmost digit)
            var digitParts = Parts.Where(p => p.IsDigit).Reverse().ToList();
            for (int i = 0; i < digitParts.Count; i++)
            {
                digitParts[i].IsSelected = (SelectedDigitIndex == i);
            }
        }

        private void UpdateParts(bool forceReset)
        {
            if (Value == null)
            {
                _previousFormatted = "";
                _previousValue = null;
                Parts.Clear();
                return;
            }

            string newFormatted = Value.Value.ToString(FormatString, Culture);
            bool isIncreasing = _previousValue.HasValue && Value.Value > _previousValue.Value;

            if (forceReset || _previousFormatted.Length == 0)
            {
                // Initial population or format change - no animation
                Parts.Clear();
                foreach (char c in newFormatted)
                {
                    Parts.Add(new NumberFlowPartViewModel(c, char.IsDigit(c)));
                }
                AssignDigitIndices();
                UpdateSelectedDigitVisuals();
                _previousFormatted = newFormatted;
                _previousValue = Value;
                return;
            }

            string oldStr = _previousFormatted;
            string newStr = newFormatted;

            // Handle structural changes (different length or separator positions changed)
            if (oldStr.Length != newStr.Length)
            {
                RebuildPartsWithAnimation(oldStr, newStr, isIncreasing);
            }
            else
            {
                // Same length - update each position with animation
                for (int i = 0; i < Parts.Count && i < newStr.Length; i++)
                {
                    Parts[i].Update(newStr[i], isIncreasing, Duration, Easing);
                }
            }

            AssignDigitIndices();
            UpdateSelectedDigitVisuals();
            _previousFormatted = newFormatted;
            _previousValue = Value;
        }

        /// <summary>
        /// Assigns DigitIndex to each digit part (0 = rightmost digit).
        /// </summary>
        private void AssignDigitIndices()
        {
            int digitIndex = 0;
            // Iterate from right to left to assign indices
            for (int i = Parts.Count - 1; i >= 0; i--)
            {
                if (Parts[i].IsDigit)
                {
                    Parts[i].DigitIndex = digitIndex++;
                }
                else
                {
                    Parts[i].DigitIndex = -1;
                }
            }
        }

        private void RebuildPartsWithAnimation(string oldStr, string newStr, bool isIncreasing)
        {
            // When digit count changes, rebuild the parts list
            // For digits that exist in both, animate them
            // For new digits, fade them in; for removed digits, fade them out

            // Simple approach: if structure changed significantly, rebuild with fade
            Parts.Clear();
            foreach (char c in newStr)
            {
                var part = new NumberFlowPartViewModel(c, char.IsDigit(c));
                // Start faded out then fade in
                part.StartFadeIn(Duration, Easing);
                Parts.Add(part);
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            StopRepeat();
        }
    }

    /// <summary>
    /// ViewModel for a single part of the NumberFlow, using a two-element animation approach.
    /// Each part shows both the previous and current character, animating between them.
    /// </summary>
    public class NumberFlowPartViewModel : AvaloniaObject
    {
        public static readonly DirectProperty<NumberFlowPartViewModel, char> CharacterProperty =
            AvaloniaProperty.RegisterDirect<NumberFlowPartViewModel, char>(nameof(Character), o => o.Character);

        private char _character;
        /// <summary>
        /// The current character to display.
        /// </summary>
        public char Character
        {
            get => _character;
            private set => SetAndRaise(CharacterProperty, ref _character, value);
        }

        public static readonly DirectProperty<NumberFlowPartViewModel, char> PreviousCharacterProperty =
            AvaloniaProperty.RegisterDirect<NumberFlowPartViewModel, char>(nameof(PreviousCharacter), o => o.PreviousCharacter);

        private char _previousCharacter;
        /// <summary>
        /// The previous character (used during animation).
        /// </summary>
        public char PreviousCharacter
        {
            get => _previousCharacter;
            private set => SetAndRaise(PreviousCharacterProperty, ref _previousCharacter, value);
        }

        public static readonly DirectProperty<NumberFlowPartViewModel, bool> IsDigitProperty =
            AvaloniaProperty.RegisterDirect<NumberFlowPartViewModel, bool>(nameof(IsDigit), o => o.IsDigit);

        private bool _isDigit;
        public bool IsDigit
        {
            get => _isDigit;
            private set => SetAndRaise(IsDigitProperty, ref _isDigit, value);
        }

        public static readonly DirectProperty<NumberFlowPartViewModel, int> DigitIndexProperty =
            AvaloniaProperty.RegisterDirect<NumberFlowPartViewModel, int>(nameof(DigitIndex), o => o.DigitIndex);

        private int _digitIndex = -1;
        /// <summary>
        /// The digit index (0 = rightmost/ones place). -1 for non-digit characters.
        /// </summary>
        public int DigitIndex
        {
            get => _digitIndex;
            set => SetAndRaise(DigitIndexProperty, ref _digitIndex, value);
        }

        public static readonly DirectProperty<NumberFlowPartViewModel, bool> IsSelectedProperty =
            AvaloniaProperty.RegisterDirect<NumberFlowPartViewModel, bool>(nameof(IsSelected), o => o.IsSelected);

        private bool _isSelected;
        /// <summary>
        /// Whether this digit is currently selected for increment/decrement control.
        /// </summary>
        public bool IsSelected
        {
            get => _isSelected;
            set => SetAndRaise(IsSelectedProperty, ref _isSelected, value);
        }

        public static readonly DirectProperty<NumberFlowPartViewModel, double> CurrentOffsetProperty =
            AvaloniaProperty.RegisterDirect<NumberFlowPartViewModel, double>(nameof(CurrentOffset), o => o.CurrentOffset);

        private double _currentOffset;
        /// <summary>
        /// Y offset for current character (0 = visible, -1 or 1 = offscreen).
        /// </summary>
        public double CurrentOffset
        {
            get => _currentOffset;
            private set => SetAndRaise(CurrentOffsetProperty, ref _currentOffset, value);
        }

        public static readonly DirectProperty<NumberFlowPartViewModel, double> PreviousOffsetProperty =
            AvaloniaProperty.RegisterDirect<NumberFlowPartViewModel, double>(nameof(PreviousOffset), o => o.PreviousOffset);

        private double _previousOffset;
        /// <summary>
        /// Y offset for previous character (0 = visible, -1 or 1 = offscreen).
        /// </summary>
        public double PreviousOffset
        {
            get => _previousOffset;
            private set => SetAndRaise(PreviousOffsetProperty, ref _previousOffset, value);
        }

        public static readonly DirectProperty<NumberFlowPartViewModel, bool> IsCurrentlyAnimatingProperty =
            AvaloniaProperty.RegisterDirect<NumberFlowPartViewModel, bool>(nameof(IsCurrentlyAnimating), o => o.IsCurrentlyAnimating);

        private bool _isCurrentlyAnimating;
        /// <summary>
        /// Whether this part is currently animating.
        /// </summary>
        public bool IsCurrentlyAnimating
        {
            get => _isCurrentlyAnimating;
            private set => SetAndRaise(IsCurrentlyAnimatingProperty, ref _isCurrentlyAnimating, value);
        }

        public static readonly DirectProperty<NumberFlowPartViewModel, double> OpacityProperty =
            AvaloniaProperty.RegisterDirect<NumberFlowPartViewModel, double>(nameof(Opacity), o => o.Opacity);

        private double _opacity = 1.0;
        public double Opacity
        {
            get => _opacity;
            private set => SetAndRaise(OpacityProperty, ref _opacity, value);
        }

        private CancellationTokenSource? _cts;

        public NumberFlowPartViewModel(char initialChar, bool isDigit)
        {
            _character = initialChar;
            _previousCharacter = initialChar;
            _isDigit = isDigit;
            _currentOffset = 0;
            _previousOffset = -1; // Start offscreen so only current is visible initially
            _isCurrentlyAnimating = false;
        }

        /// <summary>
        /// Updates the character with slide animation.
        /// </summary>
        public void Update(char newChar, bool isIncreasing, TimeSpan duration, Easing easing)
        {
            if (Character == newChar) return;

            // Cancel any ongoing animation and dispose the old CTS
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            PreviousCharacter = Character;
            Character = newChar;
            IsDigit = char.IsDigit(newChar);

            // Animate: previous slides out, current slides in
            if (char.IsDigit(PreviousCharacter) && char.IsDigit(Character))
            {
                AnimateSlide(isIncreasing, duration, easing, _cts.Token);
            }
            else
            {
                // Non-digit change - just snap
                CurrentOffset = 0;
                PreviousOffset = isIncreasing ? -1 : 1;
            }
        }

        /// <summary>
        /// Starts a fade-in animation (for newly appearing parts).
        /// </summary>
        public void StartFadeIn(TimeSpan duration, Easing easing)
        {
            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            Opacity = 0;
            AnimateFadeIn(duration, easing, _cts.Token);
        }

        private async void AnimateSlide(bool isIncreasing, TimeSpan duration, Easing easing, CancellationToken ct)
        {
            try
            {
                IsCurrentlyAnimating = true;

                // Starting positions:
                // - Previous character is visible (offset 0)
                // - Current character is offscreen (offset = direction)
                PreviousOffset = 0;
                CurrentOffset = isIncreasing ? 1 : -1;

                await AnimationHelper.AnimateAsync(
                    progress =>
                    {
                        // Previous slides out (0 → -direction)
                        PreviousOffset = progress * (isIncreasing ? -1 : 1);
                        // Current slides in (direction → 0)
                        CurrentOffset = (1 - progress) * (isIncreasing ? 1 : -1);
                    },
                    duration,
                    easing,
                    AnimationHelper.DefaultSteps,
                    ct);

                // Animation complete - snap to final positions
                PreviousOffset = isIncreasing ? -1 : 1;
                CurrentOffset = 0;
                IsCurrentlyAnimating = false;
            }
            catch (OperationCanceledException)
            {
                // Animation was cancelled, expected when value changes mid-animation
            }
        }

        private async void AnimateFadeIn(TimeSpan duration, Easing easing, CancellationToken ct)
        {
            try
            {
                await AnimationHelper.AnimateAsync(
                    progress =>
                    {
                        Opacity = progress;
                    },
                    duration,
                    easing,
                    AnimationHelper.DefaultSteps,
                    ct);

                Opacity = 1.0;
            }
            catch (OperationCanceledException)
            {
                Opacity = 1.0;
            }
        }
    }
}
