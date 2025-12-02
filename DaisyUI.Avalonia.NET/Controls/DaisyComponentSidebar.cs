using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace DaisyUI.Avalonia.Controls
{
    public class SidebarCategory
    {
        public string Name { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public bool IsExpanded { get; set; } = true;
        public ObservableCollection<SidebarItem> Items { get; set; } = new();
    }

    public class SidebarItem
    {
        public string Name { get; set; } = string.Empty;
        public string TabHeader { get; set; } = string.Empty;
        public string? Badge { get; set; }
    }

    public class SidebarItemSelectedEventArgs : RoutedEventArgs
    {
        public SidebarItem Item { get; }
        public SidebarCategory Category { get; }

        public SidebarItemSelectedEventArgs(RoutedEvent routedEvent, SidebarItem item, SidebarCategory category)
            : base(routedEvent)
        {
            Item = item;
            Category = category;
        }
    }

    public class DaisyComponentSidebar : TemplatedControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyComponentSidebar);

        private ObservableCollection<SidebarCategory> _allCategories = new();

        public static readonly RoutedEvent<SidebarItemSelectedEventArgs> ItemSelectedEvent =
            RoutedEvent.Register<DaisyComponentSidebar, SidebarItemSelectedEventArgs>(
                nameof(ItemSelected), RoutingStrategies.Bubble);

        public event EventHandler<SidebarItemSelectedEventArgs>? ItemSelected
        {
            add => AddHandler(ItemSelectedEvent, value);
            remove => RemoveHandler(ItemSelectedEvent, value);
        }

        public static readonly StyledProperty<ObservableCollection<SidebarCategory>> CategoriesProperty =
            AvaloniaProperty.Register<DaisyComponentSidebar, ObservableCollection<SidebarCategory>>(
                nameof(Categories), new ObservableCollection<SidebarCategory>());

        public ObservableCollection<SidebarCategory> Categories
        {
            get => GetValue(CategoriesProperty);
            set => SetValue(CategoriesProperty, value);
        }

        public static readonly StyledProperty<SidebarItem?> SelectedItemProperty =
            AvaloniaProperty.Register<DaisyComponentSidebar, SidebarItem?>(nameof(SelectedItem));

        public SidebarItem? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly StyledProperty<double> SidebarWidthProperty =
            AvaloniaProperty.Register<DaisyComponentSidebar, double>(nameof(SidebarWidth), 220);

        public double SidebarWidth
        {
            get => GetValue(SidebarWidthProperty);
            set => SetValue(SidebarWidthProperty, value);
        }

        public static readonly StyledProperty<string> SearchTextProperty =
            AvaloniaProperty.Register<DaisyComponentSidebar, string>(nameof(SearchText), string.Empty);

        public string SearchText
        {
            get => GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        static DaisyComponentSidebar()
        {
            SearchTextProperty.Changed.AddClassHandler<DaisyComponentSidebar>((s, e) => s.OnSearchTextChanged());
        }

        public DaisyComponentSidebar()
        {
            _allCategories = CreateDefaultCategories();
            Categories = _allCategories;
        }

        private void OnSearchTextChanged()
        {
            FilterCategories(SearchText);
        }

        private void FilterCategories(string searchText)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                // Restore all categories with original items
                Categories = _allCategories;
                return;
            }

            var search = searchText.ToLowerInvariant();
            var filtered = new ObservableCollection<SidebarCategory>();

            foreach (var category in _allCategories)
            {
                // Check if category name matches
                var categoryMatches = category.Name.ToLowerInvariant().Contains(search);

                // Filter items that match
                var matchingItems = category.Items
                    .Where(item => item.Name.ToLowerInvariant().Contains(search))
                    .ToList();

                // Include category if name matches or has matching items
                if (categoryMatches || matchingItems.Count > 0)
                {
                    var filteredCategory = new SidebarCategory
                    {
                        Name = category.Name,
                        IconKey = category.IconKey,
                        IsExpanded = true, // Expand filtered categories
                        Items = categoryMatches
                            ? category.Items // Show all items if category name matches
                            : new ObservableCollection<SidebarItem>(matchingItems)
                    };
                    filtered.Add(filteredCategory);
                }
            }

            Categories = filtered;
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            AddHandler(Button.ClickEvent, OnSidebarButtonClick);

            // Wire up clear button
            var clearButton = e.NameScope.Find<Button>("PART_ClearButton");
            if (clearButton != null)
            {
                clearButton.Click += (s, args) =>
                {
                    SearchText = string.Empty;
                    args.Handled = true;
                };
            }
        }

        private void OnSidebarButtonClick(object? sender, RoutedEventArgs e)
        {
            if (e.Source is Button button && button.Tag is SidebarItem item)
            {
                var category = FindCategoryForItem(item);
                if (category != null)
                {
                    SelectItem(item, category);
                    UpdateSelectedButtonVisuals(button);
                }
            }
        }

        private SidebarCategory? FindCategoryForItem(SidebarItem item)
        {
            // Search in original categories, not filtered ones
            foreach (var category in _allCategories)
            {
                if (category.Items.Any(i => i.Name == item.Name && i.TabHeader == item.TabHeader))
                    return category;
            }
            return null;
        }

        private void UpdateSelectedButtonVisuals(Button selectedButton)
        {
            foreach (var button in this.GetVisualDescendants().OfType<Button>())
            {
                if (button.Classes.Contains("sidebar-item"))
                {
                    button.Classes.Remove("selected");
                }
            }
            selectedButton.Classes.Add("selected");
        }

        internal void SelectItem(SidebarItem item, SidebarCategory category)
        {
            SelectedItem = item;
            RaiseEvent(new SidebarItemSelectedEventArgs(ItemSelectedEvent, item, category));
        }

        private static ObservableCollection<SidebarCategory> CreateDefaultCategories()
        {
            return new ObservableCollection<SidebarCategory>
            {
                new SidebarCategory
                {
                    Name = "Home",
                    IconKey = "DaisyIconHome",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "Welcome", TabHeader = "Home" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Actions",
                    IconKey = "DaisyIconActions",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "Button", TabHeader = "Actions" },
                        new SidebarItem { Name = "Dropdown", TabHeader = "Actions" },
                        new SidebarItem { Name = "FAB / Speed Dial", TabHeader = "Actions", Badge = "new" },
                        new SidebarItem { Name = "Modal", TabHeader = "Actions" },
                        new SidebarItem { Name = "Swap", TabHeader = "Actions" },
                        new SidebarItem { Name = "Theme Controller", TabHeader = "Actions" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Data Display",
                    IconKey = "DaisyIconDataDisplay",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "Accordion", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Avatar", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Badge", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Carousel", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Chat Bubble", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Collapse", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Countdown", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Diff", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Hover Gallery", TabHeader = "Data Display", Badge = "new" },
                        new SidebarItem { Name = "Kbd", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Modifier Keys", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Stat", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Table", TabHeader = "Data Display" },
                        new SidebarItem { Name = "Timeline", TabHeader = "Data Display" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Navigation",
                    IconKey = "DaisyIconNavigation",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "Breadcrumbs", TabHeader = "Navigation" },
                        new SidebarItem { Name = "Menu", TabHeader = "Navigation" },
                        new SidebarItem { Name = "Navbar", TabHeader = "Navigation" },
                        new SidebarItem { Name = "Pagination", TabHeader = "Navigation" },
                        new SidebarItem { Name = "Steps", TabHeader = "Navigation" },
                        new SidebarItem { Name = "Tabs", TabHeader = "Navigation" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Feedback",
                    IconKey = "DaisyIconFeedback",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "Alert", TabHeader = "Feedback" },
                        new SidebarItem { Name = "Loading", TabHeader = "Feedback" },
                        new SidebarItem { Name = "Progress", TabHeader = "Feedback" },
                        new SidebarItem { Name = "Radial Progress", TabHeader = "Feedback" },
                        new SidebarItem { Name = "Skeleton", TabHeader = "Feedback" },
                        new SidebarItem { Name = "Toast", TabHeader = "Feedback" },
                        new SidebarItem { Name = "Tooltip", TabHeader = "Feedback" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Cards",
                    IconKey = "DaisyIconCard",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "Card", TabHeader = "Cards" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Data Input",
                    IconKey = "DaisyIconDataInput",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "Checkbox", TabHeader = "Form Controls" },
                        new SidebarItem { Name = "File Input", TabHeader = "Form Controls" },
                        new SidebarItem { Name = "Input", TabHeader = "Form Controls" },
                        new SidebarItem { Name = "Radio", TabHeader = "Form Controls" },
                        new SidebarItem { Name = "Range", TabHeader = "Form Controls" },
                        new SidebarItem { Name = "Rating", TabHeader = "Form Controls" },
                        new SidebarItem { Name = "Select", TabHeader = "Form Controls" },
                        new SidebarItem { Name = "TextArea", TabHeader = "Form Controls" },
                        new SidebarItem { Name = "Toggle", TabHeader = "Form Controls" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Divider",
                    IconKey = "DaisyIconDivider",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "Divider", TabHeader = "Divider" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Layout",
                    IconKey = "DaisyIconLayout",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "Drawer", TabHeader = "Layout" },
                        new SidebarItem { Name = "Hero", TabHeader = "Layout" },
                        new SidebarItem { Name = "Indicator", TabHeader = "Layout" },
                        new SidebarItem { Name = "Join", TabHeader = "Layout" },
                        new SidebarItem { Name = "Mask", TabHeader = "Layout" },
                        new SidebarItem { Name = "Mockup", TabHeader = "Layout" },
                        new SidebarItem { Name = "Stack", TabHeader = "Layout" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Theming",
                    IconKey = "DaisyIconTheme",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Name = "CSS Theme Converter", TabHeader = "Theming" },
                        new SidebarItem { Name = "Theme Radio", TabHeader = "Theming" }
                    }
                }
            };
        }
    }

    public class IconKeyConverter : IValueConverter
    {
        public static readonly IconKeyConverter Instance = new();

        private static readonly Dictionary<string, StreamGeometry> IconCache = new();

        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is not string iconKey || string.IsNullOrEmpty(iconKey))
                return null;

            if (IconCache.TryGetValue(iconKey, out var cached))
                return cached;

            if (Application.Current?.TryFindResource(iconKey, out var resource) == true && resource is StreamGeometry geometry)
            {
                IconCache[iconKey] = geometry;
                return geometry;
            }

            return null;
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotSupportedException();
        }
    }
}
