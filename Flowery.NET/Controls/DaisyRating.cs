using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Specifies how rating values are snapped when clicking.
    /// </summary>
    public enum RatingPrecision
    {
        /// <summary>Only whole star values (1, 2, 3, 4, 5)</summary>
        Full,
        /// <summary>Half-star increments (0.5, 1, 1.5, 2, ...)</summary>
        Half,
        /// <summary>One decimal place (0.1 increments)</summary>
        Precise
    }

    /// <summary>
    /// A star rating control styled after DaisyUI's Rating component.
    /// Includes accessibility support for screen readers via the AccessibleText attached property.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyRating : RangeBase, IScalableControl
    {
        private const string DefaultAccessibleText = "Rating";
        private const double BaseTextFontSize = 14.0;

        protected override Type StyleKeyOverride => typeof(DaisyRating);

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        private Control? _foregroundPart;
        private Control? _backgroundPart;

        private const double StarSpacing = 4.0;

        static DaisyRating()
        {
            DaisyAccessibility.SetupAccessibility<DaisyRating>(DefaultAccessibleText);
        }

        public DaisyRating()
        {
            Minimum = 0;
            Maximum = 5;
            Value = 0;
            Cursor = Cursor.Parse("Hand");
        }

        /// <summary>
        /// Calculates the actual width occupied by the stars based on count and size.
        /// Each star's width equals the Height property, with StarSpacing between them.
        /// </summary>
        private double GetStarsWidth()
        {
            var starCount = (int)Maximum;
            if (starCount <= 0) return 0;

            var starSize = Height;
            return (starCount * starSize) + ((starCount - 1) * StarSpacing);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyRating, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<bool> IsReadOnlyProperty =
            AvaloniaProperty.Register<DaisyRating, bool>(nameof(IsReadOnly));

        public bool IsReadOnly
        {
            get => GetValue(IsReadOnlyProperty);
            set => SetValue(IsReadOnlyProperty, value);
        }

        public static readonly StyledProperty<RatingPrecision> PrecisionProperty =
            AvaloniaProperty.Register<DaisyRating, RatingPrecision>(nameof(Precision), RatingPrecision.Full);

        /// <summary>
        /// Gets or sets how rating values are snapped when clicking.
        /// Full = whole stars only, Half = 0.5 increments, Precise = 0.1 increments.
        /// </summary>
        public RatingPrecision Precision
        {
            get => GetValue(PrecisionProperty);
            set => SetValue(PrecisionProperty, value);
        }

        /// <summary>
        /// Gets or sets the accessible text announced by screen readers.
        /// Default is "Rating". The star count is automatically appended (e.g., "Rating: 3 of 5 stars").
        /// </summary>
        public string? AccessibleText
        {
            get => DaisyAccessibility.GetAccessibleText(this);
            set => DaisyAccessibility.SetAccessibleText(this, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _foregroundPart = e.NameScope.Find<Control>("PART_ForegroundStars");
            _backgroundPart = e.NameScope.Find<Control>("PART_BackgroundStars");

            UpdateVisuals();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ValueProperty ||
                change.Property == MinimumProperty ||
                change.Property == MaximumProperty)
            {
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            if (_foregroundPart == null || _backgroundPart == null) return;
            InvalidateArrange();
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);
            UpdateClip(finalSize);
            return result;
        }

        private void UpdateClip(Size bounds)
        {
            if (_foregroundPart == null) return;

            var range = Maximum - Minimum;
            if (range <= 0) return;

            var percent = (Value - Minimum) / range;
            if (percent < 0) percent = 0;
            if (percent > 1) percent = 1;

            var starsWidth = GetStarsWidth();
            var clipWidth = starsWidth * percent;

            _foregroundPart.Width = clipWidth;
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);
            if (IsReadOnly) return;

            UpdateValueFromPoint(e.GetPosition(this));
            e.Pointer.Capture(this);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            if (IsReadOnly) return;

            if (this.Equals(e.Pointer.Captured))
            {
                UpdateValueFromPoint(e.GetPosition(this));
            }
        }

        protected override void OnPointerReleased(PointerReleasedEventArgs e)
        {
            base.OnPointerReleased(e);
            if (IsReadOnly) return;

            if (this.Equals(e.Pointer.Captured))
            {
                e.Pointer.Capture(null);
            }
        }

        private void UpdateValueFromPoint(Point p)
        {
            var starsWidth = GetStarsWidth();
            if (starsWidth <= 0) return;

            var percent = p.X / starsWidth;
            if (percent < 0) percent = 0;
            if (percent > 1) percent = 1;

            var range = Maximum - Minimum;
            var rawValue = (percent * range) + Minimum;

            var newValue = SnapValue(rawValue);

            SetCurrentValue(ValueProperty, newValue);
        }

        private double SnapValue(double rawValue)
        {
            switch (Precision)
            {
                case RatingPrecision.Half:
                    return Math.Ceiling(rawValue * 2) / 2.0;

                case RatingPrecision.Precise:
                    return Math.Ceiling(rawValue * 10) / 10.0;

                case RatingPrecision.Full:
                default:
                    return Math.Ceiling(rawValue);
            }
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DaisyRatingAutomationPeer(this);
        }
    }

    /// <summary>
    /// AutomationPeer for DaisyRating that exposes it as a slider to assistive technologies.
    /// Announces the current star count out of the maximum.
    /// </summary>
    internal class DaisyRatingAutomationPeer : ControlAutomationPeer
    {
        private const string DefaultAccessibleText = "Rating";

        public DaisyRatingAutomationPeer(DaisyRating owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.Slider;
        }

        protected override string GetClassNameCore()
        {
            return "DaisyRating";
        }

        protected override string? GetNameCore()
        {
            var rating = (DaisyRating)Owner;
            var localizedDefault = FloweryLocalization.GetStringInternal("Accessibility_Rating");
            var text = DaisyAccessibility.GetEffectiveAccessibleText(rating, localizedDefault);
            var valueText = FormatValue(rating.Value, rating.Precision);
            var maxText = FormatValue(rating.Maximum, RatingPrecision.Full);
            return $"{text}: {valueText} of {maxText} stars";
        }

        private static string FormatValue(double value, RatingPrecision precision)
        {
            return precision switch
            {
                RatingPrecision.Precise => value.ToString("F1"),
                RatingPrecision.Half => value.ToString("F1"),
                _ => ((int)value).ToString()
            };
        }

        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
    }
}
