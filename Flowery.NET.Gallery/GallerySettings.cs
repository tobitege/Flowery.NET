using System;
using System.Globalization;
using System.IO;
using Flowery.Controls;

namespace Flowery.NET.Gallery;

public static class GallerySettings
{
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
}
