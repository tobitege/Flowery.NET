using System.IO;

namespace Flowery.NET.Photos.Services
{
    /// <summary>
    /// Represents saved window position and size.
    /// </summary>
    public record WindowBounds(int X, int Y, int Width, int Height);

    public enum PhotoSortMode
    {
        DateTakenNewest,
        DateTakenOldest,
        DateModifiedNewest,
        NameAscending,
        NameDescending,
        SizeLargest
    }

    public enum PhotoGroupMode
    {
        Day,
        Month,
        Year,
        None
    }

    /// <summary>
    /// Persisted app settings (theme, language, size, window, library and viewer preferences).
    /// Uses the Flowery state storage the same way as the Gallery app.
    /// </summary>
    public static class PhotosSettings
    {
        private const string ThemeKey = "theme";
        private const string LanguageKey = "language";
        private const string SizeKey = "size";
        private const string WindowKey = "window";
        private const string FoldersKey = "photos.folders";
        private const string FavoritesKey = "photos.favorites";
        private const string AlbumsKey = "photos.albums";
        private const string OptionsKey = "photos.options";

        public static readonly WindowBounds DefaultWindowBounds = new(80, 60, 1280, 820);

        public static string StateDirectory { get; private set; } = System.IO.Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flowery.NET.Photos");

        public static void Initialize(string stateDirectory)
        {
            if (string.IsNullOrWhiteSpace(stateDirectory) || !Path.IsPathFullyQualified(stateDirectory))
                throw new ArgumentException("The Photos state directory must be an absolute path.", nameof(stateDirectory));
            var normalized = Path.GetFullPath(stateDirectory);
            StateDirectory = normalized;
            StateStorageProvider.Configure(new FileStateStorage(normalized));
            LoadOptions();
        }

        private static string? LoadSingle(string key)
        {
            var lines = StateStorageProvider.Instance.LoadLines(key);
            var line = lines.Count > 0 ? lines[0] : null;
            return string.IsNullOrWhiteSpace(line) ? null : line.Trim();
        }

        private static void SaveSingle(string key, string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return;
            StateStorageProvider.Instance.SaveLines(key, (string[])[value]);
        }

        public static string? LoadTheme() => LoadSingle(ThemeKey);
        public static void SaveTheme(string themeName) => SaveSingle(ThemeKey, themeName);

        public static string? LoadLanguage() => LoadSingle(LanguageKey);
        public static void SaveLanguage(string cultureName) => SaveSingle(LanguageKey, cultureName);

        public static DaisySize? LoadGlobalSize()
        {
            var line = LoadSingle(SizeKey);
            return line != null && Enum.TryParse(line, ignoreCase: true, out DaisySize size) ? size : null;
        }

        public static void SaveGlobalSize(DaisySize size) => SaveSingle(SizeKey, size.ToString());

        public static WindowBounds? LoadWindowBounds()
        {
            var lines = StateStorageProvider.Instance.LoadLines(WindowKey);
            if (lines.Count < 4)
                return null;

            try
            {
                var x = int.Parse(lines[0].Trim(), CultureInfo.InvariantCulture);
                var y = int.Parse(lines[1].Trim(), CultureInfo.InvariantCulture);
                var width = int.Parse(lines[2].Trim(), CultureInfo.InvariantCulture);
                var height = int.Parse(lines[3].Trim(), CultureInfo.InvariantCulture);
                if (width < 100 || height < 100 || width > 10000 || height > 10000)
                    return null;
                return new WindowBounds(x, y, width, height);
            }
            catch
            {
                return null;
            }
        }

        public static void SaveWindowBounds(WindowBounds bounds)
        {
            StateStorageProvider.Instance.SaveLines(WindowKey,
            (string[])[
                bounds.X.ToString(CultureInfo.InvariantCulture),
                bounds.Y.ToString(CultureInfo.InvariantCulture),
                bounds.Width.ToString(CultureInfo.InvariantCulture),
                bounds.Height.ToString(CultureInfo.InvariantCulture)
            ]);
        }

        public static List<string> LoadFolders()
        {
            return [.. StateStorageProvider.Instance.LoadLines(FoldersKey).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim())];
        }

        public static bool HasSavedFolders() => StateStorageProvider.Instance.LoadLines(FoldersKey).Count > 0;

        public static void SaveFolders(IEnumerable<string> folders)
        {
            var lines = folders.ToList();
            // Keep a marker line so an intentionally empty library is not re-seeded with defaults.
            StateStorageProvider.Instance.SaveLines(FoldersKey, lines.Count == 0 ? [" "] : lines);
        }

        public static HashSet<string> LoadFavorites()
        {
            return new HashSet<string>(
                StateStorageProvider.Instance.LoadLines(FavoritesKey).Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()),
                PhotoLibrary.PathComparer);
        }

        public static void SaveFavorites(IEnumerable<string> favorites)
        {
            StateStorageProvider.Instance.SaveLines(FavoritesKey, favorites);
        }

        /// <summary>
        /// Albums are stored one per line: name, then paths, separated by tab characters
        /// (tabs are not valid in Windows file paths).
        /// </summary>
        public static List<(string Name, List<string> Paths)> LoadAlbums()
        {
            List<(string, List<string>)> result = [];
            foreach (var line in StateStorageProvider.Instance.LoadLines(AlbumsKey))
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var parts = line.Split('\t');
                result.Add((parts[0], [.. parts.Skip(1).Where(p => !string.IsNullOrWhiteSpace(p))]));
            }
            return result;
        }

        public static void SaveAlbums(IEnumerable<(string Name, IEnumerable<string> Paths)> albums)
        {
            StateStorageProvider.Instance.SaveLines(AlbumsKey,
                albums.Select(a => string.Join('\t', new[] { a.Name.Replace('\t', ' ') }.Concat(a.Paths))));
        }

        #region View options

        public static double TileHeight { get; set; } = 200;
        public static PhotoSortMode SortMode { get; set; } = PhotoSortMode.DateTakenNewest;
        public static PhotoGroupMode GroupMode { get; set; } = PhotoGroupMode.Month;
        public static bool ShowFileNames { get; set; }
        public static bool IncludeSubfolders { get; set; } = true;
        public static bool ConfirmDelete { get; set; } = true;
        public static bool WheelNavigates { get; set; }
        public static double SlideshowSeconds { get; set; } = 4;
        public static int JpegQuality { get; set; } = 92;
        public static bool EditPanelOpen { get; set; } = true;
        public static bool SquareTiles { get; set; }

        // Viewer "View" options
        public static bool ShowRulers { get; set; }
        public static bool ShowGrid { get; set; }
        public static bool ShowStatusBar { get; set; } = true;
        public static bool AutoHideToolbar { get; set; }
        public static bool ShowNavigator { get; set; }

        /// <summary>Secondary markup color (drawn with the right mouse button).</summary>
        public static Color SecondaryColor { get; set; } = Color.FromArgb(255, 255, 255, 255);

        /// <summary>User-defined markup colors (9 slots, white until configured).</summary>
        public static Color[] CustomColors { get; } = [.. Enumerable.Repeat(Color.FromArgb(255, 255, 255, 255), 9)];

        private static string ToHex(Color c) => $"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}";

        private static bool TryParseHex(string text, out Color color)
        {
            color = default;
            text = text.Trim().TrimStart('#');
            if (text.Length != 8 || !uint.TryParse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var v))
                return false;
            color = Color.FromArgb((byte)(v >> 24), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
            return true;
        }

        private static void LoadOptions()
        {
            foreach (var line in StateStorageProvider.Instance.LoadLines(OptionsKey))
            {
                var idx = line.IndexOf('=');
                if (idx <= 0)
                    continue;
                var key = line[..idx].Trim();
                var value = line[(idx + 1)..].Trim();
                var inv = CultureInfo.InvariantCulture;
                switch (key)
                {
                    case nameof(TileHeight) when double.TryParse(value, NumberStyles.Float, inv, out var d):
                        TileHeight = Math.Clamp(d, 90, 420);
                        break;
                    case nameof(SortMode) when Enum.TryParse(value, out PhotoSortMode sort):
                        SortMode = sort;
                        break;
                    case nameof(GroupMode) when Enum.TryParse(value, out PhotoGroupMode group):
                        GroupMode = group;
                        break;
                    case nameof(ShowFileNames) when bool.TryParse(value, out var b):
                        ShowFileNames = b;
                        break;
                    case nameof(IncludeSubfolders) when bool.TryParse(value, out var b):
                        IncludeSubfolders = b;
                        break;
                    case nameof(ConfirmDelete) when bool.TryParse(value, out var b):
                        ConfirmDelete = b;
                        break;
                    case nameof(WheelNavigates) when bool.TryParse(value, out var b):
                        WheelNavigates = b;
                        break;
                    case nameof(SlideshowSeconds) when double.TryParse(value, NumberStyles.Float, inv, out var d):
                        SlideshowSeconds = Math.Clamp(d, 1, 30);
                        break;
                    case nameof(JpegQuality) when int.TryParse(value, NumberStyles.Integer, inv, out var i):
                        JpegQuality = Math.Clamp(i, 40, 100);
                        break;
                    case nameof(EditPanelOpen) when bool.TryParse(value, out var b):
                        EditPanelOpen = b;
                        break;
                    case nameof(SquareTiles) when bool.TryParse(value, out var b):
                        SquareTiles = b;
                        break;
                    case nameof(ShowRulers) when bool.TryParse(value, out var b):
                        ShowRulers = b;
                        break;
                    case nameof(ShowGrid) when bool.TryParse(value, out var b):
                        ShowGrid = b;
                        break;
                    case nameof(ShowStatusBar) when bool.TryParse(value, out var b):
                        ShowStatusBar = b;
                        break;
                    case nameof(AutoHideToolbar) when bool.TryParse(value, out var b):
                        AutoHideToolbar = b;
                        break;
                    case nameof(ShowNavigator) when bool.TryParse(value, out var b):
                        ShowNavigator = b;
                        break;
                    case nameof(SecondaryColor) when TryParseHex(value, out var secondary):
                        SecondaryColor = secondary;
                        break;
                    case nameof(CustomColors):
                        var parts = value.Split(',', StringSplitOptions.RemoveEmptyEntries);
                        for (var i = 0; i < Math.Min(parts.Length, CustomColors.Length); i++)
                        {
                            if (TryParseHex(parts[i], out var color))
                                CustomColors[i] = color;
                        }
                        break;
                }
            }
        }

        public static void SaveOptions()
        {
            var inv = CultureInfo.InvariantCulture;
            StateStorageProvider.Instance.SaveLines(OptionsKey,
            (string[])[
                $"{nameof(TileHeight)}={TileHeight.ToString(inv)}",
                $"{nameof(SortMode)}={SortMode}",
                $"{nameof(GroupMode)}={GroupMode}",
                $"{nameof(ShowFileNames)}={ShowFileNames}",
                $"{nameof(IncludeSubfolders)}={IncludeSubfolders}",
                $"{nameof(ConfirmDelete)}={ConfirmDelete}",
                $"{nameof(WheelNavigates)}={WheelNavigates}",
                $"{nameof(SlideshowSeconds)}={SlideshowSeconds.ToString(inv)}",
                $"{nameof(JpegQuality)}={JpegQuality.ToString(inv)}",
                $"{nameof(EditPanelOpen)}={EditPanelOpen}",
                $"{nameof(SquareTiles)}={SquareTiles}",
                $"{nameof(ShowRulers)}={ShowRulers}",
                $"{nameof(ShowGrid)}={ShowGrid}",
                $"{nameof(ShowStatusBar)}={ShowStatusBar}",
                $"{nameof(AutoHideToolbar)}={AutoHideToolbar}",
                $"{nameof(ShowNavigator)}={ShowNavigator}",
                $"{nameof(SecondaryColor)}={ToHex(SecondaryColor)}",
                $"{nameof(CustomColors)}={string.Join(',', CustomColors.Select(ToHex))}"
            ]);
        }

        #endregion
    }
}
