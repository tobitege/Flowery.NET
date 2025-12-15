using System.Collections.ObjectModel;
using Flowery.Controls;

namespace Flowery.NET.Gallery;

/// <summary>
/// Provides Gallery-specific sidebar categories and languages.
/// This data is specific to the Flowery.NET Gallery showcase app.
/// </summary>
public static class GallerySidebarData
{
    /// <summary>
    /// Creates the default categories for the Gallery showcase sidebar.
    /// </summary>
    public static ObservableCollection<SidebarCategory> CreateCategories()
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
                    new GalleryThemeSelectorItem { Id = "theme", Name = "Theme", TabHeader = "Home" },
                    new GalleryLanguageSelectorItem { Id = "language", Name = "Language", TabHeader = "Home" },
                    new GallerySizeSelectorItem { Id = "size", Name = "Size", TabHeader = "Home" }
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

    /// <summary>
    /// Creates the default languages for the Gallery showcase app.
    /// </summary>
    public static ObservableCollection<SidebarLanguage> CreateLanguages()
    {
        return new ObservableCollection<SidebarLanguage>
        {
            new SidebarLanguage { Code = "en", DisplayName = "English" },
            new SidebarLanguage { Code = "de", DisplayName = "Deutsch" },
            new SidebarLanguage { Code = "es", DisplayName = "Español" },
            new SidebarLanguage { Code = "fr", DisplayName = "Français" },
            new SidebarLanguage { Code = "it", DisplayName = "Italiano" },
            new SidebarLanguage { Code = "ja", DisplayName = "日本語" },
            new SidebarLanguage { Code = "ko", DisplayName = "한국어" },
            new SidebarLanguage { Code = "ar", DisplayName = "العربية" },
            new SidebarLanguage { Code = "tr", DisplayName = "Türkçe" },
            new SidebarLanguage { Code = "uk", DisplayName = "Українська" },
            new SidebarLanguage { Code = "zh-CN", DisplayName = "简体中文" },
        };
    }
}

/// <summary>
/// Gallery-specific sidebar item for theme selection.
/// This item type triggers a special template in the sidebar that shows a theme dropdown.
/// Extends the library's SidebarThemeSelectorItem so the existing template works.
/// </summary>
public class GalleryThemeSelectorItem : SidebarThemeSelectorItem
{
}

/// <summary>
/// Gallery-specific sidebar item for language selection.
/// This item type triggers a special template in the sidebar that shows a language dropdown.
/// Extends the library's SidebarLanguageSelectorItem so the existing template works.
/// </summary>
public class GalleryLanguageSelectorItem : SidebarLanguageSelectorItem
{
}

/// <summary>
/// Gallery-specific sidebar item for global size selection.
/// This item type triggers a special template in the sidebar that shows a size dropdown.
/// Extends the library's SidebarSizeSelectorItem so the existing template works.
/// </summary>
public class GallerySizeSelectorItem : SidebarSizeSelectorItem
{
}
