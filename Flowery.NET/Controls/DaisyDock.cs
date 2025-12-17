using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DockSize
    {
        Medium,
        ExtraSmall,
        Small,
        Large,
        ExtraLarge
    }

    public class DockItemSelectedEventArgs : RoutedEventArgs
    {
        public Control Item { get; }

        public DockItemSelectedEventArgs(RoutedEvent routedEvent, Control item)
            : base(routedEvent)
        {
            Item = item;
        }
    }

    /// <summary>
    /// A dock/taskbar control styled after macOS dock.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyDock : ItemsControl, IScalableControl
    {
        public static readonly StyledProperty<DockSize> SizeProperty =
            AvaloniaProperty.Register<DaisyDock, DockSize>(nameof(Size), DockSize.Medium);

        public DockSize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<bool> AutoSelectProperty =
            AvaloniaProperty.Register<DaisyDock, bool>(nameof(AutoSelect), true);

        /// <summary>
        /// Gets or sets a value indicating whether clicking an item automatically applies the 'dock-active' class
        /// and removes it from other items. Defaults to true.
        /// </summary>
        public bool AutoSelect
        {
            get => GetValue(AutoSelectProperty);
            set => SetValue(AutoSelectProperty, value);
        }

        public static readonly RoutedEvent<DockItemSelectedEventArgs> ItemSelectedEvent =
            RoutedEvent.Register<DaisyDock, DockItemSelectedEventArgs>(nameof(ItemSelected), RoutingStrategies.Bubble);

        public event EventHandler<DockItemSelectedEventArgs> ItemSelected
        {
            add => AddHandler(ItemSelectedEvent, value);
            remove => RemoveHandler(ItemSelectedEvent, value);
        }

        protected override Type StyleKeyOverride => typeof(DaisyDock);

        private const double BaseTextFontSize = 12.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public DaisyDock()
        {
            AddHandler(Button.ClickEvent, OnButtonClick);
        }

        private void OnButtonClick(object? sender, RoutedEventArgs e)
        {
            var button = e.Source as Button ?? (e.Source as Control)?.FindAncestorOfType<Button>();
            if (button != null && this.IsLogicalAncestorOf(button))
            {
                if (AutoSelect)
                    UpdateSelection(button);

                RaiseEvent(new DockItemSelectedEventArgs(ItemSelectedEvent, button));
            }
        }

        private void UpdateSelection(Button selectedButton)
        {
            foreach (var child in this.GetLogicalChildren())
            {
                if (child is Button btn)
                {
                    btn.Classes.Set("dock-active", btn == selectedButton);
                }
            }
        }
    }
}
