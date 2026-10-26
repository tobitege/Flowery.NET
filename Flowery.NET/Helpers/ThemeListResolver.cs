using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Media;
using Flowery.Helpers;
using Flowery.Theming;

namespace Flowery.Controls;

internal interface IThemeListItem
{
    string Name { get; }
    string DisplayName { get; }
    IThemeListItem WithSeparator(bool show);
}

/// <summary>Resolves theme lists without changing the source entries or applying a theme.</summary>
internal static class ThemeListResolver
{
    private static List<ThemePreviewInfo>? _standardThemes;
    private static List<ProductThemePreviewInfo>? _productThemes;

    static ThemeListResolver()
    {
        DaisyThemeManager.AvailableThemesChanged += OnAvailableThemesChanged;
    }

    private static void OnAvailableThemesChanged(object? sender, EventArgs e) => InvalidateStandardThemes();
    internal static void InvalidateStandardThemes() => _standardThemes = null;
    internal static void InvalidateProductThemes() => _productThemes = null;

    internal static IEnumerable<ThemePreviewInfo> StandardThemes() =>
        _standardThemes ??= [.. DaisyThemeManager.AvailableThemes.Select(static info => CreatePreview(info.Name, null, null))];

    internal static IEnumerable<ProductThemePreviewInfo> ProductThemes() =>
        _productThemes ??= [.. ProductPaletteFactory.GetAll().Select(static palette => new ProductThemePreviewInfo(palette))];

    internal static List<DaisyThemeInfo> AvailableThemes(IEnumerable<DaisyThemeInfo> themes) =>
        [.. Order(themes, static info => info.Name, static info => info.Name, DaisyThemeManager.ThemeOverrides, true)];

    internal static List<T> Resolve<T>(IEnumerable<T> source,
        IReadOnlyDictionary<string, DaisyAddedTheme> localOverrides,
        Func<DaisyAddedTheme, T> createOverride) where T : IThemeListItem
    {
        var overrides = new Dictionary<string, DaisyAddedTheme>(DaisyThemeManager.ThemeOverrides, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in localOverrides)
            overrides[entry.Key] = entry.Value;

        var entries = new Dictionary<string, T>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in source)
            entries[item.Name] = item;
        foreach (var entry in overrides.Values)
        {
            if (DaisyThemeManager.IsThemeVisible(entry.Name))
                entries[entry.Name] = createOverride(entry);
        }

        List<T> result = [.. Order(entries.Values, static item => item.Name, static item => item.DisplayName, overrides, overrides.Count > 0)];
        var boundary = result.FindIndex(item => !IsPreferred(item.Name, overrides));
        for (var index = 0; index < result.Count; index++)
            result[index] = (T)result[index].WithSeparator(boundary > 0 && index == boundary);
        return result;
    }

    private static IEnumerable<T> Order<T>(IEnumerable<T> source, Func<T, string> name, Func<T, string> displayName,
        IReadOnlyDictionary<string, DaisyAddedTheme> overrides, bool alphabetical)
    {
        var ordered = source.Where(item => DaisyThemeManager.IsThemeVisible(name(item)))
            .OrderByDescending(item => IsPreferred(name(item), overrides));
        return alphabetical
            ? ordered.ThenBy(item => overrides.TryGetValue(name(item), out var entry) ? entry.DisplayName : displayName(item), StringComparer.CurrentCultureIgnoreCase)
                .ThenBy(name, StringComparer.OrdinalIgnoreCase)
            : ordered.ThenBy(item => IsPreferred(name(item), overrides) ? displayName(item) : string.Empty,
                StringComparer.CurrentCultureIgnoreCase);
    }

    private static bool IsPreferred(string name, IReadOnlyDictionary<string, DaisyAddedTheme> overrides) =>
        overrides.TryGetValue(name, out var entry)
            ? entry.Preferred
            : DaisyThemeManager.IsThemePreferred(name);

    internal static ThemePreviewInfo CreatePreview(DaisyAddedTheme entry) =>
        CreatePreview(entry.Name, entry.DisplayName, entry.ProductPalette);

    private static ThemePreviewInfo CreatePreview(string name, string? displayName, ProductPalette? product)
    {
        var registered = DaisyThemeManager.GetThemeInfo(name);
        var preview = new ThemePreviewInfo
        {
            Name = name,
            DisplayNameOverride = displayName,
            IsDark = registered?.IsDark ?? (product != null && FloweryColorHelpers.IsDark(product.Background))
        };
        ResourceDictionary? palette = null;
        if (registered != null)
            DaisyThemeManager.TryCreatePalette(name, out palette);
        else if (product != null)
        {
            var precompiled = ProductPalettes.Get(name);
            palette = precompiled != null ? DaisyPaletteFactory.Create(precompiled) : ProductPaletteFactory.CreateResourceDictionary(product);
        }
        if (palette != null)
        {
            preview.Base100 = Brush(palette, "DaisyBase100Brush");
            preview.BaseContent = Brush(palette, "DaisyBaseContentBrush");
            preview.Primary = Brush(palette, "DaisyPrimaryBrush");
            preview.Secondary = Brush(palette, "DaisySecondaryBrush");
            preview.Accent = Brush(palette, "DaisyAccentBrush");
        }
        return preview;
    }

    private static IBrush Brush(ResourceDictionary palette, string key) =>
        palette.TryGetResource(key, null, out var value) && value is IBrush brush ? brush : Brushes.Gray;
}
