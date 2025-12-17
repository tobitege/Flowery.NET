using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Defines how tab widths are calculated.
    /// </summary>
    public enum DaisyTabWidthMode
    {
        /// <summary>
        /// Each tab sizes to fit its content (default behavior).
        /// </summary>
        Auto,

        /// <summary>
        /// All tabs have equal width, based on the widest tab.
        /// </summary>
        Equal,

        /// <summary>
        /// All tabs have a fixed width specified by TabWidth property.
        /// </summary>
        Fixed
    }

    public enum DaisyTabVariant
    {
        None,
        Bordered,
        Lifted,
        Boxed
    }

    /// <summary>
    /// Theme-independent tab palette colors for <see cref="DaisyTabs.TabPaletteColorProperty"/>.
    /// These use fixed colors (not theme semantic colors) and are intended for end-user color selection.
    /// </summary>
    public enum DaisyTabPaletteColor
    {
        /// <summary>No palette color; uses default styling.</summary>
        Default,
        Purple,
        Indigo,
        Pink,
        SkyBlue,
        Blue,
        Lime,
        Green,
        Yellow,
        Orange,
        Red,
        Gray
    }

    /// <summary>
    /// Event args for tab-related requests (close, color change, etc.).
    /// </summary>
    public class DaisyTabEventArgs : EventArgs
    {
        /// <summary>
        /// The TabItem associated with the request.
        /// </summary>
        public TabItem TabItem { get; }

        /// <summary>
        /// The index of the tab in the Items collection.
        /// </summary>
        public int TabIndex { get; }

        /// <summary>
        /// The underlying data item if using ItemsSource binding.
        /// </summary>
        public object? DataItem { get; }

        public DaisyTabEventArgs(TabItem tabItem, int tabIndex, object? dataItem = null)
        {
            TabItem = tabItem;
            TabIndex = tabIndex;
            DataItem = dataItem;
        }
    }

    /// <summary>
    /// Event args for tab color change requests.
    /// </summary>
    public class DaisyTabColorChangedEventArgs : DaisyTabEventArgs
    {
        /// <summary>
        /// The new color requested for the tab.
        /// </summary>
        public DaisyColor NewColor { get; }

        public DaisyTabColorChangedEventArgs(TabItem tabItem, int tabIndex, DaisyColor newColor, object? dataItem = null)
            : base(tabItem, tabIndex, dataItem)
        {
            NewColor = newColor;
        }
    }

    /// <summary>
    /// Event args for tab palette color change requests.
    /// </summary>
    public class DaisyTabPaletteColorChangedEventArgs : DaisyTabEventArgs
    {
        /// <summary>
        /// The new palette color requested for the tab.
        /// </summary>
        public DaisyTabPaletteColor NewColor { get; }

        public DaisyTabPaletteColorChangedEventArgs(TabItem tabItem, int tabIndex, DaisyTabPaletteColor newColor, object? dataItem = null)
            : base(tabItem, tabIndex, dataItem)
        {
            NewColor = newColor;
        }
    }

    /// <summary>
    /// A styled TabControl with DaisyUI-inspired variants, sizes, and optional tab colors/context menu.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyTabs : TabControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTabs);

        private const string DefaultPaletteStrokeHex = "#9ca3af";
        private static readonly Avalonia.Media.IBrush DefaultPaletteStrokeBrush = Avalonia.Media.Brush.Parse(DefaultPaletteStrokeHex);
        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = Services.FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        #region Attached Property: TabColor

        /// <summary>
        /// Attached property to set a semantic color on individual TabItems.
        /// </summary>
        public static readonly AttachedProperty<DaisyColor> TabColorProperty =
            AvaloniaProperty.RegisterAttached<DaisyTabs, TabItem, DaisyColor>("TabColor", DaisyColor.Default);

        /// <summary>
        /// Gets the tab color for a TabItem.
        /// </summary>
        public static DaisyColor GetTabColor(TabItem element) => element.GetValue(TabColorProperty);

        /// <summary>
        /// Sets the tab color for a TabItem.
        /// </summary>
        public static void SetTabColor(TabItem element, DaisyColor value) => element.SetValue(TabColorProperty, value);

        #endregion

        #region Attached Property: TabPaletteColor

        /// <summary>
        /// Attached property to set a theme-independent palette color on individual TabItems.
        /// This is intended for end-user color selection (fixed colors, not theme semantic colors).
        /// </summary>
        public static readonly AttachedProperty<DaisyTabPaletteColor> TabPaletteColorProperty =
            AvaloniaProperty.RegisterAttached<DaisyTabs, TabItem, DaisyTabPaletteColor>("TabPaletteColor", DaisyTabPaletteColor.Default);

        /// <summary>
        /// Gets the palette color for a TabItem.
        /// </summary>
        public static DaisyTabPaletteColor GetTabPaletteColor(TabItem element) => element.GetValue(TabPaletteColorProperty);

        /// <summary>
        /// Sets the palette color for a TabItem.
        /// </summary>
        public static void SetTabPaletteColor(TabItem element, DaisyTabPaletteColor value) => element.SetValue(TabPaletteColorProperty, value);

        #endregion

        #region Context Menu & Callback Properties

        /// <summary>
        /// Gets or sets whether the tab context menu is enabled.
        /// When true, right-clicking a tab shows close/color options.
        /// </summary>
        public static readonly StyledProperty<bool> EnableTabContextMenuProperty =
            AvaloniaProperty.Register<DaisyTabs, bool>(nameof(EnableTabContextMenu), false);

        public bool EnableTabContextMenu
        {
            get => GetValue(EnableTabContextMenuProperty);
            set => SetValue(EnableTabContextMenuProperty, value);
        }

        /// <summary>
        /// Raised when the user requests to close a tab via context menu.
        /// The host app should handle removal from the Items/ItemsSource collection.
        /// </summary>
        public event EventHandler<DaisyTabEventArgs>? CloseTabRequested;

        /// <summary>
        /// Raised when the user requests to close all tabs except the clicked one.
        /// </summary>
        public event EventHandler<DaisyTabEventArgs>? CloseOtherTabsRequested;

        /// <summary>
        /// Raised when the user requests to close tabs to the right of the clicked one.
        /// </summary>
        public event EventHandler<DaisyTabEventArgs>? CloseTabsToRightRequested;

        /// <summary>
        /// Raised when the user selects a new color for a tab via context menu.
        /// </summary>
        public event EventHandler<DaisyTabColorChangedEventArgs>? TabColorChangeRequested;

        /// <summary>
        /// Raised when the user selects a new palette color for a tab via context menu.
        /// </summary>
        public event EventHandler<DaisyTabPaletteColorChangedEventArgs>? TabPaletteColorChangeRequested;

        /// <summary>
        /// Optional callback invoked when close tab is requested.
        /// Alternative to the CloseTabRequested event.
        /// </summary>
        public static readonly StyledProperty<Action<DaisyTabEventArgs>?> CloseTabCallbackProperty =
            AvaloniaProperty.Register<DaisyTabs, Action<DaisyTabEventArgs>?>(nameof(CloseTabCallback));

        public Action<DaisyTabEventArgs>? CloseTabCallback
        {
            get => GetValue(CloseTabCallbackProperty);
            set => SetValue(CloseTabCallbackProperty, value);
        }

        /// <summary>
        /// Optional callback invoked when close other tabs is requested.
        /// </summary>
        public static readonly StyledProperty<Action<DaisyTabEventArgs>?> CloseOtherTabsCallbackProperty =
            AvaloniaProperty.Register<DaisyTabs, Action<DaisyTabEventArgs>?>(nameof(CloseOtherTabsCallback));

        public Action<DaisyTabEventArgs>? CloseOtherTabsCallback
        {
            get => GetValue(CloseOtherTabsCallbackProperty);
            set => SetValue(CloseOtherTabsCallbackProperty, value);
        }

        /// <summary>
        /// Optional callback invoked when close tabs to right is requested.
        /// </summary>
        public static readonly StyledProperty<Action<DaisyTabEventArgs>?> CloseTabsToRightCallbackProperty =
            AvaloniaProperty.Register<DaisyTabs, Action<DaisyTabEventArgs>?>(nameof(CloseTabsToRightCallback));

        public Action<DaisyTabEventArgs>? CloseTabsToRightCallback
        {
            get => GetValue(CloseTabsToRightCallbackProperty);
            set => SetValue(CloseTabsToRightCallbackProperty, value);
        }

        /// <summary>
        /// Optional callback invoked when tab color change is requested.
        /// </summary>
        public static readonly StyledProperty<Action<DaisyTabColorChangedEventArgs>?> TabColorChangeCallbackProperty =
            AvaloniaProperty.Register<DaisyTabs, Action<DaisyTabColorChangedEventArgs>?>(nameof(TabColorChangeCallback));

        public Action<DaisyTabColorChangedEventArgs>? TabColorChangeCallback
        {
            get => GetValue(TabColorChangeCallbackProperty);
            set => SetValue(TabColorChangeCallbackProperty, value);
        }

        /// <summary>
        /// Optional callback invoked when tab palette color change is requested.
        /// </summary>
        public static readonly StyledProperty<Action<DaisyTabPaletteColorChangedEventArgs>?> TabPaletteColorChangeCallbackProperty =
            AvaloniaProperty.Register<DaisyTabs, Action<DaisyTabPaletteColorChangedEventArgs>?>(nameof(TabPaletteColorChangeCallback));

        public Action<DaisyTabPaletteColorChangedEventArgs>? TabPaletteColorChangeCallback
        {
            get => GetValue(TabPaletteColorChangeCallbackProperty);
            set => SetValue(TabPaletteColorChangeCallbackProperty, value);
        }

        #endregion

        #region Variant/Size Properties

        public static readonly StyledProperty<DaisyTabVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyTabs, DaisyTabVariant>(nameof(Variant), DaisyTabVariant.Bordered);

        public DaisyTabVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyTabs, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        #endregion

        #region Tab Width Properties

        public static readonly StyledProperty<DaisyTabWidthMode> TabWidthModeProperty =
            AvaloniaProperty.Register<DaisyTabs, DaisyTabWidthMode>(nameof(TabWidthMode), DaisyTabWidthMode.Auto);

        /// <summary>
        /// Gets or sets how tab widths are calculated.
        /// </summary>
        public DaisyTabWidthMode TabWidthMode
        {
            get => GetValue(TabWidthModeProperty);
            set => SetValue(TabWidthModeProperty, value);
        }

        public static readonly StyledProperty<double> TabWidthProperty =
            AvaloniaProperty.Register<DaisyTabs, double>(nameof(TabWidth), double.NaN);

        /// <summary>
        /// Gets or sets the fixed width for tabs when TabWidthMode is Fixed.
        /// </summary>
        public double TabWidth
        {
            get => GetValue(TabWidthProperty);
            set => SetValue(TabWidthProperty, value);
        }

        public static readonly StyledProperty<double> TabMaxWidthProperty =
            AvaloniaProperty.Register<DaisyTabs, double>(nameof(TabMaxWidth), double.PositiveInfinity);

        /// <summary>
        /// Gets or sets the maximum width for each tab. Works with any TabWidthMode.
        /// </summary>
        public double TabMaxWidth
        {
            get => GetValue(TabMaxWidthProperty);
            set => SetValue(TabMaxWidthProperty, value);
        }

        public static readonly StyledProperty<double> TabMinWidthProperty =
            AvaloniaProperty.Register<DaisyTabs, double>(nameof(TabMinWidth), 0d);

        /// <summary>
        /// Gets or sets the minimum width for each tab.
        /// </summary>
        public double TabMinWidth
        {
            get => GetValue(TabMinWidthProperty);
            set => SetValue(TabMinWidthProperty, value);
        }

        #endregion

        static DaisyTabs()
        {
            TabWidthModeProperty.Changed.AddClassHandler<DaisyTabs>((x, _) => x.UpdateTabWidths());
            TabWidthProperty.Changed.AddClassHandler<DaisyTabs>((x, _) => x.UpdateTabWidths());
            TabMaxWidthProperty.Changed.AddClassHandler<DaisyTabs>((x, _) => x.UpdateTabWidths());
            TabMinWidthProperty.Changed.AddClassHandler<DaisyTabs>((x, _) => x.UpdateTabWidths());
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            FloweryLocalization.CultureChanged += OnCultureChanged;
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            FloweryLocalization.CultureChanged -= OnCultureChanged;
        }

        private void OnCultureChanged(object? sender, CultureInfo culture)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnCultureChanged(sender, culture));
                return;
            }

            // Rebuild context menus to update localized strings
            if (EnableTabContextMenu)
            {
                RebuildContextMenus();
            }
        }

        private void RebuildContextMenus()
        {
            var tabItems = Items.OfType<TabItem>().ToList();
            foreach (var tabItem in tabItems)
            {
                if (tabItem.ContextMenu is ContextMenu menu && menu.Tag as string == "DaisyTabsContextMenu")
                {
                    tabItem.ContextMenu = CreateTabContextMenu(tabItem);
                }
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            UpdateTabWidths();
            SetupTabContextMenus();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemCountProperty)
            {
                // Defer to allow items to be realized
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    UpdateTabWidths();
                    SetupTabContextMenus();
                });
            }
            else if (change.Property == EnableTabContextMenuProperty)
            {
                SetupTabContextMenus();
            }
        }

        #region Context Menu Setup

        private void SetupTabContextMenus()
        {
            var tabItems = Items.OfType<TabItem>().ToList();

            foreach (var tabItem in tabItems)
            {
                if (EnableTabContextMenu)
                {
                    if (tabItem.ContextMenu == null)
                    {
                        tabItem.ContextMenu = CreateTabContextMenu(tabItem);
                    }
                }
                else
                {
                    if (tabItem.ContextMenu is ContextMenu menu && menu.Tag as string == "DaisyTabsContextMenu")
                    {
                        tabItem.ContextMenu = null;
                    }
                }
            }
        }

        private ContextMenu CreateTabContextMenu(TabItem tabItem)
        {
            var menu = new ContextMenu { Tag = "DaisyTabsContextMenu" };

            var closeItem = new MenuItem { Header = FloweryLocalization.GetStringInternal("Tabs_CloseTab", "Close Tab") };
            closeItem.Click += (_, _) => OnCloseTabRequested(tabItem);

            var closeOthersItem = new MenuItem { Header = FloweryLocalization.GetStringInternal("Tabs_CloseOtherTabs", "Close Other Tabs") };
            closeOthersItem.Click += (_, _) => OnCloseOtherTabsRequested(tabItem);

            var closeRightItem = new MenuItem { Header = FloweryLocalization.GetStringInternal("Tabs_CloseTabsToRight", "Close Tabs to the Right") };
            closeRightItem.Click += (_, _) => OnCloseTabsToRightRequested(tabItem);

            menu.Items.Add(closeItem);
            menu.Items.Add(closeOthersItem);
            menu.Items.Add(closeRightItem);
            menu.Items.Add(new Separator());

            // Tab Color grid (theme-independent palette) - no submenu
            menu.Items.Add(new MenuItem
            {
                Header = FloweryLocalization.GetStringInternal("Tabs_TabColor", "Tab Color"),
                IsEnabled = false
            });

            var colors = new[]
            {
                (DaisyTabPaletteColor.Default, FloweryLocalization.GetStringInternal("Tabs_Palette_Default", "Default"), (string?)null),
                (DaisyTabPaletteColor.Purple, FloweryLocalization.GetStringInternal("Tabs_Palette_Purple", "Purple"), "#7c3aed"),
                (DaisyTabPaletteColor.Indigo, FloweryLocalization.GetStringInternal("Tabs_Palette_Indigo", "Indigo"), "#6366f1"),
                (DaisyTabPaletteColor.Pink, FloweryLocalization.GetStringInternal("Tabs_Palette_Pink", "Pink"), "#f472b6"),
                (DaisyTabPaletteColor.SkyBlue, FloweryLocalization.GetStringInternal("Tabs_Palette_SkyBlue", "Sky Blue"), "#38bdf8"),
                (DaisyTabPaletteColor.Blue, FloweryLocalization.GetStringInternal("Tabs_Palette_Blue", "Blue"), "#0ea5e9"),
                (DaisyTabPaletteColor.Lime, FloweryLocalization.GetStringInternal("Tabs_Palette_Lime", "Lime"), "#84cc16"),
                (DaisyTabPaletteColor.Green, FloweryLocalization.GetStringInternal("Tabs_Palette_Green", "Green"), "#22c55e"),
                (DaisyTabPaletteColor.Yellow, FloweryLocalization.GetStringInternal("Tabs_Palette_Yellow", "Yellow"), "#eab308"),
                (DaisyTabPaletteColor.Orange, FloweryLocalization.GetStringInternal("Tabs_Palette_Orange", "Orange"), "#f59e0b"),
                (DaisyTabPaletteColor.Red, FloweryLocalization.GetStringInternal("Tabs_Palette_Red", "Red"), "#ef4444"),
                (DaisyTabPaletteColor.Gray, FloweryLocalization.GetStringInternal("Tabs_Palette_Gray", "Gray"), "#64748b"),
            };

            var grid = new UniformGrid
            {
                Columns = 6,
                Margin = new Thickness(8, 2, 8, 6)
            };

            foreach (var (color, name, hex) in colors)
            {
                var button = new Button
                {
                    Width = 22,
                    Height = 22,
                    Padding = new Thickness(0),
                    Background = Avalonia.Media.Brushes.Transparent,
                    BorderBrush = Avalonia.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Content = new Avalonia.Controls.Shapes.Ellipse
                    {
                        Width = 12,
                        Height = 12,
                        Fill = hex == null ? Avalonia.Media.Brushes.Transparent : Avalonia.Media.Brush.Parse(hex),
                        Stroke = hex == null ? DefaultPaletteStrokeBrush : null,
                        StrokeThickness = hex == null ? 1 : 0
                    }
                };

                ToolTip.SetTip(button, name);
                button.Click += (_, _) =>
                {
                    OnTabPaletteColorChangeRequested(tabItem, color);
                    menu.Close();
                };

                grid.Children.Add(button);
            }

            menu.Items.Add(new MenuItem { Header = grid });
            return menu;
        }

        private void OnCloseTabRequested(TabItem tabItem)
        {
            var index = Items.OfType<TabItem>().ToList().IndexOf(tabItem);
            var args = new DaisyTabEventArgs(tabItem, index, tabItem.DataContext);

            CloseTabRequested?.Invoke(this, args);
            CloseTabCallback?.Invoke(args);
        }

        private void OnCloseOtherTabsRequested(TabItem tabItem)
        {
            var index = Items.OfType<TabItem>().ToList().IndexOf(tabItem);
            var args = new DaisyTabEventArgs(tabItem, index, tabItem.DataContext);

            CloseOtherTabsRequested?.Invoke(this, args);
            CloseOtherTabsCallback?.Invoke(args);
        }

        private void OnCloseTabsToRightRequested(TabItem tabItem)
        {
            var index = Items.OfType<TabItem>().ToList().IndexOf(tabItem);
            var args = new DaisyTabEventArgs(tabItem, index, tabItem.DataContext);

            CloseTabsToRightRequested?.Invoke(this, args);
            CloseTabsToRightCallback?.Invoke(args);
        }

        private void OnTabColorChangeRequested(TabItem tabItem, DaisyColor newColor)
        {
            var index = Items.OfType<TabItem>().ToList().IndexOf(tabItem);
            var args = new DaisyTabColorChangedEventArgs(tabItem, index, newColor, tabItem.DataContext);

            // Apply the color to the attached property
            SetTabColor(tabItem, newColor);

            TabColorChangeRequested?.Invoke(this, args);
            TabColorChangeCallback?.Invoke(args);
        }

        private void OnTabPaletteColorChangeRequested(TabItem tabItem, DaisyTabPaletteColor newColor)
        {
            var index = Items.OfType<TabItem>().ToList().IndexOf(tabItem);
            var args = new DaisyTabPaletteColorChangedEventArgs(tabItem, index, newColor, tabItem.DataContext);

            // Apply the palette color to the attached property
            SetTabPaletteColor(tabItem, newColor);

            TabPaletteColorChangeRequested?.Invoke(this, args);
            TabPaletteColorChangeCallback?.Invoke(args);
        }

        #endregion

        private void UpdateTabWidths()
        {
            var mode = TabWidthMode;
            var fixedWidth = TabWidth;
            var maxWidth = TabMaxWidth;
            var minWidth = TabMinWidth;

            // Get all TabItem children from Items collection
            var tabItems = Items
                .OfType<TabItem>()
                .ToList();

            if (tabItems.Count == 0)
                return;

            foreach (var tabItem in tabItems)
            {
                switch (mode)
                {
                    case DaisyTabWidthMode.Auto:
                        tabItem.Width = double.NaN;
                        tabItem.MinWidth = minWidth;
                        tabItem.MaxWidth = maxWidth;
                        break;

                    case DaisyTabWidthMode.Equal:
                        // For Equal mode, we need to measure all tabs first
                        // Reset to auto, measure, then apply the max
                        tabItem.Width = double.NaN;
                        tabItem.MinWidth = 0;
                        tabItem.MaxWidth = double.PositiveInfinity;
                        break;

                    case DaisyTabWidthMode.Fixed:
                        if (!double.IsNaN(fixedWidth) && fixedWidth > 0)
                        {
                            tabItem.Width = Math.Min(fixedWidth, maxWidth);
                            tabItem.MinWidth = minWidth;
                            tabItem.MaxWidth = maxWidth;
                        }
                        break;
                }
            }

            if (mode == DaisyTabWidthMode.Equal)
            {
                // Force layout to measure natural sizes
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    var measuredWidths = tabItems
                        .Select(t => t.DesiredSize.Width)
                        .Where(w => w > 0)
                        .ToList();

                    if (measuredWidths.Count > 0)
                    {
                        var equalWidth = measuredWidths.Max();
                        equalWidth = Math.Max(equalWidth, minWidth);
                        equalWidth = Math.Min(equalWidth, maxWidth);

                        foreach (var tabItem in tabItems)
                        {
                            tabItem.Width = equalWidth;
                            tabItem.MinWidth = equalWidth;
                            tabItem.MaxWidth = equalWidth;
                        }
                    }
                }, Avalonia.Threading.DispatcherPriority.Render);
            }
        }
    }
}
