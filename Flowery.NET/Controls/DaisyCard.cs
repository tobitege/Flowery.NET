using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Flowery.Enums;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A Card control styled after DaisyUI's Card component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyCard : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyCard);

        private const double BaseTitleFontSize = 20.0;
        private const double BaseBodyFontSize = 14.0;
        private readonly DaisyControlLifecycle _lifecycle;

        public DaisyCard()
        {
            _lifecycle = new DaisyControlLifecycle(
                this,
                ApplyAll,
                () => Size,
                s => Size = s);
        }

        private void ApplyAll()
        {
            // Most visual changes are handled via AXAML style triggers.
            // Complex code-behind logic can be added here if needed.
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            // Factor in the Size property for base font sizes
            double sizeMultiplier = Size switch
            {
                DaisySize.ExtraSmall => 0.75,
                DaisySize.Small => 0.85,
                DaisySize.Medium => 1.0,
                DaisySize.Large => 1.25,
                DaisySize.ExtraLarge => 1.5,
                _ => 1.0
            };

            TitleFontSize = FloweryScaleManager.ApplyScale(BaseTitleFontSize * sizeMultiplier, 14.0 * sizeMultiplier, scaleFactor);
            BodyFontSize = FloweryScaleManager.ApplyScale(BaseBodyFontSize * sizeMultiplier, 11.0 * sizeMultiplier, scaleFactor);
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyCardVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyCard, DaisyCardVariant>(nameof(Variant), DaisyCardVariant.Normal);

        /// <summary>
        /// Gets or sets the card layout variant (Normal, Compact, Side).
        /// </summary>
        public DaisyCardVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyCard, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the card size tier.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ColorVariant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyColor> ColorVariantProperty =
            AvaloniaProperty.Register<DaisyCard, DaisyColor>(nameof(ColorVariant), DaisyColor.Default);

        /// <summary>
        /// Gets or sets the semantic color variant (Primary, Secondary, etc.).
        /// </summary>
        public DaisyColor ColorVariant
        {
            get => GetValue(ColorVariantProperty);
            set => SetValue(ColorVariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="CardStyle"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyCardStyle> CardStyleProperty =
            AvaloniaProperty.Register<DaisyCard, DaisyCardStyle>(nameof(CardStyle), DaisyCardStyle.Default);

        /// <summary>
        /// Gets or sets the visual style variant.
        /// </summary>
        public DaisyCardStyle CardStyle
        {
            get => GetValue(CardStyleProperty);
            set => SetValue(CardStyleProperty, value);
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

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SizeProperty)
            {
                ApplyScaleFactor(FloweryScaleManager.GetScaleFactor(this));
            }

            if (change.Property == ColorVariantProperty ||
                change.Property == CardStyleProperty ||
                change.Property == IsGlassProperty ||
                change.Property == VariantProperty)
            {
                ApplyAll();
            }
        }
    }
}
