using System;
using System.Reflection;
using Avalonia.Controls;
using Avalonia;

namespace Flowery.Controls
{
    /// <summary>
    /// Manages theme and size subscriptions for Daisy controls.
    /// Use via composition, not inheritance.
    /// </summary>
    public sealed class DaisyControlLifecycle
    {
        private readonly Control _owner;
        private readonly Action _applyAll;
        private readonly Func<DaisySize> _getSize;
        private readonly Action<DaisySize> _setSize;
        private readonly bool _subscribeSizeChanges;

        public DaisyControlLifecycle(
            Control owner,
            Action applyAll,
            Func<DaisySize> getSize,
            Action<DaisySize> setSize,
            bool handleLifecycleEvents = true,
            bool subscribeSizeChanges = true)
        {
            _owner = owner ?? throw new ArgumentNullException(nameof(owner));
            _applyAll = applyAll ?? throw new ArgumentNullException(nameof(applyAll));
            _getSize = getSize ?? throw new ArgumentNullException(nameof(getSize));
            _setSize = setSize ?? throw new ArgumentNullException(nameof(setSize));
            _subscribeSizeChanges = subscribeSizeChanges;

            // Do not apply global size here; wait until load so XAML-set values are available.
            if (handleLifecycleEvents)
            {
                _owner.AttachedToVisualTree += OnAttachedToVisualTree;
                _owner.DetachedFromVisualTree += OnDetachedFromVisualTree;
            }
        }

        /// <summary>
        /// Call when auto-handling lifecycle events. Subscribes to theme/size changes.
        /// </summary>
        public void HandleLoaded()
        {
            DaisyThemeManager.ThemeChanged += OnThemeChanged;

            if (_subscribeSizeChanges)
            {
                FlowerySizeManager.SizeChanged += OnGlobalSizeChanged;

                if (FlowerySizeManager.UseGlobalSizeByDefault && !FlowerySizeManager.ShouldIgnoreGlobalSize(_owner))
                {
                    var sizeProperty = TryGetSizeProperty(_owner);
                    if (sizeProperty == null || !_owner.IsSet(sizeProperty))
                    {
                        _setSize(FlowerySizeManager.CurrentSize);
                    }
                }
            }

            _applyAll();
        }

        /// <summary>
        /// Call when auto-handling lifecycle events. Unsubscribes from theme/size changes.
        /// </summary>
        public void HandleUnloaded()
        {
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;

            if (_subscribeSizeChanges)
            {
                FlowerySizeManager.SizeChanged -= OnGlobalSizeChanged;
            }
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => HandleLoaded();

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e) => HandleUnloaded();

        private void OnThemeChanged(object? sender, string themeName) => _applyAll();

        private void OnGlobalSizeChanged(object? sender, DaisySize size)
        {
            if (FlowerySizeManager.ShouldIgnoreGlobalSize(_owner))
            {
                return;
            }

            if (FlowerySizeManager.UseGlobalSizeByDefault)
            {
                _setSize(size);
            }
        }

        private static AvaloniaProperty? TryGetSizeProperty(Control owner)
        {
            return owner.GetType().GetField(
                    "SizeProperty",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                ?.GetValue(null) as AvaloniaProperty;
        }
    }
}
