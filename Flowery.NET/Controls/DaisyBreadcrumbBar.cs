using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Flowery.Localization;

namespace Flowery.Controls
{
    /// <summary>
    /// Event data for <see cref="DaisyBreadcrumbBar.ItemClicked"/>.
    /// </summary>
    public class DaisyBreadcrumbBarItemClickedEventArgs : EventArgs
    {
        public DaisyBreadcrumbBarItemClickedEventArgs(int index, object? item)
        {
            Index = index;
            Item = item;
        }

        /// <summary>
        /// Gets the index of the clicked item in <see cref="DaisyBreadcrumbBar.ItemsSource"/>.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Gets the clicked item.
        /// </summary>
        public object? Item { get; }
    }

    /// <summary>
    /// A breadcrumb trail built from <see cref="ItemsSource"/>, styled with Daisy tokens.
    /// When the items do not fit, the leading items collapse into an ellipsis button whose menu lists them.
    /// </summary>
    /// <remarks>
    /// Counterpart of the WinUI <c>BreadcrumbBar</c> wrapper in Flowery.WinUI. Use <see cref="DaisyBreadcrumbs"/>
    /// for hand-written <see cref="DaisyBreadcrumbItem"/> children, icons or custom separators.
    /// </remarks>
    [TemplatePart(PART_ItemsPanel, typeof(DaisyBreadcrumbBarPanel))]
    public class DaisyBreadcrumbBar : TemplatedControl
    {
        private const string PART_ItemsPanel = "PART_ItemsPanel";

        private DaisyBreadcrumbBarPanel? _itemsPanel;
        private DaisyBreadcrumbBarItem? _ellipsisItem;
        private INotifyCollectionChanged? _observedCollection;
        private readonly List<object?> _items = [];

        /// <summary>
        /// Defines the <see cref="ItemsSource"/> property.
        /// </summary>
        public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbBar, IEnumerable?>(nameof(ItemsSource));

        /// <summary>
        /// Gets or sets the path segments, from the root to the current item.
        /// </summary>
        public IEnumerable? ItemsSource
        {
            get => GetValue(ItemsSourceProperty);
            set => SetValue(ItemsSourceProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ItemTemplate"/> property.
        /// </summary>
        public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbBar, IDataTemplate?>(nameof(ItemTemplate));

        /// <summary>
        /// Gets or sets the template used to display each item. Without a template the item's text is shown.
        /// </summary>
        public IDataTemplate? ItemTemplate
        {
            get => GetValue(ItemTemplateProperty);
            set => SetValue(ItemTemplateProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbBar, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of the breadcrumb bar.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Occurs when an item is clicked, either in the bar or in the ellipsis menu.
        /// </summary>
        public event EventHandler<DaisyBreadcrumbBarItemClickedEventArgs>? ItemClicked;

        public DaisyBreadcrumbBar()
        {
            FloweryLocalization.AddWeakCultureObserver(this, static (target, _) =>
            {
                if (Dispatcher.UIThread.CheckAccess())
                    target.UpdateEllipsisText();
                else
                    Dispatcher.UIThread.Post(target.UpdateEllipsisText);
            });
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _itemsPanel = e.NameScope.Find<DaisyBreadcrumbBarPanel>(PART_ItemsPanel);
            RebuildItems();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemsSourceProperty)
            {
                ObserveCollection(change.GetNewValue<IEnumerable?>());
                RebuildItems();
            }
            else if (change.Property == ItemTemplateProperty)
            {
                RebuildItems();
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            ObserveCollection(ItemsSource);
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            ObserveCollection(null);
            base.OnDetachedFromVisualTree(e);
        }

        private void ObserveCollection(IEnumerable? source)
        {
            var collection = source as INotifyCollectionChanged;
            if (ReferenceEquals(collection, _observedCollection))
                return;

            if (_observedCollection != null)
                _observedCollection.CollectionChanged -= OnItemsCollectionChanged;

            _observedCollection = collection;

            if (_observedCollection != null)
                _observedCollection.CollectionChanged += OnItemsCollectionChanged;
        }

        private void OnItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildItems();
        }

        private void RebuildItems()
        {
            _items.Clear();
            if (ItemsSource != null)
            {
                foreach (var item in ItemsSource)
                    _items.Add(item);
            }

            if (_itemsPanel == null)
                return;

            foreach (var child in _itemsPanel.Children)
            {
                if (child is DaisyBreadcrumbBarItem existing)
                    existing.Click -= OnItemClick;
            }

            _itemsPanel.Children.Clear();

            if (_ellipsisItem == null)
            {
                _ellipsisItem = new DaisyBreadcrumbBarItem { IsEllipsis = true, Index = -1 };
                _ellipsisItem.Click += OnEllipsisClick;
                UpdateEllipsisText();
            }

            _itemsPanel.Children.Add(_ellipsisItem);

            for (var i = 0; i < _items.Count; i++)
            {
                var itemControl = new DaisyBreadcrumbBarItem
                {
                    Content = _items[i],
                    ContentTemplate = ItemTemplate,
                    Index = i,
                    IsCurrent = i == _items.Count - 1
                };
                itemControl.Click += OnItemClick;
                _itemsPanel.Children.Add(itemControl);
            }
        }

        private void OnItemClick(object? sender, RoutedEventArgs e)
        {
            if (sender is DaisyBreadcrumbBarItem { Index: >= 0 } itemControl && itemControl.Index < _items.Count)
            {
                RaiseItemClicked(itemControl.Index);
            }
        }

        private void OnEllipsisClick(object? sender, RoutedEventArgs e)
        {
            if (_ellipsisItem == null || _itemsPanel == null)
                return;

            var hiddenCount = Math.Min(_itemsPanel.FirstVisibleIndex, _items.Count);
            if (hiddenCount <= 0)
                return;

            var menuItems = new List<MenuItem>(hiddenCount);
            for (var i = 0; i < hiddenCount; i++)
            {
                var menuItem = new MenuItem
                {
                    Header = _items[i],
                    HeaderTemplate = ItemTemplate,
                    Tag = i
                };
                menuItem.Click += OnEllipsisMenuItemClick;
                menuItems.Add(menuItem);
            }

            var flyout = new MenuFlyout
            {
                Placement = PlacementMode.BottomEdgeAlignedLeft,
                ItemsSource = menuItems
            };
            flyout.ShowAt(_ellipsisItem);
        }

        private void OnEllipsisMenuItemClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: int index } && index < _items.Count)
            {
                RaiseItemClicked(index);
            }
        }

        private void UpdateEllipsisText()
        {
            if (_ellipsisItem == null)
                return;

            var text = FloweryLocalization.GetString("BreadcrumbBar_More", "More");
            AutomationProperties.SetName(_ellipsisItem, text);
            ToolTip.SetTip(_ellipsisItem, text);
        }

        private void RaiseItemClicked(int index)
        {
            ItemClicked?.Invoke(this, new DaisyBreadcrumbBarItemClickedEventArgs(index, _items[index]));
        }
    }

    /// <summary>
    /// A single segment of a <see cref="DaisyBreadcrumbBar"/>: an item link or the ellipsis button.
    /// Created by the bar; not meant to be added by hand.
    /// </summary>
    public class DaisyBreadcrumbBarItem : Button
    {
        protected override Type StyleKeyOverride => typeof(DaisyBreadcrumbBarItem);

        /// <summary>
        /// Defines the <see cref="IsCurrent"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsCurrentProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbBarItem, bool>(nameof(IsCurrent));

        /// <summary>
        /// Gets or sets whether this is the last item, which marks the current location.
        /// </summary>
        public bool IsCurrent
        {
            get => GetValue(IsCurrentProperty);
            set => SetValue(IsCurrentProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IsEllipsis"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsEllipsisProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbBarItem, bool>(nameof(IsEllipsis));

        /// <summary>
        /// Gets or sets whether this is the ellipsis button that lists collapsed items.
        /// </summary>
        public bool IsEllipsis
        {
            get => GetValue(IsEllipsisProperty);
            set => SetValue(IsEllipsisProperty, value);
        }

        /// <summary>
        /// Gets the index of the item in the bar's items, or -1 for the ellipsis button.
        /// </summary>
        public int Index { get; internal set; }
    }

    /// <summary>
    /// Lays out breadcrumb segments in a row. When they do not fit, it hides leading items (never the last one)
    /// and shows the ellipsis button, which is always the first child, in their place.
    /// </summary>
    public class DaisyBreadcrumbBarPanel : Panel
    {
        public DaisyBreadcrumbBarPanel()
        {
            ClipToBounds = true;
        }

        /// <summary>
        /// Gets the index of the first item shown in the bar. Items before it are collapsed into the ellipsis menu.
        /// </summary>
        public int FirstVisibleIndex { get; private set; }

        protected override Size MeasureOverride(Size availableSize)
        {
            var height = 0.0;
            foreach (var child in Children)
            {
                child.Measure(new Size(double.PositiveInfinity, availableSize.Height));
                height = Math.Max(height, child.DesiredSize.Height);
            }

            var (firstVisible, showEllipsis) = GetLayout(availableSize.Width);
            return new Size(GetRowWidth(firstVisible, showEllipsis), height);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var (firstVisible, showEllipsis) = GetLayout(finalSize.Width);
            FirstVisibleIndex = firstVisible;

            var x = 0.0;
            for (var i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                var isShown = i == 0 ? showEllipsis : i - 1 >= firstVisible;

                // Hidden segments stay in the tree for measuring, but take no input and are not announced.
                child.Focusable = isShown;
                child.IsHitTestVisible = isShown;
                AutomationProperties.SetAccessibilityView(child, isShown ? AccessibilityView.Default : AccessibilityView.Raw);

                if (isShown)
                {
                    var width = child.DesiredSize.Width;
                    child.Arrange(new Rect(x, (finalSize.Height - child.DesiredSize.Height) / 2, width, child.DesiredSize.Height));
                    x += width;
                }
                else
                {
                    // Outside the clipped bounds: hidden and not hit-testable.
                    child.Arrange(new Rect(-child.DesiredSize.Width - 1, 0, child.DesiredSize.Width, child.DesiredSize.Height));
                }
            }

            return finalSize;
        }

        private (int FirstVisible, bool ShowEllipsis) GetLayout(double availableWidth)
        {
            var itemCount = Math.Max(0, Children.Count - 1);
            if (itemCount == 0 || double.IsInfinity(availableWidth) || GetRowWidth(0, false) <= availableWidth)
                return (0, false);

            if (itemCount == 1)
                return (0, false);

            // Collapse leading items into the ellipsis until the rest fits; the last item always stays.
            var firstVisible = 1;
            while (firstVisible < itemCount - 1 && GetRowWidth(firstVisible, true) > availableWidth)
                firstVisible++;

            return (firstVisible, true);
        }

        private double GetRowWidth(int firstVisible, bool showEllipsis)
        {
            var width = showEllipsis && Children.Count > 0 ? Children[0].DesiredSize.Width : 0;
            for (var i = firstVisible + 1; i < Children.Count; i++)
                width += Children[i].DesiredSize.Width;
            return width;
        }
    }
}
