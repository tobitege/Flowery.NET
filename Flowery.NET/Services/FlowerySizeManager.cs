using System;
using Avalonia;
using Avalonia.Controls;

namespace Flowery.Controls
{
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
            SizeChanged?.Invoke(null, size);
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
