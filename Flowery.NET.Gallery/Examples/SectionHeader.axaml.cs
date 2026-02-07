using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Flowery.Capture;

namespace Flowery.NET.Gallery.Examples;

public partial class SectionHeader : UserControl
{
    private bool _isSizeSubscriptionActive;

    /// <summary>
    /// Maps SectionId to Daisy control name. Used for screenshot filenames and documentation.
    /// Keep in sync with generate_docs.py _section_to_control() mapping.
    /// </summary>
    public static readonly Dictionary<string, string> SectionIdToControlName = new(StringComparer.OrdinalIgnoreCase)
    {
        // Actions
        ["button"] = "DaisyButton",
        ["buttongroup"] = "DaisyButtonGroup",
        ["copybutton"] = "DaisyCopyButton",
        ["dropdown"] = "DaisySelect",
        ["dropdownmenu"] = "DaisyDropdown",
        ["popover"] = "DaisyPopover",
        ["fab"] = "DaisyFab",
        ["modal"] = "DaisyModal",
        ["modalradii"] = "DaisyModal",
        ["swap"] = "DaisySwap",

        // Cards & Layout
        ["card"] = "DaisyCard",

        // Data Input
        ["checkbox"] = "DaisyCheckBox",
        ["fileinput"] = "DaisyFileInput",
        ["input"] = "DaisyInput",
        ["otpinput"] = "DaisyOtpInput",
        ["tagpicker"] = "DaisyTagPicker",
        ["maskinput"] = "DaisyMaskInput",
        ["numericupdown"] = "DaisyNumericUpDown",
        ["radio"] = "DaisyRadio",
        ["range"] = "DaisyRange",
        ["rating"] = "DaisyRating",
        ["select"] = "DaisySelect",
        ["textarea"] = "DaisyTextArea",
        ["toggle"] = "DaisyToggle",

        // Feedback
        ["alert"] = "DaisyAlert",
        ["badge"] = "DaisyBadge",
        ["loading"] = "DaisyLoading",
        ["progress"] = "DaisyProgress",
        ["skeleton"] = "DaisySkeleton",
        ["toast"] = "DaisyToast",

        // Data Display
        ["accordion"] = "DaisyAccordion",
        ["avatar"] = "DaisyAvatar",
        ["carousel"] = "DaisyCarousel",
        ["chatbubble"] = "DaisyChatBubble",
        ["collapse"] = "DaisyCollapse",
        ["countdown"] = "DaisyCountdown",
        ["diff"] = "DaisyDiff",
        ["divider"] = "DaisyDivider",
        ["kbd"] = "DaisyKbd",
        ["list"] = "DaisyList",
        ["stat"] = "DaisyStat",
        ["table"] = "DaisyTable",
        ["contributiongraph"] = "DaisyContributionGraph",
        ["animatednumber"] = "DaisyAnimatedNumber",
        ["numberflow"] = "DaisyNumberFlow",

        // Date Display
        ["date-timeline"] = "DaisyDateTimeline",
        ["timeline"] = "DaisyTimeline",

        // Navigation
        ["breadcrumbs"] = "DaisyBreadcrumbs",
        ["dock"] = "DaisyDock",
        ["drawer"] = "DaisyDrawer",
        ["menu"] = "DaisyMenu",
        ["navbar"] = "DaisyNavbar",
        ["pagination"] = "DaisyPagination",
        ["steps"] = "DaisySteps",
        ["tabs"] = "DaisyTabs",

        // Layout
        ["statusdot"] = "DaisyStatusIndicator",
        ["radialprogress"] = "DaisyRadialProgress",
        ["indicator"] = "DaisyIndicator",
        ["mask"] = "DaisyMask",
        ["stack"] = "DaisyStack",
        ["hero"] = "DaisyHero",
        ["join"] = "DaisyJoin",
        ["mockup"] = "DaisyMockup",
        ["hovergallery"] = "DaisyHoverGallery",
        ["glass"] = "DaisyGlass",
        ["daisyglasssimulated"] = "DaisyGlass",
        ["daisyglassbitmapcapture"] = "DaisyGlass",
        ["daisyglassskiamatrix"] = "DaisyGlass",
        ["daisyglassfullwidth"] = "DaisyGlass",
        ["textrotate"] = "DaisyTextRotate",

        // Color Picker
        ["colorslider"] = "DaisyColorSlider",
        ["colorwheel"] = "DaisyColorWheel",
        ["colorgrid"] = "DaisyColorGrid",
        ["coloreditor"] = "DaisyColorEditor",
        ["screenpicker"] = "DaisyScreenColorPicker",
        ["screencolorpicker"] = "DaisyScreenColorPicker",
        ["colorpickerdialog"] = "DaisyColorPickerDialog",
        ["colorpicker"] = "DaisyColorPickerDialog",

        // Custom controls
        ["modifierkeys"] = "DaisyModifierKeys",
        ["weathericon"] = "DaisyWeatherIcon",
        ["weathercard"] = "DaisyWeatherCard",
        ["currentweather"] = "DaisyWeatherCurrent",
        ["weatherforecast"] = "DaisyWeatherForecast",
        ["weathermetrics"] = "DaisyWeatherMetrics",

        // Theming
        ["themecontroller"] = "DaisyThemeController",
        ["themedropdown"] = "DaisyThemeDropdown",
        ["productthemes"] = "DaisyProductThemeDropdown",
        ["themeswap"] = "DaisyThemeSwap",
        ["themeradio"] = "DaisyThemeRadio",
        // Showcase / Eye Candy
        ["expandablecards"] = "DaisyExpandableCard",
        ["expandable-batteries"] = "DaisyExpandableCard",
        ["power-off-slide"] = "Showcase_PowerOff",
        ["typewriter"] = "Showcase_Typewriter",
        ["scroll-reveal"] = "Showcase_ScrollReveal",
    };

    private static readonly Lazy<IScreenCaptureService> _captureService = new(() => ScreenCapture.CreateDefault());

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<SectionHeader, string>(nameof(Title), string.Empty);

    public static readonly StyledProperty<string> SectionIdProperty =
        AvaloniaProperty.Register<SectionHeader, string>(nameof(SectionId), string.Empty);

    public static readonly StyledProperty<Flowery.Controls.DaisySize> SizeProperty =
        AvaloniaProperty.Register<SectionHeader, Flowery.Controls.DaisySize>(nameof(Size), Flowery.Controls.DaisySize.Medium);

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string SectionId
    {
        get => GetValue(SectionIdProperty);
        set => SetValue(SectionIdProperty, value);
    }

    /// <summary>
    /// Gets or sets the size of the header. Affects font size and padding.
    /// </summary>
    public Flowery.Controls.DaisySize Size
    {
        get => GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>
    /// Gets the control name for this section, derived from SectionId using the mapping.
    /// Returns the SectionId as fallback if no mapping exists.
    /// </summary>
    public string ControlName
    {
        get
        {
            var sectionId = SectionId;
            if (string.IsNullOrEmpty(sectionId))
                return string.Empty;

            var normalized = sectionId.ToLowerInvariant().Replace("-", "").Replace("_", "");

            return SectionIdToControlName.TryGetValue(normalized, out var controlName)
                ? controlName
                : sectionId;
        }
    }

    public SectionHeader()
    {
        InitializeComponent();

        SubscribeToGlobalSizeChanges();

        // Apply initial size
        ApplySizeToHeader(Flowery.Controls.FlowerySizeManager.CurrentSize);
    }

    private void OnGlobalSizeChanged(object? sender, Flowery.Controls.DaisySize newSize)
    {
        Size = newSize;
        ApplySizeToHeader(newSize);
    }

    private void ApplySizeToHeader(Flowery.Controls.DaisySize size)
    {
        var textBlock = this.FindControl<TextBlock>("TitleText");
        if (textBlock == null) return;

        // Map DaisySize to font sizes
        textBlock.FontSize = size switch
        {
            Flowery.Controls.DaisySize.ExtraSmall => 14,
            Flowery.Controls.DaisySize.Small => 16,
            Flowery.Controls.DaisySize.Medium => 20,
            Flowery.Controls.DaisySize.Large => 24,
            Flowery.Controls.DaisySize.ExtraLarge => 28,
            _ => 20
        };
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        SubscribeToGlobalSizeChanges();
        ApplySizeToHeader(Flowery.Controls.FlowerySizeManager.CurrentSize);
    }

    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        UnsubscribeFromGlobalSizeChanges();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty)
        {
            var textBlock = this.FindControl<TextBlock>("TitleText");
            if (textBlock != null)
                textBlock.Text = Title;
        }
    }

    /// <summary>
    /// Captures and saves screenshot(s) of this section to the specified file path.
    /// For tall sections, multiple files with _a, _b, etc. suffixes are created.
    /// </summary>
    public async Task<bool> SaveScreenshotToFileAsync(string filePath, bool hideScreenshotButton = false)
    {
        var sectionContainer = FindSectionContainer();
        if (sectionContainer == null) return false;

        var screenshotButton = this.FindControl<Flowery.Controls.DaisyButton>("ScreenshotButton");
        if (hideScreenshotButton && screenshotButton != null)
            screenshotButton.Opacity = 0;

        try
        {
            var scrollViewer = sectionContainer.FindAncestorOfType<ScrollViewer>();
            var contentPanel = FindContentPanel(sectionContainer);

            var options = new ScreenCaptureOptions
            {
                ScrollViewer = scrollViewer,
                ContentPanel = contentPanel,
                CaptureMargin = new Thickness(-8, 0, 0, 0),
                ScrollSettleDelayMs = 500
            };

            sectionContainer.BringIntoView();
            await Task.Delay(1000);

            var result = await _captureService.Value.CaptureControlAsync(sectionContainer, options);

            if (!result.Success || result.Chunks.Count == 0)
                return false;

            var baseFileName = Path.GetFileNameWithoutExtension(filePath);
            var extension = Path.GetExtension(filePath);
            var directory = Path.GetDirectoryName(filePath) ?? ".";

            for (int i = 0; i < result.Chunks.Count; i++)
            {
                var suffix = result.Chunks.Count > 1 ? "_" + (char)('a' + i) : "";
                var chunkPath = Path.Combine(directory, $"{baseFileName}{suffix}{extension}");
                await File.WriteAllBytesAsync(chunkPath, result.Chunks[i]);
            }

            return true;
        }
        catch
        {
            return false;
        }
        finally
        {
            if (hideScreenshotButton && screenshotButton != null)
                screenshotButton.Opacity = 1;
        }
    }

    private async void OnScreenshotClick(object? sender, RoutedEventArgs e)
    {
        await CaptureWithDialogAsync();
    }

    private async Task CaptureWithDialogAsync()
    {
        var sectionContainer = FindSectionContainer();
        if (sectionContainer == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var scrollViewer = sectionContainer.FindAncestorOfType<ScrollViewer>();
        var contentPanel = FindContentPanel(sectionContainer);
        var viewportHeight = scrollViewer?.Bounds.Height ?? 800;

        // Pre-calculate if we'll have multiple chunks
        var bounds = sectionContainer.Bounds;
        var willHaveMultipleChunks = bounds.Height > viewportHeight;

        var safeTitle = SanitizeFileName(Title);

        try
        {
            var storageProvider = topLevel.StorageProvider;

            if (!willHaveMultipleChunks)
            {
                var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = "Save Screenshot",
                    SuggestedFileName = $"{safeTitle}.png",
                    DefaultExtension = "png",
                    FileTypeChoices = new[]
                    {
                        new FilePickerFileType("PNG Images") { Patterns = new[] { "*.png" } }
                    }
                });

                if (file == null) return;

                var options = new ScreenCaptureOptions
                {
                    ScrollViewer = scrollViewer,
                    ContentPanel = contentPanel,
                    CaptureMargin = new Thickness(-8, 0, 0, 0)
                };

                sectionContainer.BringIntoView();
                await Task.Delay(500);

                var result = await _captureService.Value.CaptureControlAsync(sectionContainer, options);
                if (result.Success && result.Chunks.Count > 0)
                {
                    await File.WriteAllBytesAsync(file.Path.LocalPath, result.Chunks[0]);
                }
            }
            else
            {
                var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
                {
                    Title = "Select Folder for Screenshots",
                    AllowMultiple = false
                });

                if (folders == null || folders.Count == 0) return;
                var folderPath = folders[0].Path.LocalPath;

                var options = new ScreenCaptureOptions
                {
                    ScrollViewer = scrollViewer,
                    ContentPanel = contentPanel,
                    CaptureMargin = new Thickness(-8, 0, 0, 0)
                };

                sectionContainer.BringIntoView();
                await Task.Delay(500);

                var result = await _captureService.Value.CaptureControlAsync(sectionContainer, options);
                if (result.Success)
                {
                    for (int i = 0; i < result.Chunks.Count; i++)
                    {
                        var suffix = result.Chunks.Count > 1 ? "_" + (char)('a' + i) : "";
                        var filePath = Path.Combine(folderPath, $"{safeTitle}{suffix}.png");
                        await File.WriteAllBytesAsync(filePath, result.Chunks[i]);
                    }
                }
            }

            var button = this.FindControl<Flowery.Controls.DaisyButton>("ScreenshotButton");
            if (button != null)
            {
                var originalContent = button.Content;
                button.Content = "✓";
                await Task.Delay(1000);
                button.Content = originalContent;
            }
        }
        catch
        {
            // Silently fail
        }
    }

    private static string SanitizeFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Join("_", name.Split(invalid, StringSplitOptions.RemoveEmptyEntries)).Replace(" ", "_");
    }

    private Control? FindSectionContainer()
    {
        var parent = this.Parent as Control;
        if (parent is StackPanel sectionPanel)
            return sectionPanel;
        return parent;
    }

    private Panel? FindContentPanel(Control sectionContainer)
    {
        if (sectionContainer is not Panel panel || panel.Children.Count <= 1)
            return null;

        var headerIndex = -1;
        for (int i = 0; i < panel.Children.Count; i++)
        {
            if (panel.Children[i] == this)
            {
                headerIndex = i;
                break;
            }
        }

        if (headerIndex != -1 && headerIndex + 1 < panel.Children.Count)
            return panel.Children[headerIndex + 1] as Panel;

        return null;
    }

    private void SubscribeToGlobalSizeChanges()
    {
        if (_isSizeSubscriptionActive)
            return;

        Flowery.Controls.FlowerySizeManager.SizeChanged += OnGlobalSizeChanged;
        _isSizeSubscriptionActive = true;
    }

    private void UnsubscribeFromGlobalSizeChanges()
    {
        if (!_isSizeSubscriptionActive)
            return;

        Flowery.Controls.FlowerySizeManager.SizeChanged -= OnGlobalSizeChanged;
        _isSizeSubscriptionActive = false;
    }
}
