using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyCardVariant
    {
        Normal,
        Compact,
        Side
    }

    /// <summary>
    /// A Card control styled after DaisyUI's Card component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyCard : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyCard);

        private const double BaseTitleFontSize = 20.0;
        private const double BaseBodyFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            TitleFontSize = FloweryScaleManager.ApplyScale(BaseTitleFontSize, 14.0, scaleFactor);
            BodyFontSize = FloweryScaleManager.ApplyScale(BaseBodyFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<DaisyCardVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyCard, DaisyCardVariant>(nameof(Variant), DaisyCardVariant.Normal);

        public DaisyCardVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Gets or sets the padding of the card body (maps to --card-p).
        /// </summary>
        public static readonly StyledProperty<Thickness> BodyPaddingProperty =
            AvaloniaProperty.Register<DaisyCard, Thickness>(nameof(BodyPadding), new Thickness(32));

        public Thickness BodyPadding
        {
            get => GetValue(BodyPaddingProperty);
            set => SetValue(BodyPaddingProperty, value);
        }

        /// <summary>
        /// Gets or sets the font size of the card body (maps to --card-fs).
        /// </summary>
        public static readonly StyledProperty<double> BodyFontSizeProperty =
            AvaloniaProperty.Register<DaisyCard, double>(nameof(BodyFontSize), 14.0);

        public double BodyFontSize
        {
            get => GetValue(BodyFontSizeProperty);
            set => SetValue(BodyFontSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the font size of the card title (maps to --cardtitle-fs).
        /// </summary>
        public static readonly StyledProperty<double> TitleFontSizeProperty =
            AvaloniaProperty.Register<DaisyCard, double>(nameof(TitleFontSize), 20.0);

        public double TitleFontSize
        {
            get => GetValue(TitleFontSizeProperty);
            set => SetValue(TitleFontSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the card uses a glass effect.
        /// </summary>
        public static readonly StyledProperty<bool> IsGlassProperty =
            AvaloniaProperty.Register<DaisyCard, bool>(nameof(IsGlass), false);

        public bool IsGlass
        {
            get => GetValue(IsGlassProperty);
            set => SetValue(IsGlassProperty, value);
        }

        /// <summary>
        /// Gets or sets the blur amount for the glass effect (maps to --glass-blur).
        /// </summary>
        public static readonly StyledProperty<double> GlassBlurProperty =
            AvaloniaProperty.Register<DaisyCard, double>(nameof(GlassBlur), 40.0);

        public double GlassBlur
        {
            get => GetValue(GlassBlurProperty);
            set => SetValue(GlassBlurProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the glass effect (maps to --glass-opacity).
        /// </summary>
        public static readonly StyledProperty<double> GlassOpacityProperty =
            AvaloniaProperty.Register<DaisyCard, double>(nameof(GlassOpacity), 0.3);

        public double GlassOpacity
        {
            get => GetValue(GlassOpacityProperty);
            set => SetValue(GlassOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the tint color for the glass effect.
        /// </summary>
        public static readonly StyledProperty<Color> GlassTintProperty =
            AvaloniaProperty.Register<DaisyCard, Color>(nameof(GlassTint), Colors.White);

        public Color GlassTint
        {
            get => GetValue(GlassTintProperty);
            set => SetValue(GlassTintProperty, value);
        }

        /// <summary>
        /// Gets or sets the tint opacity for the glass effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassTintOpacityProperty =
            AvaloniaProperty.Register<DaisyCard, double>(nameof(GlassTintOpacity), 0.5);

        public double GlassTintOpacity
        {
            get => GetValue(GlassTintOpacityProperty);
            set => SetValue(GlassTintOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the glass border (maps to --glass-border-opacity).
        /// </summary>
        public static readonly StyledProperty<double> GlassBorderOpacityProperty =
            AvaloniaProperty.Register<DaisyCard, double>(nameof(GlassBorderOpacity), 0.2);

        public double GlassBorderOpacity
        {
            get => GetValue(GlassBorderOpacityProperty);
            set => SetValue(GlassBorderOpacityProperty, value);
        }
    }
}
