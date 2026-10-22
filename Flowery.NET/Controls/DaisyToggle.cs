using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Helpers;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyToggleVariant
    {
        Default,
        Primary,
        Secondary,
        Accent,
        Success,
        Warning,
        Info,
        Error
    }

    /// <summary>
    /// A ToggleSwitch control styled after DaisyUI's Toggle component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyToggle : ToggleSwitch, IScalableControl
    {
        // A clearly visible CIE76 color difference. A luminance ratio such as WCAG 3:1 would change most variant
        // knobs in the bundled themes, because their colors share the track's lightness and differ in hue.
        private const double MinimumKnobDifference = 20;

        private Ellipse? _knob;
        private Border? _track;
        private Color? _knobStyleColor;
        private bool _isAdjustingKnob;
        private bool _isKnobContrastQueued;

        protected override Type StyleKeyOverride => typeof(DaisyToggle);

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_knob != null)
                _knob.PropertyChanged -= OnKnobOrTrackPropertyChanged;
            if (_track != null)
                _track.PropertyChanged -= OnKnobOrTrackPropertyChanged;

            _knob = e.NameScope.Find<Ellipse>("Knob");
            _track = e.NameScope.Find<Border>("SwitchArea");
            _knobStyleColor = (_knob?.Fill as ISolidColorBrush)?.Color;

            if (_knob != null)
                _knob.PropertyChanged += OnKnobOrTrackPropertyChanged;
            if (_track != null)
                _track.PropertyChanged += OnKnobOrTrackPropertyChanged;

            EnsureKnobContrast();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            // The adjusted color outlives the checked style; clearing it lets the unchecked knob color through.
            if (change.Property == IsCheckedProperty && IsChecked != true && _knob != null)
                _knob.ClearValue(Shape.FillProperty);
        }

        private void OnKnobOrTrackPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == Shape.FillProperty && !_isAdjustingKnob)
            {
                // The styles set the knob color (variant, checked state, theme); remember it before adjusting.
                _knobStyleColor = (_knob?.Fill as ISolidColorBrush)?.Color;
                QueueKnobContrast();
            }
            else if (e.Property == Border.BackgroundProperty)
            {
                QueueKnobContrast();
            }
        }

        // Setting Fill while its change is still being raised leaves a stale style value in effect,
        // so the adjustment runs right after the notification, still before the next frame renders.
        private void QueueKnobContrast()
        {
            if (_isKnobContrastQueued)
                return;

            _isKnobContrastQueued = true;
            Dispatcher.UIThread.Post(() =>
            {
                _isKnobContrastQueued = false;
                EnsureKnobContrast();
            }, DispatcherPriority.Send);
        }

        /// <summary>
        /// Darkens the checked knob on a light track or lightens it on a dark one in 10 % steps (at most 60 %)
        /// until it differs from the track by <see cref="MinimumKnobDifference"/> (CIE76 Delta E).
        /// The steps keep the knob's hue, so a variant knob stays recognizable.
        /// </summary>
        private void EnsureKnobContrast()
        {
            // Only the checked knob takes a theme color that can match the track.
            if (IsChecked != true || _knob == null || _knobStyleColor is not { } styleColor
                || _track?.Background is not ISolidColorBrush track)
                return;

            var darken = !FloweryColorHelpers.IsDark(track.Color);
            var color = styleColor;
            for (var step = 1; step <= 6 && FloweryColorHelpers.GetColorDifference(color, track.Color) < MinimumKnobDifference; step++)
            {
                color = darken
                    ? FloweryColorHelpers.Darken(styleColor, step * 0.1)
                    : FloweryColorHelpers.Lighten(styleColor, step * 0.1);
            }

            if ((_knob.Fill as ISolidColorBrush)?.Color == color)
                return;

            // SetCurrentValue keeps the style value underneath: a new style value replaces it, and unchecking
            // clears it (OnPropertyChanged).
            _isAdjustingKnob = true;
            try
            {
                _knob.SetCurrentValue(Shape.FillProperty, new SolidColorBrush(color));
            }
            finally
            {
                _isAdjustingKnob = false;
            }
        }


        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FloweryScaleManager.SetScaleFactor(this, FloweryScaleManager.SanitizeScaleFactor(scaleFactor));
        }

        public static readonly StyledProperty<DaisyToggleVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyToggle, DaisyToggleVariant>(nameof(Variant), DaisyToggleVariant.Default);

        public DaisyToggleVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyToggle, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the internal padding of the toggle knob area (maps to --toggle-p).
        /// </summary>
        public static readonly StyledProperty<double> TogglePaddingProperty =
            AvaloniaProperty.Register<DaisyToggle, double>(nameof(TogglePadding), 1.0);

        public double TogglePadding
        {
            get => GetValue(TogglePaddingProperty);
            set => SetValue(TogglePaddingProperty, value);
        }
    }

    internal sealed class ToggleKnobOffsetConverter : IMultiValueConverter
    {
        public static ToggleKnobOffsetConverter Instance { get; } = new();

        public object Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count == 4 && values[1] is double width && values[2] is double knob && values[3] is double padding)
                return values[0] is true ? Math.Max(padding, width - knob - padding) : padding;
            return 0d;
        }
    }
}
