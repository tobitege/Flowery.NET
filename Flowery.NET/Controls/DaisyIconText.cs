using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Icon placement relative to text.
    /// </summary>
    public enum IconPlacement
    {
        /// <summary>Icon on the left side of text.</summary>
        Left,
        /// <summary>Icon on the right side of text.</summary>
        Right,
        /// <summary>Icon above text.</summary>
        Top,
        /// <summary>Icon below text.</summary>
        Bottom
    }

    /// <summary>
    /// A control that displays an icon and/or text with proper scaling.
    /// Encapsulates the Viewbox-wrapping pattern for icons and FontSize for text.
    /// Can be used standalone or as content for buttons, labels, menu items, etc.
    /// </summary>
    /// <remarks>
    /// Usage examples:
    /// <code>
    /// &lt;!-- Icon only --&gt;
    /// &lt;daisy:DaisyIconText IconData="M12 2C8.13..." Size="Medium" /&gt;
    ///
    /// &lt;!-- Text only --&gt;
    /// &lt;daisy:DaisyIconText Text="Hello" Size="Small" /&gt;
    ///
    /// &lt;!-- Icon + Text --&gt;
    /// &lt;daisy:DaisyIconText IconData="M..." Text="Save" IconPlacement="Left" /&gt;
    /// </code>
    /// </remarks>
    public class DaisyIconText : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyIconText);

        private const double BaseIconSize = 16.0;
        private const double BaseFontSize = 14.0;
        private const double BaseSpacing = 8.0;

        #region Dependency Properties

        public static readonly StyledProperty<StreamGeometry?> IconDataProperty =
            AvaloniaProperty.Register<DaisyIconText, StreamGeometry?>(nameof(IconData));

        /// <summary>
        /// Gets or sets the path geometry for a custom icon.
        /// </summary>
        public StreamGeometry? IconData
        {
            get => GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }

        public static readonly StyledProperty<string?> TextProperty =
            AvaloniaProperty.Register<DaisyIconText, string?>(nameof(Text));

        /// <summary>
        /// Gets or sets the text to display.
        /// </summary>
        public string? Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyIconText, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size preset. Controls both icon dimensions and font size.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<IconPlacement> IconPlacementProperty =
            AvaloniaProperty.Register<DaisyIconText, IconPlacement>(nameof(IconPlacement), IconPlacement.Left);

        /// <summary>
        /// Gets or sets the placement of the icon relative to the text.
        /// </summary>
        public IconPlacement IconPlacement
        {
            get => GetValue(IconPlacementProperty);
            set => SetValue(IconPlacementProperty, value);
        }

        public static readonly StyledProperty<double> SpacingProperty =
            AvaloniaProperty.Register<DaisyIconText, double>(nameof(Spacing), double.NaN);

        /// <summary>
        /// Gets or sets the spacing between icon and text.
        /// If NaN (default), auto-computed based on Size.
        /// </summary>
        public double Spacing
        {
            get => GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        public static readonly StyledProperty<double> IconSizeProperty =
            AvaloniaProperty.Register<DaisyIconText, double>(nameof(IconSize), double.NaN);

        /// <summary>
        /// Gets or sets an explicit icon size override.
        /// If NaN (default), auto-computed based on Size.
        /// </summary>
        public double IconSize
        {
            get => GetValue(IconSizeProperty);
            set => SetValue(IconSizeProperty, value);
        }

        public static readonly StyledProperty<double> FontSizeOverrideProperty =
            AvaloniaProperty.Register<DaisyIconText, double>(nameof(FontSizeOverride), double.NaN);

        /// <summary>
        /// Gets or sets an explicit font size override.
        /// If NaN (default), auto-computed based on Size.
        /// </summary>
        public double FontSizeOverride
        {
            get => GetValue(FontSizeOverrideProperty);
            set => SetValue(FontSizeOverrideProperty, value);
        }

        public static readonly StyledProperty<DaisyBadgeVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyIconText, DaisyBadgeVariant>(nameof(Variant), DaisyBadgeVariant.Default);

        /// <summary>
        /// Gets or sets the color variant of the control.
        /// </summary>
        public DaisyBadgeVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        #endregion

        #region Read-Only Computed Properties

        public static readonly DirectProperty<DaisyIconText, double> EffectiveIconSizeProperty =
            AvaloniaProperty.RegisterDirect<DaisyIconText, double>(
                nameof(EffectiveIconSize),
                o => o.EffectiveIconSize);

        private double _effectiveIconSize;

        /// <summary>
        /// Gets the effective icon size, computing from Size if not explicitly set.
        /// </summary>
        public double EffectiveIconSize
        {
            get => _effectiveIconSize;
            private set => SetAndRaise(EffectiveIconSizeProperty, ref _effectiveIconSize, value);
        }

        public static readonly DirectProperty<DaisyIconText, double> EffectiveFontSizeProperty =
            AvaloniaProperty.RegisterDirect<DaisyIconText, double>(
                nameof(EffectiveFontSize),
                o => o.EffectiveFontSize);

        private double _effectiveFontSize;

        /// <summary>
        /// Gets the effective font size, computing from Size if not explicitly set.
        /// </summary>
        public double EffectiveFontSize
        {
            get => _effectiveFontSize;
            private set => SetAndRaise(EffectiveFontSizeProperty, ref _effectiveFontSize, value);
        }

        public static readonly DirectProperty<DaisyIconText, double> EffectiveSpacingProperty =
            AvaloniaProperty.RegisterDirect<DaisyIconText, double>(
                nameof(EffectiveSpacing),
                o => o.EffectiveSpacing);

        private double _effectiveSpacing;

        /// <summary>
        /// Gets the effective spacing, computing from Size if not explicitly set.
        /// </summary>
        public double EffectiveSpacing
        {
            get => _effectiveSpacing;
            private set => SetAndRaise(EffectiveSpacingProperty, ref _effectiveSpacing, value);
        }

        public static readonly DirectProperty<DaisyIconText, bool> HasIconProperty =
            AvaloniaProperty.RegisterDirect<DaisyIconText, bool>(
                nameof(HasIcon),
                o => o.HasIcon);

        private bool _hasIcon;

        /// <summary>
        /// Returns true if an icon should be displayed.
        /// </summary>
        public bool HasIcon
        {
            get => _hasIcon;
            private set => SetAndRaise(HasIconProperty, ref _hasIcon, value);
        }

        public static readonly DirectProperty<DaisyIconText, bool> HasTextProperty =
            AvaloniaProperty.RegisterDirect<DaisyIconText, bool>(
                nameof(HasText),
                o => o.HasText);

        private bool _hasText;

        /// <summary>
        /// Returns true if text should be displayed.
        /// </summary>
        public bool HasText
        {
            get => _hasText;
            private set => SetAndRaise(HasTextProperty, ref _hasText, value);
        }

        public static readonly DirectProperty<DaisyIconText, Orientation> EffectiveOrientationProperty =
            AvaloniaProperty.RegisterDirect<DaisyIconText, Orientation>(
                nameof(EffectiveOrientation),
                o => o.EffectiveOrientation);

        private Orientation _effectiveOrientation;

        /// <summary>
        /// Gets the effective orientation based on IconPlacement.
        /// </summary>
        public Orientation EffectiveOrientation
        {
            get => _effectiveOrientation;
            private set => SetAndRaise(EffectiveOrientationProperty, ref _effectiveOrientation, value);
        }

        public static readonly DirectProperty<DaisyIconText, bool> IconFirstProperty =
            AvaloniaProperty.RegisterDirect<DaisyIconText, bool>(
                nameof(IconFirst),
                o => o.IconFirst);

        private bool _iconFirst = true;

        /// <summary>
        /// Gets whether icon should appear before text in layout order.
        /// </summary>
        public bool IconFirst
        {
            get => _iconFirst;
            private set => SetAndRaise(IconFirstProperty, ref _iconFirst, value);
        }

        #endregion

        public DaisyIconText()
        {
            UpdateComputedProperties();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SizeProperty ||
                change.Property == IconSizeProperty ||
                change.Property == FontSizeOverrideProperty ||
                change.Property == SpacingProperty ||
                change.Property == IconDataProperty ||
                change.Property == TextProperty ||
                change.Property == IconPlacementProperty)
            {
                UpdateComputedProperties();
            }
        }

        private void UpdateComputedProperties()
        {
            // Compute effective icon size
            if (!double.IsNaN(IconSize))
            {
                EffectiveIconSize = IconSize;
            }
            else
            {
                EffectiveIconSize = Size switch
                {
                    DaisySize.ExtraSmall => 12.0,
                    DaisySize.Small => 14.0,
                    DaisySize.Medium => 16.0,
                    DaisySize.Large => 20.0,
                    DaisySize.ExtraLarge => 24.0,
                    _ => 16.0
                };
            }

            // Compute effective font size
            if (!double.IsNaN(FontSizeOverride))
            {
                EffectiveFontSize = FontSizeOverride;
            }
            else
            {
                EffectiveFontSize = Size switch
                {
                    DaisySize.ExtraSmall => 10.0,
                    DaisySize.Small => 12.0,
                    DaisySize.Medium => 14.0,
                    DaisySize.Large => 16.0,
                    DaisySize.ExtraLarge => 18.0,
                    _ => 14.0
                };
            }

            // Compute effective spacing
            if (!double.IsNaN(Spacing))
            {
                EffectiveSpacing = Spacing;
            }
            else
            {
                EffectiveSpacing = Size switch
                {
                    DaisySize.ExtraSmall => 4.0,
                    DaisySize.Small => 6.0,
                    DaisySize.Medium => 8.0,
                    DaisySize.Large => 10.0,
                    DaisySize.ExtraLarge => 12.0,
                    _ => 8.0
                };
            }

            // Update has icon/text
            HasIcon = IconData != null;
            HasText = !string.IsNullOrEmpty(Text);

            // Update orientation and order based on placement
            EffectiveOrientation = IconPlacement switch
            {
                IconPlacement.Left or IconPlacement.Right => Orientation.Horizontal,
                _ => Orientation.Vertical
            };

            IconFirst = IconPlacement switch
            {
                IconPlacement.Right or IconPlacement.Bottom => false,
                _ => true
            };
        }

        #region Scaling Properties

        public static readonly StyledProperty<double> ScaledIconSizeProperty =
            AvaloniaProperty.Register<DaisyIconText, double>(nameof(ScaledIconSize), BaseIconSize);

        /// <summary>
        /// Gets the scaled icon size. Automatically updated by FloweryScaleManager.
        /// </summary>
        public double ScaledIconSize
        {
            get => GetValue(ScaledIconSizeProperty);
            private set => SetValue(ScaledIconSizeProperty, value);
        }

        public static readonly StyledProperty<double> ScaledFontSizeProperty =
            AvaloniaProperty.Register<DaisyIconText, double>(nameof(ScaledFontSize), BaseFontSize);

        /// <summary>
        /// Gets the scaled font size. Automatically updated by FloweryScaleManager.
        /// </summary>
        public double ScaledFontSize
        {
            get => GetValue(ScaledFontSizeProperty);
            private set => SetValue(ScaledFontSizeProperty, value);
        }

        public static readonly StyledProperty<double> ScaledSpacingProperty =
            AvaloniaProperty.Register<DaisyIconText, double>(nameof(ScaledSpacing), BaseSpacing);

        /// <summary>
        /// Gets the scaled spacing. Automatically updated by FloweryScaleManager.
        /// </summary>
        public double ScaledSpacing
        {
            get => GetValue(ScaledSpacingProperty);
            private set => SetValue(ScaledSpacingProperty, value);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            ScaledIconSize = FloweryScaleManager.ApplyScale(BaseIconSize, 10.0, scaleFactor);
            ScaledFontSize = FloweryScaleManager.ApplyScale(BaseFontSize, 10.0, scaleFactor);
            ScaledSpacing = FloweryScaleManager.ApplyScale(BaseSpacing, 4.0, scaleFactor);

            EffectiveIconSize = ScaledIconSize;
            EffectiveFontSize = ScaledFontSize;
            EffectiveSpacing = ScaledSpacing;
        }

        #endregion
    }
}
