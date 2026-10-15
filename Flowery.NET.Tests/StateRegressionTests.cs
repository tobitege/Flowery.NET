using System.Globalization;
using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.ColorPicker;
using Xunit;

namespace Flowery.NET.Tests;

public class StateRegressionTests
{
    [AvaloniaTheory]
    [InlineData(DaisyNumberBase.Decimal)]
    [InlineData(DaisyNumberBase.IPAddress)]
    public void ReadOnlyNumericInputRejectsSpinAndClear(DaisyNumberBase numberBase)
    {
        var control = new DaisyNumericUpDown
        {
            Value = 100, IsReadOnly = true, NumberBase = numberBase, ShowClearButton = true
        };
        var window = Show(control);
        try
        {
            foreach (var name in new[] { "PART_IncreaseButton", "PART_DecreaseButton", "PART_ClearButton" })
            {
                var button = control.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
                Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(button)).Invoke();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(100m, control.Value);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ColorEditorExternalHslUpdatesComponentsAndEventOnce()
    {
        var control = new DaisyColorEditor { ShowAlphaChannel = false };
        var window = Show(control);
        try
        {
            var changes = new List<Color>();
            control.ColorChanged += (_, args) => changes.Add(args.Color);
            control.HslColor = new Flowery.Controls.ColorPicker.HslColor(120, 1, .5);
            Assert.Equal(Colors.Lime, control.Color);
            Assert.Equal(0, control.Red);
            Assert.Equal(255, control.Green);
            Assert.Equal(0, control.Blue);
            Assert.Equal("#00FF00", control.HexValue);
            Assert.Equal(new[] { Colors.Lime }, changes);
            var green = control.GetVisualDescendants().OfType<NumericUpDown>()
                .Single(input => input.Name == "PART_GreenInput");
            Assert.Equal(255m, green.Value);
            control.HexValue = "#123";
            Assert.Equal(Color.FromRgb(0x11, 0x22, 0x33), control.Color);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ColorEditorPrimaryModeAndVisibilityRemainIndependent()
    {
        var editor = new DaisyColorEditor();
        var window = Show(editor);
        try
        {
            var rgb = editor.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "PART_RgbSection");
            var hsl = editor.GetVisualDescendants().OfType<StackPanel>().Single(panel => panel.Name == "PART_HslSection");
            foreach (var mode in new[] { ColorEditingMode.Rgb, ColorEditingMode.Hsl })
            foreach (var showRgb in new[] { true, false })
            foreach (var showHsl in new[] { true, false })
            {
                editor.EditingMode = mode;
                editor.ShowRgbSliders = showRgb;
                editor.ShowHslSliders = showHsl;
                Flush(window);
                Assert.Equal(showRgb, rgb.IsVisible);
                Assert.Equal(showHsl, hsl.IsVisible);
                Assert.Equal(mode == ColorEditingMode.Rgb ? 1 : 2, Grid.GetRow(rgb));
                Assert.Equal(mode == ColorEditingMode.Hsl ? 1 : 2, Grid.GetRow(hsl));
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void CheckBoxMixedStateHasADistinctMark()
    {
        var control = new DaisyCheckBox { IsThreeState = true, IsChecked = true };
        var window = Show(control);
        try
        {
            var mark = control.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>()
                .Single(path => path.Name == "CheckMark");
            var checkedGeometry = mark.Data;
            foreach (var state in new bool?[] { null, false, null, true })
            {
                control.IsChecked = state;
                Flush(window);
                Assert.Equal(state != false, mark.IsVisible);
                if (state is null) Assert.NotSame(checkedGeometry, mark.Data);
                if (state == true) Assert.Same(checkedGeometry, mark.Data);
            }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void DateTimelineLocaleRefreshesHeaderAndDisabledDatesCannotBeConfirmed()
    {
        var date = new DateTime(2026, 10, 15);
        var timeline = new DaisyDateTimeline
        {
            FirstDate = date.AddMonths(-2), LastDate = date.AddMonths(2), SelectedDate = date,
            Locale = CultureInfo.GetCultureInfo("en-US")
        };
        var window = Show(timeline);
        try
        {
            var english = timeline.HeaderText;
            timeline.Locale = CultureInfo.GetCultureInfo("de-DE");
            Assert.NotEqual(english, timeline.HeaderText);
            var confirmed = 0;
            timeline.DateConfirmed += (_, _) => confirmed++;
            timeline.DisableStrategy = DateDisableStrategy.All;
            timeline.GoToDate(date.AddDays(1));
            Assert.Equal(date, timeline.SelectedDate);
            timeline.Focus();
            Press(window, Key.Enter);
            Press(window, Key.PageDown);
            Assert.Equal(0, confirmed);
            Assert.Equal(date, timeline.SelectedDate);
            timeline.DisableStrategy = DateDisableStrategy.BeforeDate;
            timeline.DisableBeforeDate = date.AddMonths(1).AddDays(2);
            Press(window, Key.PageDown);
            Assert.Equal(timeline.DisableBeforeDate, timeline.SelectedDate);
            Press(window, Key.Enter);
            Assert.Equal(1, confirmed);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(2000, DayOfWeek.Monday, 53)]
    [InlineData(2000, DayOfWeek.Sunday, 54)]
    [InlineData(2026, DayOfWeek.Monday, 53)]
    public void ContributionGraphIncludesEveryDayOfYear(int year, DayOfWeek firstDay, int weeks)
    {
        var graph = new DaisyContributionGraph { Year = year, StartDayOfWeek = firstDay };
        var window = Show(graph);
        try
        {
            Assert.Equal(weeks, graph.WeekCount);
            Assert.Equal(weeks * 7, graph.Cells.Count);
            var dates = graph.Cells.Where(cell => cell.Date?.Year == year).Select(cell => cell.Date).ToArray();
            Assert.Equal(DateTime.IsLeapYear(year) ? 366 : 365, dates.Length);
            Assert.Contains(new DateTime(year, 12, 31), dates);
            Assert.Equal(weeks, graph.GetVisualDescendants().OfType<UniformGrid>().Single().Columns);
            Assert.Equal(weeks * graph.CellStepX, graph.GridWidth);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ClockTimerNotifiesRemainingTimeOnDurationTickAndReset()
    {
        var clock = new DaisyClock { Mode = ClockMode.Timer, TimerDuration = TimeSpan.FromSeconds(3) };
        var changes = new List<TimeSpan>();
        clock.PropertyChanged += (_, args) =>
        {
            if (args.Property == DaisyClock.TimerRemainingProperty) changes.Add(clock.TimerRemaining);
        };
        var window = Show(clock);
        try
        {
            clock.TimerDuration = TimeSpan.FromSeconds(2);
            Assert.Equal(new[] { TimeSpan.FromSeconds(2) }, changes);
            var tick = new TaskCompletionSource();
            clock.TimerTick += (_, _) => { clock.Pause(); tick.TrySetResult(); };
            clock.Start();
            await tick.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Contains(TimeSpan.FromSeconds(1), changes);
            clock.Reset();
            Assert.Equal(TimeSpan.FromSeconds(2), changes.Last());
        }
        finally { clock.Pause(); window.Close(); }
    }

    [AvaloniaFact]
    public void TextRotateExternalIndexChangesVisibleContainerAfterReattachment()
    {
        var control = new DaisyTextRotate { ItemsSource = new[] { "First", "Second" }, IsPaused = true };
        var window = Show(control);
        try
        {
            control.CurrentIndex = 1;
            Flush(window);
            Assert.Equal(0, control.ContainerFromIndex(0)!.Opacity);
            Assert.Equal(1, control.ContainerFromIndex(1)!.Opacity);
            window.Content = null;
            window.Content = control;
            Flush(window);
            control.CurrentIndex = 0;
            Assert.Equal(1, control.ContainerFromIndex(0)!.Opacity);
            Assert.Equal(0, control.ContainerFromIndex(1)!.Opacity);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void SuppressedThemeIsAppliedWhenSuppressionEndsAndDropdownPropertySelectsTheme()
    {
        var previousTheme = DaisyThemeManager.CurrentThemeName;
        var previousSuppression = DaisyThemeManager.SuppressThemeApplication;
        try
        {
            DaisyThemeManager.SuppressThemeApplication = false;
            Assert.True(DaisyThemeManager.ApplyTheme("Dark"));
            Assert.True(DaisyThemeManager.ApplyTheme("Light"));
            var before = Application.Current!.Resources.MergedDictionaries.Last();
            DaisyThemeManager.SuppressThemeApplication = true;
            Assert.True(DaisyThemeManager.ApplyTheme("Dark"));
            Assert.Same(before, Application.Current.Resources.MergedDictionaries.Last());
            DaisyThemeManager.SuppressThemeApplication = false;
            Assert.True(DaisyThemeManager.ApplyTheme("Dark"));
            Assert.NotSame(before, Application.Current.Resources.MergedDictionaries.Last());
            var dropdown = new DaisyThemeDropdown { SelectedTheme = "Light" };
            Assert.Equal("Light", Assert.IsType<ThemePreviewInfo>(dropdown.SelectedItem).Name);
            Assert.Equal("Light", DaisyThemeManager.CurrentThemeName);
        }
        finally
        {
            DaisyThemeManager.SuppressThemeApplication = false;
            if (previousTheme is { } theme) DaisyThemeManager.ApplyTheme(theme);
            DaisyThemeManager.SuppressThemeApplication = previousSuppression;
        }
    }

    private static Window Show(Control control)
    {
        var window = new Window { Content = control, Width = 1200, Height = 900 };
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
}
