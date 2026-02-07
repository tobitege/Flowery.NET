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
        return
        [
            // Home stays at top
            new() {
                Name = "Sidebar_Home",
                IconKey = "DaisyIconHome",
                Items =
                [
                    new SidebarItem { Id = "welcome", Name = "Sidebar_Welcome", TabHeader = "Sidebar_Home" },
                    new GalleryThemeSelectorItem { Id = "theme", Name = "Sidebar_Theme", TabHeader = "Sidebar_Home" },
                    new GalleryLanguageSelectorItem { Id = "language", Name = "Sidebar_Language", TabHeader = "Sidebar_Home" },
                    new GallerySizeSelectorItem { Id = "size", Name = "Sidebar_Size", TabHeader = "Sidebar_Home" }
                ]
            },
            // Alphabetically sorted categories
            new() {
                Name = "Sidebar_Actions",
                IconKey = "DaisyIconActions",
                Items =
                [
                    new SidebarItem { Id = "button", Name = "Sidebar_Button", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "button-group", Name = "Sidebar_ButtonGroup", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "figma-comment", Name = "Sidebar_FigmaComment", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "copybutton", Name = "Sidebar_CopyButton", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "dropdown", Name = "Sidebar_Dropdown", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "dropdownmenu", Name = "Sidebar_DropdownMenu", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "fab", Name = "Sidebar_FAB", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "popover", Name = "Sidebar_Popover", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "modal", Name = "Sidebar_Modal", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "modal-radii", Name = "Sidebar_ModalRadii", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "swap", Name = "Sidebar_Swap", TabHeader = "Sidebar_Actions" }
                ]
            },
            new() {
                Name = "Sidebar_Cards",
                IconKey = "DaisyIconCard",
                Items =
                [
                    new SidebarItem { Id = "card", Name = "Sidebar_Card", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "card-stack", Name = "Sidebar_CardStack", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "expandable-cards", Name = "Showcase_ExpandableCards_Title", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "patterned-card", Name = "Sidebar_PatternedCard", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "glass-card", Name = "Sidebar_GlassCard", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "daisyglass-simulated", Name = "Sidebar_DaisyGlassSimulated", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "daisyglass-bitmap-capture", Name = "Sidebar_DaisyGlassBitmapCapture", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "daisyglass-skia-matrix", Name = "Sidebar_DaisyGlassSkiaMatrix", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "daisyglass-full-width", Name = "Sidebar_DaisyGlassFullWidth", TabHeader = "Sidebar_Cards" }
                ]
            },
            new() {
                Name = "Sidebar_DataDisplay",
                IconKey = "DaisyIconDataDisplay",
                Items =
                [
                    new SidebarItem { Id = "accordion", Name = "Sidebar_Accordion", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "animatednumber", Name = "Sidebar_AnimatedNumber", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "avatar", Name = "Sidebar_Avatar", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "badge", Name = "Sidebar_Badge", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "carousel", Name = "Sidebar_Carousel", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "chat-bubble", Name = "Sidebar_ChatBubble", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "collapse", Name = "Sidebar_Collapse", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "contributiongraph", Name = "Sidebar_ContributionGraph", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "clock", Name = "Sidebar_Clock", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "countdown", Name = "Sidebar_Countdown", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "diff", Name = "Sidebar_Diff", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "hover-gallery", Name = "Sidebar_HoverGallery", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "kbd", Name = "Sidebar_Kbd", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "list", Name = "Sidebar_List", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "numberflow", Name = "Sidebar_NumberFlow", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "stat", Name = "Sidebar_Stat", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "status", Name = "Sidebar_Status", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "table", Name = "Sidebar_Table", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "text-rotate", Name = "Sidebar_TextRotate", TabHeader = "Sidebar_DataDisplay" }
                ]
            },
            new() {
                Name = "Sidebar_DateDisplay",
                IconKey = "DaisyIconDateDisplay",
                Items =
                [
                    new SidebarItem { Id = "date-timeline", Name = "Sidebar_DateTimeline", TabHeader = "Sidebar_DateDisplay" },
                    new SidebarItem { Id = "timeline", Name = "Sidebar_Timeline", TabHeader = "Sidebar_DateDisplay" }
                ]
            },
            new() {
                Name = "Sidebar_DataInput",
                IconKey = "DaisyIconDataInput",
                Items =
                [
                    new SidebarItem { Id = "checkbox", Name = "Sidebar_Checkbox", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "file-input", Name = "Sidebar_FileInput", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "input", Name = "Sidebar_Input", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "mask-input", Name = "Sidebar_MaskInput", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "numericupdown", Name = "Sidebar_NumericUpDown", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "otpinput", Name = "Sidebar_OtpInput", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "passwordbox", Name = "Sidebar_PasswordBox", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "radio", Name = "Sidebar_Radio", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "range", Name = "Sidebar_Range", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "rating", Name = "Sidebar_Rating", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "select", Name = "Sidebar_Select", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "slide-to-confirm", Name = "Sidebar_SlideToConfirm", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "tagpicker", Name = "Sidebar_TagPicker", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "textarea", Name = "Sidebar_TextArea", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "toggle", Name = "Sidebar_Toggle", TabHeader = "Sidebar_DataInput" }
                ]
            },
            new() {
                Name = "Sidebar_Divider",
                IconKey = "DaisyIconDivider",
                Items =
                [
                    new SidebarItem { Id = "divider", Name = "Sidebar_DividerItem", TabHeader = "Sidebar_Divider" }
                ]
            },
            new() {
                Name = "Sidebar_Feedback",
                IconKey = "DaisyIconFeedback",
                Items =
                [
                    new SidebarItem { Id = "alert", Name = "Sidebar_Alert", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "loading", Name = "Sidebar_Loading", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "progress", Name = "Sidebar_Progress", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "radial-progress", Name = "Sidebar_RadialProgress", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "skeleton", Name = "Sidebar_Skeleton", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "toast", Name = "Sidebar_Toast", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "tooltip", Name = "Sidebar_Tooltip", TabHeader = "Sidebar_Feedback" }
                ]
            },
            new() {
                Name = "Sidebar_Layout",
                IconKey = "DaisyIconLayout",
                Items =
                [
                    new SidebarItem { Id = "drawer", Name = "Sidebar_Drawer", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "hero", Name = "Sidebar_Hero", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "indicator", Name = "Sidebar_Indicator", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "join", Name = "Sidebar_Join", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "mask", Name = "Sidebar_Mask", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "mockup", Name = "Sidebar_Mockup", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "stack", Name = "Sidebar_Stack", TabHeader = "Sidebar_Layout" }
                ]
            },
            new() {
                Name = "Sidebar_Navigation",
                IconKey = "DaisyIconNavigation",
                Items =
                [
                    new SidebarItem { Id = "breadcrumbs", Name = "Sidebar_Breadcrumbs", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "dock", Name = "Sidebar_Dock", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "menu", Name = "Sidebar_Menu", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "navbar", Name = "Sidebar_Navbar", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "pagination", Name = "Sidebar_Pagination", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "steps", Name = "Sidebar_Steps", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "tabs", Name = "Sidebar_Tabs", TabHeader = "Sidebar_Navigation" }
                ]
            },
            new() {
                Name = "Sidebar_Theming",
                IconKey = "DaisyIconTheme",
                Items =
                [
                    new SidebarItem { Id = "product-themes", Name = "Sidebar_ProductThemes", TabHeader = "Sidebar_ProductThemes" },
                    new SidebarItem { Id = "theme-controller", Name = "Sidebar_ThemeController", TabHeader = "Sidebar_Theming" },
                    new SidebarItem { Id = "theme-radio", Name = "Sidebar_ThemeRadio", TabHeader = "Sidebar_Theming" }
                ]
            },
            new() {
                Name = "Sidebar_Effects",
                IconKey = "DaisyIconEffects",
                Items =
                [
                    new SidebarItem { Id = "reveal", Name = "Sidebar_Reveal", TabHeader = "Sidebar_Effects" },
                    new SidebarItem { Id = "scramble", Name = "Sidebar_Scramble", TabHeader = "Sidebar_Effects" },
                    new SidebarItem { Id = "wave", Name = "Sidebar_Wave", TabHeader = "Sidebar_Effects" },
                    new SidebarItem { Id = "typewriter", Name = "Sidebar_Typewriter", TabHeader = "Sidebar_Effects" },
                    new SidebarItem { Id = "cursor-follow", Name = "Sidebar_CursorFollow", TabHeader = "Sidebar_Effects" },
                    new SidebarItem { Id = "showcase", Name = "Sidebar_Showcase", TabHeader = "Sidebar_Effects" }
                ]
            },
            // Custom Controls and Color Picker stay at bottom
            new() {
                Name = "Sidebar_CustomControls",
                IconKey = "DaisyIconSun",
                Items =
                [
                    new SidebarItem { Id = "scaling", Name = "Sidebar_ScalingItem", TabHeader = "Sidebar_Scaling" },
                    new SidebarItem { Id = "size-dropdown", Name = "Sidebar_SizeDropdown", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "modifier-keys", Name = "Sidebar_ModifierKeys", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "weather-card", Name = "Sidebar_WeatherCard", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "current-weather", Name = "Sidebar_CurrentWeather", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "weather-forecast", Name = "Sidebar_WeatherForecast", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "weather-metrics", Name = "Sidebar_WeatherMetrics", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "weather-conditions", Name = "Sidebar_WeatherConditions", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "service-integration", Name = "Sidebar_ServiceIntegration", TabHeader = "Sidebar_CustomControls" }
                ]
            },
            new() {
                Name = "Sidebar_ColorPicker",
                IconKey = "DaisyIconPalette",
                Items =
                [
                    new SidebarItem { Id = "colorwheel", Name = "Sidebar_ColorWheel", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "colorgrid", Name = "Sidebar_ColorGrid", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "colorslider", Name = "Sidebar_ColorSliders", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "coloreditor", Name = "Sidebar_ColorEditor", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "screenpicker", Name = "Sidebar_ScreenPicker", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "colorpickerdialog", Name = "Sidebar_ColorPickerDialog", TabHeader = "Sidebar_ColorPicker" }
                ]
            }
        ];
    }

    /// <summary>
    /// Creates the default languages for the Gallery showcase app.
    /// Uses the centralized language data from the library.
    /// </summary>
    public static ObservableCollection<SidebarLanguage> CreateLanguages()
    {
        return SidebarLanguage.CreateAll();
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
