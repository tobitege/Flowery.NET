using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia.Controls;
using DaisyUI.Avalonia.Controls;
using DaisyUI.Avalonia.Gallery.Examples;

namespace DaisyUI.Avalonia.Gallery;

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
            ["Theming"] = () => new ThemingExamples()
        };

        // Initialize with HomePage (with event handler attached)
        var mainContent = this.FindControl<ContentControl>("MainContent");
        if (mainContent != null)
            mainContent.Content = CreateHomePage();
    }

    private Control CreateHomePage()
    {
        var homePage = new HomePage();
        homePage.BrowseComponentsRequested += OnBrowseComponentsRequested;
        return homePage;
    }

    private void OnBrowseComponentsRequested(object? sender, EventArgs e)
    {
        NavigateToCategory("Actions", "Button");
    }

    private void NavigateToCategory(string tabHeader, string? itemName = null)
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
            if (itemName != null && newContent is IScrollableExample scrollable)
            {
                global::Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    scrollable.ScrollToSection(itemName);
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
        }
        return _actionsExamples;
    }

    public void ComponentSidebar_ItemSelected(object? sender, SidebarItemSelectedEventArgs e)
    {
        NavigateToCategory(e.Item.TabHeader, e.Item.Name);
    }

    public void OnOpenModalRequested(object? sender, EventArgs e)
    {
        var modal = this.FindControl<DaisyModal>("DemoModal");
        if (modal != null) modal.IsOpen = true;
    }

    public void CloseModalBtn_Click(object? sender, global::Avalonia.Interactivity.RoutedEventArgs e)
    {
        var modal = this.FindControl<DaisyModal>("DemoModal");
        if (modal != null) modal.IsOpen = false;
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
