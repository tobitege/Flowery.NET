using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class ThemeVisibilityTests
{
    [AvaloniaFact]
    public void Exclusions_Are_CaseInsensitive_Snapshots_And_Changes_Are_Idempotent()
    {
        using var scope = new ThemeScope();
        var names = new[] { "Cyberpunk", "CYBERPUNK", "FutureHiddenTheme" };
        DaisyThemeManager.ExcludedThemes = names;
        names[0] = "Business";
        Assert.Equal(2, DaisyThemeManager.ExcludedThemes.Count);
        Assert.False(DaisyThemeManager.IsThemeVisible("cyberpunk"));
        Assert.True(DaisyThemeManager.IsThemeVisible("Business"));
        Assert.Equal(1, scope.ListChanges);

        Assert.False(DaisyThemeManager.HideTheme("CYBERPUNK"));
        Assert.False(DaisyThemeManager.ShowTheme("Business"));
        DaisyThemeManager.ExcludedThemes = new[] { "futurehiddentheme", "cyberpunk" };
        Assert.Equal(1, scope.ListChanges);
        Assert.Throws<ArgumentException>(() => DaisyThemeManager.ExcludedThemes = new[] { "Business", " " });
        Assert.True(DaisyThemeManager.IsThemeVisible("Business"));
        Assert.False(DaisyThemeManager.IsThemeVisible("Cyberpunk"));

        DaisyThemeManager.PreferredThemes = ["FutureHiddenTheme"];
        DaisyThemeManager.RegisterTheme(new DaisyThemeInfo("FutureHiddenTheme", false), static () => new ResourceDictionary());
        Assert.DoesNotContain(DaisyThemeManager.AvailableThemes, theme => theme.Name == "FutureHiddenTheme");
        Assert.NotNull(DaisyThemeManager.GetPaletteFactory("FutureHiddenTheme"));
        Assert.True(DaisyThemeManager.ApplyTheme("FutureHiddenTheme"));
        Assert.Equal(1, scope.ApplyCalls);
        Assert.True(DaisyThemeManager.ShowTheme("FUTUREHIDDENTHEME"));
        Assert.Equal("FutureHiddenTheme", DaisyThemeManager.AvailableThemes[0].Name);

        var changes = scope.ListChanges;
        DaisyThemeManager.PreferredThemes = ["futurehiddentheme"];
        Assert.Equal(changes, scope.ListChanges);
        DaisyThemeManager.PreferredThemes = [];
        var ordered = DaisyThemeManager.AvailableThemes.Select(theme => theme.Name).ToArray();
        Assert.Equal(ordered.OrderBy(static name => name), ordered);
    }

    [AvaloniaFact]
    public void Multiple_Preferred_Names_Are_Copied_And_Grouped_In_Both_Dropdowns()
    {
        using var scope = new ThemeScope();
        var names = new[] { "TheaterCinema", "Dark", "MICROSAAS", "Business", "business" };
        DaisyThemeManager.PreferredThemes = names;
        names[0] = "Cyberpunk";
        var snapshot = DaisyThemeManager.PreferredThemes;
        Assert.Equal(4, snapshot.Count);
        Assert.DoesNotContain("Cyberpunk", snapshot);
        var changes = scope.ListChanges;
        DaisyThemeManager.PreferredThemes = ["business", "MicroSaaS", "dark", "theatercinema"];
        Assert.Equal(changes, scope.ListChanges);
        Assert.Throws<ArgumentException>(() => DaisyThemeManager.PreferredThemes = ["Business", " "]);
        Assert.Equal(4, DaisyThemeManager.PreferredThemes.Count);

        var standard = new DaisyThemeDropdown();
        var product = new DaisyProductThemeDropdown();
        scope.Show(standard, product);
        Assert.Equal(new[] { "Business", "Dark" }, StandardNames(standard).Take(2));
        Assert.Equal(new[] { "MicroSaaS", "TheaterCinema" }, ProductNames(product).Take(2));
        var productItems = ProductItems(product).ToArray();
        Assert.False(productItems[0].HasSeparatorBefore);
        Assert.False(productItems[1].HasSeparatorBefore);
        Assert.True(productItems[2].HasSeparatorBefore);
        Assert.Single(productItems, static item => item.HasSeparatorBefore);

        standard.AddTheme("Business", "MyBusiness", false);
        Assert.Equal("Dark", StandardNames(standard)[0]);
        DaisyThemeManager.HideTheme("MicroSaaS");
        Assert.Equal("TheaterCinema", ProductNames(product)[0]);
        DaisyThemeManager.PreferredThemes = [];
        Assert.DoesNotContain(ProductItems(product), static item => item.HasSeparatorBefore);
        Assert.Equal(4, snapshot.Count);
        Assert.Equal("Business", DaisyThemeManager.CurrentThemeName);
        Assert.Equal(0, scope.ApplyCalls);
        Assert.Equal(0, scope.ThemeChanges);
    }

    [AvaloniaFact]
    public void Early_Settings_Apply_To_Both_Dropdowns()
    {
        using var scope = new ThemeScope();
        DaisyThemeManager.ExcludedThemes = new[] { "cyberpunk", "saas" };
        DaisyThemeManager.PreferredThemes = ["business"];
        var standard = new DaisyThemeDropdown();
        var product = new DaisyProductThemeDropdown();
        scope.Show(standard, product);

        Assert.DoesNotContain("Cyberpunk", StandardNames(standard));
        Assert.DoesNotContain("SaaS", ProductNames(product));
        Assert.Equal("Business", StandardNames(standard)[0]);
        Assert.Equal("Business", Assert.IsType<ThemePreviewInfo>(standard.SelectedItem).Name);
        Assert.Null(product.SelectedItem);
        Assert.Equal(0, scope.ApplyCalls);
        Assert.Equal(0, scope.ThemeChanges);
    }

    [AvaloniaFact]
    public void Runtime_Changes_Refresh_All_Instances_Without_Applying_Themes()
    {
        using var scope = new ThemeScope();
        var standard = new DaisyThemeDropdown();
        var secondStandard = new DaisyThemeDropdown();
        var product = new DaisyProductThemeDropdown();
        var secondProduct = new DaisyProductThemeDropdown();
        product.ProductThemeSelected += scope.OnProductSelected;
        secondProduct.ProductThemeSelected += scope.OnProductSelected;
        scope.Show(standard, secondStandard, product, secondProduct);
        var normalProductOrder = ProductNames(product);

        DaisyThemeManager.PreferredThemes = ["MicroSaaS"];
        Assert.Equal("MicroSaaS", ProductNames(product)[0]);
        Assert.Equal(ProductNames(product), ProductNames(secondProduct));
        Assert.Equal(normalProductOrder.Where(static name => name != "MicroSaaS"), ProductNames(product).Skip(1));
        DaisyThemeManager.HideTheme("MicroSaaS");
        DaisyThemeManager.HideTheme("Business");
        Assert.DoesNotContain("MicroSaaS", ProductNames(product));
        Assert.Null(standard.SelectedItem);
        Assert.Null(secondStandard.SelectedItem);
        Assert.Equal("Business", standard.SelectedTheme);
        Assert.Equal("Business", DaisyThemeManager.CurrentThemeName);

        DaisyThemeManager.ShowTheme("microsaas");
        DaisyThemeManager.ShowTheme("BUSINESS");
        Assert.Equal("MicroSaaS", ProductNames(product)[0]);
        Assert.Equal("Business", Assert.IsType<ThemePreviewInfo>(standard.SelectedItem).Name);
        DaisyThemeManager.PreferredThemes = ["Corporate"];
        Assert.Equal("Corporate", StandardNames(standard)[0]);
        Assert.Equal(StandardNames(standard), StandardNames(secondStandard));
        Assert.Equal(normalProductOrder, ProductNames(product));
        Assert.Equal(0, scope.ApplyCalls);
        Assert.Equal(0, scope.ThemeChanges);
        Assert.Equal(0, scope.ProductSelections);
    }

    [AvaloniaFact]
    public void Product_Registration_And_Local_Selection_Survive_List_Refresh()
    {
        using var scope = new ThemeScope();
        var standard = new DaisyThemeDropdown();
        var product = new DaisyProductThemeDropdown();
        product.ProductThemeSelected += scope.OnProductSelected;
        scope.Show(standard, product);
        var entry = ProductItems(product).First(info => DaisyThemeManager.GetThemeInfo(info.Name) == null);
        product.SelectedItem = entry;
        Assert.Equal(1, scope.ApplyCalls);
        Assert.Equal(1, scope.ProductSelections);
        Assert.Equal(entry.Name, DaisyThemeManager.CurrentThemeName);
        Assert.Equal(entry.Name, Assert.IsType<ProductThemePreviewInfo>(product.SelectedItem).Name);
        Assert.Equal(entry.Name, Assert.IsType<ThemePreviewInfo>(standard.SelectedItem).Name);

        DaisyThemeManager.HideTheme(entry.Name);
        Assert.Null(product.SelectedItem);
        Assert.Null(standard.SelectedItem);
        Assert.Equal(entry.Name, product.SelectedTheme);
        Assert.Equal(entry.Name, DaisyThemeManager.CurrentThemeName);
        DaisyThemeManager.ShowTheme(entry.Name);
        Assert.Equal(entry.Name, Assert.IsType<ProductThemePreviewInfo>(product.SelectedItem).Name);

        product.ApplyOnSelection = false;
        var local = ProductItems(product).First(info => info.Name != entry.Name);
        product.SelectedItem = local;
        DaisyThemeManager.PreferredThemes = [local.Name];
        DaisyThemeManager.HideTheme(entry.Name);
        Assert.Equal(local.Name, Assert.IsType<ProductThemePreviewInfo>(product.SelectedItem).Name);
        Assert.Equal(entry.Name, DaisyThemeManager.CurrentThemeName);
        Assert.Equal(1, scope.ApplyCalls);
        Assert.Equal(2, scope.ProductSelections);
    }

    [AvaloniaFact]
    public void Empty_Lists_And_Reattach_Respect_Current_Policy()
    {
        using var scope = new ThemeScope();
        var standard = new DaisyThemeDropdown();
        var product = new DaisyProductThemeDropdown();
        scope.Show(standard, product);
        var allNames = StandardNames(standard).Concat(ProductNames(product)).ToArray();
        DaisyThemeManager.ExcludedThemes = allNames;
        Assert.Empty(StandardNames(standard));
        Assert.Empty(ProductNames(product));
        Assert.Null(standard.SelectedItem);
        Assert.Null(product.SelectedItem);

        var panel = scope.Window.Content;
        scope.Window.Content = null;
        DaisyThemeManager.ExcludedThemes = new[] { "Cyberpunk", "SaaS" };
        DaisyThemeManager.PreferredThemes = ["MicroSaaS"];
        scope.Window.Content = panel;
        Dispatcher.UIThread.RunJobs();
        Assert.DoesNotContain("Cyberpunk", StandardNames(standard));
        Assert.DoesNotContain("SaaS", ProductNames(product));
        Assert.Equal("MicroSaaS", ProductNames(product)[0]);
        Assert.Equal("Business", Assert.IsType<ThemePreviewInfo>(standard.SelectedItem).Name);
        Assert.Equal(0, scope.ApplyCalls);
    }

    private static string[] StandardNames(DaisyThemeDropdown dropdown) =>
        Assert.IsAssignableFrom<IEnumerable<ThemePreviewInfo>>(dropdown.ItemsSource).Select(static info => info.Name).ToArray();

    private static IEnumerable<ProductThemePreviewInfo> ProductItems(DaisyProductThemeDropdown dropdown) =>
        Assert.IsAssignableFrom<IEnumerable<ProductThemePreviewInfo>>(dropdown.ItemsSource);

    private static string[] ProductNames(DaisyProductThemeDropdown dropdown) =>
        ProductItems(dropdown).Select(static info => info.Name).ToArray();

    private sealed class ThemeScope : IDisposable
    {
        private readonly IReadOnlyCollection<string> _excluded = DaisyThemeManager.ExcludedThemes;
        private readonly IReadOnlyCollection<string> _preferred = DaisyThemeManager.PreferredThemes;
        private readonly string? _current = DaisyThemeManager.CurrentThemeName;
        private readonly Func<string, bool>? _applicator = DaisyThemeManager.CustomThemeApplicator;
        private readonly bool _suppressed = DaisyThemeManager.SuppressThemeApplication;
        private readonly HashSet<string> _registered;
        public Window Window { get; } = new() { Width = 500, Height = 300 };
        public int ApplyCalls { get; private set; }
        public int ThemeChanges { get; private set; }
        public int ListChanges { get; private set; }
        public int ProductSelections { get; private set; }

        public ThemeScope()
        {
            _registered = ThemeRegistrations.Capture();
            DaisyThemeManager.PreferredThemes = [];
            DaisyThemeManager.CustomThemeApplicator = Apply;
            DaisyThemeManager.SuppressThemeApplication = false;
            DaisyThemeManager.SetCurrentTheme("Business");
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            DaisyThemeManager.AvailableThemesChanged += OnListChanged;
        }

        public void Show(params Control[] controls)
        {
            var panel = new StackPanel();
            panel.Children.AddRange(controls);
            Window.Content = panel;
            Window.Show();
            Dispatcher.UIThread.RunJobs();
        }

        private bool Apply(string name) { ApplyCalls++; return true; }
        private void OnThemeChanged(object? sender, string name) => ThemeChanges++;
        private void OnListChanged(object? sender, EventArgs e) => ListChanges++;
        public void OnProductSelected(object? sender, string name) => ProductSelections++;

        public void Dispose()
        {
            Window.Close();
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
            DaisyThemeManager.AvailableThemesChanged -= OnListChanged;
            ThemeRegistrations.RemoveAddedSince(_registered);
            DaisyThemeManager.ExcludedThemes = _excluded;
            DaisyThemeManager.PreferredThemes = _preferred;
            DaisyThemeManager.SetCurrentTheme(_current ?? string.Empty);
            DaisyThemeManager.CustomThemeApplicator = _applicator;
            DaisyThemeManager.SuppressThemeApplication = _suppressed;
        }
    }
}
