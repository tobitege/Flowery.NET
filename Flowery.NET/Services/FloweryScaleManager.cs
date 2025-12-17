using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Services
{
    /// <summary>
    /// Provides opt-in automatic font scaling for Daisy controls based on window size.
    /// Set EnableScaling="True" on any container to make all child Daisy controls auto-scale.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;UserControl services:FloweryScaleManager.EnableScaling="True"&gt;
    ///     &lt;!-- All Daisy controls will auto-scale their text --&gt;
    ///     &lt;controls:DaisyInput Label="Street" /&gt;
    /// &lt;/UserControl&gt;
    /// </code>
    /// </example>
    public static class FloweryScaleManager
    {
        internal const double MaxSanityScaleFactor = 5.0;

        // Use FloweryScaleConfig as the single source of truth for configuration
        private static readonly Dictionary<TopLevel, TopLevelScaleTracker> _topLevelTrackers = new();

        private static bool _isEnabled = true;
        private static double? _overrideScaleFactor;

        /// <summary>
        /// Gets or sets whether global scaling is enabled. When disabled, scale factor defaults to 1.0.
        /// This can be toggled at runtime via the sidebar or programmatically.
        /// </summary>
        public static bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value)
                    return;

                _isEnabled = value;
                IsEnabledChanged?.Invoke(null, value);

                // Recalculate scale for all tracked windows
                foreach (var tracker in _topLevelTrackers.Values)
                {
                    tracker.ForceRecalculate();
                }
            }
        }

        /// <summary>
        /// Event raised when the IsEnabled property changes.
        /// </summary>
        public static event EventHandler<bool>? IsEnabledChanged;

        #region Attached Properties

        /// <summary>
        /// Enables automatic font scaling for all Daisy controls within this container.
        /// </summary>
        public static readonly AttachedProperty<bool> EnableScalingProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, bool>(
                "EnableScaling",
                false,
                inherits: true);

        /// <summary>
        /// The current scale factor (0.5 to 1.0) based on window size.
        /// This is automatically calculated and set when EnableScaling is true.
        /// </summary>
        public static readonly AttachedProperty<double> ScaleFactorProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, double>(
                "ScaleFactor",
                1.0,
                inherits: true);

        /// <summary>
        /// Event raised when the scale factor changes for any window.
        /// </summary>
        public static event EventHandler<ScaleChangedEventArgs>? ScaleFactorChanged;

        /// <summary>
        /// Event raised when scale configuration values change (e.g. MaxScaleFactor).
        /// This can be used by bindings (like ScaleExtension) to refresh without requiring a resize.
        /// </summary>
        public static event EventHandler? ScaleConfigChanged;

        #endregion

        #region Property Accessors

        public static bool GetEnableScaling(Control control) => control.GetValue(EnableScalingProperty);
        public static void SetEnableScaling(Control control, bool value) => control.SetValue(EnableScalingProperty, value);

        public static double GetScaleFactor(Control control) => control.GetValue(ScaleFactorProperty);
        internal static void SetScaleFactor(Control control, double value) => control.SetValue(ScaleFactorProperty, value);

        #endregion

        #region Configuration (delegates to FloweryScaleConfig)

        /// <summary>
        /// Gets or sets the reference width for scale calculations (default: 1920).
        /// </summary>
        public static double ReferenceWidth
        {
            get => FloweryScaleConfig.ReferenceWidth;
            set => FloweryScaleConfig.ReferenceWidth = value;
        }

        /// <summary>
        /// Gets or sets the reference height for scale calculations (default: 1080).
        /// </summary>
        public static double ReferenceHeight
        {
            get => FloweryScaleConfig.ReferenceHeight;
            set => FloweryScaleConfig.ReferenceHeight = value;
        }

        /// <summary>
        /// Gets or sets the minimum scale factor (default: 0.6).
        /// </summary>
        public static double MinScaleFactor
        {
            get => FloweryScaleConfig.MinScaleFactor;
            set => FloweryScaleConfig.MinScaleFactor = value;
        }

        /// <summary>
        /// Gets or sets the maximum scale factor (default: 1.0).
        /// </summary>
        public static double MaxScaleFactor
        {
            get => FloweryScaleConfig.MaxScaleFactor;
            set
            {
                var clamped = value;
                if (double.IsNaN(clamped) || double.IsInfinity(clamped) || clamped <= 0)
                    clamped = 1.0;

                // Sanity cap: prevent extreme values (e.g. accidental 5000%).
                if (clamped > MaxSanityScaleFactor)
                    clamped = MaxSanityScaleFactor;

                if (Math.Abs(FloweryScaleConfig.MaxScaleFactor - clamped) < 0.001)
                    return;

                FloweryScaleConfig.MaxScaleFactor = clamped;
                ScaleConfigChanged?.Invoke(null, EventArgs.Empty);

                // Recalculate scale for all tracked top levels.
                foreach (var tracker in _topLevelTrackers.Values)
                {
                    tracker.ForceRecalculate();
                }
            }
        }

        /// <summary>
        /// Gets or sets a manual scale factor override.
        /// When set, window-size based scaling is bypassed and this value is used instead.
        /// Set to null to use automatic scaling.
        /// </summary>
        public static double? OverrideScaleFactor
        {
            get => _overrideScaleFactor;
            set
            {
                double? clamped = value;
                if (clamped.HasValue)
                {
                    var v = clamped.Value;
                    if (double.IsNaN(v) || double.IsInfinity(v) || v <= 0)
                        v = 1.0;

                    // Sanity cap: prevent extreme values (e.g. accidental 5000%).
                    if (v > MaxSanityScaleFactor)
                        v = MaxSanityScaleFactor;

                    clamped = v;
                }

                if (_overrideScaleFactor == clamped)
                    return;

                _overrideScaleFactor = clamped;
                ScaleConfigChanged?.Invoke(null, EventArgs.Empty);

                // Recalculate scale for all tracked top levels.
                foreach (var tracker in _topLevelTrackers.Values)
                {
                    tracker.ForceRecalculate();
                }
            }
        }

        #endregion

        #region Static Constructor

        static FloweryScaleManager()
        {
            EnableScalingProperty.Changed.AddClassHandler<Control>(OnEnableScalingChanged);
        }

        #endregion

        #region Event Handlers

        private static void OnEnableScalingChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                // Subscribe to visual tree attachment to find the window
                control.AttachedToVisualTree += OnControlAttachedToVisualTree;

                // If already attached, set up tracking immediately
                if (control.IsAttachedToVisualTree())
                {
                    SetupWindowTracking(control);
                }
            }
            else
            {
                control.AttachedToVisualTree -= OnControlAttachedToVisualTree;
            }
        }

        private static void OnControlAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control control)
            {
                SetupWindowTracking(control);
            }
        }

        #endregion

        #region Window Tracking

        private static void SetupWindowTracking(Control control)
        {
            var topLevel = GetParentTopLevel(control);
            if (topLevel == null)
                return;

            // Check if we already have a tracker for this window
            if (!_topLevelTrackers.TryGetValue(topLevel, out var tracker))
            {
                tracker = new TopLevelScaleTracker(topLevel);
                _topLevelTrackers[topLevel] = tracker;

                // Clean up when window closes
                if (topLevel is Window window)
                {
                    window.Closed += (s, e) =>
                    {
                        if (s is Window w && _topLevelTrackers.ContainsKey(w))
                        {
                            _topLevelTrackers[w].Dispose();
                            _topLevelTrackers.Remove(w);
                        }
                    };
                }
            }

            // Apply initial scale factor to this control and all its children
            var scaleFactor = tracker.CurrentScaleFactor;
            ApplyScaleToControlTree(control, scaleFactor);
        }

        /// <summary>
        /// Applies scale factor to a control and recursively to all children that implement IScalableControl.
        /// Only applies to controls within an EnableScaling region.
        /// </summary>
        private static void ApplyScaleToControlTree(Visual visual, double scaleFactor)
        {
            if (visual is Control control && GetEnableScaling(control))
            {
                SetScaleFactor(control, scaleFactor);

                // Call ApplyScaleFactor on controls that implement IScalableControl
                if (visual is IScalableControl scalable)
                {
                    scalable.ApplyScaleFactor(scaleFactor);
                }
            }

            // Recurse to children
            foreach (var child in visual.GetVisualChildren())
            {
                ApplyScaleToControlTree(child, scaleFactor);
            }
        }

        private static TopLevel? GetParentTopLevel(Control control)
        {
            return TopLevel.GetTopLevel(control);
        }

        #endregion

        #region Scale Calculation

        /// <summary>
        /// Calculates the scale factor for a given window size.
        /// </summary>
        public static double CalculateScaleFactor(double width, double height)
        {
            if (width <= 0 || height <= 0)
                return 1.0;

            if (_overrideScaleFactor.HasValue)
            {
                var manual = SanitizeScaleFactor(_overrideScaleFactor.Value);
                if (manual > FloweryScaleConfig.MaxScaleFactor)
                    manual = FloweryScaleConfig.MaxScaleFactor;

                return manual;
            }

            var widthScale = width / FloweryScaleConfig.ReferenceWidth;
            var heightScale = height / FloweryScaleConfig.ReferenceHeight;

            // Use the smaller scale to ensure content fits
            var scale = Math.Min(widthScale, heightScale);

            return Math.Max(FloweryScaleConfig.MinScaleFactor, Math.Min(scale, FloweryScaleConfig.MaxScaleFactor));
        }

        internal static double CalculateScaleFactor(double width, double height, double renderScaling)
        {
            if (width <= 0 || height <= 0)
                return 1.0;

            var widthScale = width / FloweryScaleConfig.ReferenceWidth;
            var heightScale = height / FloweryScaleConfig.ReferenceHeight;

            var min = FloweryScaleConfig.MinScaleFactor;
            var maxPhysical = FloweryScaleConfig.MaxScaleFactor;
            var max = maxPhysical;

            // Treat MaxScaleFactor as a physical max. Window sizing/rendering uses DIPs, so convert the max to DIPs
            // to keep the physical cap consistent across Windows DPI scaling.
            if (renderScaling > 0)
            {
                max /= renderScaling;
            }

            if (_overrideScaleFactor.HasValue)
            {
                var manualPhysical = SanitizeScaleFactor(_overrideScaleFactor.Value);

                // Keep the manual override within the current physical max.
                if (manualPhysical > maxPhysical)
                    manualPhysical = maxPhysical;

                var manual = manualPhysical;
                if (renderScaling > 0)
                {
                    manual /= renderScaling;
                }

                return Math.Min(manual, max);
            }

            // Default: fit to the smaller dimension (keeps content from exploding on cramped windows).
            var fitScale = Math.Min(widthScale, heightScale);

            // When scaling-up is enabled, allow upscaling even if only one dimension exceeds the reference
            // by using the larger dimension for scale-up. This avoids "stuck at 1.0" behavior in browsers
            // where height is often limited by browser chrome.
            var scale = fitScale;
            if (maxPhysical > 1.0)
            {
                var upScale = Math.Max(widthScale, heightScale);
                if (upScale > 1.0)
                    scale = upScale;
            }

            return Math.Max(min, Math.Min(scale, max));
        }

        /// <summary>
        /// Applies a scale factor to a base value.
        /// </summary>
        public static double ApplyScale(double baseValue, double scaleFactor)
        {
            scaleFactor = SanitizeScaleFactor(scaleFactor);
            return baseValue * scaleFactor;
        }

        /// <summary>
        /// Applies a scale factor with a minimum value.
        /// </summary>
        public static double ApplyScale(double baseValue, double minValue, double scaleFactor)
        {
            scaleFactor = SanitizeScaleFactor(scaleFactor);
            var scaled = baseValue * scaleFactor;
            return Math.Max(scaled, minValue);
        }

        internal static double SanitizeScaleFactor(double scaleFactor)
        {
            if (double.IsNaN(scaleFactor) || double.IsInfinity(scaleFactor) || scaleFactor <= 0)
                return 1.0;

            return scaleFactor > MaxSanityScaleFactor ? MaxSanityScaleFactor : scaleFactor;
        }

        internal static void RaiseScaleFactorChanged(Window window, double scaleFactor)
        {
            ScaleFactorChanged?.Invoke(null, new ScaleChangedEventArgs(window, scaleFactor));
        }

        #endregion

        #region Window Scale Tracker

        private class TopLevelScaleTracker : IDisposable
        {
            private readonly TopLevel _topLevel;
            private bool _isDisposed;
            private DispatcherTimer? _debounceTimer;
            private double _pendingScaleFactor;

            // Debounce delay in milliseconds - prevents "snap back" during rapid resize events
            private const int DebounceDelayMs = 50;

            public double CurrentScaleFactor { get; private set; } = 1.0;

            public TopLevelScaleTracker(TopLevel topLevel)
            {
                _topLevel = topLevel;

                // Subscribe to size changes
                _topLevel.PropertyChanged += OnTopLevelPropertyChanged;

                // Calculate initial scale (no debounce for initial)
                UpdateScaleFactorImmediate();
            }

            private void OnTopLevelPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
            {
                if (_isDisposed) return;

                if (e.Property == TopLevel.BoundsProperty || e.Property == TopLevel.ClientSizeProperty)
                {
                    // Debounce rapid resize events to prevent "snap back" in browser
                    ScheduleDebouncedUpdate();
                }
            }

            private void ScheduleDebouncedUpdate()
            {
                var size = _topLevel.ClientSize;
                var renderScaling = _topLevel is Window ? _topLevel.RenderScaling : 0;
                _pendingScaleFactor = CalculateScaleFactor(size.Width, size.Height, renderScaling);

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
                if (_isDisposed) return;

                ApplyScaleFactor(_pendingScaleFactor);
            }

            /// <summary>
            /// Forces a recalculation of the scale factor (used when IsEnabled changes).
            /// </summary>
            public void ForceRecalculate()
            {
                if (_isDisposed) return;

                // Reset current to force update
                CurrentScaleFactor = -1;
                UpdateScaleFactorImmediate();
            }

            private void UpdateScaleFactorImmediate()
            {
                double newFactor;

                if (!IsEnabled)
                {
                    // When scaling is disabled, use factor of 1.0
                    newFactor = 1.0;
                }
                else
                {
                    var size = _topLevel.ClientSize;
                    var renderScaling = _topLevel is Window ? _topLevel.RenderScaling : 0;
                    newFactor = CalculateScaleFactor(size.Width, size.Height, renderScaling);
                }

                ApplyScaleFactor(newFactor);
            }

            private void ApplyScaleFactor(double newFactor)
            {
                if (Math.Abs(newFactor - CurrentScaleFactor) > 0.001)
                {
                    CurrentScaleFactor = newFactor;

                    // Update all controls with EnableScaling in this window
                    Dispatcher.UIThread.Post(() =>
                    {
                        PropagateScaleFactor(_topLevel, newFactor);
                        if (_topLevel is Window window)
                        {
                            RaiseScaleFactorChanged(window, newFactor);
                        }
                    }, DispatcherPriority.Render);
                }
            }

            private void PropagateScaleFactor(Visual visual, double scaleFactor)
            {
                if (visual is Control control)
                {
                    // Only apply scaling to controls within an EnableScaling region
                    if (GetEnableScaling(control))
                    {
                        // Set the scale factor on all controls (for binding/styling purposes)
                        SetScaleFactor(control, scaleFactor);

                        // Call ApplyScaleFactor on controls that implement IScalableControl
                        if (visual is IScalableControl scalable)
                        {
                            scalable.ApplyScaleFactor(scaleFactor);
                        }
                    }
                }

                // Recurse to children using VisualTree extension
                foreach (var child in visual.GetVisualChildren())
                {
                    PropagateScaleFactor(child, scaleFactor);
                }
            }

            public void Dispose()
            {
                if (_isDisposed) return;
                _isDisposed = true;
                _topLevel.PropertyChanged -= OnTopLevelPropertyChanged;

                if (_debounceTimer != null)
                {
                    _debounceTimer.Stop();
                    _debounceTimer.Tick -= OnDebounceTimerTick;
                    _debounceTimer = null;
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Event args for scale factor changes.
    /// </summary>
    public class ScaleChangedEventArgs : EventArgs
    {
        public Window Window { get; }
        public double ScaleFactor { get; }

        public ScaleChangedEventArgs(Window window, double scaleFactor)
        {
            Window = window;
            ScaleFactor = scaleFactor;
        }
    }
}
