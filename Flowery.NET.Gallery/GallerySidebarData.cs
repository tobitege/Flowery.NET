using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Flowery.Controls;

namespace Flowery.NET.Gallery;

/// <summary>
/// Provides Gallery-specific sidebar categories and languages.
/// This data is specific to the Flowery.NET Gallery showcase app.
/// </summary>
public static class GallerySidebarData
{
    private static readonly Lazy<Dictionary<string, SidebarItem>> _itemsById = new(() =>
        CreateCategories()
            .SelectMany(category => category.Items)
            .GroupBy(item => item.Id, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Finds the sidebar item with the given Id (e.g. a demo page's SectionId), or null if there is none.
    /// </summary>
    public static SidebarItem? FindItem(string? id)
    {
        return !string.IsNullOrEmpty(id) && _itemsById.Value.TryGetValue(id, out var item) ? item : null;
    }

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
                    new GallerySizeSelectorItem { Id = "size", Name = "Sidebar_Size", TabHeader = "Sidebar_Home" },
                    new GalleryNeumorphicSelectorItem { Id = "neumorphic", Name = "Sidebar_Neumorphic", TabHeader = "Sidebar_Home" },
                    new GalleryGlassModeItem { Id = "glass-mode", Name = "Sidebar_GlassMode", TabHeader = "Sidebar_Home" },
                    new GalleryBaseColorItem { Id = "base-color", Name = "Sidebar_BaseColor", TabHeader = "Sidebar_Home" },
                    new GalleryBackdropItem { Id = "backdrop", Name = "Sidebar_Backdrop", TabHeader = "Sidebar_Home" }
                ]
            },
            // Alphabetically sorted categories
            new() {
                Name = "Sidebar_Actions",
                IconKey = "DaisyIconActions",
                Items =
                [
                    new SidebarItem { Id = "button", Name = "Sidebar_Button", ControlName = "DaisyButton", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "button-group", Name = "Sidebar_ButtonGroup", ControlName = "DaisyButtonGroup", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "figma-comment", Name = "Sidebar_FigmaComment", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "copybutton", Name = "Sidebar_CopyButton", ControlName = "DaisyCopyButton", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "dropdown", Name = "Sidebar_Dropdown", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "dropdownmenu", Name = "Sidebar_DropdownMenu", ControlName = "DaisyDropdown", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "fab", Name = "Sidebar_FAB", ControlName = "DaisyFab", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "popover", Name = "Sidebar_Popover", ControlName = "DaisyPopover", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "modal", Name = "Sidebar_Modal", ControlName = "DaisyModal", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "modal-radii", Name = "Sidebar_ModalRadii", TabHeader = "Sidebar_Actions" },
                    new SidebarItem { Id = "swap", Name = "Sidebar_Swap", ControlName = "DaisySwap", TabHeader = "Sidebar_Actions" }
                ]
            },
            new() {
                Name = "Sidebar_Cards",
                IconKey = "DaisyIconCard",
                Items =
                [
                    new SidebarItem { Id = "card", Name = "Sidebar_Card", ControlName = "DaisyCard", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "card-stack", Name = "Sidebar_CardStack", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "expandable-cards", Name = "Showcase_ExpandableCards_Title", ControlName = "DaisyExpandableCard", TabHeader = "Sidebar_Cards" },
                    new SidebarItem { Id = "patterned-card", Name = "Sidebar_PatternedCard", ControlName = "DaisyPatternedCard", TabHeader = "Sidebar_Cards" }
                ]
            },
            new() {
                Name = "Sidebar_DataDisplay",
                IconKey = "DaisyIconDataDisplay",
                Items =
                [
                    new SidebarItem { Id = "accordion", Name = "Sidebar_Accordion", ControlName = "DaisyAccordion", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "animatednumber", Name = "Sidebar_AnimatedNumber", ControlName = "DaisyAnimatedNumber", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "avatar", Name = "Sidebar_Avatar", ControlName = "DaisyAvatar", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "badge", Name = "Sidebar_Badge", ControlName = "DaisyBadge", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "carousel", Name = "Sidebar_Carousel", ControlName = "DaisyCarousel", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "chat-bubble", Name = "Sidebar_ChatBubble", ControlName = "DaisyChatBubble", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "collapse", Name = "Sidebar_Collapse", ControlName = "DaisyCollapse", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "contributiongraph", Name = "Sidebar_ContributionGraph", ControlName = "DaisyContributionGraph", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "clock", Name = "Sidebar_Clock", ControlName = "DaisyClock", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "countdown", Name = "Sidebar_Countdown", ControlName = "DaisyCountdown", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "diff", Name = "Sidebar_Diff", ControlName = "DaisyDiff", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "hover-gallery", Name = "Sidebar_HoverGallery", ControlName = "DaisyHoverGallery", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "kbd", Name = "Sidebar_Kbd", ControlName = "DaisyKbd", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "list", Name = "Sidebar_List", ControlName = "DaisyList", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "numberflow", Name = "Sidebar_NumberFlow", ControlName = "DaisyNumberFlow", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "stat", Name = "Sidebar_Stat", ControlName = "DaisyStat", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "status", Name = "Sidebar_Status", ControlName = "DaisyStatusIndicator", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "table", Name = "Sidebar_Table", ControlName = "DaisyTable", TabHeader = "Sidebar_DataDisplay" },
                    new SidebarItem { Id = "text-rotate", Name = "Sidebar_TextRotate", ControlName = "DaisyTextRotate", TabHeader = "Sidebar_DataDisplay" }
                ]
            },
            new() {
                Name = "Sidebar_DateDisplay",
                IconKey = "DaisyIconDateDisplay",
                Items =
                [
                    new SidebarItem { Id = "date-timeline", Name = "Sidebar_DateTimeline", ControlName = "DaisyDateTimeline", TabHeader = "Sidebar_DateDisplay" },
                    new SidebarItem { Id = "timeline", Name = "Sidebar_Timeline", ControlName = "DaisyTimeline", TabHeader = "Sidebar_DateDisplay" }
                ]
            },
            new() {
                Name = "Sidebar_DataInput",
                IconKey = "DaisyIconDataInput",
                Items =
                [
                    new SidebarItem { Id = "checkbox", Name = "Sidebar_Checkbox", ControlName = "DaisyCheckBox", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "file-input", Name = "Sidebar_FileInput", ControlName = "DaisyFileInput", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "input", Name = "Sidebar_Input", ControlName = "DaisyInput", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "calendar-date-picker", Name = "Sidebar_CalendarDatePicker", ControlName = "DaisyCalendarDatePicker", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "mask-input", Name = "Sidebar_MaskInput", ControlName = "DaisyMaskInput", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "numericupdown", Name = "Sidebar_NumericUpDown", ControlName = "DaisyNumericUpDown", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "otpinput", Name = "Sidebar_OtpInput", ControlName = "DaisyOtpInput", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "passwordbox", Name = "Sidebar_PasswordBox", ControlName = "DaisyPasswordBox", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "radio", Name = "Sidebar_Radio", ControlName = "DaisyRadio", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "range", Name = "Sidebar_Range", ControlName = "DaisyRange", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "rating", Name = "Sidebar_Rating", ControlName = "DaisyRating", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "select", Name = "Sidebar_Select", ControlName = "DaisySelect", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "slide-to-confirm", Name = "Sidebar_SlideToConfirm", ControlName = "DaisySlideToConfirm", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "tagpicker", Name = "Sidebar_TagPicker", ControlName = "DaisyTagPicker", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "textarea", Name = "Sidebar_TextArea", ControlName = "DaisyTextArea", TabHeader = "Sidebar_DataInput" },
                    new SidebarItem { Id = "toggle", Name = "Sidebar_Toggle", ControlName = "DaisyToggle", TabHeader = "Sidebar_DataInput" }
                ]
            },
            new() {
                Name = "Sidebar_Divider",
                IconKey = "DaisyIconDivider",
                Items =
                [
                    new SidebarItem { Id = "divider", Name = "Sidebar_DividerItem", ControlName = "DaisyDivider", TabHeader = "Sidebar_Divider" }
                ]
            },
            new() {
                Name = "Sidebar_Feedback",
                IconKey = "DaisyIconFeedback",
                Items =
                [
                    new SidebarItem { Id = "alert", Name = "Sidebar_Alert", ControlName = "DaisyAlert", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "loading", Name = "Sidebar_Loading", ControlName = "DaisyLoading", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "progress", Name = "Sidebar_Progress", ControlName = "DaisyProgress", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "radial-progress", Name = "Sidebar_RadialProgress", ControlName = "DaisyRadialProgress", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "skeleton", Name = "Sidebar_Skeleton", ControlName = "DaisySkeleton", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "toast", Name = "Sidebar_Toast", ControlName = "DaisyToast", TabHeader = "Sidebar_Feedback" },
                    new SidebarItem { Id = "tooltip", Name = "Sidebar_Tooltip", ControlName = "DaisyTooltip", TabHeader = "Sidebar_Feedback" }
                ]
            },
            new() {
                Name = "Sidebar_Glass",
                IconKey = "DaisyIconNestedSquares",
                Items =
                [
                    new SidebarItem { Id = "glass-button", Name = "Sidebar_GlassButton", ControlName = "DaisyButton", TabHeader = "Sidebar_Glass" },
                    new SidebarItem { Id = "glass-designer", Name = "Sidebar_GlassDesigner", ControlName = "DaisyGlassDesigner", TabHeader = "Sidebar_Glass" },
                    new SidebarItem { Id = "glass-controls", Name = "Sidebar_GlassControls", SearchTerms = "DaisyGlassSurface DaisyNavbar DaisyInput DaisyBadge DaisyModal", TabHeader = "Sidebar_Glass" },
                    new SidebarItem { Id = "glass-interface", Name = "Sidebar_GlassInterface", SearchTerms = "DaisyGlassSurface DaisyButtonGroup DaisyRange DaisyToggle DaisyMenu Glassy", TabHeader = "Sidebar_Glass" },
                    new SidebarItem { Id = "glass-card", Name = "Sidebar_GlassCard", ControlName = "DaisyGlass", TabHeader = "Sidebar_Glass" },
                    new SidebarItem { Id = "daisyglass-liquid", Name = "Sidebar_DaisyGlassLiquid", ControlName = "DaisyGlass", TabHeader = "Sidebar_Glass" },
                    new SidebarItem { Id = "daisyglass-bitmap-capture", Name = "Sidebar_DaisyGlassBitmapCapture", ControlName = "DaisyGlass", TabHeader = "Sidebar_Glass" },
                    new SidebarItem { Id = "daisyglass-full-width", Name = "Sidebar_DaisyGlassFullWidth", ControlName = "DaisyGlass", TabHeader = "Sidebar_Glass" },
                    new SidebarItem { Id = "daisyglass-skia-matrix", Name = "Sidebar_DaisyGlassSkiaMatrix", ControlName = "DaisyGlass", TabHeader = "Sidebar_Glass" }
                ]
            },
            new() {
                Name = "Sidebar_Layout",
                IconKey = "DaisyIconLayout",
                Items =
                [
                    new SidebarItem { Id = "drawer", Name = "Sidebar_Drawer", ControlName = "DaisyDrawer", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "hero", Name = "Sidebar_Hero", ControlName = "DaisyHero", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "indicator", Name = "Sidebar_Indicator", ControlName = "DaisyIndicator", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "join", Name = "Sidebar_Join", ControlName = "DaisyJoin", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "mask", Name = "Sidebar_Mask", ControlName = "DaisyMask", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "mockup", Name = "Sidebar_Mockup", ControlName = "DaisyMockup", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "resizablecolumns", Name = "Sidebar_ResizableColumns", ControlName = "DaisyResizableColumnsHost", TabHeader = "Sidebar_Layout" },
                    new SidebarItem { Id = "stack", Name = "Sidebar_Stack", ControlName = "DaisyStack", TabHeader = "Sidebar_Layout" }
                ]
            },
            new() {
                Name = "Sidebar_Navigation",
                IconKey = "DaisyIconNavigation",
                Items =
                [
                    new SidebarItem { Id = "breadcrumbs", Name = "Sidebar_Breadcrumbs", ControlName = "DaisyBreadcrumbs", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "breadcrumbbar", Name = "Sidebar_BreadcrumbBar", ControlName = "DaisyBreadcrumbBar", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "dock", Name = "Sidebar_Dock", ControlName = "DaisyDock", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "menu", Name = "Sidebar_Menu", ControlName = "DaisyMenu", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "navbar", Name = "Sidebar_Navbar", ControlName = "DaisyNavbar", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "pagination", Name = "Sidebar_Pagination", ControlName = "DaisyPagination", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "steps", Name = "Sidebar_Steps", ControlName = "DaisySteps", TabHeader = "Sidebar_Navigation" },
                    new SidebarItem { Id = "tabs", Name = "Sidebar_Tabs", ControlName = "DaisyTabs", TabHeader = "Sidebar_Navigation" }
                ]
            },
            new() {
                Name = "Sidebar_Tour",
                IconKey = "DaisyIconNavigation",
                Items =
                [
                    new SidebarItem { Id = "tour", Name = "Sidebar_Tour", ControlName = "DaisyTour", TabHeader = "Sidebar_Tour" }
                ]
            },
            new() {
                Name = "Sidebar_Theming",
                IconKey = "DaisyIconTheme",
                Items =
                [
                    new SidebarItem { Id = "product-themes", Name = "Sidebar_ProductThemes", ControlName = "DaisyProductThemeDropdown", TabHeader = "Sidebar_ProductThemes" },
                    new SidebarItem { Id = "theme-controller", Name = "Sidebar_ThemeController", ControlName = "DaisyThemeController", TabHeader = "Sidebar_Theming" },
                    new SidebarItem { Id = "theme-radio", Name = "Sidebar_ThemeRadio", ControlName = "DaisyThemeRadio", TabHeader = "Sidebar_Theming" }
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
                    new SidebarItem { Id = "neumorphic-effect", Name = "Sidebar_NeumorphicEffect", ControlName = "DaisyNeumorphic", TabHeader = "Sidebar_Effects" },
                    new SidebarItem { Id = "showcase", Name = "Sidebar_Showcase", TabHeader = "Sidebar_Effects" }
                ]
            },
            // Custom Controls and Color Picker stay at bottom
            new() {
                Name = "Sidebar_CustomControls",
                IconKey = "DaisyIconSun",
                Items =
                [
                    new SidebarItem { Id = "flow-kanban", Name = "Sidebar_FlowKanban", ControlName = "FlowKanban", TabHeader = "Sidebar_FlowKanban" },
                    new SidebarItem { Id = "scheduler", Name = "Sidebar_Scheduler", ControlName = "DaisyScheduler", TabHeader = "Sidebar_Scheduler" },
                    new SidebarItem { Id = "scaling", Name = "Sidebar_ScalingItem", TabHeader = "Sidebar_Scaling" },
                    new SidebarItem { Id = "size-dropdown", Name = "Sidebar_SizeDropdown", ControlName = "DaisySizeDropdown", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "modifier-keys", Name = "Sidebar_ModifierKeys", ControlName = "DaisyModifierKeys", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "weather-card", Name = "Sidebar_WeatherCard", ControlName = "DaisyWeatherCard", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "current-weather", Name = "Sidebar_CurrentWeather", ControlName = "DaisyWeatherCurrent", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "weather-forecast", Name = "Sidebar_WeatherForecast", ControlName = "DaisyWeatherForecast", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "weather-metrics", Name = "Sidebar_WeatherMetrics", ControlName = "DaisyWeatherMetrics", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "weather-conditions", Name = "Sidebar_WeatherConditions", TabHeader = "Sidebar_CustomControls" },
                    new SidebarItem { Id = "service-integration", Name = "Sidebar_ServiceIntegration", TabHeader = "Sidebar_CustomControls" }
                ]
            },
            new() {
                Name = "Sidebar_ColorPicker",
                IconKey = "DaisyIconPalette",
                Items =
                [
                    new SidebarItem { Id = "colorwheel", Name = "Sidebar_ColorWheel", ControlName = "DaisyColorWheel", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "colorgrid", Name = "Sidebar_ColorGrid", ControlName = "DaisyColorGrid", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "colorslider", Name = "Sidebar_ColorSliders", ControlName = "DaisyColorSlider", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "coloreditor", Name = "Sidebar_ColorEditor", ControlName = "DaisyColorEditor", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "screenpicker", Name = "Sidebar_ScreenPicker", ControlName = "DaisyScreenColorPicker", TabHeader = "Sidebar_ColorPicker" },
                    new SidebarItem { Id = "colorpickerdialog", Name = "Sidebar_ColorPickerDialog", ControlName = "DaisyColorPickerDialog", TabHeader = "Sidebar_ColorPicker" }
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

/// <summary>
/// Gallery-specific sidebar item for the global neumorphic mode.
/// This item type triggers a special template in the sidebar that shows a mode dropdown.
/// Extends the library's SidebarNeumorphicSelectorItem so the existing template works.
/// </summary>
public class GalleryNeumorphicSelectorItem : SidebarNeumorphicSelectorItem
{
}

/// <summary>
/// Gallery-specific sidebar item for the glass mode.
/// This item type triggers a special template in the sidebar that shows a glass mode switch.
/// Extends the library's SidebarGlassModeItem so the existing template works.
/// </summary>
public class GalleryGlassModeItem : SidebarGlassModeItem
{
}

/// <summary>
/// Gallery-specific sidebar item for the base color of the theme.
/// This item type triggers a special template in the sidebar that shows a color button and a reset button.
/// Extends the library's SidebarBaseColorItem so the existing template works.
/// </summary>
public class GalleryBaseColorItem : SidebarBaseColorItem
{
}

/// <summary>
/// Gallery-specific sidebar item for the backdrop of the app.
/// This item type triggers a special template in the sidebar that shows a backdrop dropdown.
/// Extends the library's SidebarBackdropItem so the existing template works.
/// </summary>
public class GalleryBackdropItem : SidebarBackdropItem
{
}
