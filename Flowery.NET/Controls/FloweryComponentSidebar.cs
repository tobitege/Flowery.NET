using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    public class SidebarCategory : INotifyPropertyChanged
    {
        private bool _isExpanded = true;
        private string _name = string.Empty;

        public SidebarCategory()
        {
            FloweryLocalization.CultureChanged += (_, _) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                }
            }
        }

        public string DisplayName => FloweryLocalization.GetString(Name);

        public string IconKey { get; set; } = string.Empty;
        public bool IsExpanded
        {
            get => _isExpanded;
            set { if (_isExpanded != value) { _isExpanded = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsExpanded))); } }
        }
        public ObservableCollection<SidebarItem> Items { get; set; } = new();
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class SidebarItem : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private bool _isFavorite;
        private bool _showFavoriteIcon;

        public SidebarItem()
        {
            FloweryLocalization.CultureChanged += (_, _) =>
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
        }

        public string Id { get; set; } = string.Empty;
        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name)));
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayName)));
                }
            }
        }

        public string DisplayName => FloweryLocalization.GetString(Name);

        public string TabHeader { get; set; } = string.Empty;
        public string? Badge { get; set; }

        /// <summary>
        /// Whether this item is marked as a favorite. When changed, updates the star icon state.
        /// </summary>
        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite != value)
                {
                    _isFavorite = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsFavorite)));
                }
            }
        }

        /// <summary>
        /// Whether to show the favorite (star) icon for this item.
        /// </summary>
        public bool ShowFavoriteIcon
        {
            get => _showFavoriteIcon;
            set
            {
                if (_showFavoriteIcon != value)
                {
                    _showFavoriteIcon = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowFavoriteIcon)));
                }
            }
        }

        /// <summary>
        /// Event raised when the favorite icon is clicked.
        /// </summary>
        public event EventHandler? FavoriteToggleRequested;

        /// <summary>
        /// Raises the FavoriteToggleRequested event.
        /// </summary>
        internal void RaiseFavoriteToggleRequested()
        {
            FavoriteToggleRequested?.Invoke(this, EventArgs.Empty);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    public class SidebarLanguageSelectorItem : SidebarItem
    {
    }

    public class SidebarThemeSelectorItem : SidebarItem
    {
    }

    public class SidebarSizeSelectorItem : SidebarItem
    {
    }

    public class SidebarScalingSelectorItem : SidebarItem
    {
    }

    public class SidebarLanguage
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public override string ToString() => DisplayName;

        /// <summary>
        /// Creates a list of all supported languages with their native display names.
        /// Uses the centralized language data from FloweryLocalization.
        /// </summary>
        public static ObservableCollection<SidebarLanguage> CreateAll()
        {
            var result = new ObservableCollection<SidebarLanguage>();
            foreach (var code in FloweryLocalization.SupportedLanguages)
            {
                var displayName = FloweryLocalization.LanguageDisplayNames.TryGetValue(code, out var name)
                    ? name
                    : code;
                result.Add(new SidebarLanguage { Code = code, DisplayName = displayName });
            }
            return result;
        }
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

    public class SidebarFavoriteToggledEventArgs : RoutedEventArgs
    {
        public SidebarItem Item { get; }

        public SidebarFavoriteToggledEventArgs(RoutedEvent routedEvent, SidebarItem item)
            : base(routedEvent)
        {
            Item = item;
        }
    }

    public class FloweryComponentSidebar : TemplatedControl
    {
        protected override Type StyleKeyOverride => typeof(FloweryComponentSidebar);

        private const string StateKey = "sidebar";

        private ObservableCollection<SidebarCategory> _allCategories = new();

        private ObservableCollection<SidebarLanguage> _availableLanguages = new();
        private SidebarLanguage? _selectedLanguage;
        private bool _updatingLanguage;
        private Button? _selectedItemButton;

        public static readonly DirectProperty<FloweryComponentSidebar, ObservableCollection<SidebarLanguage>> AvailableLanguagesProperty =
            AvaloniaProperty.RegisterDirect<FloweryComponentSidebar, ObservableCollection<SidebarLanguage>>(
                nameof(AvailableLanguages),
                o => o.AvailableLanguages,
                (o, v) => o.AvailableLanguages = v);

        public ObservableCollection<SidebarLanguage> AvailableLanguages
        {
            get => _availableLanguages;
            set
            {
                if (SetAndRaise(AvailableLanguagesProperty, ref _availableLanguages, value))
                {
                    // Update selected language to match current culture when languages are set
                    if (value != null && value.Count > 0)
                        UpdateSelectedLanguageFromCulture(FloweryLocalization.CurrentCulture);
                }
            }
        }

        public static readonly DirectProperty<FloweryComponentSidebar, SidebarLanguage?> SelectedLanguageProperty =
            AvaloniaProperty.RegisterDirect<FloweryComponentSidebar, SidebarLanguage?>(
                nameof(SelectedLanguage),
                o => o.SelectedLanguage,
                (o, v) => o.SelectedLanguage = v);

        public SidebarLanguage? SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (!SetAndRaise(SelectedLanguageProperty, ref _selectedLanguage, value))
                    return;

                if (_updatingLanguage)
                    return;

                if (value != null)
                {
                    _updatingLanguage = true;
                    try
                    {
                        FloweryLocalization.SetCulture(value.Code);
                    }
                    finally
                    {
                        _updatingLanguage = false;
                    }
                }
            }
        }

        public static readonly RoutedEvent<SidebarItemSelectedEventArgs> ItemSelectedEvent =
            RoutedEvent.Register<FloweryComponentSidebar, SidebarItemSelectedEventArgs>(
                nameof(ItemSelected), RoutingStrategies.Bubble);

        public event EventHandler<SidebarItemSelectedEventArgs>? ItemSelected
        {
            add => AddHandler(ItemSelectedEvent, value);
            remove => RemoveHandler(ItemSelectedEvent, value);
        }

        public static readonly RoutedEvent<SidebarFavoriteToggledEventArgs> FavoriteToggledEvent =
            RoutedEvent.Register<FloweryComponentSidebar, SidebarFavoriteToggledEventArgs>(
                nameof(FavoriteToggled), RoutingStrategies.Bubble);

        public event EventHandler<SidebarFavoriteToggledEventArgs>? FavoriteToggled
        {
            add => AddHandler(FavoriteToggledEvent, value);
            remove => RemoveHandler(FavoriteToggledEvent, value);
        }

        public static readonly StyledProperty<ObservableCollection<SidebarCategory>> CategoriesProperty =
            AvaloniaProperty.Register<FloweryComponentSidebar, ObservableCollection<SidebarCategory>>(
                nameof(Categories), new ObservableCollection<SidebarCategory>());

        public ObservableCollection<SidebarCategory> Categories
        {
            get => GetValue(CategoriesProperty);
            set => SetValue(CategoriesProperty, value);
        }

        public static readonly StyledProperty<SidebarItem?> SelectedItemProperty =
            AvaloniaProperty.Register<FloweryComponentSidebar, SidebarItem?>(nameof(SelectedItem));

        public SidebarItem? SelectedItem
        {
            get => GetValue(SelectedItemProperty);
            set => SetValue(SelectedItemProperty, value);
        }

        public static readonly StyledProperty<double> SidebarWidthProperty =
            AvaloniaProperty.Register<FloweryComponentSidebar, double>(nameof(SidebarWidth), 224);

        public double SidebarWidth
        {
            get => GetValue(SidebarWidthProperty);
            set => SetValue(SidebarWidthProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property for the sidebar's text and control sizing.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<FloweryComponentSidebar, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of sidebar elements. Affects font sizes, icon sizes, and control sizing.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<string> SearchTextProperty =
            AvaloniaProperty.Register<FloweryComponentSidebar, string>(nameof(SearchText), string.Empty);

        public string SearchText
        {
            get => GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        private bool _isScalingEnabled = Flowery.Services.FloweryScaleManager.IsEnabled;

        /// <summary>
        /// Defines the <see cref="IsScalingEnabled"/> property.
        /// </summary>
        public static readonly DirectProperty<FloweryComponentSidebar, bool> IsScalingEnabledProperty =
            AvaloniaProperty.RegisterDirect<FloweryComponentSidebar, bool>(
                nameof(IsScalingEnabled),
                o => o.IsScalingEnabled,
                (o, v) => o.IsScalingEnabled = v);

        /// <summary>
        /// Gets or sets whether automatic scaling is enabled. Bound to FloweryScaleManager.IsEnabled.
        /// </summary>
        public bool IsScalingEnabled
        {
            get => _isScalingEnabled;
            set
            {
                if (SetAndRaise(IsScalingEnabledProperty, ref _isScalingEnabled, value))
                {
                    Flowery.Services.FloweryScaleManager.IsEnabled = value;
                }
            }
        }

        static FloweryComponentSidebar()
        {
            SearchTextProperty.Changed.AddClassHandler<FloweryComponentSidebar>((s, e) => s.OnSearchTextChanged());
            CategoriesProperty.Changed.AddClassHandler<FloweryComponentSidebar>((s, e) => s.OnCategoriesChanged());
        }

        public FloweryComponentSidebar()
        {
            // Initialize with empty collections - caller should set Categories and AvailableLanguages
            _allCategories = new ObservableCollection<SidebarCategory>();
            FloweryLocalization.CultureChanged += OnLocalizationCultureChanged;
            Flowery.Services.FloweryScaleManager.IsEnabledChanged += OnScalingEnabledChanged;
        }

        private void OnScalingEnabledChanged(object? sender, bool isEnabled)
        {
            if (_isScalingEnabled != isEnabled)
            {
                SetAndRaise(IsScalingEnabledProperty, ref _isScalingEnabled, isEnabled);
            }
        }

        private void OnCategoriesChanged()
        {
            var newCategories = Categories;
            if (newCategories == null || newCategories.Count == 0)
                return;

            _allCategories = newCategories;
            LoadState();

            // Wire up property changed handlers for expand/collapse state persistence
            foreach (var cat in _allCategories)
                cat.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(SidebarCategory.IsExpanded))
                        SaveState(SelectedItem?.Id);
                };
        }

        private void OnLocalizationCultureChanged(object? sender, CultureInfo culture)
        {
            if (_updatingLanguage)
                return;

            UpdateSelectedLanguageFromCulture(culture);
        }

        private void UpdateSelectedLanguageFromCulture(CultureInfo culture)
        {
            var language = FindLanguageForCulture(culture) ??
                           AvailableLanguages.FirstOrDefault(l => string.Equals(l.Code, "en", StringComparison.OrdinalIgnoreCase)) ??
                           AvailableLanguages.FirstOrDefault();

            _updatingLanguage = true;
            try
            {
                SetAndRaise(SelectedLanguageProperty, ref _selectedLanguage, language);
            }
            finally
            {
                _updatingLanguage = false;
            }
        }

        private SidebarLanguage? FindLanguageForCulture(CultureInfo culture)
        {
            var exact = AvailableLanguages.FirstOrDefault(l =>
                string.Equals(l.Code, culture.Name, StringComparison.OrdinalIgnoreCase));
            if (exact != null)
                return exact;

            var twoLetter = culture.TwoLetterISOLanguageName;
            return AvailableLanguages.FirstOrDefault(l =>
                string.Equals(l.Code, twoLetter, StringComparison.OrdinalIgnoreCase));
        }

        private static ObservableCollection<SidebarLanguage> CreateDefaultLanguages()
        {
            return SidebarLanguage.CreateAll();
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
                var categoryMatches = category.DisplayName.ToLowerInvariant().Contains(search);

                // Filter items that match
                var matchingItems = category.Items
                    .Where(item => item.DisplayName.ToLowerInvariant().Contains(search))
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
            // Check if this is a click on the favorite button
            if (e.Source is Button favoriteButton && favoriteButton.Classes.Contains("sidebar-favorite-btn"))
            {
                if (favoriteButton.Tag is SidebarItem favoriteItem)
                {
                    // Toggle IsFavorite and raise events
                    favoriteItem.IsFavorite = !favoriteItem.IsFavorite;
                    favoriteItem.RaiseFavoriteToggleRequested();
                    RaiseEvent(new SidebarFavoriteToggledEventArgs(FavoriteToggledEvent, favoriteItem));
                    e.Handled = true;
                    return;
                }
            }

            // e.Source might be a child element (TextBlock, Grid, etc.) inside the Button
            // Walk up the visual tree to find the actual sidebar-item Button
            Button? button = null;

            if (e.Source is Button b && b.Classes.Contains("sidebar-item"))
            {
                button = b;
            }
            else if (e.Source is Visual visual)
            {
                // Walk up visual tree to find parent Button with sidebar-item class
                var parent = visual.GetVisualParent();
                while (parent != null)
                {
                    // Skip if we hit a favorite button - don't treat as item click
                    if (parent is Button parentButton && parentButton.Classes.Contains("sidebar-favorite-btn"))
                    {
                        return;
                    }
                    if (parent is Button itemButton && itemButton.Classes.Contains("sidebar-item"))
                    {
                        button = itemButton;
                        break;
                    }
                    parent = parent.GetVisualParent();
                }
            }

            if (button?.Tag is SidebarItem item)
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
            if (_selectedItemButton != null && _selectedItemButton.Classes.Contains("sidebar-item"))
                _selectedItemButton.Classes.Remove("selected");

            _selectedItemButton = selectedButton;
            if (!_selectedItemButton.Classes.Contains("selected"))
                _selectedItemButton.Classes.Add("selected");
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
                var lines = StateStorageProvider.Instance.LoadLines(StateKey);
                if (lines.Count == 0)
                    return;

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
                var lines = new List<string>();
                if (!string.IsNullOrEmpty(currentItemId))
                    lines.Add("last:" + currentItemId);
                foreach (var cat in _allCategories.Where(c => !c.IsExpanded))
                    lines.Add("collapsed:" + cat.Name);
                StateStorageProvider.Instance.SaveLines(StateKey, lines);
            }
            catch { }
        }

        private static ObservableCollection<SidebarCategory> CreateDefaultCategories()
        {
            return new ObservableCollection<SidebarCategory>
            {
                // Home stays at top
                new SidebarCategory
                {
                    Name = "Home",
                    IconKey = "DaisyIconHome",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "welcome", Name = "Welcome", TabHeader = "Home" },
                        new SidebarLanguageSelectorItem { Id = "language", Name = "Language", TabHeader = "Home" }
                    }
                },
                // Alphabetically sorted categories
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
                    Name = "Cards",
                    IconKey = "DaisyIconCard",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "card", Name = "Card", TabHeader = "Cards" }
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
                        new SidebarItem { Id = "numberflow", Name = "Number Flow", TabHeader = "Data Display" },
                        new SidebarItem { Id = "stat", Name = "Stat", TabHeader = "Data Display" },
                        new SidebarItem { Id = "status", Name = "Status", TabHeader = "Data Display" },
                        new SidebarItem { Id = "table", Name = "Table", TabHeader = "Data Display" },
                        new SidebarItem { Id = "text-rotate", Name = "Text Rotate", TabHeader = "Data Display" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Date Display",
                    IconKey = "DaisyIconDateDisplay",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "date-timeline", Name = "Date Timeline", TabHeader = "Date Display" },
                        new SidebarItem { Id = "timeline", Name = "Timeline", TabHeader = "Date Display" }
                    }
                },
                new SidebarCategory
                {
                    Name = "Data Input",
                    IconKey = "DaisyIconDataInput",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "checkbox", Name = "Checkbox", TabHeader = "Data Input" },
                        new SidebarItem { Id = "file-input", Name = "File Input", TabHeader = "Data Input" },
                        new SidebarItem { Id = "input", Name = "Input", TabHeader = "Data Input" },
                        new SidebarItem { Id = "mask-input", Name = "Mask Input", TabHeader = "Data Input" },
                        new SidebarItem { Id = "numericupdown", Name = "NumericUpDown", TabHeader = "Data Input" },
                        new SidebarItem { Id = "radio", Name = "Radio", TabHeader = "Data Input" },
                        new SidebarItem { Id = "range", Name = "Range", TabHeader = "Data Input" },
                        new SidebarItem { Id = "rating", Name = "Rating", TabHeader = "Data Input" },
                        new SidebarItem { Id = "select", Name = "Select", TabHeader = "Data Input" },
                        new SidebarItem { Id = "textarea", Name = "TextArea", TabHeader = "Data Input" },
                        new SidebarItem { Id = "toggle", Name = "Toggle", TabHeader = "Data Input" }
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
                    Name = "Effects",
                    IconKey = "DaisyIconEffects",
                    Items = new ObservableCollection<SidebarItem>
                    {
                        new SidebarItem { Id = "reveal", Name = "Reveal", TabHeader = "Effects" },
                        new SidebarItem { Id = "scramble", Name = "Scramble Hover", TabHeader = "Effects" },
                        new SidebarItem { Id = "wave", Name = "Wave Text", TabHeader = "Effects" },
                        new SidebarItem { Id = "cursor-follow", Name = "Cursor Follow", TabHeader = "Effects" },
                        new SidebarItem { Id = "showcase", Name = "Showcase", TabHeader = "Effects" }
                    }
                },
                // Custom Controls and Color Picker stay at bottom
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
