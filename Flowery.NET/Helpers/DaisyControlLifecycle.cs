using System;
using Avalonia.Controls;
using Avalonia;

namespace Flowery.Controls
{
    /// <summary>
    /// Manages theme subscriptions for Daisy controls.
    /// Use via composition, not inheritance. Global size is supplied by
    /// <see cref="FlowerySizeManager"/> to every control with a Size property.
    /// </summary>
    public sealed class DaisyControlLifecycle
    {
        private readonly Control _owner;
        private readonly Action _applyAll;

        public DaisyControlLifecycle(
            Control owner,
            Action applyAll,
            bool handleLifecycleEvents = true)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _applyAll = applyAll ?? throw new ArgumentNullException(nameof(applyAll));

            if (handleLifecycleEvents)
            {
                _owner.AttachedToVisualTree += OnAttachedToVisualTree;
                _owner.DetachedFromVisualTree += OnDetachedFromVisualTree;
            }
        }

        /// <summary>
        /// Call when auto-handling lifecycle events. Subscribes to theme changes.
        /// </summary>
        public void HandleLoaded()
        {
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            _applyAll();
        }

        /// <summary>
        /// Call when auto-handling lifecycle events. Unsubscribes from theme changes.
        /// </summary>
        public void HandleUnloaded()
        {
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => HandleLoaded();

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => HandleUnloaded();

        private void OnThemeChanged(object? sender, string themeName) => _applyAll();
    }
}
