using System;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Flowery.Enums;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyButtonGroupShape
    {
        Default,
        Square,
        Rounded,
        Pill
    }

    public enum DaisyButtonGroupSelectionMode
    {
        Single,
        Multiple
    }

    public class ButtonGroupItemSelectedEventArgs : RoutedEventArgs
    {
        public Control Item { get; }

        public ButtonGroupItemSelectedEventArgs(RoutedEvent routedEvent, Control item)
            : base(routedEvent)
        {
            Item = item;
        }
    }

    /// <summary>
    /// A segmented button container styled after the "Button Group" pattern.
    /// Supports optional auto-selection and consistent styling for mixed segments
    /// (e.g., buttons with non-clickable text/count parts).
    /// </summary>
    public class DaisyButtonGroup : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyButtonGroup);

        private const double BaseTextFontSize = 14.0;
        private readonly DaisyControlLifecycle _lifecycle;
        private readonly ObservableCollection<int> _selectedIndices = new();
        private INotifyCollectionChanged? _itemsObservable;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyButtonVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyButtonGroup, DaisyButtonVariant>(nameof(Variant), DaisyButtonVariant.Default);

        /// <summary>
        /// Gets or sets the visual variant applied to all segments (Default, Primary, Secondary, etc.).
        /// </summary>
        public DaisyButtonVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyButtonGroup, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size applied to all segments.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ButtonStyle"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyButtonStyle> ButtonStyleProperty =
            AvaloniaProperty.Register<DaisyButtonGroup, DaisyButtonStyle>(nameof(ButtonStyle), DaisyButtonStyle.Default);

        /// <summary>
        /// Gets or sets the segment style (Default, Outline, Dash, Soft).
        /// </summary>
        public DaisyButtonStyle ButtonStyle
        {
            get => GetValue(ButtonStyleProperty);
            set => SetValue(ButtonStyleProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Shape"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyButtonGroupShape> ShapeProperty =
            AvaloniaProperty.Register<DaisyButtonGroup, DaisyButtonGroupShape>(nameof(Shape), DaisyButtonGroupShape.Default);

        /// <summary>
        /// Gets or sets the overall group shape. This influences corner radii for the container
        /// and the first/last segments.
        /// </summary>
        public DaisyButtonGroupShape Shape
        {
            get => GetValue(ShapeProperty);
            set => SetValue(ShapeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Orientation"/> property.
        /// </summary>
        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyButtonGroup, Orientation>(nameof(Orientation), Orientation.Horizontal);

        /// <summary>
        /// Gets or sets whether segments are laid out horizontally or vertically.
        /// </summary>
        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="AutoSelect"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> AutoSelectProperty =
            AvaloniaProperty.Register<DaisyButtonGroup, bool>(nameof(AutoSelect), false);

        /// <summary>
        /// Gets or sets a value indicating whether clicking a segment automatically applies the
        /// 'button-group-active' class and removes it from other button segments.
        /// </summary>
        public bool AutoSelect
        {
            get => GetValue(AutoSelectProperty);
            set => SetValue(AutoSelectProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowShadow"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowShadowProperty =
            AvaloniaProperty.Register<DaisyButtonGroup, bool>(nameof(ShowShadow), false);

        /// <summary>
        /// Gets or sets whether a subtle shadow is rendered around the group container.
        /// </summary>
        public bool ShowShadow
        {
            get => GetValue(ShowShadowProperty);
            set => SetValue(ShowShadowProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="SelectedIndex"/> property.
        /// </summary>
        public static readonly StyledProperty<int> SelectedIndexProperty =
            AvaloniaProperty.Register<DaisyButtonGroup, int>(nameof(SelectedIndex), -1);

        /// <summary>
        /// Gets or sets the selected index when SelectionMode is Single.
        /// </summary>
        public int SelectedIndex
        {
            get => GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="SelectionMode"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyButtonGroupSelectionMode> SelectionModeProperty =
            AvaloniaProperty.Register<DaisyButtonGroup, DaisyButtonGroupSelectionMode>(
                nameof(SelectionMode),
                DaisyButtonGroupSelectionMode.Single);

        /// <summary>
        /// Gets or sets the selection mode (Single or Multiple).
        /// </summary>
        public DaisyButtonGroupSelectionMode SelectionMode
        {
            get => GetValue(SelectionModeProperty);
            set => SetValue(SelectionModeProperty, value);
        }

        /// <summary>
        /// Gets the collection of selected indices when SelectionMode is Multiple.
        /// Modifying this collection updates the visual selection state.
        /// </summary>
        public ObservableCollection<int> SelectedIndices => _selectedIndices;

        /// <summary>
        /// Raised when the selected indices collection changes (Multiple mode).
        /// </summary>
        public event EventHandler<NotifyCollectionChangedEventArgs>? SelectedIndicesChanged;

        public static readonly RoutedEvent<ButtonGroupItemSelectedEventArgs> ItemSelectedEvent =
            RoutedEvent.Register<DaisyButtonGroup, ButtonGroupItemSelectedEventArgs>(
                nameof(ItemSelected), RoutingStrategies.Bubble);

        /// <summary>
        /// Raised when a button segment is clicked.
        /// </summary>
        public event EventHandler<ButtonGroupItemSelectedEventArgs> ItemSelected
        {
            add => AddHandler(ItemSelectedEvent, value);
            remove => RemoveHandler(ItemSelectedEvent, value);
        }

        public DaisyButtonGroup()
        {
            _lifecycle = new DaisyControlLifecycle(
                this,
                ApplyAll,
                () => Size,
                s => Size = s,
                handleLifecycleEvents: false);

            AddHandler(Button.ClickEvent, OnButtonClick);
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;

            _selectedIndices.CollectionChanged += OnSelectedIndicesChanged;
            SubscribeItemsCollection(Items);
        }

        private void OnButtonClick(object? sender, RoutedEventArgs e)
        {
            var button = e.Source as Button ?? (e.Source as Control)?.FindAncestorOfType<Button>();
            if (button != null && this.IsLogicalAncestorOf(button))
            {
                var index = GetItemIndexForButton(button);

                if (AutoSelect && index >= 0)
                {
                    if (SelectionMode == DaisyButtonGroupSelectionMode.Multiple)
                    {
                        if (_selectedIndices.Contains(index))
                            _selectedIndices.Remove(index);
                        else
                            _selectedIndices.Add(index);
                    }
                    else
                    {
                        SelectedIndex = index;
                    }
                }

                RaiseEvent(new ButtonGroupItemSelectedEventArgs(ItemSelectedEvent, button));
            }
        }

        private void SubscribeItemsCollection(object? items)
        {
            if (_itemsObservable != null)
            {
                _itemsObservable.CollectionChanged -= OnItemsCollectionChanged;
                _itemsObservable = null;
            }

            if (items is INotifyCollectionChanged notify)
            {
                _itemsObservable = notify;
                _itemsObservable.CollectionChanged += OnItemsCollectionChanged;
            }
        }

        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            ApplyAll();
        }

        private void OnSelectedIndicesChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (SelectionMode == DaisyButtonGroupSelectionMode.Multiple)
            {
                ApplySelectionClasses();
                SelectedIndicesChanged?.Invoke(this, e);
            }
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            SubscribeItemsCollection(Items);
            _lifecycle.HandleLoaded();
            ApplyAll();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (_itemsObservable != null)
            {
                _itemsObservable.CollectionChanged -= OnItemsCollectionChanged;
                _itemsObservable = null;
            }
            _lifecycle.HandleUnloaded();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedIndexProperty ||
                change.Property == SelectionModeProperty ||
                change.Property == AutoSelectProperty ||
                change.Property == ItemsSourceProperty ||
                change.Property == VariantProperty ||
                change.Property == SizeProperty ||
                change.Property == ButtonStyleProperty ||
                change.Property == ShapeProperty ||
                change.Property == OrientationProperty)
            {
                if (change.Property == ItemsSourceProperty)
                {
                    SubscribeItemsCollection(change.NewValue);
                }

                if (change.Property == SelectionModeProperty &&
                    change.NewValue is DaisyButtonGroupSelectionMode mode &&
                    mode == DaisyButtonGroupSelectionMode.Single)
                {
                    _selectedIndices.Clear();
                    if (SelectedIndex >= ItemCount)
                    {
                        SelectedIndex = -1;
                    }
                }

                ApplyAll();
            }
        }

        private void ApplyAll()
        {
            ApplySelectionClasses();
        }

        private int GetItemIndexForButton(Button button)
        {
            // First try ItemContainerGenerator for bound items
            Control? container = button;
            while (container != null)
            {
                var index = IndexFromContainer(container);
                if (index >= 0)
                    return index;

                container = container.GetVisualParent() as Control;
            }

            // Fallback: find index among direct logical children (for XAML-placed buttons)
            var buttons = this.GetLogicalChildren().OfType<Button>().ToList();
            return buttons.IndexOf(button);
        }

        private void ApplySelectionClasses()
        {
            // Try generated containers first
            var itemsCount = ItemCount;
            var foundAny = false;

            for (var i = 0; i < itemsCount; i++)
            {
                var container = ContainerFromIndex(i);
                var button = container as Button ?? container?.GetVisualDescendants().FirstOrDefault(ctrl => ctrl is Button) as Button;

                if (button == null)
                    continue;

                foundAny = true;
                ApplyActiveClass(button, i);
            }

            // Fallback: direct logical children (for XAML-placed buttons)
            if (!foundAny)
            {
                var buttons = this.GetLogicalChildren().OfType<Button>().ToList();
                for (var i = 0; i < buttons.Count; i++)
                {
                    ApplyActiveClass(buttons[i], i);
                }
            }
        }

        private void ApplyActiveClass(Button button, int index)
        {
            var isActive = false;
            if (AutoSelect)
            {
                if (SelectionMode == DaisyButtonGroupSelectionMode.Multiple)
                {
                    isActive = _selectedIndices.Contains(index);
                }
                else
                {
                    isActive = SelectedIndex == index;
                }
            }

            button.Classes.Set("button-group-active", isActive);
        }
    }
}
