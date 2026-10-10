using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class BusinessThemeContrastTests
{
    [AvaloniaTheory]
    [InlineData(DaisyButtonStyle.Outline, DaisyButtonVariant.Primary)]
    [InlineData(DaisyButtonStyle.Dash, DaisyButtonVariant.Primary)]
    [InlineData(DaisyButtonStyle.Soft, DaisyButtonVariant.Primary)]
    [InlineData(DaisyButtonStyle.Default, DaisyButtonVariant.Link)]
    public void Button_Text_Remains_White_During_Hover_And_Press(
        DaisyButtonStyle style, DaisyButtonVariant variant)
    {
        using var palette = new PaletteScope();
        palette.Apply("Business", true);
        var button = new DaisyButton { Content = "Primary", ButtonStyle = style, Variant = variant };
        var window = Show(button);
        try
        {
            var background = Part<Border>(button, "PART_Background");
            background.Transitions = null;
            AssertWhiteContent(button);
            if (style == DaisyButtonStyle.Soft)
                Assert.InRange(Assert.IsAssignableFrom<ISolidColorBrush>(background.Background).Opacity, 0.01, 0.3);

            var point = Center(button, window);
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            Assert.True(button.IsPointerOver);
            AssertWhiteContent(button);
            if (style is DaisyButtonStyle.Outline or DaisyButtonStyle.Dash)
            {
                AssertColor("#1C4E80", background.Background);
                Assert.Equal(1, background.Opacity);
            }
            else if (style == DaisyButtonStyle.Soft)
            {
                AssertColor("#1C4E80", background.Background);
                Assert.InRange(Assert.IsAssignableFrom<ISolidColorBrush>(background.Background).Opacity, 0.2, 0.5);
                Assert.Equal(1, background.Opacity);
            }

            window.MouseDown(point, MouseButton.Left);
            Dispatcher.UIThread.RunJobs();
            Assert.True(button.IsPressed);
            AssertWhiteContent(button);
            window.MouseUp(point, MouseButton.Left);
            window.MouseMove(new Point(390, 290));
            Dispatcher.UIThread.RunJobs();
            Assert.False(button.IsPressed);
            AssertWhiteContent(button);

            palette.Apply("Light", false);
            AssertResourceColor("DaisyPrimaryBrush", button.Foreground);
            palette.Apply("Business", true);
            AssertWhiteContent(button);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void Disabled_Primary_Keeps_Text_And_Command_State()
    {
        using var palette = new PaletteScope();
        palette.Apply("Business", true);
        var command = new ToggleCommand();
        var button = new DaisyButton { Content = "Save", Variant = DaisyButtonVariant.Primary, Command = command };
        var window = Show(button);
        try
        {
            var background = Part<Border>(button, "PART_Background");
            background.Transitions = null;
            Assert.True(button.IsEnabled);
            Assert.False(button.IsEffectivelyEnabled);
            Assert.Equal(1, button.Opacity);
            AssertWhiteContent(button);
            AssertColor("#2B4055", background.Background);

            var point = Center(button, window);
            window.MouseMove(point);
            window.MouseDown(point, MouseButton.Left);
            window.MouseUp(point, MouseButton.Left);
            Assert.Equal(0, command.Executions);
            Assert.False(command.CanExecute(null));

            command.SetEnabled(true);
            Dispatcher.UIThread.RunJobs();
            Assert.True(button.IsEffectivelyEnabled);
            window.MouseMove(new Point(390, 290));
            AssertColor("#1C4E80", background.Background);
            button.IsEnabled = false;
            Assert.False(button.IsEffectivelyEnabled);
            Assert.True(command.CanExecute(null));
            Assert.Equal(1, button.Opacity);
            AssertWhiteContent(button);
            AssertColor("#2B4055", background.Background);

            palette.Apply("Light", false);
            Assert.Equal(0.5, button.Opacity);
            AssertResourceColor("DaisyPrimaryBrush", background.Background);
            palette.Apply("Business", true);
            Assert.Equal(1, button.Opacity);
            AssertColor("#2B4055", background.Background);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaTheory]
    [InlineData("Light", false)]
    [InlineData("Dark", true)]
    public void Tree_Header_And_Chevron_Use_Business_Selection_And_Restore_Other_Themes(string theme, bool dark)
    {
        using var palette = new PaletteScope();
        palette.Apply(theme, dark);
        var child = new TreeViewItem { Header = "Child" };
        var item = new TreeViewItem { Header = "Documents", IsExpanded = true, ItemsSource = new[] { child } };
        var tree = new TreeView { ItemsSource = new[] { item }, SelectedItem = item };
        var window = Show(tree);
        try
        {
            var header = Part<ContentPresenter>(item, "PART_HeaderPresenter");
            var background = Part<Border>(item, "PART_LayoutRoot");
            var chevron = Part<Avalonia.Controls.Shapes.Path>(item, "ChevronPath");
            var originalText = ColorOf(header.Foreground);
            var originalChevron = ColorOf(chevron.Fill);
            var originalBackground = ColorOf(background.Background);
            var childText = ColorOf(child.Foreground);
            AssertResourceColor("TreeViewItemForeground", header.Foreground);
            AssertResourceColor("TreeViewItemForeground", chevron.Fill);

            palette.Apply("Business", true);
            Assert.True(item.IsSelected);
            AssertColor("#FFFFFF", header.Foreground);
            AssertColor("#FFFFFF", chevron.Fill);
            AssertColor("#1C4E80", background.Background);
            var point = Center(header, window);
            window.MouseMove(point);
            Dispatcher.UIThread.RunJobs();
            Assert.True(background.IsPointerOver);
            AssertColor("#FFFFFF", header.Foreground);
            AssertColor("#163E66", background.Background);
            window.MouseDown(point, MouseButton.Left);
            AssertColor("#FFFFFF", header.Foreground);
            AssertColor("#163E66", background.Background);
            window.MouseUp(point, MouseButton.Left);
            window.MouseMove(new Point(390, 290));
            Dispatcher.UIThread.RunJobs();

            palette.Apply(theme, dark);
            Assert.Equal(originalText, ColorOf(header.Foreground));
            Assert.Equal(originalChevron, ColorOf(chevron.Fill));
            Assert.Equal(originalBackground, ColorOf(background.Background));
            Assert.Equal(childText, ColorOf(child.Foreground));
            palette.Apply("Business", true);
            tree.SelectedItem = child;
            Dispatcher.UIThread.RunJobs();
            Assert.False(item.IsSelected);
            Assert.True(child.IsSelected);
            AssertColor("#FFFFFF", Part<ContentPresenter>(child, "PART_HeaderPresenter").Foreground);
        }
        finally
        {
            window.Close();
        }
    }

    private static Window Show(Control content)
    {
        content.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
        content.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
        var window = new Window { Width = 400, Height = 300, Content = content };
        window.Show();
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        return window;
    }

    private static T Part<T>(Control control, string name) where T : Control =>
        control.GetVisualDescendants().OfType<T>().First(part => part.Name == name);

    private static Point Center(Control control, Window window) =>
        control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), window)!.Value;

    private static Color ColorOf(IBrush? brush) => Assert.IsAssignableFrom<ISolidColorBrush>(brush).Color;

    private static void AssertColor(string expected, IBrush? actual) => Assert.Equal(Color.Parse(expected), ColorOf(actual));

    private static void AssertResourceColor(string key, IBrush? actual)
    {
        var app = Application.Current!;
        Assert.True(app.TryFindResource(key, app.RequestedThemeVariant, out var resource));
        Assert.Equal(ColorOf(Assert.IsAssignableFrom<IBrush>(resource)), ColorOf(actual));
    }

    private static void AssertWhiteContent(DaisyButton button)
    {
        AssertColor("#FFFFFF", button.Foreground);
        AssertColor("#FFFFFF", Part<ContentPresenter>(button, "PART_ContentPresenter").Foreground);
        AssertResourceColor("DaisyErrorContentBrush", button.Foreground);
    }

    private sealed class PaletteScope : IDisposable
    {
        private readonly Application _app = Application.Current!;
        private readonly ThemeVariant? _previousVariant = Application.Current!.RequestedThemeVariant;
        private ResourceDictionary? _palette;

        public void Apply(string name, bool dark)
        {
            if (_palette is not null)
                _app.Resources.MergedDictionaries.Remove(_palette);
            _palette = (ResourceDictionary)AvaloniaXamlLoader.Load(new Uri($"avares://Flowery.NET/Themes/Palettes/Daisy{name}.axaml"));
            _app.Resources.MergedDictionaries.Add(_palette);
            _app.RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light;
            Dispatcher.UIThread.RunJobs();
        }

        public void Dispose()
        {
            if (_palette is not null)
                _app.Resources.MergedDictionaries.Remove(_palette);
            _app.RequestedThemeVariant = _previousVariant;
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class ToggleCommand : ICommand
    {
        private bool _enabled;
        public int Executions { get; private set; }
        public event EventHandler? CanExecuteChanged;
        public bool CanExecute(object? parameter) => _enabled;
        public void Execute(object? parameter) => Executions++;

        public void SetEnabled(bool enabled)
        {
            _enabled = enabled;
            CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
