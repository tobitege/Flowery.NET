using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class ThemeDropdownSeparatorTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Boundary_Exists_Only_Between_Two_Nonempty_Groups(bool product)
    {
        using var scope = new SeparatorScope(product);
        Assert.DoesNotContain(scope.Items, HasSeparator);
        var originalNames = scope.Items.Select(NameOf).ToArray();
        scope.Add("Business", "A", true);
        scope.Add("Corporate", "B", true);
        Assert.Equal(originalNames.Concat(new[] { "Business", "Corporate" }).Distinct().Count(), scope.Items.Length);
        Assert.Equal(2, Array.FindIndex(scope.Items, HasSeparator));
        Assert.Single(scope.Items, HasSeparator);

        DaisyThemeManager.ExcludedThemes = scope.Items.Skip(2).Select(NameOf).ToArray();
        Assert.Equal(2, scope.Items.Length);
        Assert.DoesNotContain(scope.Items, HasSeparator);
        DaisyThemeManager.HideTheme("Business");
        DaisyThemeManager.HideTheme("Corporate");
        Assert.Empty(scope.Items);
        Assert.Equal(0, scope.ApplyCalls);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Separator_Click_Does_Not_Select_Or_Close_And_Keyboard_Skips_It(bool product)
    {
        using var scope = new SeparatorScope(product);
        scope.Add("Business", "A", true);
        scope.Add("Corporate", "B", true);
        scope.Combo.IsDropDownOpen = true;
        scope.Layout();

        var firstNormal = scope.Items[2];
        var container = Assert.IsType<ComboBoxItem>(scope.Combo.ContainerFromIndex(2));
        var separator = Assert.Single(container.GetVisualDescendants().OfType<Border>(),
            static border => border.Name == "PART_PreferredSeparator");
        var popupRoot = Assert.IsAssignableFrom<TopLevel>(TopLevel.GetTopLevel(separator));
        popupRoot.UpdateLayout();
        Assert.True(separator.IsEffectivelyVisible);
        Assert.False(separator.Focusable);
        Assert.True(separator.Bounds.Height > 0);
        var selected = scope.Combo.SelectedItem;
        var calls = scope.ApplyCalls;
        var point = separator.TranslatePoint(new Point(separator.Bounds.Width / 2, separator.Bounds.Height / 2), popupRoot)!.Value;

        popupRoot.MouseMove(point);
        popupRoot.MouseDown(point, MouseButton.Left);
        popupRoot.MouseUp(point, MouseButton.Left);
        scope.Layout();
        Assert.Same(selected, scope.Combo.SelectedItem);
        Assert.Equal(calls, scope.ApplyCalls);
        Assert.True(scope.Combo.IsDropDownOpen);

        // The theme below the separator must still be selectable.
        var label = container.GetVisualDescendants().OfType<TextBlock>().First(text => !string.IsNullOrEmpty(text.Text));
        var labelPoint = label.TranslatePoint(new Point(label.Bounds.Width / 2, label.Bounds.Height / 2), popupRoot)!.Value;
        popupRoot.MouseMove(labelPoint);
        popupRoot.MouseDown(labelPoint, MouseButton.Left);
        popupRoot.MouseUp(labelPoint, MouseButton.Left);
        scope.Layout();
        Assert.Same(scope.Items.Single(item => NameOf(item) == NameOf(firstNormal)), scope.Combo.SelectedItem);
        Assert.Equal(calls + 1, scope.ApplyCalls);
        Assert.False(scope.Combo.IsDropDownOpen);

        var closedSeparators = scope.Combo.GetVisualDescendants().OfType<Border>()
            .Where(static border => border.Name == "PART_PreferredSeparator").ToArray();
        Assert.NotEmpty(closedSeparators);
        Assert.All(closedSeparators, static line => Assert.False(line.IsVisible));

        scope.Combo.SelectedIndex = 1;
        scope.Combo.Focus();
        scope.Window.KeyPress(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        scope.Window.KeyRelease(Key.Down, RawInputModifiers.None, PhysicalKey.ArrowDown, null);
        scope.Layout();
        Assert.Equal(2, scope.Combo.SelectedIndex);
        Assert.Equal(NameOf(firstNormal), NameOf(scope.Combo.SelectedItem!));
    }

    [AvaloniaFact]
    public void Local_Boundary_Does_Not_Mutate_The_Shared_Standard_List()
    {
        using var scope = new SeparatorScope(false);
        DaisyThemeManager.PreferredThemes = ["Business"];
        var other = new DaisyThemeDropdown();
        var otherItems = other.ItemsSource!.Cast<ThemePreviewInfo>().ToArray();
        Assert.True(otherItems[1].HasSeparatorBefore);

        scope.Add(otherItems[1].Name, "A", true);
        Assert.Equal(2, Array.FindIndex(scope.Items, HasSeparator));
        Assert.True(otherItems[1].HasSeparatorBefore);
        Assert.Single(otherItems, static item => item.HasSeparatorBefore);
        Assert.NotSame(otherItems[1], scope.Items.Single(item => NameOf(item) == otherItems[1].Name));
    }

    private static bool HasSeparator(object item) => item switch
    {
        ThemePreviewInfo theme => theme.HasSeparatorBefore,
        ProductThemePreviewInfo theme => theme.HasSeparatorBefore,
        _ => false
    };

    private static string NameOf(object item) => item switch
    {
        ThemePreviewInfo theme => theme.Name,
        ProductThemePreviewInfo theme => theme.Name,
        _ => throw new ArgumentException("Expected a theme entry.", nameof(item))
    };

    private sealed class SeparatorScope : IDisposable
    {
        private readonly IReadOnlyCollection<string> _excluded = DaisyThemeManager.ExcludedThemes;
        private readonly IReadOnlyCollection<string> _preferred = DaisyThemeManager.PreferredThemes;
        private readonly string? _current = DaisyThemeManager.CurrentThemeName;
        private readonly Func<string, bool>? _applicator = DaisyThemeManager.CustomThemeApplicator;
        private readonly bool _suppressed = DaisyThemeManager.SuppressThemeApplication;
        private readonly HashSet<string> _registered;
        public Window Window { get; } = new() { Width = 600, Height = 400 };
        public ComboBox Combo { get; }
        public int ApplyCalls { get; private set; }
        public object[] Items => [.. Combo.ItemsSource!.Cast<object>()];

        public SeparatorScope(bool product)
        {
            _registered = ThemeRegistrations.Capture();
            DaisyThemeManager.PreferredThemes = [];
            DaisyThemeManager.CustomThemeApplicator = Apply;
            DaisyThemeManager.SuppressThemeApplication = false;
            DaisyThemeManager.SetCurrentTheme("Business");
            Combo = product ? new DaisyProductThemeDropdown() : new DaisyThemeDropdown();
            Combo.Width = 300;
            Combo.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
            Combo.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
            Window.Content = Combo;
            Window.Show();
            Layout();
        }

        public void Add(string name, string label, bool preferred)
        {
            if (Combo is DaisyThemeDropdown standard)
                standard.AddTheme(name, label, preferred);
            else if (Combo is DaisyProductThemeDropdown product)
                product.AddTheme(name, label, preferred);
            Layout();
        }

        public void Layout()
        {
            Dispatcher.UIThread.RunJobs();
            Window.UpdateLayout();
        }

        private bool Apply(string name) { ApplyCalls++; return true; }

        public void Dispose()
        {
            Window.Close();
            ThemeRegistrations.RemoveAddedSince(_registered);
            DaisyThemeManager.ExcludedThemes = _excluded;
            DaisyThemeManager.PreferredThemes = _preferred;
            DaisyThemeManager.SetCurrentTheme(_current ?? string.Empty);
            DaisyThemeManager.CustomThemeApplicator = _applicator;
            DaisyThemeManager.SuppressThemeApplication = _suppressed;
        }
    }
}
