using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyRangeVariant
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
    /// A Slider control styled after DaisyUI's Range component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyRange : Slider, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyRange);

        private const double BaseTextFontSize = 12.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<DaisyRangeVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyRange, DaisyRangeVariant>(nameof(Variant), DaisyRangeVariant.Default);

        public DaisyRangeVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyRange, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the range slider thumb (maps to --range-thumb).
        /// </summary>
        public static readonly StyledProperty<IBrush?> ThumbBrushProperty =
            AvaloniaProperty.Register<DaisyRange, IBrush?>(nameof(ThumbBrush));

        public IBrush? ThumbBrush
        {
            get => GetValue(ThumbBrushProperty);
            set => SetValue(ThumbBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets the size of the range slider thumb (maps to --range-thumb-size).
        /// </summary>
        public static readonly StyledProperty<double> ThumbSizeProperty =
            AvaloniaProperty.Register<DaisyRange, double>(nameof(ThumbSize), 24.0);

        public double ThumbSize
        {
            get => GetValue(ThumbSizeProperty);
            set => SetValue(ThumbSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the range slider progress (maps to --range-progress).
        /// </summary>
        public static readonly StyledProperty<IBrush?> ProgressBrushProperty =
            AvaloniaProperty.Register<DaisyRange, IBrush?>(nameof(ProgressBrush));

        public IBrush? ProgressBrush
        {
            get => GetValue(ProgressBrushProperty);
            set => SetValue(ProgressBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the progress fill on the track (maps to --range-fill).
        /// </summary>
        public static readonly StyledProperty<bool> ShowProgressProperty =
            AvaloniaProperty.Register<DaisyRange, bool>(nameof(ShowProgress), false);

        public bool ShowProgress
        {
            get => GetValue(ShowProgressProperty);
            set => SetValue(ShowProgressProperty, value);
        }
    }
}
