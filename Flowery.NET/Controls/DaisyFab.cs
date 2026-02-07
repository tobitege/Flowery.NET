using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum FabLayout
    {
        Vertical,
        Horizontal,
        Flower
    }

    /// <summary>
    /// A floating action button control styled after material design FAB.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyFab : Grid, IScalableControl
    {
        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            // Note: Grid doesn't have FontSize, but we set it for child inheritance
            SetValue(TextBlock.FontSizeProperty, FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor));
        }
        public static readonly StyledProperty<FabLayout> LayoutProperty =
            AvaloniaProperty.Register<DaisyFab, FabLayout>(nameof(Layout), FabLayout.Vertical);

        public static readonly StyledProperty<bool> IsOpenProperty =
            AvaloniaProperty.Register<DaisyFab, bool>(nameof(IsOpen));

        public static readonly StyledProperty<DaisyButtonVariant> TriggerVariantProperty =
            AvaloniaProperty.Register<DaisyFab, DaisyButtonVariant>(nameof(TriggerVariant), DaisyButtonVariant.Primary);

        public static readonly StyledProperty<object?> TriggerContentProperty =
            AvaloniaProperty.Register<DaisyFab, object?>(nameof(TriggerContent), "+");

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyFab, DaisySize>(nameof(Size), DaisySize.Large);

        public static readonly StyledProperty<bool> AutoCloseProperty =
            AvaloniaProperty.Register<DaisyFab, bool>(nameof(AutoClose), true);

        public static readonly StyledProperty<string?> TriggerIconDataProperty =
            AvaloniaProperty.Register<DaisyFab, string?>(nameof(TriggerIconData));

        public static readonly StyledProperty<double> TriggerIconSizeProperty =
            AvaloniaProperty.Register<DaisyFab, double>(nameof(TriggerIconSize), double.NaN);

        public FabLayout Layout
        {
            get => GetValue(LayoutProperty);
            set => SetValue(LayoutProperty, value);
        }

        public bool IsOpen
        {
            get => GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public DaisyButtonVariant TriggerVariant
        {
            get => GetValue(TriggerVariantProperty);
            set => SetValue(TriggerVariantProperty, value);
        }

        public object? TriggerContent
        {
            get => GetValue(TriggerContentProperty);
            set => SetValue(TriggerContentProperty, value);
        }

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// When true (default), clicking an action item automatically closes the FAB.
        /// </summary>
        public bool AutoClose
        {
            get => GetValue(AutoCloseProperty);
            set => SetValue(AutoCloseProperty, value);
        }

        public string? TriggerIconData
        {
            get => GetValue(TriggerIconDataProperty);
            set => SetValue(TriggerIconDataProperty, value);
        }

        public double TriggerIconSize
        {
            get => GetValue(TriggerIconSizeProperty);
            set => SetValue(TriggerIconSizeProperty, value);
        }

        private DaisyButton? _triggerButton;

        public DaisyFab()
        {
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            // Hook existing action buttons
            foreach (var child in Children)
            {
                if (child is DaisyButton btn && btn != _triggerButton)
                {
                    btn.Click -= OnActionButtonClick;
                    btn.Click += OnActionButtonClick;
                }
            }

            Children.CollectionChanged += OnChildrenChanged;
            EnsureTriggerButton();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            Children.CollectionChanged -= OnChildrenChanged;

            // Unhook all action buttons
            foreach (var child in Children)
            {
                if (child is DaisyButton btn && btn != _triggerButton)
                {
                    btn.Click -= OnActionButtonClick;
                }
            }
        }

        private void OnChildrenChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (var item in e.OldItems)
                {
                    if (item is DaisyButton btn && btn != _triggerButton)
                    {
                        btn.Click -= OnActionButtonClick;
                    }
                }
            }

            if (e.NewItems != null)
            {
                foreach (var item in e.NewItems)
                {
                    if (item is DaisyButton btn && btn != _triggerButton)
                    {
                        btn.Click -= OnActionButtonClick;
                        btn.Click += OnActionButtonClick;
                    }
                }
            }
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TriggerVariantProperty ||
                change.Property == TriggerContentProperty ||
                change.Property == TriggerIconDataProperty ||
                change.Property == TriggerIconSizeProperty ||
                change.Property == SizeProperty)
            {
                UpdateTriggerButton();
            }
        }

        private void EnsureTriggerButton()
        {
            if (_triggerButton != null) return;

            _triggerButton = new DaisyButton
            {
                Shape = DaisyButtonShape.Circle
            };
            _triggerButton.Classes.Add("fab-trigger");
            _triggerButton.Click += OnTriggerClick;

            UpdateTriggerButton();

            Children.Add(_triggerButton);
        }

        private void UpdateTriggerButton()
        {
            if (_triggerButton == null) return;
            _triggerButton.Size = Size;
            _triggerButton.Variant = TriggerVariant;

            if (!string.IsNullOrEmpty(TriggerIconData))
            {
                _triggerButton.Content = new PathIcon
                {
                    Data = StreamGeometry.Parse(TriggerIconData!),
                    Width = double.IsNaN(TriggerIconSize) ? double.NaN : TriggerIconSize,
                    Height = double.IsNaN(TriggerIconSize) ? double.NaN : TriggerIconSize
                };
            }
            else
            {
                _triggerButton.Content = TriggerContent;
            }
        }

        private void OnTriggerClick(object? sender, RoutedEventArgs e)
        {
            IsOpen = !IsOpen;
        }

        private void OnActionButtonClick(object? sender, RoutedEventArgs e)
        {
            if (AutoClose && IsOpen)
            {
                IsOpen = false;
            }
        }
    }
}
