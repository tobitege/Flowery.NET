using System.Globalization;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Kanban.Controls;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

public class P1RegressionTests
{
    [AvaloniaTheory]
    [InlineData("1.23", "N2", "en-US", 0, Key.Up, "1.24")]
    [InlineData("1.23", "N2", "de-DE", 0, Key.Down, "1.22")]
    [InlineData("0.1234", "P2", "en-US", 0, Key.Up, "0.1235")]
    [InlineData("12.34", "C2", "de-DE", 0, Key.Up, "12.35")]
    [InlineData("12.34", "F2", "en-US", 0, Key.Up, "12.35")]
    [InlineData("12.34", "G", "en-US", 0, Key.Up, "12.35")]
    [InlineData("28.34", "000.00", "en-US", 4, Key.Up, "128.34")]
    [InlineData("8", "000", "en-US", 2, Key.D2, "208")]
    [InlineData("-12", "N0", "en-US", 0, Key.D3, "-13")]
    [InlineData("-0.01", "N2", "en-US", 0, Key.Up, "0")]
    [InlineData("123000", "0,", "en-US", 0, Key.Up, "124000")]
    [InlineData("0.123", "0.0%", "en-US", 0, Key.Up, "0.124")]
    public void NumberFlow_EditingUsesTheClickedDisplayedPlace(
        string initial, string format, string culture, int digitIndex, Key key, string expected)
    {
        var control = new DaisyNumberFlow
        {
            Value = decimal.Parse(initial, CultureInfo.InvariantCulture),
            FormatString = format,
            Culture = CultureInfo.GetCultureInfo(culture),
            AllowDigitSelection = true,
            Duration = TimeSpan.Zero
        };
        var window = Show(control);
        try
        {
            Assert.True(control.Focus());
            var digit = control.GetVisualDescendants().OfType<Border>()
                .Single(border => border.Name == "DigitContainer"
                    && border.Tag is int index && index == digitIndex);
            var point = digit.TranslatePoint(new Point(digit.Bounds.Width / 2, digit.Bounds.Height / 2), window);
            Assert.NotNull(point);
            window.MouseMove(point.Value);
            window.MouseDown(point.Value, MouseButton.Left);
            window.MouseUp(point.Value, MouseButton.Left);
            Assert.Equal(digitIndex, control.SelectedDigitIndex);
            Press(window, key);
            Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), control.Value);
            Assert.Equal(control.Value!.Value.ToString(format, control.Culture),
                string.Concat(control.Parts.Select(part => part.Character)));
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, "-15")]
    [InlineData(true, "15")]
    public void NumberFlow_ReplacingNegativeDigitsHonorsBounds(bool wrap, string expected)
    {
        var control = new DaisyNumberFlow
        {
            Value = -12, FormatString = "000", AllowDigitSelection = true,
            Minimum = -15, Maximum = 15, WrapAround = wrap, Duration = TimeSpan.Zero
        };
        var window = Show(control);
        try
        {
            control.Focus();
            Press(window, Key.D9);
            Assert.Equal(decimal.Parse(expected, CultureInfo.InvariantCulture), control.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void NumberFlow_ArrowKeysUseStepWithoutDigitSelection()
    {
        var control = new DaisyNumberFlow { Value = 1.25m, Step = .25m, ShowControls = true };
        var window = Show(control);
        try
        {
            control.Focus();
            Press(window, Key.Up);
            Assert.Equal(1.5m, control.Value);
            Press(window, Key.Down);
            Assert.Equal(1.25m, control.Value);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Carousel_DeclarativeSlidesSurviveTemplateAndAttachmentChanges()
    {
        var first = new TextBlock { Text = "First slide" };
        var second = new TextBlock { Text = "Second slide" };
        var source = new StackPanel { Children = { first, second } };
        var carousel = new DaisyCarousel { Content = source };
        var window = Show(carousel);
        try
        {
            Assert.Equal(2, carousel.ItemCount);
            Assert.Contains(first, carousel.GetVisualDescendants());
            Invoke(carousel.GetVisualDescendants().OfType<Button>()
                .Single(button => button.Name == "PART_NextButton"));
            Flush(window);
            Assert.Equal(1, carousel.SelectedIndex);
            Assert.Contains(second, carousel.GetVisualDescendants());
            var template = carousel.Template;
            carousel.Template = null;
            carousel.ApplyTemplate();
            carousel.Template = template;
            Flush(window);
            Assert.Equal(2, carousel.ItemCount);
            Assert.Contains(second, carousel.GetVisualDescendants());
            window.Content = null;
            window.Content = carousel;
            Flush(window);
            Assert.Equal(2, carousel.ItemCount);
            Assert.Contains(second, carousel.GetVisualDescendants());
            var replacement = new TextBlock { Text = "Replacement" };
            carousel.Content = replacement;
            Flush(window);
            Assert.Same(replacement, carousel.Content);
            Assert.Equal(1, carousel.ItemCount);
            Assert.Contains(replacement, carousel.GetVisualDescendants());
            carousel.Content = null;
            Flush(window);
            Assert.Equal(0, carousel.ItemCount);
            Assert.DoesNotContain(replacement, carousel.GetVisualDescendants());
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void Carousel_SingleControlContentRemainsVisible()
    {
        var slide = new TextBlock { Text = "Only slide" };
        var carousel = new DaisyCarousel { Content = slide };
        var window = Show(carousel);
        try
        {
            Assert.Same(slide, carousel.Content);
            Assert.Equal(1, carousel.ItemCount);
            Assert.Contains(slide, carousel.GetVisualDescendants());
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(false, false, "Original")]
    [InlineData(true, false, "Added")]
    [InlineData(true, true, null)]
    public async Task TaskEditor_SavesCurrentTagSelectionOnlyWhenConfirmed(bool save, bool clear, string? expected)
    {
        var task = new FlowTask { Title = "Tag test", Tags = "Original" };
        var window = Show(new Border());
        FlowTaskEditorDialog? dialog = null;
        try
        {
            var completion = FlowTaskEditorDialog.ShowAsync(task, window, new[] { "Original", "Added" });
            Flush(window);
            dialog = window.GetVisualDescendants().OfType<FlowTaskEditorDialog>().Single();
            dialog.GetVisualDescendants().OfType<DaisyTabs>().Single().SelectedIndex = 1;
            Flush(window);
            var picker = dialog.GetVisualDescendants().OfType<DaisyTagPicker>().Single();
            Invoke(picker.GetVisualDescendants().OfType<DaisyButton>()
                .Single(button => Equals(button.Content, "Original")));
            Flush(window);
            if (!clear)
            {
                Invoke(picker.GetVisualDescendants().OfType<DaisyButton>()
                    .Single(button => Equals(button.Content, "Added")));
            }
            Assert.Equal("Original", task.Tags);
            Invoke(dialog.GetVisualDescendants().OfType<DaisyButton>().Single(button =>
                Equals(button.Content, FloweryLocalization.GetString(save ? "Common_Save" : "Common_Cancel"))));
            Assert.Equal(save, await completion);
            Assert.Equal(expected, task.Tags);
        }
        finally
        {
            if (dialog?.IsOpen == true) dialog.IsOpen = false;
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Kanban_BulkDateFlyoutAppliesSelectedDateAndClearsOnlySelectedTasks()
    {
        var previousStorage = StateStorageProvider.Instance;
        StateStorageProvider.Configure(new MemoryStorage());
        Window? window = null;
        try
        {
            var selected = new FlowTask { Title = "Selected", PlannedEndDate = new DateTime(2026, 1, 1) };
            var untouched = new FlowTask { Title = "Unselected", PlannedEndDate = new DateTime(2026, 1, 2) };
            var column = new FlowKanbanColumnData { Title = "Tasks" };
            column.Tasks.Add(selected);
            column.Tasks.Add(untouched);
            var board = new FlowKanbanData();
            board.Columns.Add(column);
            var control = new FlowKanban { Board = board, AutoSaveAfterEdits = false };
            window = Show(control);
            selected.IsSelected = true;
            Flush(window);
            var opener = control.GetVisualDescendants().OfType<DaisyButton>().Single(button =>
                AutomationProperties.GetName(button) == FloweryLocalization.GetString("Kanban_Bulk_DueDate"));
            var flyout = Assert.IsType<Flyout>(opener.Flyout);
            Invoke(opener);
            Flush(window);
            var content = Assert.IsAssignableFrom<Control>(flyout.Content);
            var picker = content.GetVisualDescendants().OfType<DatePicker>().Single();
            var date = new DateTimeOffset(2026, 10, 22, 0, 0, 0, TimeSpan.Zero);
            picker.SelectedDate = date;
            Invoke(content.GetVisualDescendants().OfType<DaisyButton>().Single(button =>
                Equals(button.Content, FloweryLocalization.GetString("Common_Apply"))));
            Assert.Equal(date.Date, selected.PlannedEndDate);
            Assert.Equal(new DateTime(2026, 1, 2), untouched.PlannedEndDate);
            Invoke(content.GetVisualDescendants().OfType<DaisyButton>().Single(button =>
                Equals(button.Content, FloweryLocalization.GetString("Common_Clear"))));
            Assert.Null(selected.PlannedEndDate);
            Assert.Equal(new DateTime(2026, 1, 2), untouched.PlannedEndDate);
            flyout.Hide();
        }
        finally
        {
            window?.Close();
            StateStorageProvider.Configure(previousStorage);
        }
    }

    private static Window Show(Control content)
    {
        var window = new Window { Width = 1200, Height = 900, Content = content };
        window.Show();
        Flush(window);
        return window;
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private static void Invoke(Button button)
    {
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(button)).Invoke();
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class MemoryStorage : IStateStorage
    {
        private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);
        public IReadOnlyList<string> LoadLines(string key) => _values.TryGetValue(key, out var value) ? value : [];
        public void SaveLines(string key, IEnumerable<string> lines) => _values[key] = [.. lines];
        public void Delete(string key) => _values.Remove(key);
        public void Rename(string sourceKey, string targetKey)
        {
            _values[targetKey] = _values[sourceKey];
            _values.Remove(sourceKey);
        }
        public IEnumerable<string> GetKeys(string prefix) => _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    }
}
