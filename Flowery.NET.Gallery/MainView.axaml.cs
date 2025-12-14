using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Gallery.Examples;

namespace Flowery.NET.Gallery;

public partial class MainView : UserControl
{
    private readonly Dictionary<string, Func<Control>> _categoryControls;
    private ActionsExamples? _actionsExamples;

    /// <summary>
    /// Controls/sections to skip during batch screenshot capture.
    /// These either have animated GIFs in documentation or are non-visual sections.
    /// </summary>
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

    public MainView()
    {
        InitializeComponent();

        _categoryControls = new Dictionary<string, Func<Control>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Home"] = () => CreateHomePage(),
            ["Actions"] = () => GetOrCreateActionsExamples(),
            ["Data Input"] = () => new DataInputExamples(),
            ["Navigation"] = () => new NavigationExamples(),
            ["Data Display"] = () => new DataDisplayExamples(),
            ["Date Display"] = () => new DateDisplayExamples(),
            ["Feedback"] = () => new FeedbackExamples(),
            ["Cards"] = () => new CardsExamples(),
            ["Divider"] = () => new DividerExamples(),
            ["Layout"] = () => new LayoutExamples(),
            ["Theming"] = () => new ThemingExamples(),
            ["Effects"] = () => new EffectsExamples(),
            ["Custom Controls"] = () => new CustomControls(),
            ["Color Picker"] = () => new ColorPickerExamples(),
        };

        if (ComponentSidebar != null)
        {
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

        if (MainContent != null)
            MainContent.Content = CreateHomePage();
    }

    private Control CreateHomePage()
    {
        var homePage = new HomePage();
        homePage.BrowseComponentsRequested += OnBrowseComponentsRequested;
        return homePage;
    }

    private void OnBrowseComponentsRequested(object? sender, EventArgs e)
    {
        NavigateToCategory("Actions", "button");
    }

    private void NavigateToCategory(string tabHeader, string? sectionId = null)
    {
        if (MainContent == null)
            return;

        if (CategoryTitle != null)
            CategoryTitle.Text = tabHeader;
        if (CategoryTitleBar != null)
            CategoryTitleBar.IsVisible = tabHeader != "Home";

        if (_categoryControls.TryGetValue(tabHeader, out var factory))
        {
            var newContent = factory();
            MainContent.Content = newContent;

            if (sectionId != null && newContent is IScrollableExample scrollable)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    scrollable.ScrollToSection(sectionId);
                }, global::Avalonia.Threading.DispatcherPriority.Loaded);
            }
        }
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

                if (MainContent?.Content is Control view)
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
