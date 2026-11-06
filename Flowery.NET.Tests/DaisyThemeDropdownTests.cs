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
    }
}
