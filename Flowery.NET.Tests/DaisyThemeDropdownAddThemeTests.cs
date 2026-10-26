using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class DaisyThemeDropdownAddThemeTests
{
    [AvaloniaFact]
    public void Local_Add_Retains_Defaults_And_Does_Not_Change_Other_Combos()
    {
        using var scope = new ThemeScope();
        var combo = new DaisyThemeDropdown();
        var other = new DaisyThemeDropdown();
        var defaults = StandardItems(combo).Select(static item => item.Name).ToArray();
        scope.Show(combo, other);

        combo.AddTheme("TheaterCinema", "DarkAndRed", true);
        combo.AddTheme("business", "MyBusiness", false);
        Dispatcher.UIThread.RunJobs();

        var items = StandardItems(combo);
        Assert.All(defaults, name => Assert.Contains(items, item => item.Name == name));
        Assert.Equal(defaults.Append("TheaterCinema").Distinct().Count(), items.Length);
        Assert.Equal("TheaterCinema", items[0].Name);
        Assert.Equal("DarkAndRed", items[0].DisplayName);
        Assert.True(items[0].IsDark);
        Assert.Equal("MyBusiness", items.Single(static item => item.Name == "Business").DisplayName);
        Assert.DoesNotContain(StandardItems(other), static item => item.DisplayName == "MyBusiness");
        Assert.Equal("Business", combo.SelectedTheme);
        Assert.Equal(0, scope.ApplyCalls);

        combo.AddTheme("THEATERCINEMA", "Cinema", false);
        Assert.Single(StandardItems(combo), static item => item.Name == "TheaterCinema");
        Assert.Equal("Cinema", StandardItems(combo).Single(static item => item.Name == "TheaterCinema").DisplayName);
    }

    [AvaloniaFact]
    public void Preferred_Groups_Are_Sorted_By_Display_Name()
    {
        using var scope = new ThemeScope();
        var combo = new DaisyThemeDropdown();
        combo.AddTheme("Business", "Zulu", true);
        combo.AddTheme("TheaterCinema", "Middle", true);
        combo.AddTheme("Corporate", "Alpha", true);
        combo.AddTheme("SaaS", "alpha", true);
        var items = StandardItems(combo);
        Assert.Equal(new[] { "Corporate", "SaaS", "TheaterCinema", "Business" }, items.Take(4).Select(static item => item.Name));
        var remaining = items.Skip(4).Select(static item => item.DisplayName).ToArray();
        Assert.Equal(remaining.OrderBy(static name => name, StringComparer.CurrentCultureIgnoreCase), remaining);
        Assert.Equal(0, scope.ApplyCalls);
    }

    [AvaloniaFact]
    public void Selection_Uses_Original_Name_And_Renders_The_Alias()
    {
        using var scope = new ThemeScope();
        var combo = new DaisyThemeDropdown();
        combo.AddTheme("TheaterCinema", "DarkAndRed", true);
        scope.Show(combo);
        var entry = StandardItems(combo).Single(static item => item.Name == "TheaterCinema");
        var expectedPrimary = Assert.IsAssignableFrom<ISolidColorBrush>(entry.Primary).Color;
        combo.SelectedItem = entry;
        Dispatcher.UIThread.RunJobs();
        scope.Window.UpdateLayout();

        Assert.Equal("TheaterCinema", DaisyThemeManager.CurrentThemeName);
        Assert.Equal("TheaterCinema", combo.SelectedTheme);
        var selected = Assert.IsType<ThemePreviewInfo>(combo.SelectedItem);
        Assert.Same(StandardItems(combo).Single(static item => item.Name == "TheaterCinema"), selected);
        Assert.Equal(1, scope.ApplyCalls);
        Assert.Null(DaisyThemeManager.GetThemeInfo("DarkAndRed"));
        Assert.True(DaisyThemeManager.TryCreatePalette("TheaterCinema", out var palette));
        Assert.NotNull(palette);
        Assert.True(palette.TryGetResource("DaisyPrimaryBrush", null, out var primary));
        Assert.Equal(expectedPrimary, Assert.IsAssignableFrom<ISolidColorBrush>(primary).Color);
        Assert.Contains(combo.GetVisualDescendants().OfType<TextBlock>(), static text => text.Text == "DarkAndRed");

        combo.AddTheme("TheaterCinema", "Renamed cinema", false);
        Dispatcher.UIThread.RunJobs();
        scope.Window.UpdateLayout();
        Assert.Equal(1, scope.ApplyCalls);
        Assert.Equal("TheaterCinema", combo.SelectedTheme);
        Assert.Contains(combo.GetVisualDescendants().OfType<TextBlock>(), static text => text.Text == "Renamed cinema");
    }

    [AvaloniaFact]
    public void Global_Add_Updates_Both_Types_And_Local_Overrides_Win()
    {
        using var scope = new ThemeScope();
        var standard = new DaisyThemeDropdown();
        var local = new DaisyThemeDropdown();
        var product = new DaisyProductThemeDropdown();
        var localProduct = new DaisyProductThemeDropdown();
        local.AddTheme("Business", "Only here", true);
        localProduct.AddTheme("TheaterCinema", "Local cinema", false);
        scope.Show(standard, local, product, localProduct);

        scope.AddGlobal("Business", "MyBusiness", false);
        scope.AddGlobal("TheaterCinema", "DarkAndRed", true);
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("DarkAndRed", StandardItems(standard)[0].DisplayName);
        Assert.Equal("DarkAndRed", ProductItems(product)[0].DisplayName);
        Assert.Equal("MyBusiness", ProductItems(product).Single(static item => item.Name == "Business").DisplayName);
        Assert.Equal("Only here", StandardItems(local).Single(static item => item.Name == "Business").DisplayName);
        Assert.Equal("Local cinema", ProductItems(localProduct).Single(static item => item.Name == "TheaterCinema").DisplayName);

        scope.AddGlobal("Business", "Global update", true);
        scope.AddGlobal("TheaterCinema", "Global cinema", true);
        Assert.Equal("Global update", StandardItems(standard).Single(static item => item.Name == "Business").DisplayName);
        Assert.Equal("Only here", StandardItems(local).Single(static item => item.Name == "Business").DisplayName);
        Assert.Equal("Local cinema", ProductItems(localProduct).Single(static item => item.Name == "TheaterCinema").DisplayName);
        Assert.True(local.RemoveThemeOverride("BUSINESS"));
        Assert.True(localProduct.RemoveThemeOverride("THEATERCINEMA"));
        Assert.Equal("Global update", StandardItems(local).Single(static item => item.Name == "Business").DisplayName);
        Assert.Equal("Global cinema", ProductItems(localProduct).Single(static item => item.Name == "TheaterCinema").DisplayName);

        DaisyThemeManager.HideTheme("TheaterCinema");
        Assert.DoesNotContain(StandardItems(standard), static item => item.Name == "TheaterCinema");
        Assert.DoesNotContain(ProductItems(product), static item => item.Name == "TheaterCinema");
        DaisyThemeManager.ShowTheme("TheaterCinema");
        Assert.Single(ProductItems(product), static item => item.Name == "TheaterCinema");
        Dispatcher.UIThread.RunJobs();
        Assert.Equal(0, scope.ApplyCalls);
        Assert.Equal("Business", DaisyThemeManager.CurrentThemeName);
    }

    [AvaloniaFact]
    public void Early_Global_Settings_And_Standard_Selection_In_Product_Combo_Work()
    {
        using var scope = new ThemeScope();
        scope.AddGlobal("Business", "MyBusiness", true);
        scope.AddGlobal("TheaterCinema", "DarkAndRed", true);
        var standard = new DaisyThemeDropdown();
        var product = new DaisyProductThemeDropdown();
        scope.Show(standard, product);
        Assert.Equal(new[] { "DarkAndRed", "MyBusiness" }, StandardItems(standard).Take(2).Select(static item => item.DisplayName));
        Assert.Equal(new[] { "DarkAndRed", "MyBusiness" }, ProductItems(product).Take(2).Select(static item => item.DisplayName));
        Assert.Contains(ProductItems(product), static item => item.Name == "SaaS");
        Assert.Contains(StandardItems(standard), static item => item.Name == "Light");

        DaisyThemeManager.SetCurrentTheme("Light");
        var business = ProductItems(product).Single(static item => item.Name == "Business");
        Assert.Null(business.Palette);
        product.SelectedItem = business;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Business", DaisyThemeManager.CurrentThemeName);
        Assert.Equal("Business", product.SelectedTheme);
        Assert.Equal(1, scope.ApplyCalls);
        Assert.Contains(product.GetVisualDescendants().OfType<TextBlock>(), static text => text.Text == "MyBusiness");
    }

    [AvaloniaFact]
    public void Exclusions_Invalid_Names_And_Reattach_Preserve_List_State()
    {
        using var scope = new ThemeScope();
        var combo = new DaisyThemeDropdown();
        scope.Show(combo);
        var count = StandardItems(combo).Length;
        Assert.Throws<ArgumentException>(() => combo.AddTheme("NoSuchTheme", "Unknown", true));
        Assert.Throws<ArgumentException>(() => combo.AddTheme("Business", " ", true));
        Assert.Throws<ArgumentException>(() => DaisyThemeManager.AddTheme("NoSuchTheme", "Unknown", true));
        Assert.Equal(count, StandardItems(combo).Length);

        DaisyThemeManager.HideTheme("Business");
        combo.AddTheme("Business", "Local business", true);
        Assert.DoesNotContain(StandardItems(combo), static item => item.Name == "Business");
        scope.Window.Content = null;
        DaisyThemeManager.ShowTheme("Business");
        scope.Window.Content = combo;
        Dispatcher.UIThread.RunJobs();
        Assert.Equal("Local business", StandardItems(combo)[0].DisplayName);
        Assert.Equal("Business", combo.SelectedTheme);
        Assert.Equal(0, scope.ApplyCalls);
    }

    private static ThemePreviewInfo[] StandardItems(DaisyThemeDropdown combo) =>
        [.. Assert.IsAssignableFrom<IEnumerable<ThemePreviewInfo>>(combo.ItemsSource)];

    private static ProductThemePreviewInfo[] ProductItems(DaisyProductThemeDropdown combo) =>
        [.. Assert.IsAssignableFrom<IEnumerable<ProductThemePreviewInfo>>(combo.ItemsSource)];

    private sealed class ThemeScope : IDisposable
    {
        private readonly IReadOnlyCollection<string> _excluded = DaisyThemeManager.ExcludedThemes;
        private readonly IReadOnlyCollection<string> _preferred = DaisyThemeManager.PreferredThemes;
        private readonly string? _current = DaisyThemeManager.CurrentThemeName;
        private readonly Func<string, bool>? _applicator = DaisyThemeManager.CustomThemeApplicator;
        private readonly bool _suppressed = DaisyThemeManager.SuppressThemeApplication;
        private readonly HashSet<string> _registered;
        private readonly HashSet<string> _globalNames = new(StringComparer.OrdinalIgnoreCase);
        public Window Window { get; } = new() { Width = 600, Height = 350 };
        public int ApplyCalls { get; private set; }

        public ThemeScope()
        {
            _registered = ThemeRegistrations.Capture();
            DaisyThemeManager.PreferredThemes = [];
            DaisyThemeManager.CustomThemeApplicator = Apply;
            DaisyThemeManager.SuppressThemeApplication = false;
            DaisyThemeManager.SetCurrentTheme("Business");
        }

        public void AddGlobal(string name, string label, bool preferred)
        {
            _globalNames.Add(name);
            DaisyThemeManager.AddTheme(name, label, preferred);
        }

        public void Show(params Control[] controls)
        {
            if (controls.Length == 1)
                Window.Content = controls[0];
            else
            {
                var panel = new StackPanel();
                panel.Children.AddRange(controls);
                Window.Content = panel;
            }
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        private bool Apply(string name) { ApplyCalls++; return true; }

        public void Dispose()
        {
            Window.Close();
            foreach (var name in _globalNames) DaisyThemeManager.RemoveThemeOverride(name);
            ThemeRegistrations.RemoveAddedSince(_registered);
            DaisyThemeManager.ExcludedThemes = _excluded;
            DaisyThemeManager.PreferredThemes = _preferred;
            DaisyThemeManager.SetCurrentTheme(_current ?? string.Empty);
            DaisyThemeManager.CustomThemeApplicator = _applicator;
            DaisyThemeManager.SuppressThemeApplication = _suppressed;
        }
    }
}
