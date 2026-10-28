using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Flowery.Controls;
using Flowery.Services;

namespace Flowery.NET.Gallery;

public static class GallerySettings
{
    private const string CarouselGlKey = "carousel.gl";

    private static readonly CarouselGlSettings DefaultCarouselGlSettings = new(
        Enabled: true,
        ModeTag: "Slideshow",
        TransitionTag: "Fade",
        DurationTag: "0.4",
        EffectTag: "None",
        IntervalSeconds: 3m,
        Infinite: false,
        SliceCount: 8m,
        Stagger: true,
        StaggerMs: 50m,
        PixelateSize: 20m,
        DissolveDensity: 0.5m,
        FlipAngle: 90m);

    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Flowery.NET.Gallery",
        "theme.txt");

    private static readonly string LanguagePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Flowery.NET.Gallery",
        "language.txt");

    private static readonly string GlobalSizePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Flowery.NET.Gallery",
        "size.txt");

    private static readonly string BackdropPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Flowery.NET.Gallery",
        "backdrop.txt");

    private static readonly string BaseColorPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Flowery.NET.Gallery",
        "base-color.txt");

    private static readonly string WindowPlacementPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Flowery.NET.Gallery",
        "window-placement.txt");

    public sealed class WindowPlacement
    {
        public required int X { get; init; }
        public required int Y { get; init; }
        public required double Width { get; init; }
        public required double Height { get; init; }
        public required string WindowState { get; init; }
    }

    public static string? Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
                return File.ReadAllText(SettingsPath).Trim();
        }
        catch { }
        return null;
    }

    public static void Save(string themeName)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            File.WriteAllText(SettingsPath, themeName);
        }
        catch { /* ignore */ }
    }

    public static string? LoadLanguage()
    {
        try
        {
            if (File.Exists(LanguagePath))
                return File.ReadAllText(LanguagePath).Trim();
        }
        catch { }
        return null;
    }

    public static void SaveLanguage(string cultureName)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LanguagePath)!);
            File.WriteAllText(LanguagePath, cultureName);
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// The backdrop chosen in the sidebar, as the text of <see cref="DaisyThemeManager.BackdropOverride"/>.
    /// </summary>
    public static string? LoadBackdrop()
    {
        try
        {
            if (File.Exists(BackdropPath))
                return File.ReadAllText(BackdropPath).Trim();
        }
        catch { }
        return null;
    }

    public static void SaveBackdrop(string? backdrop)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BackdropPath)!);
            File.WriteAllText(BackdropPath, backdrop ?? string.Empty);
        }
        catch { /* ignore */ }
    }

    /// <summary>
    /// The base color chosen in the sidebar for <see cref="DaisyThemeManager.BaseOverride"/>.
    /// </summary>
    public static Avalonia.Media.Color? LoadBaseColor()
    {
        try
        {
            if (File.Exists(BaseColorPath) && Avalonia.Media.Color.TryParse(File.ReadAllText(BaseColorPath).Trim(), out var color))
                return color;
        }
        catch { }
        return null;
    }

    public static void SaveBaseColor(Avalonia.Media.Color? color)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(BaseColorPath)!);
            File.WriteAllText(BaseColorPath, color?.ToString() ?? string.Empty);
        }
        catch { /* ignore */ }
    }

    public static DaisySize? LoadGlobalSize()
    {
        try
        {
            if (File.Exists(GlobalSizePath))
            {
                var raw = File.ReadAllText(GlobalSizePath).Trim();
                if (Enum.TryParse(raw, out DaisySize size))
                    return size;
            }
        }
        catch { }
        return null;
    }

    public static void SaveGlobalSize(DaisySize size)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(GlobalSizePath)!);
            File.WriteAllText(GlobalSizePath, size.ToString());
        }
        catch { /* ignore */ }
    }

    public static WindowPlacement? LoadWindowPlacement()
    {
        try
        {
            if (!File.Exists(WindowPlacementPath))
                return null;

            var lines = File.ReadAllLines(WindowPlacementPath);
            if (lines.Length < 5)
                return null;

            if (!int.TryParse(lines[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var x))
                return null;
            if (!int.TryParse(lines[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var y))
                return null;
            if (!double.TryParse(lines[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var width))
                return null;
            if (!double.TryParse(lines[3], NumberStyles.Float, CultureInfo.InvariantCulture, out var height))
                return null;

            var state = lines[4].Trim();
            if (string.IsNullOrWhiteSpace(state))
                return null;

            return new WindowPlacement
            {
                X = x,
                Y = y,
                Width = width,
                Height = height,
                WindowState = state
            };
        }
        catch
        {
            return null;
        }
    }

    public static void SaveWindowPlacement(WindowPlacement placement)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(WindowPlacementPath)!);
            File.WriteAllLines(WindowPlacementPath,
            [
                placement.X.ToString(CultureInfo.InvariantCulture),
                placement.Y.ToString(CultureInfo.InvariantCulture),
                placement.Width.ToString(CultureInfo.InvariantCulture),
                placement.Height.ToString(CultureInfo.InvariantCulture),
                placement.WindowState
            ]);
        }
        catch { /* ignore */ }
    }

    public static CarouselGlSettings? LoadCarouselGlSettings()
    {
        var values = LoadKeyValueLines(CarouselGlKey);
        if (values.Count == 0)
            return null;

        var defaults = DefaultCarouselGlSettings;
        var intervalSeconds = GetDecimal(values, "Interval", defaults.IntervalSeconds);
        if (intervalSeconds < 1m)
        {
            intervalSeconds = defaults.IntervalSeconds;
        }
        return new CarouselGlSettings(
            Enabled: GetBool(values, "Enabled", defaults.Enabled),
            ModeTag: GetString(values, "Mode") ?? defaults.ModeTag,
            TransitionTag: GetString(values, "Transition") ?? defaults.TransitionTag,
            DurationTag: GetString(values, "Duration") ?? defaults.DurationTag,
            EffectTag: GetString(values, "Effect") ?? defaults.EffectTag,
            IntervalSeconds: intervalSeconds,
            Infinite: GetBool(values, "Infinite", defaults.Infinite),
            SliceCount: GetDecimal(values, "SliceCount", defaults.SliceCount),
            Stagger: GetBool(values, "Stagger", defaults.Stagger),
            StaggerMs: GetDecimal(values, "StaggerMs", defaults.StaggerMs),
            PixelateSize: GetDecimal(values, "PixelateSize", defaults.PixelateSize),
            DissolveDensity: GetDecimal(values, "DissolveDensity", defaults.DissolveDensity),
            FlipAngle: GetDecimal(values, "FlipAngle", defaults.FlipAngle));
    }

    public static void SaveCarouselGlSettings(CarouselGlSettings settings)
    {
        SaveKeyValueLines(CarouselGlKey,
        (KeyValuePair<string, string?>[])[
            new("Enabled", settings.Enabled ? "1" : "0"),
            new("Mode", settings.ModeTag),
            new("Transition", settings.TransitionTag),
            new("Duration", settings.DurationTag),
            new("Effect", settings.EffectTag),
            new("Interval", settings.IntervalSeconds.ToString(CultureInfo.InvariantCulture)),
            new("Infinite", settings.Infinite ? "1" : "0"),
            new("SliceCount", settings.SliceCount.ToString(CultureInfo.InvariantCulture)),
            new("Stagger", settings.Stagger ? "1" : "0"),
            new("StaggerMs", settings.StaggerMs.ToString(CultureInfo.InvariantCulture)),
            new("PixelateSize", settings.PixelateSize.ToString(CultureInfo.InvariantCulture)),
            new("DissolveDensity", settings.DissolveDensity.ToString(CultureInfo.InvariantCulture)),
            new("FlipAngle", settings.FlipAngle.ToString(CultureInfo.InvariantCulture))
        ]);
    }

    private static Dictionary<string, string> LoadKeyValueLines(string key)
    {
        var lines = StateStorageProvider.Instance.LoadLines(key);
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;

            var parts = line.Split('=', 2);
            if (parts.Length != 2)
                continue;

            var name = parts[0].Trim();
            if (name.Length == 0)
                continue;

            values[name] = parts[1].Trim();
        }

        return values;
    }

    private static void SaveKeyValueLines(string key, IEnumerable<KeyValuePair<string, string?>> values)
    {
        var lines = new List<string>();
        foreach (var entry in values)
        {
            if (string.IsNullOrWhiteSpace(entry.Key))
                continue;

            var value = entry.Value ?? string.Empty;
            lines.Add($"{entry.Key}={value}");
        }

        // The state storage of Flowery.NET reports its failures; the other Gallery settings ignore them as well.
        try { StateStorageProvider.Instance.SaveLines(key, lines); }
        catch { /* ignore */ }
    }

    private static string? GetString(Dictionary<string, string> values, string key)
    {
        return values.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;
    }

    private static bool GetBool(Dictionary<string, string> values, string key, bool fallback)
    {
        if (!values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return fallback;

        if (bool.TryParse(raw, out var parsed))
            return parsed;

        if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number))
            return number != 0;

        return fallback;
    }

    private static decimal GetDecimal(Dictionary<string, string> values, string key, decimal fallback)
    {
        if (!values.TryGetValue(key, out var raw) || string.IsNullOrWhiteSpace(raw))
            return fallback;

        return decimal.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : fallback;
    }
}

public sealed record CarouselGlSettings(
    bool Enabled,
    string? ModeTag,
    string? TransitionTag,
    string? DurationTag,
    string? EffectTag,
    decimal IntervalSeconds,
    bool Infinite,
    decimal SliceCount,
    bool Stagger,
    decimal StaggerMs,
    decimal PixelateSize,
    decimal DissolveDensity,
    decimal FlipAngle);
