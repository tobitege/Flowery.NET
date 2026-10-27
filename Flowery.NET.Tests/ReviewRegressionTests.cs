using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Custom.Weather;
using Flowery.Controls.Custom.Weather.Models;
using Flowery.NET.Kanban.Controls;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

public class ReviewRegressionTests
{
    [AvaloniaFact]
    public async Task CopyButtonRestoresCurrentContentAndPreservesBindingUpdates()
    {
        var label = new TextBlock { Text = "First" };
        var button = new TestCopyButton { CopyText = "Copied value", SuccessDuration = TimeSpan.FromMilliseconds(20) };
        button.Bind(ContentControl.ContentProperty, new Avalonia.Data.Binding(nameof(TextBlock.Text)) { Source = label });
        using var scope = new Scope(button);
        var restored = WaitForContent(button, "First");
        button.Copy();
        await restored.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Copied value", await scope.Window.Clipboard!.TryGetTextAsync());
        label.Text = "Second";
        Assert.Equal("Second", button.Content);
        restored = WaitForContent(button, "Second");
        button.Copy();
        await restored.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal("Second", button.Content);
        scope.Window.Content = null;
        scope.Flush();
        button.Copy();
        Assert.Equal("Second", button.Content);
    }

    private static Task WaitForContent(DaisyCopyButton button, string value)
    {
        var complete = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<AvaloniaPropertyChangedEventArgs>? changed = null;
        changed = (_, args) =>
        {
            if (args.Property != ContentControl.ContentProperty || !Equals(button.Content, value)) return;
            button.PropertyChanged -= changed;
            complete.TrySetResult();
        };
        button.PropertyChanged += changed;
        return complete.Task;
    }

    private sealed class TestCopyButton : DaisyCopyButton { public void Copy() => OnClick(); }

    [AvaloniaFact]
    public void TableSharesFixedAndWeightedHeaderWidthsWithBody()
    {
        var header = new DaisyTableRow();
        header.Items.Add(new DaisyTableHeaderCell { Content = "Id", ColumnWidth = new GridLength(90) });
        header.Items.Add(new DaisyTableHeaderCell { Content = "Name", ColumnWidth = new GridLength(2, GridUnitType.Star) });
        header.Items.Add(new DaisyTableHeaderCell { Content = "Status" });
        var body = new DaisyTableRow();
        foreach (var text in new[] { "1", "Ada", "Active" }) body.Items.Add(new DaisyTableCell { Content = text });
        var headSection = new DaisyTableHead();
        headSection.Items.Add(header);
        var bodySection = new DaisyTableBody();
        bodySection.Items.Add(body);
        var table = new DaisyTable { Width = 600 };
        table.Items.Add(headSection);
        table.Items.Add(bodySection);
        using var scope = new Scope(table);
        var headerCells = header.GetVisualDescendants().OfType<DaisyTableHeaderCell>().ToArray();
        var bodyCells = body.GetVisualDescendants().OfType<DaisyTableCell>().ToArray();
        Assert.Equal(90, headerCells[0].Bounds.Width);
        Assert.Equal(340, headerCells[1].Bounds.Width);
        Assert.Equal(170, headerCells[2].Bounds.Width);
        for (var i = 0; i < 3; i++) Assert.Equal(headerCells[i].Bounds.Width, bodyCells[i].Bounds.Width);
        headerCells[0].ColumnWidth = new GridLength(120);
        table.Width = 720;
        scope.Flush();
        Assert.Equal(120, bodyCells[0].Bounds.Width);
        Assert.Equal(400, bodyCells[1].Bounds.Width);
        Assert.Equal(200, bodyCells[2].Bounds.Width);
    }

    [AvaloniaFact]
    public void TablePinsSectionsAndEdgeColumnsWithinScrollViewport()
    {
        var head = new DaisyTableHead();
        var header = new DaisyTableRow();
        for (var i = 0; i < 4; i++) header.Items.Add(new DaisyTableHeaderCell { Content = "Column", ColumnWidth = new GridLength(200) });
        head.Items.Add(header);
        var body = new DaisyTableBody();
        for (var rowIndex = 0; rowIndex < 30; rowIndex++)
        {
            var row = new DaisyTableRow();
            for (var i = 0; i < 4; i++) row.Items.Add(new DaisyTableCell { Content = "Value" });
            body.Items.Add(row);
        }
        var foot = new DaisyTableFoot();
        var footer = new DaisyTableRow();
        footer.Items.Add(new DaisyTableCell { Content = "Total" });
        foot.Items.Add(footer);
        var table = new DaisyTable { Width = 800, PinRows = true, PinCols = true };
        table.Items.Add(head);
        table.Items.Add(body);
        table.Items.Add(foot);
        var viewer = new ScrollViewer
        {
            Width = 480, Height = 300, Content = table,
            HorizontalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto
        };
        using var scope = new Scope(viewer);
        viewer.Offset = new Vector(100, 200);
        scope.Flush();
        var viewportOrigin = viewer.TranslatePoint(new Point(), table)!.Value;
        Assert.Equal(viewportOrigin.Y, head.Bounds.Y, 1);
        Assert.Equal(viewportOrigin.Y + viewer.Viewport.Height, foot.Bounds.Bottom, 1);
        var cells = header.GetVisualDescendants().OfType<DaisyTableHeaderCell>().ToArray();
        Assert.Equal(100, cells[0].Bounds.X, 1);
        Assert.Equal(100 + viewer.Viewport.Width, cells[^1].Bounds.Right, 1);
        table.PinRows = false;
        table.PinCols = false;
        scope.Flush();
        Assert.Equal(0, head.Bounds.Y);
        Assert.Equal(0, cells[0].Bounds.X);
        Assert.Equal(800, cells[^1].Bounds.Right);
    }

    [AvaloniaFact]
    public void TextAreaResizeGripSupportsDragKeyboardAndDisable()
    {
        var area = new DaisyTextArea { Width = 350, MinHeight = 100, MaxHeight = 300 };
        using var scope = new Scope(new StackPanel { Children = { area } });
        var grip = area.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_ResizeGrip");
        var origin = grip.TranslatePoint(new Point(grip.Bounds.Width / 2, grip.Bounds.Height / 2), scope.Window)!.Value;
        scope.Window.MouseMove(origin);
        scope.Window.MouseDown(origin, MouseButton.Left);
        scope.Window.MouseMove(origin + new Vector(0, 40));
        scope.Window.MouseUp(origin + new Vector(0, 40), MouseButton.Left);
        Assert.Equal(140, area.MinHeight);
        Assert.True(grip.Focus());
        Press(scope.Window, Key.Down);
        Assert.Equal(140 + area.FontSize, area.MinHeight);
        area.CanResize = false;
        scope.Flush();
        Assert.False(grip.IsVisible);
        var height = area.MinHeight;
        Press(scope.Window, Key.Down);
        Assert.Equal(height, area.MinHeight);
    }

    [AvaloniaFact]
    public void ForecastDisplaysConfiguredUnitAndPrecipitationWithoutChangingSourceData()
    {
        var day = new ForecastDay { Date = new DateTime(2026, 10, 15), HighTemperature = 68, LowTemperature = 50, ChanceOfPrecipitation = 75 };
        var forecast = new DaisyWeatherForecast { ItemsSource = new[] { day }, TemperatureUnit = "F", ShowPrecipitation = true };
        using var scope = new Scope(forecast);
        TextBlock Find(string name) => forecast.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == name);
        Assert.Equal("68°F", Find("HighTemperatureText").Text);
        Assert.Equal("50°F", Find("LowTemperatureText").Text);
        Assert.Equal("75%", Find("PrecipitationText").Text);
        Assert.True(Find("PrecipitationText").IsVisible);
        forecast.ShowPrecipitation = false;
        forecast.TemperatureUnit = "C";
        scope.Flush();
        Assert.False(Find("PrecipitationText").IsVisible);
        Assert.Equal("68°C", Find("HighTemperatureText").Text);
        Assert.Equal(68, day.HighTemperature);
    }

    [AvaloniaFact]
    public void TaskCardRefreshesEndDateRangeAndColumnSubscriptionsAfterReattachment()
    {
        var task = new FlowTask { Title = "Schedule", PlannedStartDate = DateTime.Today };
        var card = new FlowTaskCard { Task = task };
        using var scope = new Scope(card);
        task.PlannedEndDate = DateTime.Today.AddDays(2);
        Assert.True(card.HasEndDate);
        Assert.True(card.HasDateRange);
        Assert.NotEmpty(card.EndDateText);
        task.PlannedEndDate = null;
        Assert.False(card.HasEndDate);
        Assert.False(card.HasDateRange);
        Assert.Empty(card.EndDateText);
        scope.Window.Content = null;
        scope.Window.Content = card;
        scope.Flush();
        task.PlannedEndDate = DateTime.Today.AddDays(1);
        Assert.True(card.HasDateRange);
    }

    [AvaloniaFact]
    public void KanbanFiltersRoundTripCriteriaWithoutRewritingIt()
    {
        using var scope = new Scope(new Border());
        var board = new FlowKanban { AutoSaveAfterEdits = false };
        var criteria = new FlowKanbanFilterCriteria
        {
            Priorities = [FlowTaskPriority.High], ShowOnlyBlocked = true, ShowOnlyOverdue = true,
            PlannedEndRange = new FlowKanbanDateRange { From = DateTime.Today, To = DateTime.Today.AddDays(5).AddTicks(-1) }
        };
        board.FilterCriteria = criteria;
        Assert.True(board.FilterPriorityHigh);
        Assert.False(board.FilterPriorityLow);
        Assert.True(board.FilterShowBlocked);
        Assert.True(board.FilterShowOverdue);
        Assert.Equal(DateTime.Today, board.FilterDateFrom?.Date);
        Assert.Equal(DateTime.Today.AddDays(4), board.FilterDateTo?.Date);
        Assert.Single(criteria.Priorities);
        Assert.Equal(DateTime.Today.AddDays(5).AddTicks(-1), criteria.PlannedEndRange.To);
        criteria.Priorities = [FlowTaskPriority.Low];
        criteria.ShowOnlyBlocked = false;
        criteria.NotifyChanged();
        Assert.True(board.FilterPriorityLow);
        Assert.False(board.FilterPriorityHigh);
        Assert.False(board.FilterShowBlocked);
        board.FilterCriteria = null;
        Assert.False(board.FilterPriorityLow);
        Assert.False(board.FilterShowOverdue);
        Assert.Null(board.FilterDateFrom);
        Assert.Null(board.FilterDateTo);
    }

    [AvaloniaFact]
    public void SidebarTemplateReapplicationRaisesOneSelectionEvent()
    {
        var item = new SidebarItem { Id = "one", Name = "One" };
        var sidebar = new FloweryComponentSidebar
        {
            Categories = new ObservableCollection<SidebarCategory>
            {
                new() { Name = "Group", Items = new ObservableCollection<SidebarItem> { item } }
            }
        };
        using var scope = new Scope(sidebar);
        var template = sidebar.Template;
        sidebar.Template = null;
        sidebar.ApplyTemplate();
        sidebar.Template = template;
        scope.Flush();
        var selections = 0;
        sidebar.ItemSelected += (_, _) => selections++;
        var button = sidebar.GetVisualDescendants().OfType<Button>()
            .Single(button => button.Classes.Contains("sidebar-item"));
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(button)).Invoke();
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(1, selections);
        Assert.Same(item, sidebar.SelectedItem);
    }

    [AvaloniaFact]
    public void ThemeSwapObservesThemeChangesAfterReattachment()
    {
        using var scope = new Scope(new Border());
        var previous = DaisyThemeManager.CurrentThemeName;
        var swap = new DaisyThemeSwap();
        try
        {
            scope.Window.Content = swap;
            scope.Flush();
            scope.Window.Content = null;
            scope.Window.Content = swap;
            scope.Flush();
            DaisyThemeManager.SetCurrentTheme("Light");
            Assert.False(swap.IsChecked);
            DaisyThemeManager.SetCurrentTheme("Dark");
            Assert.True(swap.IsChecked);
        }
        finally { DaisyThemeManager.SetCurrentTheme(previous ?? string.Empty); }
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Scope : IDisposable, IStateStorage
    {
        private readonly IStateStorage _previous = StateStorageProvider.Instance;
        private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);
        public Window Window { get; }
        public Scope(Control content)
        {
            StateStorageProvider.Configure(this);
            Window = new Window { Width = 1200, Height = 900, Content = content };
            Window.Show();
            Flush();
        }
        public void Flush() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() { Window.Close(); StateStorageProvider.Configure(_previous); }
        public IReadOnlyList<string> LoadLines(string key) => _values.TryGetValue(key, out var value) ? value : [];
        public void SaveLines(string key, IEnumerable<string> lines) => _values[key] = [.. lines];
        public void Delete(string key) => _values.Remove(key);
        public void Rename(string sourceKey, string targetKey) { _values[targetKey] = _values[sourceKey]; _values.Remove(sourceKey); }
        public IEnumerable<string> GetKeys(string prefix) => _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    }
}
