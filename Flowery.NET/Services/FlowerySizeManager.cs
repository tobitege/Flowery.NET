using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace Flowery.Controls
{
    /// <summary>
    /// Specifies which font size tier to use for responsive scaling.
    /// </summary>
    public enum ResponsiveFontTier
    {
        /// <summary>No responsive font sizing.</summary>
        None,
        /// <summary>Primary font size (body text).</summary>
        Primary,
        /// <summary>Secondary font size (hints, captions).</summary>
        Secondary,
        /// <summary>Tertiary font size (very small text).</summary>
        Tertiary,
        /// <summary>Header font size (section titles).</summary>
        Header
    }

    /// <summary>
    /// Centralized size manager for DaisyUI controls.
    /// Controls that subscribe to SizeChanged can dynamically update their Size property
    /// when the global size is changed via ApplySize.
    /// </summary>
    /// <remarks>
    /// This service enables a "global size override" feature for applications that want
    /// consistent sizing across all DaisyUI controls. Individual controls can opt-in by
    /// subscribing to the <see cref="SizeChanged"/> event.
    /// </remarks>
    /// <example>
    /// <code>
    /// // Apply a global size
    /// FlowerySizeManager.ApplySize(DaisySize.Small);
    ///
    /// // Check current size
    /// DaisySize currentSize = FlowerySizeManager.CurrentSize;
    ///
    /// // Listen for size changes
    /// FlowerySizeManager.SizeChanged += (sender, size) => {
    ///     // Handle size change
    /// };
    /// </code>
    /// </example>
    public static class FlowerySizeManager
    {
        private static DaisySize _currentSize = DaisySize.Small;

        /// <summary>
        /// Attached property to mark a control as ignoring global size changes.
        /// Use this on demonstration controls that should show specific sizes.
        /// </summary>
        /// <example>
        /// <code>
        /// &lt;controls:DaisyButton controls:FlowerySizeManager.IgnoreGlobalSize="True" Size="Large" /&gt;
        /// </code>
        /// </example>
        public static readonly AttachedProperty<bool> IgnoreGlobalSizeProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, bool>("IgnoreGlobalSize", false);

        /// <summary>
        /// Gets whether the control ignores global size changes.
        /// </summary>
        public static bool GetIgnoreGlobalSize(Control control) =>
            control.GetValue(IgnoreGlobalSizeProperty);

        /// <summary>
        /// Sets whether the control ignores global size changes.
        /// </summary>
        public static void SetIgnoreGlobalSize(Control control, bool value) =>
            control.SetValue(IgnoreGlobalSizeProperty, value);

        /// <summary>
        /// Attached property to make a TextBlock's FontSize respond to global size changes.
        /// </summary>
        /// <example>
        /// <code>
        /// &lt;TextBlock Text="Description" controls:FlowerySizeManager.ResponsiveFont="Primary" /&gt;
        /// &lt;TextBlock Text="Hint" controls:FlowerySizeManager.ResponsiveFont="Secondary" /&gt;
        /// &lt;TextBlock Text="Title" controls:FlowerySizeManager.ResponsiveFont="Header" /&gt;
        /// </code>
        /// </example>
        public static readonly AttachedProperty<ResponsiveFontTier> ResponsiveFontProperty =
            AvaloniaProperty.RegisterAttached<Control, Control, ResponsiveFontTier>(
                "ResponsiveFont", ResponsiveFontTier.None);

        private static readonly HashSet<Control> _responsiveControls = new();

        static FlowerySizeManager()
        {
            ResponsiveFontProperty.Changed.AddClassHandler<Control>(OnResponsiveFontChanged);
        }

        private static void OnResponsiveFontChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            var newTier = (ResponsiveFontTier)(e.NewValue ?? ResponsiveFontTier.None);
            var oldTier = (ResponsiveFontTier)(e.OldValue ?? ResponsiveFontTier.None);

            if (newTier != ResponsiveFontTier.None && oldTier == ResponsiveFontTier.None)
            {
                // Subscribe to size changes
                _responsiveControls.Add(control);
                control.Unloaded += OnResponsiveControlUnloaded;

                // Apply current size immediately
                ApplyFontSizeToControl(control, newTier, _currentSize);
            }
            else if (newTier == ResponsiveFontTier.None && oldTier != ResponsiveFontTier.None)
            {
                // Unsubscribe
                _responsiveControls.Remove(control);
                control.Unloaded -= OnResponsiveControlUnloaded;
            }
            else if (newTier != ResponsiveFontTier.None)
            {
                // Tier changed, update font size
                ApplyFontSizeToControl(control, newTier, _currentSize);
            }
        }

        private static void OnResponsiveControlUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is Control control)
            {
                _responsiveControls.Remove(control);
                control.Unloaded -= OnResponsiveControlUnloaded;
            }
        }

        private static void ApplyFontSizeToControl(Control control, ResponsiveFontTier tier, DaisySize size)
        {
            if (control is not TextBlock textBlock) return;

            var fontSize = GetFontSizeForTier(tier, size);
            textBlock.FontSize = fontSize;
        }

        private static double GetFontSizeForTier(ResponsiveFontTier tier, DaisySize size)
        {
            return tier switch
            {
                ResponsiveFontTier.Primary => size switch
                {
                    DaisySize.ExtraSmall => 10,
                    DaisySize.Small => 12,
                    DaisySize.Medium => 14,
                    DaisySize.Large => 18,
                    DaisySize.ExtraLarge => 20,
                    _ => 12
                },
                ResponsiveFontTier.Secondary => size switch
                {
                    DaisySize.ExtraSmall => 9,
                    DaisySize.Small => 10,
                    DaisySize.Medium => 12,
                    DaisySize.Large => 14,
                    DaisySize.ExtraLarge => 16,
                    _ => 10
                },
                ResponsiveFontTier.Tertiary => size switch
                {
                    DaisySize.ExtraSmall => 8,
                    DaisySize.Small => 9,
                    DaisySize.Medium => 11,
                    DaisySize.Large => 12,
                    DaisySize.ExtraLarge => 14,
                    _ => 9
                },
                ResponsiveFontTier.Header => size switch
                {
                    DaisySize.ExtraSmall => 14,
                    DaisySize.Small => 16,
                    DaisySize.Medium => 20,
                    DaisySize.Large => 24,
                    DaisySize.ExtraLarge => 28,
                    _ => 16
                },
                _ => 12
            };
        }

        /// <summary>
        /// Gets the responsive font tier for a control.
        /// </summary>
        public static ResponsiveFontTier GetResponsiveFont(Control control) =>
            control.GetValue(ResponsiveFontProperty);

        /// <summary>
        /// Sets the responsive font tier for a control.
        /// </summary>
        public static void SetResponsiveFont(Control control, ResponsiveFontTier value) =>
            control.SetValue(ResponsiveFontProperty, value);

        /// <summary>
        /// Event raised when the global size changes.
        /// Controls can subscribe to this event to automatically update their Size property.
        /// </summary>
        public static event EventHandler<DaisySize>? SizeChanged;

        /// <summary>
        /// Gets the currently active global size.
        /// </summary>
        public static DaisySize CurrentSize => _currentSize;

        /// <summary>
        /// Gets or sets whether controls should use the global size by default.
        /// When true, new controls will automatically use <see cref="CurrentSize"/>.
        /// Default is false (opt-in behavior).
        /// </summary>
        public static bool UseGlobalSizeByDefault { get; set; }

        /// <summary>
        /// Apply a global size to all subscribing controls.
        /// </summary>
        /// <param name="size">The DaisySize to apply globally.</param>
        public static void ApplySize(DaisySize size)
        {
            if (_currentSize == size)
                return;

            _currentSize = size;
            UpdateResponsiveControls(size);
            SizeChanged?.Invoke(null, size);
        }

        /// <summary>
        /// Updates all controls with ResponsiveFont attached property.
        /// </summary>
        private static void UpdateResponsiveControls(DaisySize size)
        {
            foreach (var control in _responsiveControls)
            {
                var tier = GetResponsiveFont(control);
                if (tier != ResponsiveFontTier.None)
                {
                    ApplyFontSizeToControl(control, tier, size);
                }
            }
        }

        /// <summary>
        /// Apply a global size by name.
        /// </summary>
        /// <param name="sizeName">The size name (e.g., "Small", "Medium", "Large").</param>
        /// <returns>True if the size was applied successfully.</returns>
        public static bool ApplySize(string sizeName)
        {
            if (string.IsNullOrEmpty(sizeName))
                return false;

            if (Enum.TryParse<DaisySize>(sizeName, ignoreCase: true, out var size))
            {
                ApplySize(size);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Resets the global size to Small (the default).
        /// </summary>
        public static void Reset()
        {
            ApplySize(DaisySize.Small);
        }
    }
}
