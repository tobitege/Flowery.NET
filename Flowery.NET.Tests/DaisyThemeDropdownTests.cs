using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyThemeDropdownTests
    {
        [AvaloniaFact]
        public void TryCreatePalette_UsesRegisteredFactory()
        {
            var expected = CreatePreviewPalette("#111111", "#222222", "#333333", "#444444", "#555555");
            DaisyThemeManager.RegisterTheme(
                new DaisyThemeInfo("FactoryAccessorTheme", true),
                () => expected);

            try
            {
                Assert.NotNull(DaisyThemeManager.GetPaletteFactory("FactoryAccessorTheme"));
                Assert.True(DaisyThemeManager.TryCreatePalette("FactoryAccessorTheme", out var palette));
                Assert.Same(expected, palette);
                Assert.False(DaisyThemeManager.TryCreatePalette("ThemeThatDoesNotExist", out var missing));
                Assert.Null(missing);
            }
            finally
            {
                DaisyThemeManager.UnregisterTheme("FactoryAccessorTheme");
            }
        }

        [AvaloniaFact]
        public void AvailableThemesChanged_FiresOnlyForNewThemeNames()
        {
            var raised = 0;
            EventHandler handler = (_, _) => raised++;
            var previousNotifyInternal = DaisyThemeManager.NotifyForInternalThemesChanged;
            var darkFactory = Assert.IsAssignableFrom<Func<ResourceDictionary>>(DaisyThemeManager.GetPaletteFactory("Dark"));
            DaisyThemeManager.NotifyForInternalThemesChanged = false;
            DaisyThemeManager.AvailableThemesChanged += handler;
            try
            {
                DaisyThemeManager.RegisterTheme(
                    new DaisyThemeInfo("Dark", true),
                    () => new ResourceDictionary());
                Assert.Equal(0, raised);

                DaisyThemeManager.RegisterTheme(
                    new DaisyThemeInfo("AvailableThemesChangedNewTheme", false),
                    () => new ResourceDictionary());
                Assert.Equal(1, raised);

                DaisyThemeManager.RegisterTheme(
                    new DaisyThemeInfo("AvailableThemesChangedNewTheme", true),
                    () => new ResourceDictionary());
                Assert.Equal(1, raised);

                DaisyThemeManager.NotifyForInternalThemesChanged = true;
                DaisyThemeManager.RegisterTheme(
                    new DaisyThemeInfo("Dark", true),
                    () => new ResourceDictionary());
                Assert.Equal(2, raised);
            }
            finally
            {
                DaisyThemeManager.AvailableThemesChanged -= handler;
                DaisyThemeManager.NotifyForInternalThemesChanged = previousNotifyInternal;
                // The built-in theme keeps its registration, so only its palette can be put back.
                DaisyThemeManager.RegisterTheme(new DaisyThemeInfo("Dark", true), darkFactory);
                DaisyThemeManager.UnregisterTheme("AvailableThemesChangedNewTheme");
            }
        }

        [AvaloniaFact]
        public void AddedThemes_Is_A_Snapshot_Any_Thread_Can_Read()
        {
            var before = DaisyThemeManager.AddedThemes;
            DaisyThemeManager.AddTheme("business", "Firma", true);
            try
            {
                var added = DaisyThemeManager.AddedThemes;
                Assert.Equal(new DaisyAddedTheme("Business", "Firma", true), added["BUSINESS"]);
                Assert.DoesNotContain("Business", before.Keys);
                Assert.Same(added, System.Threading.Tasks.Task.Run(static () => DaisyThemeManager.AddedThemes).Result);
            }
            finally
            {
                Assert.True(DaisyThemeManager.RemoveAddedTheme("Business"));
            }
            Assert.DoesNotContain("Business", DaisyThemeManager.AddedThemes.Keys);

            // A product theme compares by what callers see, like any other entry.
            DaisyThemeManager.AddTheme("TheaterCinema", "Kino", false);
            Assert.Equal(new DaisyAddedTheme("TheaterCinema", "Kino", false), DaisyThemeManager.AddedThemes["TheaterCinema"]);
            Assert.True(DaisyThemeManager.RemoveAddedTheme("TheaterCinema"));
            // Adding a product theme registered it, and removing the entry keeps it registered.
            Assert.True(DaisyThemeManager.UnregisterTheme("TheaterCinema"));

            // The name from before RemoveAddedTheme still works.
            DaisyThemeManager.AddTheme("Business", "Firma", false);
#pragma warning disable CS0618
            Assert.True(DaisyThemeManager.RemoveThemeOverride("Business"));
            var dropdown = new DaisyThemeDropdown();
            dropdown.AddTheme("Business", "Firma", false);
            Assert.True(dropdown.RemoveThemeOverride("Business"));
#pragma warning restore CS0618
            Assert.Empty(DaisyThemeManager.AddedThemes);

            // Unregistering a theme removes its entry too.
            DaisyThemeManager.RegisterTheme(new DaisyThemeInfo("AddedLater", false), () => new ResourceDictionary());
            DaisyThemeManager.AddTheme("AddedLater", "Later", false);
            Assert.True(DaisyThemeManager.AddedThemes.ContainsKey("addedlater"));
            Assert.True(DaisyThemeManager.UnregisterTheme("AddedLater"));
            Assert.False(DaisyThemeManager.AddedThemes.ContainsKey("AddedLater"));
        }

        [AvaloniaFact]
        public void UnregisterTheme_RemovesOnlyRegisteredThemes()
        {
            var previous = DaisyThemeManager.CurrentThemeName;
            var raised = 0;
            EventHandler handler = (_, _) => raised++;
            DaisyThemeManager.RegisterTheme(new DaisyThemeInfo("UnregisteredLater", false), () => new ResourceDictionary());
            DaisyThemeManager.AvailableThemesChanged += handler;
            try
            {
                Assert.Contains(DaisyThemeManager.AvailableThemes, theme => theme.Name == "UnregisteredLater");
                Assert.True(DaisyThemeManager.UnregisterTheme("UnregisteredLater"));
                Assert.Equal(1, raised);
                Assert.DoesNotContain(DaisyThemeManager.AvailableThemes, theme => theme.Name == "UnregisteredLater");
                Assert.Null(DaisyThemeManager.GetThemeInfo("UnregisteredLater"));

                Assert.False(DaisyThemeManager.UnregisterTheme("UnregisteredLater"));
                Assert.False(DaisyThemeManager.UnregisterTheme("Dark"));
                Assert.NotNull(DaisyThemeManager.GetThemeInfo("Dark"));
                Assert.Equal(1, raised);

                // The current theme stays, and a theme leaves the dropdowns with its added entry.
                DaisyThemeManager.RegisterTheme(new DaisyThemeInfo("UnregisteredLater", true), () => new ResourceDictionary());
                DaisyThemeManager.AddTheme("UnregisteredLater", "Later", true);
                DaisyThemeManager.SetCurrentTheme("UnregisteredLater");
                Assert.False(DaisyThemeManager.UnregisterTheme("UnregisteredLater"));
                Assert.True(DaisyThemeManager.IsDarkTheme("UnregisteredLater"));
                DaisyThemeManager.SetCurrentTheme(previous ?? string.Empty);
                Assert.True(DaisyThemeManager.UnregisterTheme("UnregisteredLater"));
                Assert.False(DaisyThemeManager.RemoveAddedTheme("UnregisteredLater"));
                Assert.Null(FindPreviewOrDefault(new DaisyThemeDropdown(), "UnregisteredLater"));
            }
            finally
            {
                DaisyThemeManager.AvailableThemesChanged -= handler;
                DaisyThemeManager.SetCurrentTheme(previous ?? string.Empty);
                DaisyThemeManager.RemoveAddedTheme("UnregisteredLater");
                DaisyThemeManager.UnregisterTheme("UnregisteredLater");
            }
        }

        [AvaloniaFact]
        public void RegisteredTheme_PreviewUsesPaletteFactoryBrushes()
        {
            var base100 = new SolidColorBrush(Color.Parse("#101010"));
            var baseContent = new SolidColorBrush(Color.Parse("#E0E0E0"));
            var primary = new SolidColorBrush(Color.Parse("#112233"));
            var secondary = new SolidColorBrush(Color.Parse("#445566"));
            var accent = new SolidColorBrush(Color.Parse("#778899"));

            DaisyThemeManager.RegisterTheme(
                new DaisyThemeInfo("PreviewFactoryTheme", true),
                () => new ResourceDictionary
                {
                    ["DaisyBase100Brush"] = base100,
                    ["DaisyBaseContentBrush"] = baseContent,
                    ["DaisyPrimaryBrush"] = primary,
                    ["DaisySecondaryBrush"] = secondary,
                    ["DaisyAccentBrush"] = accent
                });

            try
            {
                DaisyThemeDropdown.InvalidateThemeCache();
                var dropdown = new DaisyThemeDropdown();
                var preview = FindPreview(dropdown, "PreviewFactoryTheme");

                Assert.Same(base100, preview.Base100);
                Assert.Same(baseContent, preview.BaseContent);
                Assert.Same(primary, preview.Primary);
                Assert.Same(secondary, preview.Secondary);
                Assert.Same(accent, preview.Accent);
            }
            finally
            {
                DaisyThemeManager.UnregisterTheme("PreviewFactoryTheme");
            }
        }

        [AvaloniaFact]
        public void LateRegisteredTheme_AppearsInAttachedDropdown()
        {
            var dropdown = new DaisyThemeDropdown();
            var window = new Window
            {
                Width = 400,
                Height = 200,
                Content = dropdown
            };

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Assert.Null(FindPreviewOrDefault(dropdown, "LatePreviewTheme"));

                var primary = new SolidColorBrush(Color.Parse("#ABCDEF"));
                DaisyThemeManager.RegisterTheme(
                    new DaisyThemeInfo("LatePreviewTheme", false),
                    () => new ResourceDictionary
                    {
                        ["DaisyPrimaryBrush"] = primary
                    });
                Dispatcher.UIThread.RunJobs();

                var preview = FindPreview(dropdown, "LatePreviewTheme");
                Assert.Same(primary, preview.Primary);
                Assert.False(preview.IsDark);
            }
            finally
            {
                window.Close();
                DaisyThemeManager.UnregisterTheme("LatePreviewTheme");
            }
        }

        [AvaloniaFact]
        public void RuntimeRegisteredCorporateCopy_RaisesEventAndShowsSwatchColor()
        {
            var dropdown = new DaisyThemeDropdown();
            var window = new Window
            {
                Width = 400,
                Height = 200,
                Content = dropdown
            };
            var raised = 0;
            EventHandler handler = (_, _) => raised++;
            DaisyThemeManager.AvailableThemesChanged += handler;

            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();

                Assert.Null(FindPreviewOrDefault(dropdown, "CorporateCopy"));

                var expected = Color.Parse("#F0F0F0");
                DaisyThemeManager.RegisterTheme(
                    new DaisyThemeInfo("CorporateCopy", false),
                    () => CloneCorporateWithBaseOverride(expected));
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(1, raised);
                Assert.True(DaisyThemeManager.TryCreatePalette("CorporateCopy", out var registered));
                Assert.NotNull(registered);
                Assert.Equal(expected, GetResourceColor(registered, "DaisyBase200Brush"));

                var preview = FindPreview(dropdown, "CorporateCopy");
                Assert.False(preview.IsDark);
                Assert.Equal(expected, GetBrushColor(preview.Base100));
                Assert.Equal(Color.Parse("#0082CE"), GetBrushColor(preview.Primary));
                Assert.Equal(Color.Parse("#61738D"), GetBrushColor(preview.Secondary));
                Assert.Equal(Color.Parse("#009689"), GetBrushColor(preview.Accent));

                var original = FindPreview(dropdown, "Corporate");
                Assert.Equal(Color.Parse("#FFFFFF"), GetBrushColor(original.Base100));
            }
            finally
            {
                DaisyThemeManager.AvailableThemesChanged -= handler;
                window.Close();
                DaisyThemeManager.UnregisterTheme("CorporateCopy");
            }
        }

        private static ThemePreviewInfo FindPreview(DaisyThemeDropdown dropdown, string name)
        {
            var preview = FindPreviewOrDefault(dropdown, name);
            Assert.NotNull(preview);
            return preview;
        }

        private static ThemePreviewInfo? FindPreviewOrDefault(DaisyThemeDropdown dropdown, string name)
        {
            var items = Assert.IsAssignableFrom<IEnumerable<ThemePreviewInfo>>(dropdown.ItemsSource);
            return items.FirstOrDefault(item => string.Equals(item.Name, name, StringComparison.OrdinalIgnoreCase));
        }

        private static ResourceDictionary CreatePreviewPalette(
            string base100,
            string baseContent,
            string primary,
            string secondary,
            string accent)
        {
            return new ResourceDictionary
            {
                ["DaisyBase100Brush"] = new SolidColorBrush(Color.Parse(base100)),
                ["DaisyBaseContentBrush"] = new SolidColorBrush(Color.Parse(baseContent)),
                ["DaisyPrimaryBrush"] = new SolidColorBrush(Color.Parse(primary)),
                ["DaisySecondaryBrush"] = new SolidColorBrush(Color.Parse(secondary)),
                ["DaisyAccentBrush"] = new SolidColorBrush(Color.Parse(accent))
            };
        }

        // Item swatches bind Base100, not Base200. Override both so the palette
        // keeps the requested Base200 change and the visible swatch matches.
        private static ResourceDictionary CloneCorporateWithBaseOverride(Color color)
        {
            Assert.True(DaisyThemeManager.TryCreatePalette("Corporate", out var source) && source != null);

            var clone = new ResourceDictionary();
            foreach (var key in source.Keys)
            {
                clone[key] = source[key];
            }

            var brush = new SolidColorBrush(color);
            clone["DaisyBase100Color"] = color;
            clone["DaisyBase100Brush"] = brush;
            clone["DaisyBase200Color"] = color;
            clone["DaisyBase200Brush"] = new SolidColorBrush(color);
            return clone;
        }

        private static Color GetBrushColor(IBrush brush)
        {
            var solid = Assert.IsType<SolidColorBrush>(brush);
            return solid.Color;
        }

        private static Color GetResourceColor(ResourceDictionary palette, string key)
        {
            Assert.True(palette.TryGetResource(key, null, out var value));
            var brush = Assert.IsType<SolidColorBrush>(value);
            return brush.Color;
        }
    }
}
