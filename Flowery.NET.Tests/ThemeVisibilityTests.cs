using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

public class ThemeVisibilityTests
{
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

    private static string[] StandardNames(DaisyThemeDropdown dropdown) =>
        Assert.IsAssignableFrom<IEnumerable<ThemePreviewInfo>>(dropdown.ItemsSource).Select(static info => info.Name).ToArray();

    private static IEnumerable<ProductThemePreviewInfo> ProductItems(DaisyProductThemeDropdown dropdown) =>
        Assert.IsAssignableFrom<IEnumerable<ProductThemePreviewInfo>>(dropdown.ItemsSource);

    private static string[] ProductNames(DaisyProductThemeDropdown dropdown) =>
        ProductItems(dropdown).Select(static info => info.Name).ToArray();

    private sealed class ThemeScope : ThemeTestScope
    {
        public int ThemeChanges { get; private set; }
        public int ListChanges { get; private set; }
        public int ProductSelections { get; private set; }

        public ThemeScope() : base(500, 300)
        {
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

        private void OnThemeChanged(object? sender, string name) => ThemeChanges++;
        private void OnListChanged(object? sender, EventArgs e) => ListChanges++;
        public void OnProductSelected(object? sender, string name) => ProductSelections++;

        public override void Dispose()
        {
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
            DaisyThemeManager.AvailableThemesChanged -= OnListChanged;
            base.Dispose();
        }
    }
}
