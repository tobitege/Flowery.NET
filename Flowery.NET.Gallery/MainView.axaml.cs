using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Gallery.Examples;
using Avalonia.Media;

namespace Flowery.NET.Gallery;

public partial class MainView : UserControl
{
    /// <summary>
    /// The width of the sidebar when open.
    /// </summary>
    private const double SidebarWidth = 220;

    /// <summary>
    /// Minimum width the content area needs to be usable.
    /// If showing the sidebar inline would leave less than this for content, collapse to overlay.
    /// </summary>
    private const double MinContentWidth = 400;

    /// <summary>
    /// Maximum percentage of screen width the sidebar should occupy before collapsing.
    /// Prevents sidebar from dominating narrow screens.
    /// </summary>
    private const double MaxSidebarWidthPercent = 0.35;

    /// <summary>
    /// Scroll threshold in pixels before the header collapses on mobile.
    /// </summary>
    private const double HeaderCollapseScrollThreshold = 20;

    private readonly Dictionary<string, Func<Control>> _categoryControls;
    private readonly Dictionary<string, Control> _categoryControlCache;
    private Control? _activeCategoryContent;
    private ActionsExamples? _actionsExamples;

    private static readonly HashSet<string> ScreenshotSkipList = new(StringComparer.OrdinalIgnoreCase)
    {
        "DaisyLoading",
        "DaisyStatusIndicator",
        "css-theme-converter",
        "themecontroller",
        "service-integration",
    };

    private bool _isCapturing;
    private bool _stopCapture;
    private bool _isHeaderCollapsed;
    private readonly bool _isMobilePlatform;

    public MainView()
    {
        InitializeComponent();

        // Detect mobile platform early (before any navigation)
        _isMobilePlatform = OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();

        // Handle FlowDirection changes
        UpdateFlowDirection();
        FloweryLocalization.CultureChanged += (_, _) =>
            global::Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                UpdateFlowDirection();
                // Update localized title if we have an active category
                if (_activeCategoryContent != null && CategoryTitle != null && CategoryTitle.Tag is string key)
                {
                    CategoryTitle.Text = FloweryLocalization.GetString(key);
                }
            });

        this.Loaded += OnLoaded;
        this.SizeChanged += OnSizeChanged;

        _categoryControls = new Dictionary<string, Func<Control>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Sidebar_Home"] = () => CreateHomePage(),
            ["Sidebar_Actions"] = () => GetOrCreateActionsExamples(),
            ["Sidebar_DataInput"] = () => new DataInputExamples(),
            ["Sidebar_Navigation"] = () => new NavigationExamples(),
            ["Sidebar_DataDisplay"] = () => new DataDisplayExamples(),
            ["Sidebar_DateDisplay"] = () => new DateDisplayExamples(),
            ["Sidebar_Feedback"] = () => new FeedbackExamples(),
            ["Sidebar_Cards"] = () => new CardsExamples(),
            ["Sidebar_Divider"] = () => new DividerExamples(),
            ["Sidebar_Layout"] = () => new LayoutExamples(),
            ["Sidebar_Theming"] = () => new ThemingExamples(),
            ["Sidebar_ProductThemes"] = () => new ProductThemesExamples(),
            ["Sidebar_Effects"] = () => new EffectsExamples(),
            ["Sidebar_Scaling"] = () => new ScalingExamples(),
            ["Sidebar_CustomControls"] = () => new CustomControls(),
            ["Sidebar_ColorPicker"] = () => new ColorPickerExamples(),
            ["Sidebar_Showcase"] = () => new ShowcaseExamples(),
        };
        _categoryControlCache = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);

        // Subscribe to global size changes for Gallery demo
        FlowerySizeManager.SizeChanged += OnGlobalSizeChanged;

        if (ComponentSidebar != null)
        {
            // Initialize sidebar with Gallery-specific data
            ComponentSidebar.Categories = GallerySidebarData.CreateCategories();
            ComponentSidebar.AvailableLanguages = GallerySidebarData.CreateLanguages();

            var (lastItemId, category) = ComponentSidebar.GetLastViewedItem();
            if (lastItemId != null && category != null)
            {
                var item = category.Items.FirstOrDefault(i => i.Id == lastItemId);
                if (item != null)
                {
                    NavigateToCategory(item.TabHeader, item.Id);
                    return;
                }
            }
        }

        NavigateToCategory("Sidebar_Home");
    }

    private void OnGlobalSizeChanged(object? sender, DaisySize newSize)
    {
        // Apply the new size to all Daisy controls in the visual tree
        ApplyGlobalSizeToControls(this, newSize);

        // Apply size to Gallery-specific elements (CategoryTitle, etc.)
        ApplySizeToGalleryElements(newSize);
    }

    private void ApplySizeToGalleryElements(DaisySize size)
    {
        // Update CategoryTitle font size
        if (CategoryTitle != null)
        {
            CategoryTitle.FontSize = size switch
            {
                DaisySize.ExtraSmall => 16,
                DaisySize.Small => 18,
                DaisySize.Medium => 22,
                DaisySize.Large => 26,
                DaisySize.ExtraLarge => 30,
                _ => 22
            };
        }

        // Update CategoryTitleBar padding
        if (CategoryTitleBar != null)
        {
            var padding = size switch
            {
                DaisySize.ExtraSmall => new Thickness(16, 2, 16, 8),
                DaisySize.Small => new Thickness(20, 3, 20, 14),
                DaisySize.Medium => new Thickness(24, 4, 24, 20),
                DaisySize.Large => new Thickness(28, 6, 28, 24),
                DaisySize.ExtraLarge => new Thickness(32, 8, 32, 28),
                _ => new Thickness(24, 4, 24, 20)
            };
            CategoryTitleBar.Padding = padding;
        }

        // Update CategoryChevrons size
        if (CategoryChevrons != null)
        {
            var chevronSize = size switch
            {
                DaisySize.ExtraSmall => (Width: 16.0, Height: 10.0),
                DaisySize.Small => (Width: 20.0, Height: 12.0),
                DaisySize.Medium => (Width: 24.0, Height: 14.0),
                DaisySize.Large => (Width: 28.0, Height: 16.0),
                DaisySize.ExtraLarge => (Width: 32.0, Height: 18.0),
                _ => (Width: 24.0, Height: 14.0)
            };
            CategoryChevrons.Width = chevronSize.Width;
            CategoryChevrons.Height = chevronSize.Height;
        }
    }

    private static void ApplyGlobalSizeToControls(Control root, DaisySize size)
    {
        foreach (var control in root.GetVisualDescendants().OfType<Control>())
        {
            // Skip controls marked as ignoring global size (check self and ancestors)
            if (ShouldIgnoreGlobalSize(control))
                continue;

            // Check if control has a Size property of type DaisySize
            var sizeProperty = control.GetType().GetProperty("Size");
            if (sizeProperty != null && sizeProperty.PropertyType == typeof(DaisySize) && sizeProperty.CanWrite)
            {
                try
                {
                    sizeProperty.SetValue(control, size);
                }
                catch
                {
                    // Ignore controls that can't be sized
                }
            }
        }
    }

    /// <summary>
    /// Checks if a control or any of its ancestors has IgnoreGlobalSize set to true.
    /// </summary>
    private static bool ShouldIgnoreGlobalSize(Control control)
    {
        Visual? current = control;
        while (current != null)
        {
            if (current is Control c && FlowerySizeManager.GetIgnoreGlobalSize(c))
                return true;
            current = current.GetVisualParent();
        }
        return false;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        // Hide screenshot button on mobile (desktop-only feature)
        if (ScreenshotButton != null)
            ScreenshotButton.IsVisible = !_isMobilePlatform;

        // Apply responsive layout on initial load
        UpdateResponsiveLayout(this.Bounds.Width);

        // On mobile, initialize orientation and attach scroll handler
        if (_isMobilePlatform)
        {
            _isLandscape = this.Bounds.Width > this.Bounds.Height;

            // Ensure header starts expanded (especially in portrait)
            if (!_isLandscape)
                SetHeaderCollapsed(false);

            if (_activeCategoryContent != null && _currentScrollViewer == null)
                AttachScrollHandler(_activeCategoryContent);
        }

        // Apply the current global size on startup (controls default to Medium otherwise)
        ApplyGlobalSizeToControls(this, FlowerySizeManager.CurrentSize);
    }

    private bool _isLandscape;

    private void OnSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        UpdateResponsiveLayout(e.NewSize.Width);

        // On mobile, determine orientation and update header accordingly
        if (_isMobilePlatform)
        {
            var wasLandscape = _isLandscape;
            _isLandscape = e.NewSize.Width > e.NewSize.Height;

            // If switching from landscape to portrait, expand header
            if (wasLandscape && !_isLandscape && _isHeaderCollapsed)
            {
                SetHeaderCollapsed(false);
            }
            // If in portrait mode, always ensure header is expanded
            else if (!_isLandscape && _isHeaderCollapsed)
            {
                SetHeaderCollapsed(false);
            }
        }
    }

    private void UpdateResponsiveLayout(double width)
    {
        if (MainSplitView == null || HamburgerButton == null || ComponentSidebar == null)
            return;

        // Collapse sidebar if:
        // 1. Content area would be too narrow (less than MinContentWidth)
        // 2. Sidebar would take more than MaxSidebarWidthPercent of screen
        var contentWidthIfInline = width - SidebarWidth;
        var sidebarPercent = SidebarWidth / width;

        bool shouldCollapse = contentWidthIfInline < MinContentWidth || sidebarPercent > MaxSidebarWidthPercent;

        // Compact: overlay sidebar with hamburger toggle
        // Wide: inline sidebar always visible
        MainSplitView.DisplayMode = shouldCollapse ? SplitViewDisplayMode.Overlay : SplitViewDisplayMode.Inline;
        HamburgerButton.IsVisible = shouldCollapse;
        MainSplitView.IsPaneOpen = !shouldCollapse;
    }

    private void HamburgerButton_Click(object? sender, RoutedEventArgs e)
    {
        if (MainSplitView != null)
        {
            MainSplitView.IsPaneOpen = !MainSplitView.IsPaneOpen;
        }
    }

    private Control CreateHomePage()
    {
        var homePage = new HomePage();
        homePage.BrowseComponentsRequested += OnBrowseComponentsRequested;
        return homePage;
    }

    private void OnBrowseComponentsRequested(object? sender, EventArgs e)
    {
        NavigateToCategory("Sidebar_Actions", "button");
    }

    private void NavigateToCategory(string tabHeader, string? sectionId = null)
    {
        if (MainContentHost == null)
            return;

        if (CategoryTitle != null)
        {
            CategoryTitle.Text = FloweryLocalization.GetString(tabHeader);
            CategoryTitle.Tag = tabHeader; // Store key for updates
        }
        if (CategoryTitleBar != null)
            CategoryTitleBar.IsVisible = tabHeader != "Sidebar_Home";

        if (_categoryControls.TryGetValue(tabHeader, out var factory))
        {
            if (!_categoryControlCache.TryGetValue(tabHeader, out var newContent))
            {
                newContent = factory();
                _categoryControlCache[tabHeader] = newContent;
            }

            var contentChanged = !ReferenceEquals(_activeCategoryContent, newContent);
            if (contentChanged)
            {
                if (_activeCategoryContent != null)
                    _activeCategoryContent.IsVisible = false;

                if (!MainContentHost.Children.Contains(newContent))
                    MainContentHost.Children.Add(newContent);

                newContent.IsVisible = true;
                _activeCategoryContent = newContent;
            }

            if (sectionId != null && newContent is IScrollableExample scrollable)
            {
                if (contentChanged)
                {
                    global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        scrollable.ScrollToSection(sectionId);
                    }, global::Avalonia.Threading.DispatcherPriority.Loaded);
                }
                else
                {
                    scrollable.ScrollToSection(sectionId);
                }
            }

            // On mobile, attach scroll handler for header collapse
            if (_isMobilePlatform && contentChanged)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    AttachScrollHandler(newContent);
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            }

            // Apply global size to newly shown content (controls default to Medium otherwise)
            if (contentChanged)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    ApplyGlobalSizeToControls(newContent, FlowerySizeManager.CurrentSize);
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
    }

    private ScrollViewer? _currentScrollViewer;

    private void AttachScrollHandler(Control content)
    {
        // Detach from previous ScrollViewer
        if (_currentScrollViewer != null)
        {
            _currentScrollViewer.ScrollChanged -= OnContentScrollChanged;
            _currentScrollViewer = null;
        }

        // Find ScrollViewer in the content
        var scrollViewer = content.GetVisualDescendants().OfType<ScrollViewer>().FirstOrDefault();
        if (scrollViewer != null)
        {
            _currentScrollViewer = scrollViewer;
            scrollViewer.ScrollChanged += OnContentScrollChanged;
        }
    }

    private void OnContentScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        // Only allow scroll-based collapse in landscape mode
        if (!_isLandscape)
            return;

        if (sender is not ScrollViewer scrollViewer)
            return;

        var shouldCollapse = scrollViewer.Offset.Y > HeaderCollapseScrollThreshold;
        if (shouldCollapse != _isHeaderCollapsed)
        {
            SetHeaderCollapsed(shouldCollapse);
        }
    }

    private void SetHeaderCollapsed(bool collapsed)
    {
        _isHeaderCollapsed = collapsed;

        if (HeaderPanel == null || SubtitleRow == null || HeaderTitle == null)
            return;

        if (collapsed)
        {
            // Compact mode: single-line thin header
            SubtitleRow.IsVisible = false;
            HeaderTitle.FontSize = 16;
            if (HeaderContentGrid != null)
                HeaderContentGrid.Margin = new Thickness(12, 6, 16, 4);
            if (HeaderOrbRight != null)
                HeaderOrbRight.IsVisible = false;
            if (HeaderOrbLeft != null)
                HeaderOrbLeft.IsVisible = false;
            if (HeaderAccentBar != null)
                HeaderAccentBar.Height = 2;
            // Compact category title bar
            if (CategoryTitleBar != null)
                CategoryTitleBar.Padding = new Thickness(16, 2, 16, 6);
            if (CategoryTitle != null)
                CategoryTitle.FontSize = 16;
            if (CategoryChevrons != null)
                CategoryChevrons.IsVisible = false;
        }
        else
        {
            // Full mode: show all header elements
            SubtitleRow.IsVisible = true;
            HeaderTitle.FontSize = 28;
            if (HeaderContentGrid != null)
                HeaderContentGrid.Margin = new Thickness(12, 16, 16, 8);
            if (HeaderOrbRight != null)
                HeaderOrbRight.IsVisible = true;
            if (HeaderOrbLeft != null)
                HeaderOrbLeft.IsVisible = true;
            if (HeaderAccentBar != null)
                HeaderAccentBar.Height = 3;
            // Full category title bar
            if (CategoryTitleBar != null)
                CategoryTitleBar.Padding = new Thickness(24, 4, 24, 20);
            if (CategoryTitle != null)
                CategoryTitle.FontSize = 22;
            if (CategoryChevrons != null)
                CategoryChevrons.IsVisible = true;
        }
    }

    private void UpdateFlowDirection()
    {
        FlowDirection = FloweryLocalization.Instance.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    private Control GetOrCreateActionsExamples()
    {
        if (_actionsExamples == null)
        {
            _actionsExamples = new ActionsExamples();
            _actionsExamples.OpenModalRequested += OnOpenModalRequested;
            _actionsExamples.OpenModalWithRadiiRequested += OnOpenModalWithRadiiRequested;
        }
        return _actionsExamples;
    }

    public void ComponentSidebar_ItemSelected(object? sender, SidebarItemSelectedEventArgs e)
    {
        NavigateToCategory(e.Item.TabHeader, e.Item.Id);
        if (MainSplitView != null && MainSplitView.DisplayMode == SplitViewDisplayMode.Overlay)
        {
            MainSplitView.IsPaneOpen = false;
        }
    }

    public void OnOpenModalRequested(object? sender, EventArgs e)
    {
        if (DemoModal == null)
            return;

        DemoModal.TopLeftRadius = 16;
        DemoModal.TopRightRadius = 16;
        DemoModal.BottomLeftRadius = 16;
        DemoModal.BottomRightRadius = 16;
        SetModalTitle("Hello!");
        DemoModal.IsOpen = true;
    }

    public void OnOpenModalWithRadiiRequested(object? sender, ModalRadiiEventArgs e)
    {
        if (DemoModal == null)
            return;

        DemoModal.TopLeftRadius = e.TopLeft;
        DemoModal.TopRightRadius = e.TopRight;
        DemoModal.BottomLeftRadius = e.BottomLeft;
        DemoModal.BottomRightRadius = e.BottomRight;
        SetModalTitle(e.Title);
        DemoModal.IsOpen = true;
    }

    private void SetModalTitle(string title)
    {
        if (ModalTitle != null)
            ModalTitle.Text = title;
    }

    public void CloseModalBtn_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DemoModal != null)
            DemoModal.IsOpen = false;
    }

    public void OpenGitHub_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var url = "https://www.github.com/tobitege";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            Process.Start("xdg-open", url);
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            Process.Start("open", url);
    }

    public async void CaptureAllScreenshots_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var button = sender as DaisyButton;
        if (button == null)
            return;

        if (_isCapturing)
        {
            _stopCapture = true;
            return;
        }

        var storageProvider = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storageProvider == null)
            return;

        var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select Folder for Screenshots",
            AllowMultiple = false,
        });

        if (folders == null || folders.Count == 0)
            return;

        var baseDir = folders[0].Path.LocalPath;

        _isCapturing = true;
        _stopCapture = false;
        button.Variant = DaisyButtonVariant.Error;
        var originalContent = button.Content;

        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is Window hostWindow && topLevel.Screens?.Primary is { } primaryScreen)
            {
                hostWindow.WindowState = WindowState.Normal;
                hostWindow.Width = 1200;
                hostWindow.Height = Math.Min(1200, primaryScreen.WorkingArea.Height - 60);

                var screenWidth = primaryScreen.WorkingArea.Width;
                var x = primaryScreen.WorkingArea.X + (screenWidth - hostWindow.Width) / 2;
                var y = primaryScreen.WorkingArea.Y + 10;
                hostWindow.Position = new PixelPoint((int)x, (int)y);

                await Task.Delay(500);
            }

            if (!Directory.Exists(baseDir))
                Directory.CreateDirectory(baseDir);

            var capturedControls = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var categoryName in _categoryControls.Keys)
            {
                if (_stopCapture)
                    break;
                if (categoryName == "Home")
                    continue;

                NavigateToCategory(categoryName);

                await Task.Delay(1000);

                if (_activeCategoryContent is Control view)
                {
                    var headers = view.GetVisualDescendants().OfType<SectionHeader>().ToList();

                    foreach (var header in headers)
                    {
                        if (_stopCapture)
                            break;

                        var controlName = header.ControlName;
                        if (string.IsNullOrEmpty(controlName))
                            controlName = header.SectionId;
                        if (string.IsNullOrEmpty(controlName))
                            controlName = header.Title.Replace(" ", string.Empty);

                        if (ScreenshotSkipList.Contains(controlName) ||
                            ScreenshotSkipList.Contains(header.SectionId ?? string.Empty))
                            continue;

                        if (capturedControls.Contains(controlName))
                            continue;

                        capturedControls.Add(controlName);

                        var fileName = $"{controlName}.png";
                        var filePath = Path.Combine(baseDir, fileName);

                        await header.SaveScreenshotToFileAsync(filePath, hideScreenshotButton: true);

                        await Task.Delay(1000);
                    }
                }
            }

            if (Directory.Exists(baseDir) && Directory.GetFiles(baseDir, "*.png").Length > 0)
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    Process.Start(new ProcessStartInfo(baseDir) { UseShellExecute = true });
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    Process.Start("xdg-open", baseDir);
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    Process.Start("open", baseDir);
            }
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Batch capture failed: {ex.Message}");
        }
        finally
        {
            button.Variant = DaisyButtonVariant.Ghost;
            button.Content = originalContent;
            _isCapturing = false;
            _stopCapture = false;
        }
    }
}
