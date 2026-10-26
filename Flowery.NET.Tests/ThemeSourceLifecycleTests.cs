using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Theming;
using Xunit;

namespace Flowery.NET.Tests;

public class ThemeSourceLifecycleTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Bound_Observable_Source_Remains_Live_And_Can_Be_Replaced(bool product)
    {
        using var scope = new SourceScope(product);
        var first = product ? "SaaS" : "Business";
        var second = product ? "MicroSaaS" : "Corporate";
        var third = product ? "TheaterCinema" : "Light";
        var original = new ObservableCollection<object> { Entry(product, first) };
        var model = new SourceModel(original);
        scope.Combo.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(SourceModel.Source)) { Source = model, Mode = BindingMode.OneWay });
        scope.Show();
        Assert.Equal(new[] { first }, scope.Names);
        original.Add(Entry(product, second));
        Assert.Equal(new[] { first, second }, scope.Names);
        original.Move(1, 0);
        Assert.Equal(new[] { second, first }, scope.Names);
        original.RemoveAt(1);
        Assert.Equal(new[] { second }, scope.Names);
        original.Clear();
        Assert.Empty(scope.Names);
        Assert.Same(original, model.Source);

        var replacement = new ObservableCollection<object> { Entry(product, third) };
        model.Source = replacement;
        scope.Layout();
        Assert.Equal(new[] { third }, scope.Names);
        var displayed = scope.Combo.ItemsSource;
        original.Add(Entry(product, first));
        Assert.Same(displayed, scope.Combo.ItemsSource);
        replacement.Add(Entry(product, second));
        Assert.Equal(new[] { third, second }, scope.Names);
        Assert.Same(replacement, model.Source);
        Assert.Equal(0, scope.ApplyCalls);
    }

    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void Detach_Unsubscribes_And_Reattach_Resolves_Latest_Source_And_Overrides(bool product)
    {
        using var scope = new SourceScope(product);
        var first = product ? "SaaS" : "Business";
        var second = product ? "MicroSaaS" : "Corporate";
        var source = new ObservableCollection<object> { Entry(product, first) };
        var model = new SourceModel(source);
        scope.Combo.Bind(ItemsControl.ItemsSourceProperty, new Binding(nameof(SourceModel.Source)) { Source = model });
        scope.Show();
        scope.AddLocal(first, "Local", true);
        scope.AddGlobal(second, "Global", false);
        Assert.Equal(new[] { "Local", "Global" }, scope.Labels);
        Assert.Same(source, model.Source);
        scope.Window.Content = null;
        var displayed = scope.Combo.ItemsSource;
        source.Add(Entry(product, second));
        DaisyThemeManager.HideTheme(second);
        Assert.Same(displayed, scope.Combo.ItemsSource);
        scope.Window.Content = scope.Combo;
        scope.Layout();
        Assert.Equal(new[] { "Local" }, scope.Labels);
        DaisyThemeManager.ShowTheme(second);
        Assert.Equal(new[] { "Local", "Global" }, scope.Labels);
        Assert.Equal(0, scope.ApplyCalls);
    }

    private static object Entry(bool product, string name) => product
        ? new ProductThemePreviewInfo(ProductPaletteFactory.FindByName(name)!)
        : new ThemePreviewInfo { Name = name };

    private sealed class SourceModel(IEnumerable source) : INotifyPropertyChanged
    {
        private IEnumerable _source = source;
        public IEnumerable Source
        {
            get => _source;
            set { _source = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Source))); }
        }
        public event PropertyChangedEventHandler? PropertyChanged;
    }

    private sealed class SourceScope : IDisposable
    {
        private readonly IReadOnlyCollection<string> _excluded = DaisyThemeManager.ExcludedThemes;
        private readonly IReadOnlyCollection<string> _preferred = DaisyThemeManager.PreferredThemes;
        private readonly string? _current = DaisyThemeManager.CurrentThemeName;
        private readonly Func<string, bool>? _applicator = DaisyThemeManager.CustomThemeApplicator;
        private readonly bool _suppressed = DaisyThemeManager.SuppressThemeApplication;
        private readonly HashSet<string> _registered;
        private readonly List<string> _globalNames = [];
        public Window Window { get; } = new() { Width = 500, Height = 300 };
        public ComboBox Combo { get; }
        public int ApplyCalls { get; private set; }
        public string[] Names => Combo.ItemsSource!.Cast<object>().Select(static item => item is ThemePreviewInfo standard ? standard.Name : ((ProductThemePreviewInfo)item).Name).ToArray();
        public string[] Labels => Combo.ItemsSource!.Cast<object>().Select(static item => item is ThemePreviewInfo standard ? standard.DisplayName : ((ProductThemePreviewInfo)item).DisplayName).ToArray();

        public SourceScope(bool product)
        {
            _registered = ThemeRegistrations.Capture();
            DaisyThemeManager.PreferredThemes = [];
            DaisyThemeManager.CustomThemeApplicator = Apply;
            DaisyThemeManager.SuppressThemeApplication = false;
            DaisyThemeManager.SetCurrentTheme("Business");
            Combo = product ? new DaisyProductThemeDropdown() : new DaisyThemeDropdown();
        }

        public void AddLocal(string name, string label, bool preferred)
        {
            if (Combo is DaisyThemeDropdown standard) standard.AddTheme(name, label, preferred);
            else ((DaisyProductThemeDropdown)Combo).AddTheme(name, label, preferred);
        }

        public void AddGlobal(string name, string label, bool preferred)
        {
            _globalNames.Add(name);
            DaisyThemeManager.AddTheme(name, label, preferred);
        }

        public void Show() { Window.Content = Combo; Window.Show(); Layout(); }
        public void Layout() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); }
        private bool Apply(string name) { ApplyCalls++; return true; }

        public void Dispose()
        {
            Window.Close();
            foreach (var name in _globalNames) DaisyThemeManager.RemoveAddedTheme(name);
            // The current theme cannot be unregistered, so it is restored first.
            DaisyThemeManager.SetCurrentTheme(_current ?? string.Empty);
            ThemeRegistrations.RemoveAddedSince(_registered);
            DaisyThemeManager.ExcludedThemes = _excluded;
            DaisyThemeManager.PreferredThemes = _preferred;
            DaisyThemeManager.CustomThemeApplicator = _applicator;
            DaisyThemeManager.SuppressThemeApplication = _suppressed;
        }
    }
}
