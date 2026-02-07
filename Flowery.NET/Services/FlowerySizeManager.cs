using System;
using System.Collections.Generic;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

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
        /// <summary>
        /// Section headers - smaller than page headers, larger than body text.
        /// Use for control group titles, example section headers, etc.
        /// </summary>
        SectionHeader,
        /// <summary>
        /// Page/screen headers - the largest tier for main titles.
        /// </summary>
        Header
    }

    /// <summary>
    /// Centralized size manager for DaisyUI controls (Avalonia).
    /// </summary>
    /// <remarks>
    /// This service enables a "global size override" feature for applications that want
    /// consistent sizing across all DaisyUI controls. When <see cref="EnableGlobalAutoSize"/> is true,
    /// size changes propagate to all controls with a Size property in the visual tree.
    /// Controls with explicitly-set Size values (in XAML or code) are respected and not overwritten.
    /// Use <see cref="IgnoreGlobalSizeProperty"/> to opt-out entire branches of the visual tree.
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

        // Cache for SizeProperty StyledProperty by type
        private static readonly Dictionary<Type, AvaloniaProperty?> _sizeDPCache = [];

        // Cache for Size property reflection lookup
        private static readonly Dictionary<Type, PropertyInfo?> _sizePropertyCache = [];

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
        /// Gets whether the control ignores global size changes (direct property access).
        /// </summary>
        public static bool GetIgnoreGlobalSize(Control control) =>
            control.GetValue(IgnoreGlobalSizeProperty);

        /// <summary>
        /// Sets whether the control ignores global size changes.
        /// </summary>
        public static void SetIgnoreGlobalSize(Control control, bool value) =>
            control.SetValue(IgnoreGlobalSizeProperty, value);

        /// <summary>
        /// Checks if this control or any of its ancestors has IgnoreGlobalSize explicitly set.
        /// Stops at the first explicit setting and returns that value.
        /// This allows parent containers to opt-out and child controls to opt back in.
        /// </summary>
        public static bool ShouldIgnoreGlobalSize(Control control)
        {
            Visual? current = control;
            while (current != null)
            {
                if (current is Control c)
                {
                    // Check if property is explicitly set (not default)
                    if (c.IsSet(IgnoreGlobalSizeProperty))
                    {
                        // Property is explicitly set - return its value and stop walking
                        return c.GetValue(IgnoreGlobalSizeProperty);
                    }
                }
                current = current.GetVisualParent();
            }
            return false; // No explicit setting found, use global size
        }

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

        private static readonly HashSet<TextBlock> _responsiveTextBlocks = [];

        static FlowerySizeManager()
        {
            ResponsiveFontProperty.Changed.AddClassHandler<Control>(OnResponsiveFontChanged);
        }

        private static void OnResponsiveFontChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            if (control is not TextBlock textBlock)
                return;

            var newTier = (ResponsiveFontTier)(e.NewValue ?? ResponsiveFontTier.None);
            var oldTier = (ResponsiveFontTier)(e.OldValue ?? ResponsiveFontTier.None);

            if (newTier != ResponsiveFontTier.None && oldTier == ResponsiveFontTier.None)
            {
                _responsiveTextBlocks.Add(textBlock);
                textBlock.Unloaded += OnResponsiveTextBlockUnloaded;
                ApplyFontSizeToControl(textBlock, newTier, _currentSize);
            }
            else if (newTier == ResponsiveFontTier.None && oldTier != ResponsiveFontTier.None)
            {
                _responsiveTextBlocks.Remove(textBlock);
                textBlock.Unloaded -= OnResponsiveTextBlockUnloaded;
            }
            else if (newTier != ResponsiveFontTier.None)
            {
                ApplyFontSizeToControl(textBlock, newTier, _currentSize);
            }
        }

        private static void OnResponsiveTextBlockUnloaded(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (sender is TextBlock tb)
            {
                _responsiveTextBlocks.Remove(tb);
                tb.Unloaded -= OnResponsiveTextBlockUnloaded;
            }
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
        /// </summary>
        public static event EventHandler<DaisySize>? SizeChanged;

        /// <summary>
        /// Gets the currently active global size.
        /// </summary>
        public static DaisySize CurrentSize => _currentSize;

        /// <summary>
        /// Gets or sets whether controls should use the global size by default.
        /// Default is true (opt-out behavior).
        /// </summary>
        public static bool UseGlobalSizeByDefault { get; set; } = true;

        /// <summary>
        /// When true (default), ApplySize() automatically propagates to all controls
        /// with a Size property of type DaisySize in the visual tree.
        /// Use IgnoreGlobalSize="True" on individual controls to opt-out.
        /// </summary>
        public static bool EnableGlobalAutoSize { get; set; } = true;

        /// <summary>
        /// The main window reference for visual tree propagation.
        /// Set this in your App.axaml.cs after creating the window.
        /// </summary>
        public static Window? MainWindow { get; set; }

        /// <summary>
        /// Apply a global size to all subscribing controls.
        /// When EnableGlobalAutoSize is true, also propagates to all controls
        /// with a Size property in the visual tree.
        /// </summary>
        /// <param name="size">The DaisySize to apply globally.</param>
        public static void ApplySize(DaisySize size)
        {
            if (_currentSize == size)
                return;

            _currentSize = size;
            UpdateResponsiveTextBlocks(size);
            SizeChanged?.Invoke(null, size);

            if (EnableGlobalAutoSize)
            {
                PropagateToVisualTree();
            }
        }

        /// <summary>
        /// Forces propagation of the current size to all controls in the visual tree.
        /// Call this after the visual tree is fully loaded (e.g., in Window.Opened).
        /// </summary>
        public static void RefreshAllSizes()
        {
            if (EnableGlobalAutoSize)
            {
                PropagateToVisualTree();
            }
        }

        /// <summary>
        /// Propagates the current size to all controls with a Size property
        /// in the visual tree.
        /// </summary>
        private static void PropagateToVisualTree()
        {
            try
            {
                if (MainWindow?.Content is Control root)
                {
                    PropagateSize(root, _currentSize);
                }
            }
            catch
            {
                // Silently ignore errors during propagation
            }
        }

        /// <summary>
        /// Iteratively propagates size to all controls in the visual tree to avoid StackOverflow.
        /// </summary>
        private static void PropagateSize(Control root, DaisySize size)
        {
            var queue = new Queue<Control>();
            queue.Enqueue(root);

            while (queue.Count > 0)
            {
                var element = queue.Dequeue();

                if (ShouldIgnoreGlobalSize(element))
                {
                    // This branch has opted out - stop propagation for this branch
                    continue;
                }

                TrySetSizeProperty(element, size);

                // Add children to queue
                foreach (var child in element.GetVisualChildren())
                {
                    if (child is Control childControl)
                    {
                        queue.Enqueue(childControl);
                    }
                }
            }
        }

        /// <summary>
        /// Attempts to set the Size property on a control if it exists and is DaisySize.
        /// Respects explicitly-set local values (from XAML or code) by not overwriting them.
        /// Uses reflection with caching for performance.
        /// </summary>
        private static void TrySetSizeProperty(Control element, DaisySize size)
        {
            var type = element.GetType();

            // First try to find the SizeProperty StyledProperty (preferred for Avalonia)
            if (!_sizeDPCache.TryGetValue(type, out var dp))
            {
                var sizeField = type.GetField("SizeProperty", BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy);
                dp = sizeField?.GetValue(null) as AvaloniaProperty;
                _sizeDPCache[type] = dp;
            }

            if (dp != null)
            {
                // Check if a local value was set (in XAML or code) and respect it
                if (element.IsSet(dp))
                {
                    // Control has an explicit Size set - don't override
                    return;
                }

                try
                {
                    element.SetValue(dp, size);
                }
                catch
                {
                    // Silently ignore errors setting property
                }
                return;
            }

            // Fallback to reflection for non-AvaloniaProperty Size properties (rare)
            if (!_sizePropertyCache.TryGetValue(type, out var sizeProp))
            {
                sizeProp = type.GetProperty("Size", BindingFlags.Public | BindingFlags.Instance);
                if (sizeProp?.PropertyType != typeof(DaisySize) || !sizeProp.CanWrite)
                {
                    sizeProp = null;
                }
                _sizePropertyCache[type] = sizeProp;
            }

            if (sizeProp != null)
            {
                try
                {
                    sizeProp.SetValue(element, size);
                }
                catch
                {
                    // Silently ignore errors setting property
                }
            }
        }

        /// <summary>
        /// Updates all TextBlocks with ResponsiveFont attached property.
        /// </summary>
        private static void UpdateResponsiveTextBlocks(DaisySize size)
        {
            foreach (var tb in _responsiveTextBlocks)
            {
                // Respect IgnoreGlobalSize on the TextBlock or its ancestors
                if (ShouldIgnoreGlobalSize(tb))
                    continue;

                var tier = GetResponsiveFont(tb);
                if (tier != ResponsiveFontTier.None)
                {
                    ApplyFontSizeToControl(tb, tier, size);
                }
            }
        }

        private static void ApplyFontSizeToControl(TextBlock textBlock, ResponsiveFontTier tier, DaisySize size)
        {
            textBlock.FontSize = GetFontSizeForTier(tier, size);
        }

        /// <summary>
        /// Gets the font size for a given tier and size.
        /// </summary>
        public static double GetFontSizeForTier(ResponsiveFontTier tier, DaisySize size)
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
                ResponsiveFontTier.SectionHeader => size switch
                {
                    DaisySize.ExtraSmall => 12,
                    DaisySize.Small => 14,
                    DaisySize.Medium => 16,
                    DaisySize.Large => 18,
                    DaisySize.ExtraLarge => 20,
                    _ => 14
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
        /// Gets the default sidebar width for the given size.
        /// </summary>
        public static double GetSidebarWidth(DaisySize size)
        {
            return size switch
            {
                DaisySize.ExtraSmall => 190d,
                DaisySize.Small => 205d,
                DaisySize.Medium => 220d,
                DaisySize.Large => 235d,
                DaisySize.ExtraLarge => 250d,
                _ => 220d
            };
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
