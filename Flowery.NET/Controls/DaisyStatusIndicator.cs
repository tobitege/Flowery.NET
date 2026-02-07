using System;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Controls.Primitives;
using Flowery.Enums;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A status indicator control that displays a small colored dot to represent status.
    /// Includes accessibility support for screen readers via the AccessibleText attached property.
    /// </summary>
    public partial class DaisyStatusIndicator : TemplatedControl, IScalableControl
    {
        private const string DefaultAccessibleText = "Status";

        protected override Type StyleKeyOverride => typeof(DaisyStatusIndicator);

        static DaisyStatusIndicator()
        {
            DaisyAccessibility.SetupAccessibility<DaisyStatusIndicator>(DefaultAccessibleText);

            ColorProperty.Changed.AddClassHandler<DaisyStatusIndicator>((control, _) =>
            {
                control.UpdateAccessibleNameFromColor();
            });
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyStatusIndicatorVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyStatusIndicator, DaisyStatusIndicatorVariant>(nameof(Variant), DaisyStatusIndicatorVariant.Default);

        /// <summary>
        /// Gets or sets the animation variant (Default, Ping, Bounce, Pulse, Blink, etc.).
        /// </summary>
        public DaisyStatusIndicatorVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Color"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyColor> ColorProperty =
            AvaloniaProperty.Register<DaisyStatusIndicator, DaisyColor>(nameof(Color), DaisyColor.Neutral);

        /// <summary>
        /// Gets or sets the color variant (Default, Primary, Secondary, Accent, etc.).
        /// </summary>
        public DaisyColor Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyStatusIndicator, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the status indicator (ExtraSmall, Small, Medium, Large, ExtraLarge).
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="BatteryChargePercent"/> property.
        /// </summary>
        public static readonly StyledProperty<int> BatteryChargePercentProperty =
            AvaloniaProperty.Register<DaisyStatusIndicator, int>(nameof(BatteryChargePercent), 100,
                coerce: CoerceBatteryChargePercent);

        private static int CoerceBatteryChargePercent(AvaloniaObject obj, int value)
        {
            if (value < 0) return 0;
            if (value > 100) return 100;
            return value;
        }

        /// <summary>
        /// Gets or sets the battery charge percentage (0-100). Only used when Variant is Battery.
        /// </summary>
        public int BatteryChargePercent
        {
            get => GetValue(BatteryChargePercentProperty);
            set => SetValue(BatteryChargePercentProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="TrafficLightActive"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyTrafficLightState> TrafficLightActiveProperty =
            AvaloniaProperty.Register<DaisyStatusIndicator, DaisyTrafficLightState>(nameof(TrafficLightActive), DaisyTrafficLightState.Green);

        /// <summary>
        /// Gets or sets the active traffic light state. Only used when Variant is TrafficLightVertical, TrafficLightHorizontal, or TrafficLightHorizontalReversed.
        /// </summary>
        public DaisyTrafficLightState TrafficLightActive
        {
            get => GetValue(TrafficLightActiveProperty);
            set => SetValue(TrafficLightActiveProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="SignalStrength"/> property.
        /// </summary>
        public static readonly StyledProperty<int> SignalStrengthProperty =
            AvaloniaProperty.Register<DaisyStatusIndicator, int>(nameof(SignalStrength), 3,
                coerce: CoerceSignalStrength);

        private static int CoerceSignalStrength(AvaloniaObject obj, int value)
        {
            if (value < 0) return 0;
            if (value > 5) return 5;
            return value;
        }

        /// <summary>
        /// Gets or sets the signal strength (0-3 for WiFi, 0-5 for Cellular). Only used when Variant is WifiSignal or CellularSignal.
        /// </summary>
        public int SignalStrength
        {
            get => GetValue(SignalStrengthProperty);
            set => SetValue(SignalStrengthProperty, value);
        }

        /// <summary>
        /// Gets or sets the accessible text announced by screen readers.
        /// When null (default), the text is automatically derived from the Color property.
        /// </summary>
        public string? AccessibleText
        {
            get => DaisyAccessibility.GetAccessibleText(this);
            set => DaisyAccessibility.SetAccessibleText(this, value);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            var baseSize = GetBaseSize(Size);
            var scaled = FloweryScaleManager.ApplyScale(baseSize, scaleFactor);

            Width = scaled;
            Height = scaled;
        }

        private static double GetBaseSize(DaisySize size)
        {
            return size switch
            {
                DaisySize.ExtraSmall => 6.0,
                DaisySize.Small => 8.0,
                DaisySize.Large => 16.0,
                DaisySize.ExtraLarge => 20.0,
                _ => 12.0
            };
        }

        private void UpdateAccessibleNameFromColor()
        {
            if (DaisyAccessibility.GetAccessibleText(this) == null)
            {
                Avalonia.Automation.AutomationProperties.SetName(this, GetDefaultAccessibleText());
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SizeProperty && FloweryScaleManager.GetEnableScaling(this))
            {
                ApplyScaleFactor(FloweryScaleManager.GetScaleFactor(this));
            }

            if (change.Property == VariantProperty ||
                change.Property == BatteryChargePercentProperty ||
                change.Property == TrafficLightActiveProperty ||
                change.Property == SignalStrengthProperty)
            {
                UpdateAccessibleNameFromColor();
            }

            if (change.Property == VariantProperty)
            {
                UpdateGlyphVisuals();
                OnVariantChanged();
                UpdateOverlayEffects();
            }

            if (change.Property == BatteryChargePercentProperty)
            {
                OnBatteryChargePercentChanged();
            }
        }

        internal string GetDefaultAccessibleText()
        {
            // Glyph variant-specific accessible text
            return Variant switch
            {
                DaisyStatusIndicatorVariant.Battery =>
                    string.Format(FloweryLocalization.GetStringInternal("Accessibility_BatteryPercent"), BatteryChargePercent),
                DaisyStatusIndicatorVariant.TrafficLightVertical or DaisyStatusIndicatorVariant.TrafficLightHorizontal or DaisyStatusIndicatorVariant.TrafficLightHorizontalReversed =>
                    TrafficLightActive switch
                    {
                        DaisyTrafficLightState.Green => FloweryLocalization.GetStringInternal("Accessibility_TrafficLightGreen"),
                        DaisyTrafficLightState.Yellow => FloweryLocalization.GetStringInternal("Accessibility_TrafficLightYellow"),
                        DaisyTrafficLightState.Red => FloweryLocalization.GetStringInternal("Accessibility_TrafficLightRed"),
                        _ => FloweryLocalization.GetStringInternal("Accessibility_TrafficLight")
                    },
                DaisyStatusIndicatorVariant.WifiSignal =>
                    string.Format(FloweryLocalization.GetStringInternal("Accessibility_WifiSignal"), SignalStrength, 3),
                DaisyStatusIndicatorVariant.CellularSignal =>
                    string.Format(FloweryLocalization.GetStringInternal("Accessibility_CellularSignal"), SignalStrength, 5),
                _ => GetColorBasedAccessibleText()
            };
        }

        private string GetColorBasedAccessibleText()
        {
            return Color switch
            {
                DaisyColor.Success => FloweryLocalization.GetStringInternal("Accessibility_StatusOnline"),
                DaisyColor.Error => FloweryLocalization.GetStringInternal("Accessibility_StatusError"),
                DaisyColor.Warning => FloweryLocalization.GetStringInternal("Accessibility_StatusWarning"),
                DaisyColor.Info => FloweryLocalization.GetStringInternal("Accessibility_StatusInfo"),
                DaisyColor.Primary => FloweryLocalization.GetStringInternal("Accessibility_StatusActive"),
                DaisyColor.Secondary => FloweryLocalization.GetStringInternal("Accessibility_StatusSecondary"),
                DaisyColor.Accent => FloweryLocalization.GetStringInternal("Accessibility_StatusHighlighted"),
                _ => FloweryLocalization.GetStringInternal("Accessibility_Status")
            };
        }

        protected override AutomationPeer OnCreateAutomationPeer()
        {
            return new DaisyStatusIndicatorAutomationPeer(this);
        }
    }

    /// <summary>
    /// AutomationPeer for DaisyStatusIndicator that exposes it as a status element to assistive technologies.
    /// </summary>
    internal class DaisyStatusIndicatorAutomationPeer : ControlAutomationPeer
    {
        public DaisyStatusIndicatorAutomationPeer(DaisyStatusIndicator owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore()
        {
            return AutomationControlType.StatusBar;
        }

        protected override string GetClassNameCore()
        {
            return "DaisyStatusIndicator";
        }

        protected override string? GetNameCore()
        {
            var indicator = (DaisyStatusIndicator)Owner;
            return DaisyAccessibility.GetAccessibleText(indicator) ?? indicator.GetDefaultAccessibleText();
        }

        protected override bool IsContentElementCore() => true;
        protected override bool IsControlElementCore() => true;
    }
}
