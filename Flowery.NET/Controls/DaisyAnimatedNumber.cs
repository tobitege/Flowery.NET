using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Flowery.Effects;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A numeric text display that animates value changes with a slide transition.
    /// </summary>
    public class DaisyAnimatedNumber : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyAnimatedNumber);

        private const double BaseTextFontSize = 16.0;

        private TextBlock? _prevText;
        private TextBlock? _currentText;
        private TranslateTransform? _prevTransform;
        private TranslateTransform? _currentTransform;
        private int _lastValue;
        private CancellationTokenSource? _cts;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        /// <summary>
        /// Defines the <see cref="Value"/> property.
        /// </summary>
        public static readonly StyledProperty<int> ValueProperty =
            AvaloniaProperty.Register<DaisyAnimatedNumber, int>(nameof(Value), 0);

        /// <summary>
        /// Gets or sets the value displayed by the control.
        /// </summary>
        public int Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="MinDigits"/> property.
        /// </summary>
        public static readonly StyledProperty<int> MinDigitsProperty =
            AvaloniaProperty.Register<DaisyAnimatedNumber, int>(nameof(MinDigits), 0);

        /// <summary>
        /// Gets or sets the minimum digit count (pads with leading zeros).
        /// </summary>
        public int MinDigits
        {
            get => GetValue(MinDigitsProperty);
            set => SetValue(MinDigitsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Duration"/> property.
        /// </summary>
        public static readonly StyledProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.Register<DaisyAnimatedNumber, TimeSpan>(nameof(Duration), TimeSpan.FromMilliseconds(250));

        /// <summary>
        /// Gets or sets the animation duration.
        /// </summary>
        public TimeSpan Duration
        {
            get => GetValue(DurationProperty);
            set => SetValue(DurationProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="SlideDistance"/> property.
        /// </summary>
        public static readonly StyledProperty<double> SlideDistanceProperty =
            AvaloniaProperty.Register<DaisyAnimatedNumber, double>(nameof(SlideDistance), 18.0);

        /// <summary>
        /// Gets or sets the slide distance used during transition.
        /// </summary>
        public double SlideDistance
        {
            get => GetValue(SlideDistanceProperty);
            set => SetValue(SlideDistanceProperty, value);
        }

        static DaisyAnimatedNumber()
        {
            ValueProperty.Changed.AddClassHandler<DaisyAnimatedNumber>((s, e) => s.OnValueChanged(e));
            MinDigitsProperty.Changed.AddClassHandler<DaisyAnimatedNumber>((s, _) => s.RefreshText());
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _prevText = e.NameScope.Find<TextBlock>("PART_PreviousText");
            _currentText = e.NameScope.Find<TextBlock>("PART_CurrentText");

            if (_prevText != null)
            {
                _prevTransform = new TranslateTransform();
                _prevText.RenderTransform = _prevTransform;
                _prevText.Opacity = 0;
            }

            if (_currentText != null)
            {
                _currentTransform = new TranslateTransform();
                _currentText.RenderTransform = _currentTransform;
                _currentText.Opacity = 1;
            }

            _lastValue = Value;
            RefreshText();
        }

        private void RefreshText()
        {
            var text = Format(Value);
            if (_currentText != null)
            {
                _currentText.Text = text;
            }
            if (_prevText != null)
            {
                _prevText.Text = text;
                _prevText.Opacity = 0;
            }
        }

        private async void OnValueChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_currentText == null || _prevText == null || _currentTransform == null || _prevTransform == null)
            {
                _lastValue = Value;
                return;
            }

            var oldValue = _lastValue;
            var newValue = Value;
            if (oldValue == newValue)
                return;

            _cts?.Cancel();
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            var ct = _cts.Token;

            // Update _lastValue IMMEDIATELY so subsequent rapid changes use correct reference
            // This fixes animation direction bugs when buttons are pressed rapidly
            _lastValue = newValue;

            var isIncreasing = newValue > oldValue;
            var distance = SlideDistance;

            _prevText.Text = Format(oldValue);
            _currentText.Text = Format(newValue);

            _prevText.Opacity = 1;
            _currentText.Opacity = 0;

            _prevTransform.Y = 0;
            _currentTransform.Y = isIncreasing ? distance : -distance;

            var easing = new CubicEaseOut();

            try
            {
                await AnimationHelper.AnimateAsync(
                    t =>
                    {
                        _prevText.Opacity = 1.0 - t;
                        _currentText.Opacity = t;

                        _prevTransform.Y = AnimationHelper.Lerp(0, isIncreasing ? -distance : distance, t);
                        _currentTransform.Y = AnimationHelper.Lerp(isIncreasing ? distance : -distance, 0, t);
                    },
                    Duration,
                    easing,
                    ct: ct);
            }
            catch (OperationCanceledException)
            {
                // Animation cancelled by new value change - _lastValue already updated above
                return;
            }

            if (ct.IsCancellationRequested) return;

            _prevText.Opacity = 0;
            _currentText.Opacity = 1;
            _prevTransform.Y = 0;
            _currentTransform.Y = 0;
        }

        private string Format(int value)
        {
            var text = value.ToString();
            var minDigits = MinDigits;
            if (minDigits > 0)
            {
                text = text.PadLeft(minDigits, '0');
            }
            return text;
        }
    }
}
