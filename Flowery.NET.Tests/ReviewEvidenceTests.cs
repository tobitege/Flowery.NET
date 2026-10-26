using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.Custom.Weather;
using Flowery.Controls.Custom.Weather.Models;
using Flowery.Controls.Custom.Weather.Services;
using Flowery.Effects;
using Flowery.NET.Kanban.Controls;
using Xunit;

namespace Flowery.NET.Tests;

[Collection("LocalizationTests")]
public class ReviewEvidenceTests
{
    [AvaloniaFact]
    public async Task OtpTextBoxPasteNormalizesBeforeApplyingTheCodeLength()
    {
        var otp = new DaisyOtpInput();
        using var scope = new Scope(otp);
        var slot = otp.Children.OfType<TextBox>().First();
        await scope.Window.Clipboard!.SetTextAsync("123 456");
        await Paste(scope, slot);
        Assert.Equal("123456", otp.Value);
        Assert.Equal(new[] { "1", "2", "3", "4", "5", "6" }, otp.Children.OfType<TextBox>().Select(box => box.Text));
    }

    [AvaloniaTheory]
    [InlineData("rebuild")]
    [InlineData("reattach-before-completion")]
    [InlineData("reattach-after-completion")]
    public async Task OtpPendingPasteCannotUpdateDetachedOrRebuiltSlots(string change)
    {
        var otp = new DaisyOtpInput { Value = "98" };
        using var scope = new Scope(otp);
        var slot = otp.Children.OfType<TextBox>().First();
        slot.Focus();
        slot.SelectAll();
        var transfer = new DelayedText();
        await scope.Window.Clipboard!.SetDataAsync(transfer);
        slot.Paste();
        await transfer.Requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        if (change == "rebuild") otp.SeparatorInterval = 3;
        else
        {
            scope.Window.Content = null;
            if (change == "reattach-before-completion") scope.Window.Content = otp;
        }
        transfer.Completion.SetResult("123456");
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        scope.Flush();
        if (change == "reattach-after-completion") scope.Window.Content = otp;
        scope.Flush();
        Assert.Equal("98", otp.Value);
        Assert.Equal("9", slot.Text);
        Assert.Equal(new[] { "9", "8", "", "", "", "" }, otp.Children.OfType<TextBox>().Select(box => box.Text));
        Assert.Equal(1, transfer.Reads);
    }

    [AvaloniaFact]
    public async Task NumericPasteFiltersOnceAndReplacesTheActualSelection()
    {
        var number = new DaisyNumericUpDown { NumberBase = DaisyNumberBase.Hexadecimal, Value = 255 };
        using var scope = new Scope(number);
        var box = number.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "PART_DaisyTextBox");
        box.Focus();
        box.SelectAll();
        await scope.Window.Clipboard!.SetTextAsync("z1a");
        await Paste(scope, box);
        Assert.Equal("1A", box.Text);
    }

    [AvaloniaTheory]
    [InlineData("79228162514264337593543950335", -1, -1)]
    [InlineData("-79228162514264337593543950335", -1, -1)]
    [InlineData("79228162514264337593543950335", -1, 0)]
    [InlineData("0.1234567890123456789012345678", 1, -1)]
    public async Task NumericPastePreservesUnlimitedDecimalDigitsThroughCommit(string text, int integerDigits, int decimalPlaces)
    {
        var previousCulture = System.Globalization.CultureInfo.CurrentCulture;
        System.Globalization.CultureInfo.CurrentCulture = System.Globalization.CultureInfo.InvariantCulture;
        try
        {
            var number = new DaisyNumericUpDown
            {
                Value = 42, Minimum = decimal.MinValue, Maximum = decimal.MaxValue,
                MaxIntegerDigits = integerDigits, MaxDecimalPlaces = decimalPlaces
            };
            var next = new Button { Content = "Next" };
            using var scope = new Scope(new StackPanel { Children = { number, next } });
            var box = number.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "PART_DaisyTextBox");
            box.Focus();
            box.SelectAll();
            await scope.Window.Clipboard!.SetTextAsync(text);
            await Paste(scope, box);
            Assert.Equal(text, box.Text);
            next.Focus();
            scope.Flush();
            Assert.Equal(decimal.Parse(text, System.Globalization.CultureInfo.InvariantCulture), number.Value);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previousCulture; }
    }

    [AvaloniaFact]
    public async Task NumericPasteRejectsOverlengthInsteadOfChangingTheNumber()
    {
        var number = new DaisyNumericUpDown { NumberBase = DaisyNumberBase.ColorHex, Value = 255 };
        var next = new Button { Content = "Next" };
        using var scope = new Scope(new StackPanel { Children = { number, next } });
        var box = number.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "PART_DaisyTextBox");
        box.Focus();
        box.SelectAll();
        var original = box.Text;
        var transfer = new DelayedText();
        await scope.Window.Clipboard!.SetDataAsync(transfer);
        box.Paste();
        await transfer.Requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        transfer.Completion.SetResult("#12345678");
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        scope.Flush();
        Assert.Equal(original, box.Text);
        next.Focus();
        Assert.Equal(255, number.Value);
    }

    [AvaloniaTheory]
    [InlineData("10", "5", true, "15")]
    [InlineData("10", "5", false, "5")]
    [InlineData("98", "5", true, "98")]
    [InlineData("-98", "5", false, "-98")]
    [InlineData(null, "5", true, "5")]
    [InlineData("-5", "10", true, "5")]
    [InlineData("0.001", "0.001", true, "0.002")]
    public void NumericSpinButtonsUseControlIncrementAndBounds(string? value, string increment, bool increase, string expected)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var number = new DaisyNumericUpDown
        {
            Value = value is null ? null : decimal.Parse(value, culture),
            Increment = decimal.Parse(increment, culture), Minimum = -100, Maximum = 100
        };
        using var scope = new Scope(number);
        var name = increase ? "PART_IncreaseButton" : "PART_DecreaseButton";
        var button = number.GetVisualDescendants().OfType<Button>().Single(control => control.Name == name);
        button.PointerClick();
        Assert.Equal(decimal.Parse(expected, culture), number.Value);
    }

    [AvaloniaTheory]
    [InlineData("readonly")]
    [InlineData("detach")]
    [InlineData("template")]
    public async Task DelayedNumericPasteCannotWriteIntoAnInvalidatedEditor(string change)
    {
        var number = new DaisyNumericUpDown { NumberBase = DaisyNumberBase.Hexadecimal, Value = 255 };
        using var scope = new Scope(number);
        var box = number.GetVisualDescendants().OfType<TextBox>().Single(control => control.Name == "PART_DaisyTextBox");
        box.Focus();
        box.SelectAll();
        var transfer = new DelayedText();
        await scope.Window.Clipboard!.SetDataAsync(transfer);
        scope.Window.KeyPress(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
        scope.Window.KeyRelease(Key.V, RawInputModifiers.Control, PhysicalKey.V, "v");
        await transfer.Requested.Task.WaitAsync(TimeSpan.FromSeconds(5));
        switch (change)
        {
            case "readonly": number.IsReadOnly = true; break;
            case "detach": scope.Window.Content = null; break;
            case "template": number.Template = null; scope.Flush(); break;
        }
        transfer.Completion.SetResult("z1a");
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        scope.Flush();
        Assert.Equal(255, number.Value);
        Assert.DoesNotContain("1A", box.Text ?? string.Empty);
        Assert.Equal(1, transfer.Reads);
    }

    [AvaloniaFact]
    public async Task WeatherOlderCompletionCannotClearANewerLoadOrPublishAfterDetach()
    {
        var service = new WeatherService();
        var card = new DaisyWeatherCard { ShowForecast = false, ShowMetrics = false, Location = "Test" };
        using var scope = new Scope(card);
        card.WeatherService = service;
        Assert.Single(service.Requests);
        var newer = card.RefreshAsync();
        Assert.Equal(2, service.Requests.Count);
        service.Requests[0].SetResult(new CurrentWeather { Temperature = 10 });
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.True(card.IsLoading);
        service.Requests[1].SetResult(new CurrentWeather { Temperature = 20 });
        await newer;
        Assert.Equal(20, card.Temperature);
        Assert.False(card.IsLoading);
        card.ShowForecast = true;
        card.ShowMetrics = true;
        var detached = card.RefreshAsync();
        scope.Window.Content = null;
        service.Requests[2].SetResult(new CurrentWeather { Temperature = 30 });
        await detached;
        Assert.Equal(20, card.Temperature);
        Assert.False(card.IsLoading);
        Assert.Equal(0, service.ForecastCalls);
        Assert.Equal(0, service.MetricsCalls);
    }

    [AvaloniaFact]
    public async Task CountdownResumesRequestedTicksAfterReattachment()
    {
        var countdown = new DaisyCountdown { Value = 9, Interval = TimeSpan.FromMilliseconds(30), IsCountingDown = true };
        using var scope = new Scope(countdown);
        var changed = new TaskCompletionSource();
        countdown.PropertyChanged += (_, change) =>
        {
            if (change.Property == DaisyCountdown.ValueProperty) changed.TrySetResult();
        };
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        scope.Window.Content = null;
        var stoppedValue = countdown.Value;
        changed = new TaskCompletionSource();
        scope.Window.Content = countdown;
        await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(stoppedValue - 1, countdown.Value);
        countdown.Stop();
    }

    [AvaloniaFact]
    public void KanbanColumnKeepsParentEventsAndTaskTitleMeasurementAfterReattachment()
    {
        var board = new FlowKanban { AutoSaveAfterEdits = false };
        var data = new FlowKanbanColumnData { Title = "Tasks" };
        data.Tasks.Add(new FlowTask { Title = "Short" });
        var column = new FlowKanbanColumn { ParentKanban = board, ColumnData = data, Width = 220 };
        board.SetCompactLayout(false);
        using var scope = new Scope(column);
        scope.Window.Content = null;
        scope.Window.Content = column;
        scope.Flush();
        board.BoardSize = DaisySize.Large;
        Assert.Equal(DaisySize.Large, column.ColumnSize);
        var collapse = column.GetVisualDescendants().OfType<Button>().Single(button => button.Name == "PART_CollapseBtn");
        Assert.True(collapse.IsVisible);
        board.SetCompactLayout(true);
        scope.Flush();
        Assert.False(collapse.IsVisible);
        var card = new FlowTaskCard { Title = new string('W', 120), Width = 220 };
        scope.Window.Content = card;
        scope.Flush();
        scope.Window.Content = null;
        scope.Window.Content = card;
        scope.Flush();
        var title = card.GetVisualDescendants().OfType<TextBlock>().Single(text => text.Name == "PART_TitleText");
        Assert.Equal(card.Title, ToolTip.GetTip(title));
        card.Width = 3000;
        scope.Window.Width = 3100;
        scope.Flush();
        Assert.Null(ToolTip.GetTip(title));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClearingWeatherSourceInvalidatesThePendingRequest(bool clearService)
    {
        var service = new WeatherService();
        var card = new DaisyWeatherCard { ShowForecast = false, ShowMetrics = false, Location = "Test" };
        using var scope = new Scope(card);
        card.WeatherService = service;
        if (clearService) card.WeatherService = null!;
        else card.Location = string.Empty;
        service.Requests[0].SetResult(new CurrentWeather { Temperature = 30 });
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        Assert.Equal(0, card.Temperature);
        Assert.False(card.IsLoading);
    }

    [AvaloniaFact]
    public void WaveTextKeepsAChangedTextWhenStoppedAndReattached()
    {
        var text = new TextBlock { Text = "Before" };
        WaveTextBehavior.SetIsPerCharacter(text, true);
        WaveTextBehavior.SetIsEnabled(text, true);
        using var scope = new Scope(text);
        text.Text = "After";
        scope.Flush();
        WaveTextBehavior.SetIsEnabled(text, false);
        Assert.Equal("After", text.Text);
        scope.Window.Content = null;
        scope.Window.Content = text;
        scope.Flush();
        Assert.Equal("After", text.Text);
    }

    [AvaloniaFact]
    public async Task AssigneeOptionsRefreshPreservesRealizedSelectionAndCriteria()
    {
        var data = new FlowKanbanData();
        var column = new FlowKanbanColumnData { Title = "Todo" };
        column.Tasks.Add(new FlowTask { Title = "Task", AssigneeId = "ada", Assignee = "Ada" });
        data.Columns.Add(column);
        var board = new FlowKanban { Board = data, AutoSaveAfterEdits = false, CurrentView = FlowKanbanView.Board };
        using var scope = new Scope(board);
        await board.RefreshAssigneesAsync();
        var criteria = new FlowKanbanFilterCriteria { AssigneeIds = new List<string> { "ada" } };
        board.FilterCriteria = criteria;
        scope.Flush();
        var select = board.GetVisualDescendants().OfType<ComboBox>()
            .Single(control => ReferenceEquals(control.ItemsSource, board.AssigneeFilterOptions));
        Assert.Equal("ada", Assert.IsType<FlowTaskAssigneeOption>(select.SelectedItem).Id);
        await board.RefreshAssigneesAsync();
        scope.Flush();
        Assert.Equal(new[] { "ada" }, criteria.AssigneeIds);
        Assert.Equal("ada", Assert.IsType<FlowTaskAssigneeOption>(select.SelectedItem).Id);
    }

    [AvaloniaTheory]
    [InlineData("12.3", "'kg '0.0' net'", "kg 12.3 net", "12.4")]
    [InlineData("-12.3", "0.0;(0.0)", "(12.3)", "-12.2")]
    [InlineData("1200000", "0,,", "1", "2200000")]
    [InlineData("0.123", "0.0‰", "123.0‰", "0.1231")]
    [InlineData("1234.5", "0.00E+00", "1.23E+03", "1234.5")]
    [InlineData("12.3", "'v2 '0.0", "v2 12.3", "12.3")]
    public void NumberFlowComplexFormatsPreserveDisplayAndDefinedEditing(
        string value, string format, string display, string expected)
    {
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var number = new DaisyNumberFlow
        {
            Value = decimal.Parse(value, culture), FormatString = format,
            Culture = culture, AllowDigitSelection = true, Duration = TimeSpan.Zero
        };
        using var scope = new Scope(number);
        Assert.Equal(display, string.Concat(number.Parts.Select(part => part.Character)));
        var digit = number.GetVisualDescendants().OfType<Border>()
            .Single(control => control.Name == "DigitContainer" && control.Tag is int index && index == 0);
        var position = digit.TranslatePoint(new Point(digit.Bounds.Width / 2, digit.Bounds.Height / 2), scope.Window)!.Value;
        scope.Window.MouseDown(position, MouseButton.Left);
        scope.Window.MouseUp(position, MouseButton.Left);
        scope.Window.KeyPress(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
        scope.Window.KeyRelease(Key.Up, RawInputModifiers.None, PhysicalKey.ArrowUp, null);
        Assert.Equal(decimal.Parse(expected, culture), number.Value);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void DialogOpenBeforeHostIsSafeAndDoesNotRestoreAnUnavailableTarget(bool removeTarget)
    {
        var launcher = new Button { Content = "Open" };
        var panel = new StackPanel { Children = { launcher } };
        using var scope = new Scope(panel);
        var field = new TextBox();
        var dialog = new FlowKanbanDialogBase { Content = field, IsOpen = true };
        Assert.False(dialog.IsAttachedToVisualTree());
        dialog.ApplySmartSizing(scope.Window);
        dialog.IsOpen = false;
        launcher.Focus();
        dialog.IsOpen = true;
        scope.Flush();
        try
        {
            Assert.Same(field, scope.Window.FocusManager.GetFocusedElement());
            if (removeTarget) panel.Children.Remove(launcher);
            else launcher.IsEnabled = false;
        }
        finally { dialog.IsOpen = false; }
        Assert.NotSame(launcher, scope.Window.FocusManager.GetFocusedElement());
    }

    [AvaloniaFact]
    public void SelectedThemeLabelsHaveReadableContrastInEveryRegisteredPalette()
    {
        var previous = DaisyThemeManager.CurrentThemeName;
        var dropdown = new DaisyThemeDropdown();
        using var scope = new Scope(dropdown);
        var themes = DaisyThemeManager.AvailableThemes.ToArray();
        try
        {
            foreach (var theme in themes)
            {
                Assert.True(DaisyThemeManager.ApplyTheme(theme.Name));
                dropdown.SelectedTheme = theme.Name;
                dropdown.IsDropDownOpen = true;
                scope.Flush();
                var item = Assert.IsType<ComboBoxItem>(dropdown.ContainerFromIndex(dropdown.SelectedIndex));
                var label = item.GetVisualDescendants().OfType<TextBlock>().OrderByDescending(text => text.Text?.Length ?? 0).First();
                var foreground = Assert.IsAssignableFrom<ISolidColorBrush>(label.Foreground).Color;
                var presenter = item.GetVisualDescendants().OfType<ContentPresenter>()
                    .Single(control => control.Name == "PART_ContentPresenter");
                var background = Assert.IsAssignableFrom<ISolidColorBrush>(presenter.Background).Color;
                var light = Luminance(foreground);
                var dark = Luminance(background);
                var ratio = (Math.Max(light, dark) + .05) / (Math.Min(light, dark) + .05);
                Assert.True(ratio >= 4.5, $"{theme.Name}: selected label {foreground}, background {background}, contrast {ratio:F2}");
                dropdown.IsDropDownOpen = false;
            }
        }
        finally { dropdown.IsDropDownOpen = false; DaisyThemeManager.ApplyTheme(previous ?? "Light"); }
    }

    private static double Luminance(Color color)
    {
        static double Linear(byte value) => value <= 10 ? value / 3294.6 : Math.Pow((value / 255d + .055) / 1.055, 2.4);
        return .2126 * Linear(color.R) + .7152 * Linear(color.G) + .0722 * Linear(color.B);
    }

    private static async Task Paste(Scope scope, TextBox box)
    {
        var changed = new TaskCompletionSource();
        EventHandler<TextChangedEventArgs> handler = (_, _) => changed.TrySetResult();
        box.TextChanged += handler;
        try
        {
            box.Paste();
            await changed.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            scope.Flush();
        }
        finally { box.TextChanged -= handler; }
    }

    private sealed class Scope : IDisposable
    {
        private readonly bool? _motion = DaisyAccessibility.ReducedMotionOverride;
        public Window Window { get; }
        public Scope(Control content)
        {
            DaisyAccessibility.ReducedMotionOverride = false;
            Window = new Window { Width = 800, Height = 600, Content = content };
            Window.Show();
            Flush();
        }
        public void Flush() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() { Window.Close(); DaisyAccessibility.ReducedMotionOverride = _motion; }
    }

    private sealed class DelayedText : IAsyncDataTransfer, IAsyncDataTransferItem
    {
        public TaskCompletionSource Requested { get; } = new();
        public TaskCompletionSource<object?> Completion { get; } = new();
        public int Reads { get; private set; }
        public IReadOnlyList<DataFormat> Formats => new[] { DataFormat.Text };
        public IReadOnlyList<IAsyncDataTransferItem> Items => new IAsyncDataTransferItem[] { this };
        public Task<object?> TryGetRawAsync(DataFormat format)
        {
            Reads++;
            Requested.TrySetResult();
            return Completion.Task;
        }
        public void Dispose() { }
    }

    private sealed class WeatherService : IWeatherService
    {
        public List<TaskCompletionSource<CurrentWeather>> Requests { get; } = new();
        public int ForecastCalls { get; private set; }
        public int MetricsCalls { get; private set; }
        public Task<CurrentWeather> GetCurrentWeatherAsync(string location, CancellationToken cancellationToken = default)
        {
            var request = new TaskCompletionSource<CurrentWeather>();
            Requests.Add(request);
            return request.Task;
        }
        public Task<IEnumerable<ForecastDay>> GetForecastAsync(string location, int days = 5, CancellationToken cancellationToken = default)
        {
            ForecastCalls++;
            return Task.FromResult<IEnumerable<ForecastDay>>(Array.Empty<ForecastDay>());
        }
        public Task<WeatherMetrics> GetMetricsAsync(string location, CancellationToken cancellationToken = default)
        {
            MetricsCalls++;
            return Task.FromResult(new WeatherMetrics());
        }
    }
}
