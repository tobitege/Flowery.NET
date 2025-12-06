using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Flowery.Controls
{
    public class SidebarCategory : INotifyPropertyChanged
    {
        private bool _isExpanded = true;
        public string Name { get; set; } = string.Empty;
        public string IconKey { get; set; } = string.Empty;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); } }
        }
        public ObservableCollection<SidebarItem> Items { get; set; } = new();
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class SidebarItem
    {
        public string Id { get; set; } = string.Empty;
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

        private static readonly string StateFile = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "FloweryGallery", "sidebar.state");

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
            LoadState();
            foreach (var cat in _allCategories)
                cat.PropertyChanged += (s, e) => { if (e.PropertyName == nameof(SidebarCategory.IsExpanded)) SaveState(SelectedItem?.Name); };
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
            SaveState(item.Id);
            RaiseEvent(new SidebarItemSelectedEventArgs(ItemSelectedEvent, item, category));
        }

        public (string? lastItemId, SidebarCategory? category) GetLastViewedItem()
        {
            if (SelectedItem != null)
            {
                var cat = FindCategoryForItem(SelectedItem);
                return (SelectedItem.Id, cat);
            }
            return (null, null);
        }

        private void LoadState()
        {
            try
            {
                if (!File.Exists(StateFile)) return;
                var lines = File.ReadAllLines(StateFile);
                string? lastItemId = null;
                var collapsed = new HashSet<string>();
                foreach (var line in lines)
                {
                    if (line.StartsWith("last:"))
                        lastItemId = line.Substring(5);
                    else if (line.StartsWith("collapsed:"))
                        collapsed.Add(line.Substring(10));
                }
                foreach (var cat in _allCategories)
                    cat.IsExpanded = !collapsed.Contains(cat.Name);
                if (lastItemId != null)
                {
                    foreach (var cat in _allCategories)
                    {
                        var item = cat.Items.FirstOrDefault(i => i.Id == lastItemId);
                        if (item != null)
                        {
                            SelectedItem = item;
                            break;
                        }
                    }
                }
            }
            catch { }
        }

        private void SaveState(string? currentItemId)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(StateFile)!);
                var lines = new List<string>();
                if (!string.IsNullOrEmpty(currentItemId))
                    lines.Add("last:" + currentItemId);
                foreach (var cat in _allCategories.Where(c => !c.IsExpanded))
                    lines.Add("collapsed:" + cat.Name);
                File.WriteAllLines(StateFile, lines);
            }
            catch { }
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
                        new SidebarItem { Id = "welcome", Name = "Welcome", TabHeader = "Home" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Actions",
                    IconKey = "DaisyIconActions",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "button", Name = "Button", TabHeader = "Actions" },
                        new SidebarItem { Id = "dropdown", Name = "Dropdown", TabHeader = "Actions" },
                        new SidebarItem { Id = "fab", Name = "FAB / Speed Dial", TabHeader = "Actions" },
                        new SidebarItem { Id = "modal", Name = "Modal", TabHeader = "Actions" },
                        new SidebarItem { Id = "modal-radii", Name = "Modal Corner Radii", TabHeader = "Actions" },
                        new SidebarItem { Id = "swap", Name = "Swap", TabHeader = "Actions" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Data Display",
                    IconKey = "DaisyIconDataDisplay",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "accordion", Name = "Accordion", TabHeader = "Data Display" },
                        new SidebarItem { Id = "avatar", Name = "Avatar", TabHeader = "Data Display" },
                        new SidebarItem { Id = "badge", Name = "Badge", TabHeader = "Data Display" },
                        new SidebarItem { Id = "carousel", Name = "Carousel", TabHeader = "Data Display" },
                        new SidebarItem { Id = "chat-bubble", Name = "Chat Bubble", TabHeader = "Data Display" },
                        new SidebarItem { Id = "collapse", Name = "Collapse", TabHeader = "Data Display" },
                        new SidebarItem { Id = "countdown", Name = "Countdown", TabHeader = "Data Display" },
                        new SidebarItem { Id = "diff", Name = "Diff", TabHeader = "Data Display" },
                        new SidebarItem { Id = "hover-gallery", Name = "Hover Gallery", TabHeader = "Data Display" },
                        new SidebarItem { Id = "kbd", Name = "Kbd", TabHeader = "Data Display" },
                        new SidebarItem { Id = "list", Name = "List", TabHeader = "Data Display" },
                        new SidebarItem { Id = "stat", Name = "Stat", TabHeader = "Data Display" },
                        new SidebarItem { Id = "status", Name = "Status", TabHeader = "Data Display" },
                        new SidebarItem { Id = "table", Name = "Table", TabHeader = "Data Display" },
                        new SidebarItem { Id = "text-rotate", Name = "Text Rotate", TabHeader = "Data Display" },
                        new SidebarItem { Id = "timeline", Name = "Timeline", TabHeader = "Data Display" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Navigation",
                    IconKey = "DaisyIconNavigation",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "breadcrumbs", Name = "Breadcrumbs", TabHeader = "Navigation" },
                        new SidebarItem { Id = "dock", Name = "Dock", TabHeader = "Navigation" },
                        new SidebarItem { Id = "menu", Name = "Menu", TabHeader = "Navigation" },
                        new SidebarItem { Id = "navbar", Name = "Navbar", TabHeader = "Navigation" },
                        new SidebarItem { Id = "pagination", Name = "Pagination", TabHeader = "Navigation" },
                        new SidebarItem { Id = "steps", Name = "Steps", TabHeader = "Navigation" },
                        new SidebarItem { Id = "tabs", Name = "Tabs", TabHeader = "Navigation" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Feedback",
                    IconKey = "DaisyIconFeedback",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "alert", Name = "Alert", TabHeader = "Feedback" },
                        new SidebarItem { Id = "loading", Name = "Loading", TabHeader = "Feedback" },
                        new SidebarItem { Id = "progress", Name = "Progress", TabHeader = "Feedback" },
                        new SidebarItem { Id = "radial-progress", Name = "Radial Progress", TabHeader = "Feedback" },
                        new SidebarItem { Id = "skeleton", Name = "Skeleton", TabHeader = "Feedback" },
                        new SidebarItem { Id = "toast", Name = "Toast", TabHeader = "Feedback" },
                        new SidebarItem { Id = "tooltip", Name = "Tooltip", TabHeader = "Feedback" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Cards",
                    IconKey = "DaisyIconCard",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "card", Name = "Card", TabHeader = "Cards" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Data Input",
                    IconKey = "DaisyIconDataInput",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "checkbox", Name = "Checkbox", TabHeader = "Form Controls" },
                        new SidebarItem { Id = "file-input", Name = "File Input", TabHeader = "Form Controls" },
                        new SidebarItem { Id = "input", Name = "Input", TabHeader = "Form Controls" },
                        new SidebarItem { Id = "radio", Name = "Radio", TabHeader = "Form Controls" },
                        new SidebarItem { Id = "range", Name = "Range", TabHeader = "Form Controls" },
                        new SidebarItem { Id = "rating", Name = "Rating", TabHeader = "Form Controls" },
                        new SidebarItem { Id = "select", Name = "Select", TabHeader = "Form Controls" },
                        new SidebarItem { Id = "textarea", Name = "TextArea", TabHeader = "Form Controls" },
                        new SidebarItem { Id = "toggle", Name = "Toggle", TabHeader = "Form Controls" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Divider",
                    IconKey = "DaisyIconDivider",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "divider", Name = "Divider", TabHeader = "Divider" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Layout",
                    IconKey = "DaisyIconLayout",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "drawer", Name = "Drawer", TabHeader = "Layout" },
                        new SidebarItem { Id = "hero", Name = "Hero", TabHeader = "Layout" },
                        new SidebarItem { Id = "indicator", Name = "Indicator", TabHeader = "Layout" },
                        new SidebarItem { Id = "join", Name = "Join", TabHeader = "Layout" },
                        new SidebarItem { Id = "mask", Name = "Mask", TabHeader = "Layout" },
                        new SidebarItem { Id = "mockup", Name = "Mockup", TabHeader = "Layout" },
                        new SidebarItem { Id = "stack", Name = "Stack", TabHeader = "Layout" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Theming",
                    IconKey = "DaisyIconTheme",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "css-theme-converter", Name = "CSS Theme Converter", TabHeader = "Theming" },
                        new SidebarItem { Id = "theme-controller", Name = "Theme Controller", TabHeader = "Theming" },
                        new SidebarItem { Id = "theme-radio", Name = "Theme Radio", TabHeader = "Theming" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Custom Controls",
                    IconKey = "DaisyIconSun",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "modifier-keys", Name = "Modifier Keys", TabHeader = "Custom Controls" },
                        new SidebarItem { Id = "weather-card", Name = "Weather Card", TabHeader = "Custom Controls" },
                        new SidebarItem { Id = "current-weather", Name = "Current Weather", TabHeader = "Custom Controls" },
                        new SidebarItem { Id = "weather-forecast", Name = "Weather Forecast", TabHeader = "Custom Controls" },
                        new SidebarItem { Id = "weather-metrics", Name = "Weather Metrics", TabHeader = "Custom Controls" },
                        new SidebarItem { Id = "weather-conditions", Name = "Weather Conditions", TabHeader = "Custom Controls" },
                        new SidebarItem { Id = "service-integration", Name = "Service Integration", TabHeader = "Custom Controls" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Color Picker",
                    IconKey = "DaisyIconPalette",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "colorwheel", Name = "Color Wheel", TabHeader = "Color Picker" },
                        new SidebarItem { Id = "colorgrid", Name = "Color Grid", TabHeader = "Color Picker" },
                        new SidebarItem { Id = "colorslider", Name = "Color Sliders", TabHeader = "Color Picker" },
                        new SidebarItem { Id = "coloreditor", Name = "Color Editor", TabHeader = "Color Picker" },
                        new SidebarItem { Id = "screenpicker", Name = "Screen Picker", TabHeader = "Color Picker" },
                        new SidebarItem { Id = "colorpickerdialog", Name = "Color Picker Dialog", TabHeader = "Color Picker" }
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
