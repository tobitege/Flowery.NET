using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Services
{
    /// <summary>
    /// Predefined scale presets for common UI element sizes.
    /// Use these semantic names instead of raw "baseValue,minValue" strings.
    /// </summary>
    public enum FloweryScalePreset
    {
        /// <summary>Custom value - use BaseValue and MinValue properties</summary>
        Custom,

        // ═══════════════════════════════════════════════════════════════
        // FONT SIZES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Display/Hero text: 32pt base, 20pt minimum</summary>
        FontDisplay,

        /// <summary>Page title: 28pt base, 18pt minimum</summary>
        FontTitle,

        /// <summary>Section heading: 20pt base, 14pt minimum</summary>
        FontHeading,

        /// <summary>Subheading: 16pt base, 11pt minimum</summary>
        FontSubheading,

        /// <summary>Body text: 14pt base, 10pt minimum</summary>
        FontBody,

        /// <summary>Caption/secondary text: 12pt base, 9pt minimum</summary>
        FontCaption,

        /// <summary>Small/fine print: 11pt base, 8pt minimum</summary>
        FontSmall,

        // ═══════════════════════════════════════════════════════════════
        // SPACING / PADDING
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Extra large spacing: 40px base</summary>
        SpacingXL,

        /// <summary>Large spacing: 28px base</summary>
        SpacingLarge,

        /// <summary>Medium spacing: 20px base</summary>
        SpacingMedium,

        /// <summary>Small spacing: 12px base</summary>
        SpacingSmall,

        /// <summary>Extra small spacing: 8px base</summary>
        SpacingXS,

        // ═══════════════════════════════════════════════════════════════
        // DIMENSIONS / SIZES
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Card/container width: 400px base</summary>
        CardWidth,

        /// <summary>Card/container height: 120px base, 100px minimum</summary>
        CardHeight,

        /// <summary>Large icon container: 140px base</summary>
        IconContainerLarge,

        /// <summary>Medium icon container: 40px base</summary>
        IconContainerMedium,

        /// <summary>Small icon container: 24px base</summary>
        IconContainerSmall,

        /// <summary>Large icon: 32px base</summary>
        IconLarge,

        /// <summary>Medium icon: 24px base</summary>
        IconMedium,

        /// <summary>Small icon: 16px base</summary>
        IconSmall,

        /// <summary>Button/input height: 40px base</summary>
        ControlHeight,

        /// <summary>Thumbnail size: 80px base</summary>
        Thumbnail,

        /// <summary>Avatar size: 48px base</summary>
        Avatar,

        // ═══════════════════════════════════════════════════════════════
        // SPECIAL COMBINATIONS (from observed usage)
        // ═══════════════════════════════════════════════════════════════

        /// <summary>Large panel/dialog: 250px base, 200px minimum</summary>
        PanelLarge,

        /// <summary>Medium panel: 140px base</summary>
        PanelMedium,

        /// <summary>Theme selector/preview: 13pt base, 11pt minimum</summary>
        FontThemeLabel,
    }

    /// <summary>
    /// Configuration class for customizing scale preset values.
    /// Apps can set custom values at startup to override defaults.
    /// </summary>
    public static class FloweryScaleConfig
    {
        // Reference dimensions for 100% scaling (Full HD - fonts at base size at this resolution)
        public static double ReferenceWidth { get; set; } = 1920;
        public static double ReferenceHeight { get; set; } = 1080;
        public static double MinScaleFactor { get; set; } = 0.6;
        public static double MaxScaleFactor { get; set; } = 1.0;

        // Preset value overrides - apps can modify these
        private static readonly Dictionary<FloweryScalePreset, (double baseValue, double? minValue)> _presetValues = new()
        {
            // Fonts
            [FloweryScalePreset.FontDisplay] = (32, 20),
            [FloweryScalePreset.FontTitle] = (28, 18),
            [FloweryScalePreset.FontHeading] = (20, 14),
            [FloweryScalePreset.FontSubheading] = (16, 11),
            [FloweryScalePreset.FontBody] = (14, 10),
            [FloweryScalePreset.FontCaption] = (12, 9),
            [FloweryScalePreset.FontSmall] = (11, 8),

            // Spacing
            [FloweryScalePreset.SpacingXL] = (40, null),
            [FloweryScalePreset.SpacingLarge] = (28, null),
            [FloweryScalePreset.SpacingMedium] = (20, null),
            [FloweryScalePreset.SpacingSmall] = (12, null),
            [FloweryScalePreset.SpacingXS] = (8, null),

            // Dimensions
            [FloweryScalePreset.CardWidth] = (400, null),
            [FloweryScalePreset.CardHeight] = (120, 100),
            [FloweryScalePreset.IconContainerLarge] = (140, null),
            [FloweryScalePreset.IconContainerMedium] = (40, null),
            [FloweryScalePreset.IconContainerSmall] = (24, null),
            [FloweryScalePreset.IconLarge] = (32, null),
            [FloweryScalePreset.IconMedium] = (24, null),
            [FloweryScalePreset.IconSmall] = (16, null),
            [FloweryScalePreset.ControlHeight] = (40, null),
            [FloweryScalePreset.Thumbnail] = (80, null),
            [FloweryScalePreset.Avatar] = (48, null),

            // Special
            [FloweryScalePreset.PanelLarge] = (250, 200),
            [FloweryScalePreset.PanelMedium] = (140, null),
            [FloweryScalePreset.FontThemeLabel] = (13, 11),
        };

        /// <summary>
        /// Gets the base and minimum values for a preset.
        /// </summary>
        public static (double baseValue, double? minValue) GetPresetValues(FloweryScalePreset preset)
        {
            return _presetValues.TryGetValue(preset, out var values) ? values : (0, null);
        }

        /// <summary>
        /// Sets custom values for a preset. Call at app startup.
        /// </summary>
        public static void SetPresetValues(FloweryScalePreset preset, double baseValue, double? minValue = null)
        {
            _presetValues[preset] = (baseValue, minValue);
        }

        /// <summary>
        /// Resets a preset to its default value.
        /// </summary>
        public static void ResetPreset(FloweryScalePreset preset)
        {
            // Re-apply defaults
            var defaults = GetDefaultValues(preset);
            _presetValues[preset] = defaults;
        }

        private static (double, double?) GetDefaultValues(FloweryScalePreset preset) => preset switch
        {
            FloweryScalePreset.FontDisplay => (32, 20),
            FloweryScalePreset.FontTitle => (28, 18),
            FloweryScalePreset.FontHeading => (20, 14),
            FloweryScalePreset.FontSubheading => (16, 11),
            FloweryScalePreset.FontBody => (14, 10),
            FloweryScalePreset.FontCaption => (12, 9),
            FloweryScalePreset.FontSmall => (11, 8),
            FloweryScalePreset.SpacingXL => (40, null),
            FloweryScalePreset.SpacingLarge => (28, null),
            FloweryScalePreset.SpacingMedium => (20, null),
            FloweryScalePreset.SpacingSmall => (12, null),
            FloweryScalePreset.SpacingXS => (8, null),
            FloweryScalePreset.CardWidth => (400, null),
            FloweryScalePreset.CardHeight => (120, 100),
            FloweryScalePreset.IconContainerLarge => (140, null),
            FloweryScalePreset.IconContainerMedium => (40, null),
            FloweryScalePreset.IconContainerSmall => (24, null),
            FloweryScalePreset.IconLarge => (32, null),
            FloweryScalePreset.IconMedium => (24, null),
            FloweryScalePreset.IconSmall => (16, null),
            FloweryScalePreset.ControlHeight => (40, null),
            FloweryScalePreset.Thumbnail => (80, null),
            FloweryScalePreset.Avatar => (48, null),
            FloweryScalePreset.PanelLarge => (250, 200),
            FloweryScalePreset.PanelMedium => (140, null),
            FloweryScalePreset.FontThemeLabel => (13, 11),
            _ => (0, null)
        };
    }

    /// <summary>
    /// Markup extension for responsive scaling with minimal XAML syntax.
    /// Automatically finds the parent window and scales values based on window size.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;!-- Using presets --&gt;
    /// &lt;TextBlock FontSize="{services:Scale FontTitle}"/&gt;
    /// &lt;Border Padding="{services:Scale SpacingMedium}"/&gt;
    /// &lt;Grid Width="{services:Scale CardWidth}"/&gt;
    ///
    /// &lt;!-- Using custom values (base,min) --&gt;
    /// &lt;TextBlock FontSize="{services:Scale 24,12}"/&gt;
    /// &lt;Border Width="{services:Scale 300}"/&gt;
    ///
    /// &lt;!-- Using explicit properties --&gt;
    /// &lt;TextBlock FontSize="{services:Scale Preset=Custom, BaseValue=24, MinValue=12}"/&gt;
    /// </code>
    /// </example>
    public class ScaleExtension : MarkupExtension
    {
        private static readonly Dictionary<Control, Window?> _windowCache = new();

        /// <summary>
        /// The scale preset to use. Ignored if Value is specified.
        /// </summary>
        public FloweryScalePreset Preset { get; set; } = FloweryScalePreset.Custom;

        /// <summary>
        /// Custom base value (when Preset is Custom or for override).
        /// </summary>
        public double BaseValue { get; set; }

        /// <summary>
        /// Custom minimum value (when Preset is Custom or for override).
        /// </summary>
        public double? MinValue { get; set; }

        /// <summary>
        /// Direct value string in format "baseValue" or "baseValue,minValue".
        /// This is the default constructor argument.
        /// </summary>
        [ConstructorArgument("value")]
        public string? Value { get; set; }

        public ScaleExtension()
        {
        }

        /// <summary>
        /// Creates a ScaleExtension with a preset name or value string.
        /// </summary>
        /// <param name="value">Preset name (e.g., "FontTitle") or value string (e.g., "24,12")</param>
        public ScaleExtension(string value)
        {
            // Try to parse as preset first
            if (Enum.TryParse<FloweryScalePreset>(value, true, out var preset))
            {
                Preset = preset;
            }
            else
            {
                Value = value;
            }
        }

        /// <summary>
        /// Creates a ScaleExtension with a preset.
        /// </summary>
        public ScaleExtension(FloweryScalePreset preset)
        {
            Preset = preset;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            var (baseValue, minValue) = ResolveValues();

            if (baseValue <= 0)
            {
                return 0.0;
            }

            // Get the target object to find the window
            var provideValueTarget = serviceProvider.GetService(typeof(IProvideValueTarget)) as IProvideValueTarget;
            var targetObject = provideValueTarget?.TargetObject;

            if (targetObject is not Control control)
            {
                // Design-time or non-control target - return base value
                return baseValue;
            }

            var targetProperty = provideValueTarget?.TargetProperty as AvaloniaProperty;

            // Create a binding to the window's bounds that uses our converter
            return new Binding
            {
                Source = new ScaleBindingSource(control, baseValue, minValue),
                Path = "ScaledValue",
                Mode = BindingMode.OneWay
            };
        }

        private (double baseValue, double? minValue) ResolveValues()
        {
            // Priority 1: Explicit Value string
            if (!string.IsNullOrEmpty(Value))
            {
                var parts = Value!.Split(',');
                if (double.TryParse(parts[0].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var bv))
                {
                    double? mv = null;
                    if (parts.Length > 1 && double.TryParse(parts[1].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsedMin))
                    {
                        mv = parsedMin;
                    }
                    return (bv, mv);
                }
            }

            // Priority 2: Preset
            if (Preset != FloweryScalePreset.Custom)
            {
                return FloweryScaleConfig.GetPresetValues(Preset);
            }

            // Priority 3: Explicit BaseValue/MinValue properties
            return (BaseValue, MinValue);
        }
    }

    /// <summary>
    /// Helper class that provides the scaled value and subscribes to window size changes.
    /// </summary>
    internal class ScaleBindingSource : AvaloniaObject
    {
        public static readonly DirectProperty<ScaleBindingSource, double> ScaledValueProperty =
            AvaloniaProperty.RegisterDirect<ScaleBindingSource, double>(
                nameof(ScaledValue),
                o => o.ScaledValue);

        private readonly Control _control;
        private readonly double _baseValue;
        private readonly double? _minValue;
        private TopLevel? _topLevel;
        private double _scaledValue;
        private bool _isSubscribed;
        private DispatcherTimer? _debounceTimer;

        // Debounce delay in milliseconds - prevents "snap back" during rapid resize events
        private const int DebounceDelayMs = 50;

        public double ScaledValue
        {
            get => _scaledValue;
            private set => SetAndRaise(ScaledValueProperty, ref _scaledValue, value);
        }

        public ScaleBindingSource(Control control, double baseValue, double? minValue)
        {
            _control = control;
            _baseValue = baseValue;
            _minValue = minValue;
            _scaledValue = baseValue; // Initial value

            // Subscribe when control is attached to visual tree
            control.AttachedToVisualTree += OnAttachedToVisualTree;
            control.DetachedFromVisualTree += OnDetachedFromVisualTree;

            // Try to get window immediately if already attached
            TrySubscribeToWindow();
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            TrySubscribeToWindow();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            UnsubscribeFromWindow();
        }

        private void TrySubscribeToWindow()
        {
            if (_isSubscribed) return;

            _topLevel = TopLevel.GetTopLevel(_control);
            if (_topLevel == null) return;

            _topLevel.PropertyChanged += OnTopLevelPropertyChanged;
            FloweryScaleManager.IsEnabledChanged += OnScalingEnabledChanged;
            FloweryScaleManager.ScaleConfigChanged += OnScaleConfigChanged;
            _isSubscribed = true;

            // Calculate initial value (no debounce for initial)
            UpdateScaledValueImmediate();
        }

        private void UnsubscribeFromWindow()
        {
            if (_isSubscribed)
            {
                if (_topLevel != null)
                {
                    _topLevel.PropertyChanged -= OnTopLevelPropertyChanged;
                }
                FloweryScaleManager.IsEnabledChanged -= OnScalingEnabledChanged;
                FloweryScaleManager.ScaleConfigChanged -= OnScaleConfigChanged;
                _isSubscribed = false;
            }

            if (_debounceTimer != null)
            {
                _debounceTimer.Stop();
                _debounceTimer.Tick -= OnDebounceTimerTick;
                _debounceTimer = null;
            }
        }

        private void OnScalingEnabledChanged(object? sender, bool isEnabled)
        {
            UpdateScaledValueImmediate(); // Config changes apply immediately
        }

        private void OnScaleConfigChanged(object? sender, EventArgs e)
        {
            UpdateScaledValueImmediate(); // Config changes apply immediately
        }

        private void OnTopLevelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.Property == TopLevel.BoundsProperty || e.Property == TopLevel.ClientSizeProperty)
            {
                // Debounce rapid resize events to prevent "snap back" in browser
                ScheduleDebouncedUpdate();
            }
        }

        private void ScheduleDebouncedUpdate()
        {
            if (_debounceTimer == null)
            {
                _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(DebounceDelayMs) };
                _debounceTimer.Tick += OnDebounceTimerTick;
            }

            // Reset the timer on each event
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        private void OnDebounceTimerTick(object? sender, EventArgs e)
        {
            _debounceTimer?.Stop();
            UpdateScaledValueImmediate();
        }

        private void UpdateScaledValueImmediate()
        {
            // When scaling is disabled, use base value
            if (!FloweryScaleManager.IsEnabled)
            {
                ScaledValue = _baseValue;
                return;
            }

            if (_topLevel == null)
            {
                ScaledValue = _baseValue;
                return;
            }

            var size = _topLevel.ClientSize;
            if (size.Width <= 0 || size.Height <= 0)
            {
                ScaledValue = _baseValue;
                return;
            }

            var renderScaling = _topLevel is Window ? _topLevel.RenderScaling : 0;
            var scale = FloweryScaleManager.CalculateScaleFactor(size.Width, size.Height, renderScaling);

            double result = _baseValue * scale;

            // Apply minimum if specified
            if (_minValue.HasValue)
            {
                result = Math.Max(_minValue.Value, result);
            }

            ScaledValue = result;
        }
    }
}
