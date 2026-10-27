using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Transformation;
using Avalonia.Threading;
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
    /// Provides the selection changes of a <see cref="DaisyButtonGroup"/>.
    /// </summary>
    public class ButtonGroupSelectionChangedEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Gets the indices of the segments that were selected.
        /// </summary>
        public IReadOnlyList<int> AddedIndices { get; }

        /// <summary>
        /// Gets the indices of the segments that were deselected.
        /// </summary>
        public IReadOnlyList<int> RemovedIndices { get; }

        public ButtonGroupSelectionChangedEventArgs(RoutedEvent routedEvent, IReadOnlyList<int> removedIndices, IReadOnlyList<int> addedIndices)
            : base(routedEvent)
        {
            RemovedIndices = removedIndices;
            AddedIndices = addedIndices;
        }
    }

    /// <summary>
    /// A segmented button container styled after the "Button Group" pattern.
    /// Supports optional auto-selection and consistent styling for mixed segments
    /// (e.g., buttons with non-clickable text/count parts).
    /// </summary>
    /// <remarks>
    /// With <see cref="AutoSelect"/> the group works as a segmented control: it is one tab stop, the arrow keys move the
    /// selection (Single) or the focus (Multiple, where Space toggles the focused segment), and screen readers hear the
    /// selected segment, or each segment as on or off. When the template has a <c>PART_Indicator</c>, the group slides
    /// it under the selected segment in single selection, with a spring and a slight squash.
    /// </remarks>
    public class DaisyButtonGroup : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyButtonGroup);

        private const double BaseTextFontSize = 14.0;

        // How long the indicator stays squashed after it starts to move, and how much.
        private static readonly TimeSpan SquashDuration = TimeSpan.FromMilliseconds(160);
        private const double SquashScaleX = 1.08;
        private const double SquashScaleY = 0.9;

        private readonly DaisyControlLifecycle _lifecycle;
        private ObservableCollection<int> _selectedIndices = new();
        private INotifyCollectionChanged? _itemsObservable;
        private int[] _selection = [];
        private bool _setsTabNavigation;
        private Border? _indicator;
        private Point? _indicatorOrigin;
        private bool _animateIndicator;
        private DispatcherTimer? _squashTimer;

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
            AvaloniaProperty.Register<DaisyButtonGroup, int>(nameof(SelectedIndex), -1, defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets the selected index when SelectionMode is Single. It binds two-way by default, and a click keeps
        /// a binding on it.
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
        /// Defines the <see cref="SelectedIndices"/> property.
        /// </summary>
        public static readonly DirectProperty<DaisyButtonGroup, ObservableCollection<int>> SelectedIndicesProperty =
            AvaloniaProperty.RegisterDirect<DaisyButtonGroup, ObservableCollection<int>>(
                nameof(SelectedIndices), o => o.SelectedIndices, (o, v) => o.SelectedIndices = v,
                defaultBindingMode: BindingMode.TwoWay);

        /// <summary>
        /// Gets or sets the collection of selected indices when SelectionMode is Multiple.
        /// Modifying this collection updates the visual selection state. A view model can bind its own collection
        /// to it; <c>null</c> sets a new empty collection. Switching to Single selection leaves it as it is, so the
        /// multiple selection comes back with the mode.
        /// </summary>
        public ObservableCollection<int> SelectedIndices
        {
            get => _selectedIndices;
            set
            {
                value ??= new ObservableCollection<int>();
                if (ReferenceEquals(value, _selectedIndices))
                    return;
                _selectedIndices.CollectionChanged -= OnSelectedIndicesChanged;
                SetAndRaise(SelectedIndicesProperty, ref _selectedIndices, value);
                _selectedIndices.CollectionChanged += OnSelectedIndicesChanged;
                OnSelectedIndicesChanged(_selectedIndices, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
            }
        }

        /// <summary>
        /// Raised when the selected indices collection changes (Multiple mode).
        /// </summary>
        public event EventHandler<NotifyCollectionChangedEventArgs>? SelectedIndicesChanged;

        /// <summary>
        /// Defines the <see cref="SelectionChanged"/> event.
        /// </summary>
        public static readonly RoutedEvent<ButtonGroupSelectionChangedEventArgs> SelectionChangedEvent =
            RoutedEvent.Register<DaisyButtonGroup, ButtonGroupSelectionChangedEventArgs>(
                nameof(SelectionChanged), RoutingStrategies.Bubble);

        /// <summary>
        /// Raised whenever the selection changes, by a click, the keyboard or code, with the added and the removed
        /// indices. In single selection that is at most one each.
        /// </summary>
        public event EventHandler<ButtonGroupSelectionChangedEventArgs> SelectionChanged
        {
            add => AddHandler(SelectionChangedEvent, value);
            remove => RemoveHandler(SelectionChangedEvent, value);
        }

        public static readonly RoutedEvent<ButtonGroupItemSelectedEventArgs> ItemSelectedEvent =
            RoutedEvent.Register<DaisyButtonGroup, ButtonGroupItemSelectedEventArgs>(
                nameof(ItemSelected), RoutingStrategies.Bubble);

        /// <summary>
        /// Raised when a button segment is clicked, including a click on the selected segment.
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
                        SetCurrentValue(SelectedIndexProperty, index);
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
                UpdateSelection();
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
            // A squash cut short would stay: the indicator is not placed again while its segment keeps its position.
            EndSquash();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _indicator = e.NameScope.Find<Border>("PART_Indicator");
            _indicatorOrigin = null;
        }

        protected override AutomationPeer OnCreateAutomationPeer() => new DaisyButtonGroupAutomationPeer(this);

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
                    // SelectedIndices stays as it is: it may be a collection of a view model, and the multiple selection
                    // comes back when the mode does.
                    if (SelectedIndex >= ItemCount)
                    {
                        SetCurrentValue(SelectedIndexProperty, -1);
                    }
                }

                if (change.Property == AutoSelectProperty)
                {
                    // One tab stop: Tab enters at the selected (or last focused) segment, the arrow keys move inside.
                    // A tab navigation set on the group or by a style stays.
                    if (AutoSelect && !IsSet(KeyboardNavigation.TabNavigationProperty))
                    {
                        SetCurrentValue(KeyboardNavigation.TabNavigationProperty, KeyboardNavigationMode.Once);
                        _setsTabNavigation = true;
                    }
                    else if (!AutoSelect && _setsTabNavigation)
                    {
                        ClearValue(KeyboardNavigation.TabNavigationProperty);
                        _setsTabNavigation = false;
                    }
                }

                if (change.Property == SelectedIndexProperty)
                {
                    // Arranging places the indicator once the segments have their bounds.
                    _animateIndicator = _indicatorOrigin != null;
                }

                ApplyAll();

                if (change.Property == SelectedIndexProperty || change.Property == SelectionModeProperty)
                    UpdateSelection();
                InvalidateArrange();
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var size = base.ArrangeOverride(finalSize);
            PlaceIndicator();
            return size;
        }

        private int[] GetSelection() => SelectionMode == DaisyButtonGroupSelectionMode.Multiple
            ? [.. _selectedIndices.Distinct().Order()]
            : SelectedIndex >= 0 ? [SelectedIndex] : [];

        /// <summary>
        /// Raises <see cref="SelectionChanged"/> and tells screen readers about the segments whose selection changed,
        /// when the selection differs from the last one.
        /// </summary>
        private void UpdateSelection()
        {
            var selection = GetSelection();
            var removed = _selection.Except(selection).ToArray();
            var added = selection.Except(_selection).ToArray();
            _selection = selection;
            if (removed.Length == 0 && added.Length == 0)
                return;

            foreach (var (index, button) in GetSegments())
            {
                if (ControlAutomationPeer.FromElement(button) is not { } peer)
                    continue;
                if (removed.Contains(index))
                    RaiseSegmentChanged(peer, false);
                else if (added.Contains(index))
                    RaiseSegmentChanged(peer, true);
            }

            RaiseEvent(new ButtonGroupSelectionChangedEventArgs(SelectionChangedEvent, removed, added));
        }

        private void RaiseSegmentChanged(AutomationPeer peer, bool isSelected)
        {
            if (SelectionMode == DaisyButtonGroupSelectionMode.Multiple)
            {
                peer.RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
                    isSelected ? ToggleState.Off : ToggleState.On, isSelected ? ToggleState.On : ToggleState.Off);
            }
            else
            {
                peer.RaisePropertyChangedEvent(SelectionItemPatternIdentifiers.IsSelectedProperty, !isSelected, isSelected);
            }
        }

        /// <summary>
        /// Gets whether the selection of this group includes <paramref name="segment"/>; <c>null</c> when the group does
        /// not select (no <see cref="AutoSelect"/>) or the control is not one of its segments.
        /// </summary>
        internal bool? IsSegmentSelected(Control segment)
        {
            if (!AutoSelect || FindSegmentIndex(segment) is not { } index)
                return null;
            return SelectionMode == DaisyButtonGroupSelectionMode.Multiple ? _selectedIndices.Contains(index) : SelectedIndex == index;
        }

        /// <summary>
        /// Selects <paramref name="segment"/> (Single), or adds it to or removes it from the selection (Multiple), as a
        /// screen reader asks.
        /// </summary>
        internal void SetSegmentSelected(Control segment, bool isSelected)
        {
            if (!AutoSelect || FindSegmentIndex(segment) is not { } index)
                return;
            if (SelectionMode == DaisyButtonGroupSelectionMode.Multiple)
            {
                if (isSelected && !_selectedIndices.Contains(index))
                    _selectedIndices.Add(index);
                else if (!isSelected)
                    _selectedIndices.Remove(index);
            }
            else if (isSelected)
            {
                SetCurrentValue(SelectedIndexProperty, index);
            }
        }

        private int? FindSegmentIndex(Control segment)
        {
            foreach (var (index, button) in GetSegments())
            {
                if (ReferenceEquals(button, segment))
                    return index;
            }
            return null;
        }

        // ItemsControl moves the focus with the arrow keys itself; a selecting group handles them first.
        protected override void OnKeyDown(KeyEventArgs e)
        {
            HandleSegmentKey(e);
            if (!e.Handled)
                base.OnKeyDown(e);
        }

        /// <summary>
        /// Moves the selection (Single) or the focus (Multiple) with the arrow keys, Home and End.
        /// </summary>
        private void HandleSegmentKey(KeyEventArgs e)
        {
            if (!AutoSelect || e.Handled || e.KeyModifiers != KeyModifiers.None)
                return;
            var rightToLeft = FlowDirection == FlowDirection.RightToLeft;
            var step = e.Key switch
            {
                Key.Up => -1,
                Key.Down => 1,
                Key.Left => rightToLeft ? 1 : -1,
                Key.Right => rightToLeft ? -1 : 1,
                _ => 0
            };
            if (step == 0 && e.Key is not (Key.Home or Key.End))
                return;

            var segments = GetSegments().Where(s => s.Button.IsEffectivelyEnabled).ToList();
            if (segments.Count == 0)
                return;
            var current = segments.FindIndex(s => s.Button.IsKeyboardFocusWithin);
            if (current < 0)
                current = segments.FindIndex(s => s.Index == SelectedIndex);
            var target = e.Key switch
            {
                Key.Home => 0,
                Key.End => segments.Count - 1,
                _ => Math.Clamp(current < 0 ? 0 : current + step, 0, segments.Count - 1)
            };

            var (index, button) = segments[target];
            button.Focus(NavigationMethod.Directional);
            if (SelectionMode == DaisyButtonGroupSelectionMode.Single)
                SetCurrentValue(SelectedIndexProperty, index);
            e.Handled = true;
        }

        /// <summary>
        /// Moves the indicator under the selected segment: with the spring after a selection change, at once after a
        /// resize or when it appears. It only marks a single selection; in multiple selection the segments mark
        /// themselves through the <c>button-group-active</c> class.
        /// </summary>
        private void PlaceIndicator()
        {
            if (_indicator?.GetVisualParent() is not Visual layer)
                return;

            var animate = _animateIndicator;
            _animateIndicator = false;
            Button? segment = null;
            if (AutoSelect && SelectionMode == DaisyButtonGroupSelectionMode.Single && SelectedIndex >= 0)
                segment = GetSegments().FirstOrDefault(s => s.Index == SelectedIndex).Button;
            if (segment?.TranslatePoint(default, layer) is not { } origin)
            {
                _indicator.IsVisible = false;
                _indicatorOrigin = null;
                return;
            }

            _indicator.IsVisible = true;
            _indicator.Width = segment.Bounds.Width;
            _indicator.Height = segment.Bounds.Height;
            if (_indicatorOrigin == origin)
                return;
            _indicatorOrigin = origin;

            if (animate && DaisyAccessibility.GetMotionEnabled(this))
            {
                _indicator.RenderTransform = CreateIndicatorTransform(origin, SquashScaleX, SquashScaleY);
                StartSquash();
            }
            else
            {
                StopSquash();
                // A local null hides the transitions of the template; clearing it brings them back.
                _indicator.SetValue(Animatable.TransitionsProperty, null);
                _indicator.RenderTransform = CreateIndicatorTransform(origin, 1, 1);
                _indicator.ClearValue(Animatable.TransitionsProperty);
            }
        }

        private void StartSquash()
        {
            if (_squashTimer == null)
            {
                _squashTimer = new DispatcherTimer { Interval = SquashDuration };
                _squashTimer.Tick += OnSquashTimerTick;
            }
            _squashTimer.Stop();
            _squashTimer.Start();
        }

        private void StopSquash() => _squashTimer?.Stop();

        private void OnSquashTimerTick(object? sender, EventArgs e) => EndSquash();

        private void EndSquash()
        {
            if (_squashTimer is not { IsEnabled: true })
                return;
            StopSquash();
            if (_indicator != null && _indicatorOrigin is { } origin)
                _indicator.RenderTransform = CreateIndicatorTransform(origin, 1, 1);
        }

        // The scale comes first, around the center of the indicator, so it does not scale the translation.
        private static ITransform CreateIndicatorTransform(Point origin, double scaleX, double scaleY)
        {
            var builder = TransformOperations.CreateBuilder(2);
            builder.AppendScale(scaleX, scaleY);
            builder.AppendTranslate(origin.X, origin.Y);
            return builder.Build();
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
            foreach (var (index, button) in GetSegments())
            {
                ApplyActiveClass(button, index);
                // Tab enters the group at the selected segment.
                if (AutoSelect && SelectionMode == DaisyButtonGroupSelectionMode.Single && index == SelectedIndex)
                    KeyboardNavigation.SetTabOnceActiveElement(this, button);
            }
        }

        // The button of each segment with its index: the generated containers first, else the buttons placed
        // directly in XAML.
        private List<(int Index, Button Button)> GetSegments()
        {
            var segments = new List<(int Index, Button Button)>();
            for (var i = 0; i < ItemCount; i++)
            {
                var container = ContainerFromIndex(i);
                if ((container as Button ?? container?.GetVisualDescendants().OfType<Button>().FirstOrDefault()) is { } button)
                    segments.Add((i, button));
            }

            if (segments.Count == 0)
            {
                var buttons = this.GetLogicalChildren().OfType<Button>().ToList();
                for (var i = 0; i < buttons.Count; i++)
                    segments.Add((i, buttons[i]));
            }
            return segments;
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

    /// <summary>
    /// Exposes a group in single selection (<see cref="DaisyButtonGroup.AutoSelect"/>) to screen readers as a selection
    /// container, whose <see cref="DaisyButton"/> segments are its items. In multiple selection the segments are toggles
    /// of their own, so the group is a plain group.
    /// </summary>
    internal sealed class DaisyButtonGroupAutomationPeer(DaisyButtonGroup owner) : ControlAutomationPeer(owner), ISelectionProvider
    {
        private DaisyButtonGroup Group => (DaisyButtonGroup)Owner;

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override object? GetProviderCore(Type providerType) =>
            providerType == typeof(ISelectionProvider)
            && (!Group.AutoSelect || Group.SelectionMode != DaisyButtonGroupSelectionMode.Single)
                ? null
                : base.GetProviderCore(providerType);

        public bool CanSelectMultiple => false;

        public bool IsSelectionRequired => false;

        public IReadOnlyList<AutomationPeer> GetSelection() =>
        [
            .. Group.GetLogicalDescendants().OfType<DaisyButton>()
                .Where(button => Group.IsSegmentSelected(button) == true)
                .Select(CreatePeerForElement)
        ];
    }

    /// <summary>
    /// Exposes a button to screen readers as a segment of a selecting <see cref="DaisyButtonGroup"/>: as a selectable
    /// item in single selection, as an on or off toggle in multiple selection, and as a plain button elsewhere.
    /// </summary>
    internal sealed class ButtonGroupSegmentAutomationPeer(Button owner) : ButtonAutomationPeer(owner), ISelectionItemProvider, IToggleProvider
    {
        private DaisyButtonGroup? Group => Owner.FindLogicalAncestorOfType<DaisyButtonGroup>();

        private bool? IsSelectedInGroup => Group?.IsSegmentSelected(Owner);

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            IsSelectedInGroup != null && Group?.SelectionMode == DaisyButtonGroupSelectionMode.Single
                ? AutomationControlType.RadioButton
                : base.GetAutomationControlTypeCore();

        protected override object? GetProviderCore(Type providerType)
        {
            if (providerType == typeof(ISelectionItemProvider) || providerType == typeof(IToggleProvider))
            {
                if (IsSelectedInGroup == null)
                    return null;
                var multiple = Group?.SelectionMode == DaisyButtonGroupSelectionMode.Multiple;
                if (multiple != (providerType == typeof(IToggleProvider)))
                    return null;
            }
            return base.GetProviderCore(providerType);
        }

        public bool IsSelected => IsSelectedInGroup == true;

        public ISelectionProvider? SelectionContainer =>
            Group is { } group ? CreatePeerForElement(group) as ISelectionProvider : null;

        public void Select() => Group?.SetSegmentSelected(Owner, true);

        public void AddToSelection() => Group?.SetSegmentSelected(Owner, true);

        public void RemoveFromSelection() => Group?.SetSegmentSelected(Owner, false);

        public ToggleState ToggleState => IsSelectedInGroup == true ? ToggleState.On : ToggleState.Off;

        public void Toggle() => Group?.SetSegmentSelected(Owner, IsSelectedInGroup != true);
    }
}
