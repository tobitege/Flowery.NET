using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// Neumorphic (soft UI) effects for Daisy controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The effect is drawn with <see cref="Border.BoxShadow"/> on a control's surface border:
    /// Raised uses a light shadow top-left and a dark shadow bottom-right, Inset uses the same
    /// pair as inner shadows, and Flat uses a single, smaller dark shadow.
    /// </para>
    /// <para>
    /// Flowery.NET has no common base class for its controls. Instead, every participating control
    /// type is registered once with <see cref="RegisterSurface{TControl}(DaisyNeumorphicSurface{TControl})"/>,
    /// which tells the effect which template border is the visible surface. Controls that compose a single surface out of
    /// child controls (tables, lists, joins) register only the container; their rows, cells and
    /// segments stay flat.
    /// </para>
    /// <para>
    /// Per-control attached properties override the global defaults (<see cref="GlobalMode"/>,
    /// <see cref="GlobalIntensity"/> and the theme-derived shadow colors).
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Turn the effect on for all registered controls
    /// DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Raised;
    ///
    /// // Per control (XAML)
    /// &lt;controls:DaisyCard controls:DaisyNeumorphic.Mode="Inset" controls:DaisyNeumorphic.Intensity="0.7" /&gt;
    /// </code>
    /// </example>
    public static partial class DaisyNeumorphic
    {
        private static bool _globalEnabled;
        private static DaisyNeumorphicMode _globalMode = DaisyNeumorphicMode.None;
        private static DaisyNeumorphicMode _lastNonNoneMode = DaisyNeumorphicMode.Raised;
        private static double _globalIntensity = 0.45;
        private static Color? _globalDarkShadowColor;
        private static Color? _globalLightShadowColor;

        #region Attached Properties

        /// <summary>
        /// Enables or disables the effect for a control. <c>null</c> falls back to <see cref="GlobalEnabled"/>.
        /// A <see cref="ScopeEnabledProperty"/> value on the control or an ancestor overrides this value.
        /// </summary>
        public static readonly AttachedProperty<bool?> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool?>("IsEnabled", typeof(DaisyNeumorphic));

        /// <summary>
        /// The effect mode for a control. <c>null</c> falls back to <see cref="GlobalMode"/>.
        /// </summary>
        public static readonly AttachedProperty<DaisyNeumorphicMode?> ModeProperty =
            AvaloniaProperty.RegisterAttached<Control, DaisyNeumorphicMode?>("Mode", typeof(DaisyNeumorphic));

        /// <summary>
        /// Shadow strength (0.0 to 1.0). <c>null</c> falls back to <see cref="GlobalIntensity"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> IntensityProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("Intensity", typeof(DaisyNeumorphic));

        /// <summary>
        /// Minimum shadow blur radius. Setting it explicitly replaces the size-derived blur.
        /// </summary>
        public static readonly AttachedProperty<double> BlurRadiusProperty =
            AvaloniaProperty.RegisterAttached<Control, double>("BlurRadius", typeof(DaisyNeumorphic), 4.0);

        /// <summary>
        /// Minimum shadow offset. Setting it explicitly replaces the size-derived offset.
        /// </summary>
        public static readonly AttachedProperty<double> OffsetProperty =
            AvaloniaProperty.RegisterAttached<Control, double>("Offset", typeof(DaisyNeumorphic), 4.0);

        /// <summary>
        /// Dark shadow color for a control. <c>null</c> falls back to <see cref="GlobalDarkShadowColor"/>.
        /// </summary>
        public static readonly AttachedProperty<Color?> DarkShadowColorProperty =
            AvaloniaProperty.RegisterAttached<Control, Color?>("DarkShadowColor", typeof(DaisyNeumorphic));

        /// <summary>
        /// Light shadow color for a control. <c>null</c> falls back to <see cref="GlobalLightShadowColor"/>.
        /// </summary>
        public static readonly AttachedProperty<Color?> LightShadowColorProperty =
            AvaloniaProperty.RegisterAttached<Control, Color?>("LightShadowColor", typeof(DaisyNeumorphic));

        /// <summary>
        /// Enables or disables the effect for a whole subtree. The nearest ancestor (or the control itself)
        /// with a value wins and takes precedence over <see cref="IsEnabledProperty"/>, even over an explicit
        /// <c>IsEnabled="False"</c> on a control inside the subtree. To opt a single control out of an
        /// enabled scope, set <c>ScopeEnabled="False"</c> on that control. A scope never enables rows, cells
        /// or segments that their container keeps flat.
        /// </summary>
        public static readonly AttachedProperty<bool?> ScopeEnabledProperty =
            AvaloniaProperty.RegisterAttached<Control, bool?>("ScopeEnabled", typeof(DaisyNeumorphic));

        /// <summary>
        /// Mode for a whole subtree. The nearest ancestor (or the control itself) with a value wins;
        /// a control's own <see cref="ModeProperty"/> still takes precedence. <c>null</c> falls back to <see cref="GlobalMode"/>.
        /// </summary>
        public static readonly AttachedProperty<DaisyNeumorphicMode?> ScopeModeProperty =
            AvaloniaProperty.RegisterAttached<Control, DaisyNeumorphicMode?>("ScopeMode", typeof(DaisyNeumorphic));

        /// <summary>
        /// Intensity for a whole subtree. The nearest ancestor (or the control itself) with a value wins;
        /// a control's own <see cref="IntensityProperty"/> still takes precedence. <c>null</c> falls back to <see cref="GlobalIntensity"/>.
        /// </summary>
        public static readonly AttachedProperty<double?> ScopeIntensityProperty =
            AvaloniaProperty.RegisterAttached<Control, double?>("ScopeIntensity", typeof(DaisyNeumorphic));

        public static bool? GetIsEnabled(Control element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(Control element, bool? value) => element.SetValue(IsEnabledProperty, value);

        public static DaisyNeumorphicMode? GetMode(Control element) => element.GetValue(ModeProperty);
        public static void SetMode(Control element, DaisyNeumorphicMode? value) => element.SetValue(ModeProperty, value);

        public static double? GetIntensity(Control element) => element.GetValue(IntensityProperty);
        public static void SetIntensity(Control element, double? value) => element.SetValue(IntensityProperty, value);

        public static double GetBlurRadius(Control element) => element.GetValue(BlurRadiusProperty);
        public static void SetBlurRadius(Control element, double value) => element.SetValue(BlurRadiusProperty, value);

        public static double GetOffset(Control element) => element.GetValue(OffsetProperty);
        public static void SetOffset(Control element, double value) => element.SetValue(OffsetProperty, value);

        public static Color? GetDarkShadowColor(Control element) => element.GetValue(DarkShadowColorProperty);
        public static void SetDarkShadowColor(Control element, Color? value) => element.SetValue(DarkShadowColorProperty, value);

        public static Color? GetLightShadowColor(Control element) => element.GetValue(LightShadowColorProperty);
        public static void SetLightShadowColor(Control element, Color? value) => element.SetValue(LightShadowColorProperty, value);

        public static bool? GetScopeEnabled(Control element) => element.GetValue(ScopeEnabledProperty);
        public static void SetScopeEnabled(Control element, bool? value) => element.SetValue(ScopeEnabledProperty, value);

        public static DaisyNeumorphicMode? GetScopeMode(Control element) => element.GetValue(ScopeModeProperty);
        public static void SetScopeMode(Control element, DaisyNeumorphicMode? value) => element.SetValue(ScopeModeProperty, value);

        public static double? GetScopeIntensity(Control element) => element.GetValue(ScopeIntensityProperty);
        public static void SetScopeIntensity(Control element, double? value) => element.SetValue(ScopeIntensityProperty, value);

        #endregion

        #region Global Settings

        /// <summary>
        /// Fired when any global neumorphic setting changes.
        /// </summary>
        public static event EventHandler? GlobalChanged;

        /// <summary>
        /// When true, registered controls draw the effect unless they opt out.
        /// Enabling while <see cref="GlobalMode"/> is None restores the last non-None mode (Raised by default).
        /// </summary>
        public static bool GlobalEnabled
        {
            get => _globalEnabled;
            set
            {
                if (_globalEnabled == value)
                    return;

                _globalEnabled = value;
                if (value && _globalMode == DaisyNeumorphicMode.None)
                {
                    _globalMode = _lastNonNoneMode;
                }

                RaiseGlobalChanged();
            }
        }

        /// <summary>
        /// Default mode for registered controls. Any mode other than None also sets
        /// <see cref="GlobalEnabled"/> to true; None sets it to false.
        /// </summary>
        public static DaisyNeumorphicMode GlobalMode
        {
            get => _globalMode;
            set
            {
                if (_globalMode == value)
                    return;

                if (value != DaisyNeumorphicMode.None)
                {
                    _lastNonNoneMode = value;
                }

                _globalMode = value;
                _globalEnabled = value != DaisyNeumorphicMode.None;
                RaiseGlobalChanged();
            }
        }

        /// <summary>
        /// Default shadow strength (0.0 to 1.0).
        /// </summary>
        public static double GlobalIntensity
        {
            get => _globalIntensity;
            set
            {
                if (Math.Abs(_globalIntensity - value) < 0.001)
                    return;

                _globalIntensity = value;
                RaiseGlobalChanged();
            }
        }

        /// <summary>
        /// Default dark shadow color. <c>null</c> (the default) derives it from the current theme:
        /// its Base300 color, darkened by 50% for light themes and 70% for dark themes.
        /// A value set here survives theme changes.
        /// </summary>
        public static Color? GlobalDarkShadowColor
        {
            get => _globalDarkShadowColor;
            set
            {
                if (_globalDarkShadowColor == value)
                    return;

                _globalDarkShadowColor = value;
                RaiseGlobalChanged();
            }
        }

        /// <summary>
        /// Default light shadow color. <c>null</c> (the default) uses white.
        /// A value set here survives theme changes.
        /// </summary>
        public static Color? GlobalLightShadowColor
        {
            get => _globalLightShadowColor;
            set
            {
                if (_globalLightShadowColor == value)
                    return;

                _globalLightShadowColor = value;
                RaiseGlobalChanged();
            }
        }

        private static void RaiseGlobalChanged()
        {
            GlobalChanged?.Invoke(null, EventArgs.Empty);
            RefreshAll();
        }

        #endregion

        #region Effective Values

        /// <summary>
        /// Gets the scope value from the control or its nearest ancestor that has one.
        /// </summary>
        public static bool? ResolveScopeEnabled(Control control) => ResolveScope(control, ScopeEnabledProperty);

        private static T? ResolveScope<T>(Control control, AttachedProperty<T?> property) where T : struct
        {
            foreach (var visual in control.GetSelfAndVisualAncestors())
            {
                if (visual is Control c && c.GetValue(property) is { } value)
                    return value;
            }

            return null;
        }

        /// <summary>
        /// Gets whether the effect is drawn for the control right now. In order of precedence:
        /// registration, exclusion and high contrast; then segment containers (both are structural and cannot
        /// be overridden); then scope (<see cref="ScopeEnabledProperty"/>); then the control's own
        /// <see cref="IsEnabledProperty"/>; then <see cref="GlobalEnabled"/>.
        /// </summary>
        public static bool IsEffectivelyEnabled(Control control)
        {
            var registration = GetRegistration(control.GetType());
            if (registration is not { DrawsEffect: true }
                || DaisyAccessibility.GetHighContrastEnabled(control)
                || IsInsideSegmentContainer(control))
                return false;

            var enabled = ResolveScopeEnabled(control)
                ?? GetIsEnabled(control)
                ?? (registration.EnabledByDefault && _globalEnabled);

            return enabled && GetEffectiveMode(control) != DaisyNeumorphicMode.None;
        }

        /// <summary>
        /// Gets the control's mode: its own <see cref="ModeProperty"/>, then the nearest <see cref="ScopeModeProperty"/>,
        /// then <see cref="GlobalMode"/>.
        /// </summary>
        public static DaisyNeumorphicMode GetEffectiveMode(Control control) =>
            GetMode(control) ?? ResolveScope(control, ScopeModeProperty) ?? _globalMode;

        /// <summary>
        /// Gets the control's intensity clamped to 0.0 to 1.0: its own <see cref="IntensityProperty"/>, then the
        /// nearest <see cref="ScopeIntensityProperty"/>, then <see cref="GlobalIntensity"/>.
        /// </summary>
        public static double GetEffectiveIntensity(Control control) =>
            Math.Clamp(GetIntensity(control) ?? ResolveScope(control, ScopeIntensityProperty) ?? _globalIntensity, 0.0, 1.0);

        /// <summary>
        /// Gets the control's dark shadow color: its own value, then <see cref="GlobalDarkShadowColor"/>,
        /// then the color derived from the current theme.
        /// </summary>
        public static Color GetEffectiveDarkShadowColor(Control control) =>
            GetDarkShadowColor(control) ?? _globalDarkShadowColor ?? GetThemeShadowColors().Dark;

        /// <summary>
        /// Gets the control's light shadow color: its own value, then <see cref="GlobalLightShadowColor"/>, then white.
        /// </summary>
        public static Color GetEffectiveLightShadowColor(Control control) =>
            GetLightShadowColor(control) ?? _globalLightShadowColor ?? GetThemeShadowColors().Light;

        /// <summary>
        /// Gets the shadow elevation for the control from its DaisySize Size property, or the global size.
        /// </summary>
        public static double GetEffectiveElevation(Control control)
        {
            var size = AvaloniaPropertyRegistry.Instance.FindRegistered(control, "Size") is StyledProperty<DaisySize> sizeProperty
                ? control.GetValue(sizeProperty)
                : FlowerySizeManager.CurrentSize;

            return DaisyResourceLookup.GetDefaultElevation(size);
        }

        #endregion
    }
}
