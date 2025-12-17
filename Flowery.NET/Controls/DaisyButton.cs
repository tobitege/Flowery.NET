using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data.Converters;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyButtonVariant
    {
        Default,
        Neutral,
        Primary,
        Secondary,
        Accent,
        Ghost,
        Link,
        Info,
        Success,
        Warning,
        Error
    }

    public enum DaisyButtonStyle
    {
        Default,
        Outline,
        Dash,
        Soft
    }

    public enum DaisyButtonShape
    {
        Default,
        Wide,
        Block,
        Square,
        Circle
    }

    /// <summary>
    /// A Button control styled after DaisyUI's Button component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyButton : Button, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyButton);

        // Base font size for scaling
        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyButtonVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyButton, DaisyButtonVariant>(nameof(Variant), DaisyButtonVariant.Default);

        /// <summary>
        /// Gets or sets the visual variant (e.g., Primary, Secondary, Ghost).
        /// </summary>
        public DaisyButtonVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyButton, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the button (ExtraSmall, Small, Medium, Large, ExtraLarge).
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ButtonStyle"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyButtonStyle> ButtonStyleProperty =
            AvaloniaProperty.Register<DaisyButton, DaisyButtonStyle>(nameof(ButtonStyle), DaisyButtonStyle.Default);

        /// <summary>
        /// Gets or sets the button style (Default, Outline, Dash, Soft).
        /// </summary>
        public DaisyButtonStyle ButtonStyle
        {
            get => GetValue(ButtonStyleProperty);
            set => SetValue(ButtonStyleProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Shape"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyButtonShape> ShapeProperty =
            AvaloniaProperty.Register<DaisyButton, DaisyButtonShape>(nameof(Shape), DaisyButtonShape.Default);

        /// <summary>
        /// Gets or sets the button shape modifier (Default, Wide, Block, Square, Circle).
        /// </summary>
        public DaisyButtonShape Shape
        {
            get => GetValue(ShapeProperty);
            set => SetValue(ShapeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IsOutline"/> property.
        /// </summary>
        [Obsolete("Use ButtonStyle = DaisyButtonStyle.Outline instead")]
        public static readonly StyledProperty<bool> IsOutlineProperty =
            AvaloniaProperty.Register<DaisyButton, bool>(nameof(IsOutline));

        /// <summary>
        /// Gets or sets a value indicating whether the button should be an outline button.
        /// </summary>
        [Obsolete("Use ButtonStyle = DaisyButtonStyle.Outline instead")]
        public bool IsOutline
        {
            get => GetValue(IsOutlineProperty);
            set => SetValue(IsOutlineProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IsActive"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsActiveProperty =
            AvaloniaProperty.Register<DaisyButton, bool>(nameof(IsActive));

        /// <summary>
        /// Gets or sets a value indicating whether the button is in an active (pressed) state.
        /// </summary>
        public bool IsActive
        {
            get => GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the button shadow is visible (maps to --btn-shadow).
        /// </summary>
        public static readonly StyledProperty<bool> ShowShadowProperty =
            AvaloniaProperty.Register<DaisyButton, bool>(nameof(ShowShadow), false);

        public bool ShowShadow
        {
            get => GetValue(ShowShadowProperty);
            set => SetValue(ShowShadowProperty, value);
        }

        /// <summary>
        /// Gets or sets the horizontal offset of the button shadow.
        /// </summary>
        public static readonly StyledProperty<double> ShadowOffsetXProperty =
            AvaloniaProperty.Register<DaisyButton, double>(nameof(ShadowOffsetX), 0.0);

        public double ShadowOffsetX
        {
            get => GetValue(ShadowOffsetXProperty);
            set => SetValue(ShadowOffsetXProperty, value);
        }

        /// <summary>
        /// Gets or sets the vertical offset of the button shadow.
        /// </summary>
        public static readonly StyledProperty<double> ShadowOffsetYProperty =
            AvaloniaProperty.Register<DaisyButton, double>(nameof(ShadowOffsetY), 4.0);

        public double ShadowOffsetY
        {
            get => GetValue(ShadowOffsetYProperty);
            set => SetValue(ShadowOffsetYProperty, value);
        }

        /// <summary>
        /// Gets or sets the blur radius of the button shadow.
        /// </summary>
        public static readonly StyledProperty<double> ShadowBlurProperty =
            AvaloniaProperty.Register<DaisyButton, double>(nameof(ShadowBlur), 6.0);

        public double ShadowBlur
        {
            get => GetValue(ShadowBlurProperty);
            set => SetValue(ShadowBlurProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the button shadow.
        /// </summary>
        public static readonly StyledProperty<Color> ShadowColorProperty =
            AvaloniaProperty.Register<DaisyButton, Color>(nameof(ShadowColor), Color.FromArgb(64, 0, 0, 0));

        public Color ShadowColor
        {
            get => GetValue(ShadowColorProperty);
            set => SetValue(ShadowColorProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IconLeft"/> property.
        /// </summary>
        public static readonly StyledProperty<object?> IconLeftProperty =
            AvaloniaProperty.Register<DaisyButton, object?>(nameof(IconLeft));

        /// <summary>
        /// Gets or sets an optional icon displayed to the left of the button content.
        /// Typically a PathIcon, but can be any object.
        /// </summary>
        public object? IconLeft
        {
            get => GetValue(IconLeftProperty);
            set => SetValue(IconLeftProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IconRight"/> property.
        /// </summary>
        public static readonly StyledProperty<object?> IconRightProperty =
            AvaloniaProperty.Register<DaisyButton, object?>(nameof(IconRight));

        /// <summary>
        /// Gets or sets an optional icon displayed to the right of the button content.
        /// Typically a PathIcon, but can be any object.
        /// </summary>
        public object? IconRight
        {
            get => GetValue(IconRightProperty);
            set => SetValue(IconRightProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IconSpacing"/> property.
        /// </summary>
        public static readonly StyledProperty<double> IconSpacingProperty =
            AvaloniaProperty.Register<DaisyButton, double>(nameof(IconSpacing), 6.0);

        /// <summary>
        /// Gets or sets the spacing between icons and the button content.
        /// </summary>
        public double IconSpacing
        {
            get => GetValue(IconSpacingProperty);
            set => SetValue(IconSpacingProperty, value);
        }
    }

    public class ButtonShadowConverter : IMultiValueConverter
    {
        public static readonly ButtonShadowConverter Instance = new();

        public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
        {
            if (values.Count >= 5 &&
                values[0] is bool showShadow &&
                values[1] is double offsetX &&
                values[2] is double offsetY &&
                values[3] is double blur &&
                values[4] is Color color)
            {
                if (!showShadow)
                    return new BoxShadows(new BoxShadow());

                return new BoxShadows(new BoxShadow
                {
                    OffsetX = offsetX,
                    OffsetY = offsetY,
                    Blur = blur,
                    Color = color
                });
            }
            return new BoxShadows(new BoxShadow());
        }
    }
}
