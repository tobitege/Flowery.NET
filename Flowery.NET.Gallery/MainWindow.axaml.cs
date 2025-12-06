using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using Flowery.Controls;
using Flowery.NET.Gallery.Examples;

namespace Flowery.NET.Gallery;

public partial class MainWindow : Window
{
    private readonly Dictionary<string, Func<Control>> _categoryControls;
    private ActionsExamples? _actionsExamples;

    public MainWindow()
    {
        InitializeComponent();

        _categoryControls = new Dictionary<string, Func<Control>>(StringComparer.OrdinalIgnoreCase)
        {
            ["Home"] = () => CreateHomePage(),
            ["Actions"] = () => GetOrCreateActionsExamples(),
            ["Form Controls"] = () => new DataInputExamples(),
            ["Navigation"] = () => new NavigationExamples(),
            ["Data Display"] = () => new DataDisplayExamples(),
            ["Feedback"] = () => new FeedbackExamples(),
            ["Cards"] = () => new CardsExamples(),
            ["Divider"] = () => new DividerExamples(),
            ["Layout"] = () => new LayoutExamples(),
            ["Theming"] = () => new ThemingExamples(),
            ["Custom Controls"] = () => new CustomControls(),
            ["Color Picker"] = () => new ColorPickerExamples()
        };

        // Restore last viewed page or show home
        var sidebar = this.FindControl<DaisyComponentSidebar>("ComponentSidebar");
        var (lastItemId, category) = sidebar?.GetLastViewedItem() ?? (null, null);
        if (lastItemId != null && category != null)
        {
            var item = category.Items.FirstOrDefault(i => i.Id == lastItemId);
            if (item != null)
                NavigateToCategory(item.TabHeader, item.Id);
        }
        else
        {
            var mainContent = this.FindControl<ContentControl>("MainContent");
            if (mainContent != null)
                mainContent.Content = CreateHomePage();
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
        NavigateToCategory("Actions", "button");
    }

    private void NavigateToCategory(string tabHeader, string? sectionId = null)
    {
        var content = this.FindControl<ContentControl>("MainContent");
        var title = this.FindControl<TextBlock>("CategoryTitle");
        var titleBar = this.FindControl<Border>("CategoryTitleBar");
        if (content == null) return;

        if (title != null)
            title.Text = tabHeader;
        if (titleBar != null)
            titleBar.IsVisible = tabHeader != "Home";

        if (_categoryControls.TryGetValue(tabHeader, out var factory))
        {
            var newContent = factory();
            content.Content = newContent;

            // Auto-scroll to the specific section after content is loaded
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
        if (DemoModal == null) return;

        // Reset to default corner radii
        DemoModal.TopLeftRadius = 16;
        DemoModal.TopRightRadius = 16;
        DemoModal.BottomLeftRadius = 16;
        DemoModal.BottomRightRadius = 16;
        SetModalTitle("Hello!");
        DemoModal.IsOpen = true;
    }

    public void OnOpenModalWithRadiiRequested(object? sender, ModalRadiiEventArgs e)
    {
        if (DemoModal == null) return;

        DemoModal.TopLeftRadius = e.TopLeft;
        DemoModal.TopRightRadius = e.TopRight;
        DemoModal.BottomLeftRadius = e.BottomLeft;
        DemoModal.BottomRightRadius = e.BottomRight;
        SetModalTitle(e.Title);
        DemoModal.IsOpen = true;
    }

    private void SetModalTitle(string title)
    {
        var modalTitle = this.FindControl<TextBlock>("ModalTitle");
        if (modalTitle != null)
            modalTitle.Text = title;
    }

    public void CloseModalBtn_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (DemoModal != null) DemoModal.IsOpen = false;
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
}
