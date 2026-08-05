using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Flowery.Controls
{
    /// <summary>
    /// A lightweight popover control that displays content in a Popup anchored to a trigger.
    /// </summary>
    public class DaisyPopover : TemplatedControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyPopover);

        private Control? _target;
        private Button? _triggerButton;

        /// <summary>
        /// Defines the <see cref="TriggerContent"/> property.
        /// </summary>
        public static readonly StyledProperty<object?> TriggerContentProperty =
            AvaloniaProperty.Register<DaisyPopover, object?>(nameof(TriggerContent));

        /// <summary>
        /// Gets or sets the trigger content.
        /// </summary>
        public object? TriggerContent
        {
            get => GetValue(TriggerContentProperty);
            set => SetValue(TriggerContentProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="PopoverContent"/> property.
        /// </summary>
        public static readonly StyledProperty<object?> PopoverContentProperty =
            AvaloniaProperty.Register<DaisyPopover, object?>(nameof(PopoverContent));

        /// <summary>
        /// Gets or sets the popover content.
        /// </summary>
        public object? PopoverContent
        {
            get => GetValue(PopoverContentProperty);
            set => SetValue(PopoverContentProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IsOpen"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsOpenProperty =
            AvaloniaProperty.Register<DaisyPopover, bool>(nameof(IsOpen));

        /// <summary>
        /// Gets or sets whether the popover is open.
        /// </summary>
        public bool IsOpen
        {
            get => GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="PlacementMode"/> property.
        /// </summary>
        public static readonly StyledProperty<PlacementMode> PlacementModeProperty =
            AvaloniaProperty.Register<DaisyPopover, PlacementMode>(nameof(PlacementMode), PlacementMode.Bottom);

        /// <summary>
        /// Gets or sets the placement mode used by the popup.
        /// </summary>
        public PlacementMode PlacementMode
        {
            get => GetValue(PlacementModeProperty);
            set => SetValue(PlacementModeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="HorizontalOffset"/> property.
        /// </summary>
        public static readonly StyledProperty<double> HorizontalOffsetProperty =
            AvaloniaProperty.Register<DaisyPopover, double>(nameof(HorizontalOffset), 0.0);

        /// <summary>
        /// Gets or sets the horizontal offset of the popup.
        /// </summary>
        public double HorizontalOffset
        {
            get => GetValue(HorizontalOffsetProperty);
            set => SetValue(HorizontalOffsetProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="VerticalOffset"/> property.
        /// </summary>
        public static readonly StyledProperty<double> VerticalOffsetProperty =
            AvaloniaProperty.Register<DaisyPopover, double>(nameof(VerticalOffset), 8.0);

        /// <summary>
        /// Gets or sets the vertical offset of the popup.
        /// </summary>
        public double VerticalOffset
        {
            get => GetValue(VerticalOffsetProperty);
            set => SetValue(VerticalOffsetProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IsLightDismissEnabled"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsLightDismissEnabledProperty =
            AvaloniaProperty.Register<DaisyPopover, bool>(nameof(IsLightDismissEnabled), true);

        /// <summary>
        /// Gets or sets whether the popover closes when clicking outside.
        /// </summary>
        public bool IsLightDismissEnabled
        {
            get => GetValue(IsLightDismissEnabledProperty);
            set => SetValue(IsLightDismissEnabledProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="MatchTargetWidth"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> MatchTargetWidthProperty =
            AvaloniaProperty.Register<DaisyPopover, bool>(nameof(MatchTargetWidth), false);

        /// <summary>
        /// Gets or sets whether the popup minimum width matches the trigger width.
        /// </summary>
        public bool MatchTargetWidth
        {
            get => GetValue(MatchTargetWidthProperty);
            set => SetValue(MatchTargetWidthProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ToggleOnClick"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ToggleOnClickProperty =
            AvaloniaProperty.Register<DaisyPopover, bool>(nameof(ToggleOnClick), true);

        /// <summary>
        /// Gets or sets whether clicking the trigger toggles the popover.
        /// </summary>
        public bool ToggleOnClick
        {
            get => GetValue(ToggleOnClickProperty);
            set => SetValue(ToggleOnClickProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            DetachTargetHandlers();
            _target = e.NameScope.Find<Control>("PART_Target");
            AttachTargetHandlers();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TriggerContentProperty && _target is not null)
            {
                DetachTargetHandlers();
                AttachTargetHandlers();
            }
        }

        private void AttachTargetHandlers()
        {
            if (_target is null) return;

            if (TriggerContent is Button button)
            {
                _triggerButton = button;
                _triggerButton.Click += OnTriggerButtonClick;
                return;
            }

            _target.AddHandler(InputElement.PointerPressedEvent, OnTargetPointerPressed, RoutingStrategies.Bubble,
                handledEventsToo: true);
        }

        private void DetachTargetHandlers()
        {
            _target?.RemoveHandler(InputElement.PointerPressedEvent, OnTargetPointerPressed);

            if (_triggerButton is not null)
            {
                _triggerButton.Click -= OnTriggerButtonClick;
                _triggerButton = null;
            }
        }

        private void OnTriggerButtonClick(object? sender, RoutedEventArgs e)
        {
            if (ToggleOnClick)
            {
                IsOpen = !IsOpen;
            }
        }

        private void OnTargetPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!ToggleOnClick) return;

            var point = e.GetCurrentPoint(this);
            if (point.Properties.IsLeftButtonPressed)
            {
                IsOpen = !IsOpen;
                e.Handled = true;
            }
        }
    }
}
