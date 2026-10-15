using System.Collections.ObjectModel;
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
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Controls.ColorPicker;
using Flowery.Controls.Custom;
using Flowery.Helpers;
using Flowery.Localization;
using Flowery.NET.Kanban.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class AccessibilityRegressionTests
{
    [AvaloniaFact]
    public void RatingRangeProviderAndKeyboardRespectBoundsAndReadOnly()
    {
        var rating = new DaisyRating { Precision = RatingPrecision.Half, Value = 2 };
        using var scope = new Scope(rating);
        var range = Assert.IsAssignableFrom<IRangeValueProvider>(ControlAutomationPeer.CreatePeerForElement(rating));
        Assert.Equal(0, range.Minimum);
        Assert.Equal(5, range.Maximum);
        range.SetValue(3.5);
        Assert.Equal(3.5, rating.Value);
        Assert.Throws<ArgumentOutOfRangeException>(() => range.SetValue(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => range.SetValue(6));
        Assert.True(rating.Focus());
        Press(scope.Window, Key.Left);
        Assert.Equal(3, rating.Value);
        rating.IsReadOnly = true;
        Assert.True(range.IsReadOnly);
        Assert.Throws<InvalidOperationException>(() => range.SetValue(1));
        Press(scope.Window, Key.Right);
        Assert.Equal(3, rating.Value);
        rating.IsReadOnly = false;
        rating.IsEnabled = false;
        Assert.Throws<InvalidOperationException>(() => range.SetValue(1));
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void ProgressExposesReadOnlyRange(bool radial)
    {
        RangeBase progress = radial ? new DaisyRadialProgress() : new DaisyProgress();
        progress.Minimum = 10;
        progress.Maximum = 50;
        progress.Value = 30;
        using var scope = new Scope(progress);
        var range = Assert.IsAssignableFrom<IRangeValueProvider>(ControlAutomationPeer.CreatePeerForElement(progress));
        Assert.True(range.IsReadOnly);
        Assert.Equal(30, range.Value);
        Assert.Equal(10, range.Minimum);
        Assert.Equal(50, range.Maximum);
        Assert.Throws<InvalidOperationException>(() => range.SetValue(40));
        progress.Value = 40;
        Assert.Equal(40, range.Value);
    }

    [AvaloniaFact]
    public void ColorSelectorsExposeValuesAndKeyboardChangesRaiseColorEvents()
    {
        var slider = new DaisyColorSlider { Channel = ColorSliderChannel.Red, Color = Colors.Black };
        var wheel = new DaisyColorWheel { Color = Colors.Red, Width = 100, Height = 100 };
        var grid = new DaisyColorGrid();
        using var scope = new Scope(new StackPanel { Children = { slider, wheel, grid } });
        var changes = 0;
        slider.ColorChanged += (_, _) => changes++;
        var range = Assert.IsAssignableFrom<IRangeValueProvider>(ControlAutomationPeer.CreatePeerForElement(slider));
        range.SetValue(128);
        Assert.Equal(128, slider.Color.R);
        Assert.Equal(1, changes);
        slider.Focus();
        Press(scope.Window, Key.End);
        Assert.Equal(255, slider.Color.R);
        foreach (var selector in new Control[] { wheel, grid })
        {
            var value = Assert.IsAssignableFrom<IValueProvider>(ControlAutomationPeer.CreatePeerForElement(selector));
            value.SetValue("#00FF00");
            Assert.Equal(Colors.Lime, Avalonia.Media.Color.Parse(value.Value!));
            Assert.Throws<ArgumentException>(() => value.SetValue("invalid color"));
            selector.Focus();
            Press(scope.Window, Key.Right);
            Assert.NotEqual(Colors.Lime, Avalonia.Media.Color.Parse(value.Value!));
            selector.IsEnabled = false;
            Assert.Throws<InvalidOperationException>(() => value.SetValue("#FF0000"));
        }
    }

    [AvaloniaFact]
    public void InputAndMaskMetadataRemainLiveAndExplicitNamesTakePrecedence()
    {
        var input = new DaisyInput { Label = "Customer", HelperText = "Enter a name", IsRequired = true };
        var mask = new DaisyMaskInput { Label = "Code", HintText = "Three digits", Mask = "000", IsRequired = true };
        using var scope = new Scope(new StackPanel { Children = { input, mask } });
        foreach (var field in new TextBox[] { input, mask })
        {
            field.Focus();
            Assert.Same(field, scope.Window.FocusManager!.GetFocusedElement());
            var peer = ControlAutomationPeer.CreatePeerForElement(field);
            Assert.Equal(field == input ? "Customer" : "Code", peer.GetName());
            Assert.Equal(field == input ? "Enter a name" : "Three digits", peer.GetHelpText());
            Assert.True(AutomationProperties.GetIsRequiredForForm(field));
            AutomationProperties.SetName(field, "Explicit");
            Assert.Equal("Explicit", peer.GetName());
            AutomationProperties.SetHelpText(field, "Explicit help");
            Assert.Equal("Explicit help", peer.GetHelpText());
        }
        input.Label = "Changed";
        input.IsRequired = false;
        input.ClearValue(AutomationProperties.NameProperty);
        Assert.Equal("Changed", ControlAutomationPeer.CreatePeerForElement(input).GetName());
        Assert.False(AutomationProperties.GetIsRequiredForForm(input));
        var inputPanel = scope.Window.Content;
        scope.Window.Content = null;
        scope.Window.Content = inputPanel;
        scope.Flush();
        Assert.Equal("Changed", ControlAutomationPeer.CreatePeerForElement(input).GetName());
    }

    [AvaloniaFact]
    public void PasswordAndOtpActionNamesFollowCultureAndPreserveExplicitGroupName()
    {
        var previous = FloweryLocalization.CurrentCulture;
        var password = new DaisyPasswordBox();
        var otp = new DaisyOtpInput();
        using var scope = new Scope(new StackPanel { Children = { password, otp } });
        try
        {
            FloweryLocalization.SetCulture("en-US");
            var reveal = password.GetVisualDescendants().OfType<Button>().Single();
            var english = ControlAutomationPeer.CreatePeerForElement(reveal).GetName();
            var slot = otp.Children.OfType<TextBox>().First();
            var englishSlot = ControlAutomationPeer.CreatePeerForElement(slot).GetName();
            FloweryLocalization.SetCulture("de-DE");
            Assert.NotEqual(english, ControlAutomationPeer.CreatePeerForElement(reveal).GetName());
            Assert.NotEqual(englishSlot, ControlAutomationPeer.CreatePeerForElement(slot).GetName());
            AutomationProperties.SetName(otp, "PIN");
            FloweryLocalization.SetCulture("fr-FR");
            Assert.Contains("PIN", ControlAutomationPeer.CreatePeerForElement(slot).GetName());
            var panel = scope.Window.Content;
            scope.Window.Content = null;
            scope.Window.Content = panel;
            scope.Flush();
            Assert.Contains("PIN", ControlAutomationPeer.CreatePeerForElement(slot).GetName());
        }
        finally { FloweryLocalization.SetCulture(previous); }
    }

    [AvaloniaFact]
    public void SidebarFavoriteAutomationChangesModelAndReportsToggleState()
    {
        var item = new SidebarItem { Id = "entry", Name = "Entry", ShowFavoriteIcon = true };
        var sidebar = new FloweryComponentSidebar
        {
            Categories = new ObservableCollection<SidebarCategory>
            {
                new() { Name = "Group", Items = new ObservableCollection<SidebarItem> { item } }
            }
        };
        using var scope = new Scope(sidebar);
        var button = sidebar.GetVisualDescendants().OfType<Button>().Single(control => control.Classes.Contains("sidebar-favorite-btn"));
        var toggle = Assert.IsAssignableFrom<IToggleProvider>(ControlAutomationPeer.CreatePeerForElement(button));
        Assert.Equal(ToggleState.Off, toggle.ToggleState);
        toggle.Toggle();
        Assert.True(item.IsFavorite);
        Assert.Equal(ToggleState.On, toggle.ToggleState);
        item.IsFavorite = false;
        Assert.Equal(ToggleState.Off, toggle.ToggleState);
    }

    [AvaloniaFact]
    public void SlideToConfirmSupportsKeyboardAndInvokeWithoutDuplicateCompletion()
    {
        var slide = new DaisySlideToConfirm { AutoReset = false };
        using var scope = new Scope(slide);
        var completed = 0;
        slide.SlideCompleted += (_, _) => completed++;
        slide.Focus();
        Press(scope.Window, Key.Space);
        var invoke = Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(slide));
        invoke.Invoke();
        Assert.Equal(1, completed);
        slide.Reset();
        invoke.Invoke();
        Assert.Equal(2, completed);
    }

    [AvaloniaFact]
    public void DialogInitialFocusUsesBodyTabOrderAndSkipsUnavailableFields()
    {
        var hidden = new TextBox { IsVisible = false };
        var disabled = new TextBox { IsEnabled = false };
        var first = new TextBox();
        var second = new TextBox();
        KeyboardNavigation.SetTabIndex(first, 2);
        KeyboardNavigation.SetTabIndex(second, 1);
        using var scope = new Scope(new Button());
        var dialog = new FlowKanbanDialogBase
        {
            Content = FlowKanbanDialogBase.CreateDialogContent(scope.Window,
                new Button { Content = "Header" },
                new StackPanel { Children = { hidden, disabled, first, second } },
                new Button { Content = "Footer" })
        };
        dialog.ApplySmartSizing(scope.Window);
        dialog.IsOpen = true;
        scope.Flush();
        try { Assert.Same(second, scope.Window.FocusManager!.GetFocusedElement()); }
        finally { dialog.IsOpen = false; }
    }

    [AvaloniaFact]
    public async Task TextFillAnimatesStopsAndRestartsOnlyWhenConfiguredAndAttached()
    {
        var text = new TextBlock { Text = "Image fill", Width = 200, Height = 50 };
        using var image = new WriteableBitmap(new PixelSize(2, 2), new Vector(96, 96), Avalonia.Platform.PixelFormat.Bgra8888);
        FloweryTextFillEffects.SetImageSource(text, image);
        FloweryTextFillEffects.SetAnimate(text, true);
        FloweryTextFillEffects.SetDuration(text, .2);
        FloweryTextFillEffects.SetPanX(text, .5);
        using var scope = new Scope(text);
        var brush = Assert.IsType<ImageBrush>(text.Foreground);
        await WaitForPan(brush);
        FloweryTextFillEffects.StopEffect(text);
        Assert.Null(brush.Transform);
        await Task.Delay(70);
        Assert.Null(brush.Transform);
        FloweryTextFillEffects.SetAnimate(text, false);
        FloweryTextFillEffects.SetAnimate(text, true);
        await WaitForPan(brush);
        scope.Window.Content = null;
        scope.Flush();
        Assert.Null(brush.Transform);
        scope.Window.Content = text;
        scope.Flush();
        await WaitForPan(brush);
        FloweryTextFillEffects.SetAnimate(text, false);
        Assert.Null(brush.Transform);
    }

    private static async Task WaitForPan(ImageBrush brush)
    {
        var completion = new TaskCompletionSource();
        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) => { if (Math.Abs(brush.Transform?.Value.M31 ?? 0) > .1) completion.TrySetResult(); };
        timer.Start();
        try { await completion.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
        finally { timer.Stop(); }
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Scope : IDisposable
    {
        private readonly bool? _previousMotion = DaisyAccessibility.ReducedMotionOverride;
        public Window Window { get; }
        public Scope(Control content)
        {
            DaisyAccessibility.ReducedMotionOverride = false;
            Window = new Window { Width = 800, Height = 700, Content = content };
            Window.Show();
            Flush();
        }
        public void Flush() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() { Window.Close(); DaisyAccessibility.ReducedMotionOverride = _previousMotion; }
    }
}
