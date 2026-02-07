using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Controls.Primitives;
using Avalonia.Styling;
using Flowery.Enums;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A join container control styled after DaisyUI's Join component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyJoin : StackPanel, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyJoin);

        private const double BaseTextFontSize = 14.0;
        private readonly DaisyControlLifecycle _lifecycle;

        /// <summary>
        /// Gets or sets the index of the active/selected item (0-based). Set to -1 for no selection.
        /// </summary>
        public static readonly StyledProperty<int> ActiveIndexProperty =
            AvaloniaProperty.Register<DaisyJoin, int>(nameof(ActiveIndex), -1);

        /// <summary>
        /// Gets or sets the background brush for the active item.
        /// </summary>
        public static readonly StyledProperty<IBrush?> ActiveBackgroundProperty =
            AvaloniaProperty.Register<DaisyJoin, IBrush?>(nameof(ActiveBackground), defaultValue: null);

        /// <summary>
        /// Gets or sets the foreground brush for the active item.
        /// </summary>
        public static readonly StyledProperty<IBrush?> ActiveForegroundProperty =
            AvaloniaProperty.Register<DaisyJoin, IBrush?>(nameof(ActiveForeground), defaultValue: null);

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            SetValue(TextBlock.FontSizeProperty, FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor));
        }

        /// <summary>
        /// Gets or sets the active/selected item index (0-based). Set to -1 for no selection.
        /// </summary>
        public int ActiveIndex
        {
            get => GetValue(ActiveIndexProperty);
            set => SetValue(ActiveIndexProperty, value);
        }

        /// <summary>
        /// Gets or sets the background brush for the active item.
        /// </summary>
        public IBrush? ActiveBackground
        {
            get => GetValue(ActiveBackgroundProperty);
            set => SetValue(ActiveBackgroundProperty, value);
        }

        /// <summary>
        /// Gets or sets the foreground brush for the active item.
        /// </summary>
        public IBrush? ActiveForeground
        {
            get => GetValue(ActiveForegroundProperty);
            set => SetValue(ActiveForegroundProperty, value);
        }

        public DaisyJoin()
        {
            Orientation = Orientation.Horizontal;
            _lifecycle = new DaisyControlLifecycle(
                this,
                ApplyAll,
                () => DaisySize.Medium,
                _ => { },
                handleLifecycleEvents: false,
                subscribeSizeChanges: false);

            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
            Children.CollectionChanged += OnChildrenChanged;

        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _lifecycle.HandleLoaded();
            ApplyAll();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _lifecycle.HandleUnloaded();
        }

        private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ApplyAll();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ActiveIndexProperty ||
                change.Property == ActiveBackgroundProperty ||
                change.Property == ActiveForegroundProperty)
            {
                ApplyActiveHighlight();
            }
        }

        private void ApplyAll()
        {
            ApplyActiveHighlight();
        }

        private void ApplyActiveHighlight()
        {
            if (ActiveIndex < 0 || Children.Count == 0)
            {
                return;
            }

            var baseBackground = GetBrushOrFallback("DaisyBase200Brush", new SolidColorBrush(Colors.Transparent));
            var baseForeground = GetBrushOrFallback("DaisyBaseContentBrush", new SolidColorBrush(Colors.Transparent));
            var activeBackground = ActiveBackground ?? GetBrushOrFallback("DaisyPrimaryBrush", baseBackground);
            var activeForeground = ActiveForeground ?? GetBrushOrFallback("DaisyPrimaryContentBrush", baseForeground);

            for (var i = 0; i < Children.Count; i++)
            {
                if (Children[i] is Control control)
                {
                    var isActive = i == ActiveIndex;
                    if (control is TemplatedControl templated)
                    {
                        templated.Background = isActive ? activeBackground : baseBackground;
                        templated.Foreground = isActive ? activeForeground : baseForeground;
                    }
                    else
                    {
                        control.SetValue(TemplatedControl.BackgroundProperty, isActive ? activeBackground : baseBackground);
                        control.SetValue(TemplatedControl.ForegroundProperty, isActive ? activeForeground : baseForeground);
                    }
                }
            }
        }

        private IBrush GetBrushOrFallback(string key, IBrush fallback)
        {
            if (TryGetResource(key, ThemeVariant.Default, out var value) && value is IBrush brush)
            {
                return brush;
            }

            return fallback;
        }
    }
}
