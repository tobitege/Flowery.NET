using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Automation.Peers;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Kanban.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    [CollectionDefinition("LocalizationTests", DisableParallelization = true)]
    public sealed class LocalizationTestCollection
    {
    }

    // Run sequentially to avoid culture interference
    [Collection("LocalizationTests")]
    public partial class LocalizationTests : IDisposable
    {
        // These login buttons show the provider's own wording in every language.
        private static readonly HashSet<string> UntranslatedKeys = ["LoginButton_Kakao", "LoginButton_Line"];

        private readonly CultureInfo _originalCulture;

        public LocalizationTests()
        {
            _originalCulture = FloweryLocalization.CurrentCulture;
        }

        public void Dispose()
        {
            // Restore original culture after each test
            FloweryLocalization.SetCulture(_originalCulture);
        }

        [AvaloniaFact]
        public void GetString_Resolves_The_Current_Culture_And_Falls_Back()
        {
            FloweryLocalization.SetCulture("en-US");
            Assert.Equal("Pick one", FloweryLocalization.GetString("Select_Placeholder"));
            Assert.Equal("Synthwave", FloweryLocalization.GetThemeDisplayName("Synthwave"));
            Assert.Equal("NonExistentKey_12345", FloweryLocalization.GetString("NonExistentKey_12345"));

            FloweryLocalization.SetCulture("de-DE");
            Assert.Equal("Auswählen", FloweryLocalization.GetString("Select_Placeholder"));
            FloweryLocalization.SetCulture("es-ES");
            Assert.Equal("Cargando", FloweryLocalization.GetString("Accessibility_Loading"));
            Assert.Equal("En línea", FloweryLocalization.GetString("Accessibility_StatusOnline"));

            // A custom resolver that does not know a key leaves it to the registered assemblies.
            FloweryLocalization.SetCulture("en-US");
            FloweryLocalization.RegisterAssembly(typeof(FlowKanban).Assembly);
            var originalResolver = FloweryLocalization.CustomResolver;
            try
            {
                FloweryLocalization.CustomResolver = key => key;
                Assert.Equal("Add Card", FloweryLocalization.GetString("Kanban_AddCard"));
            }
            finally
            {
                FloweryLocalization.CustomResolver = originalResolver;
            }
        }

        [AvaloniaFact]
        public void SetCulture_Raises_CultureChanged_And_Keeps_The_Regional_Formatting_Culture()
        {
            var originalFormattingCulture = CultureInfo.CurrentCulture;
            var originalDefaultFormattingCulture = CultureInfo.DefaultThreadCurrentCulture;
            CultureInfo? changedTo = null;
            void OnCultureChanged(object? sender, CultureInfo culture) => changedTo = culture;
            FloweryLocalization.SetCulture("en-US");
            FloweryLocalization.CultureChanged += OnCultureChanged;

            try
            {
                FloweryLocalization.SetCulture("fr-FR");
                Assert.Equal("fr-FR", changedTo?.Name);
                Assert.Equal("fr-FR", FloweryLocalization.CurrentCulture.Name);

                var regionalCulture = new CultureInfo("de-DE");
                CultureInfo.CurrentCulture = regionalCulture;
                CultureInfo.DefaultThreadCurrentCulture = regionalCulture;
                FloweryLocalization.SetCulture("en-US");

                Assert.Equal("de-DE", CultureInfo.CurrentCulture.Name);
                Assert.Equal("de-DE", CultureInfo.DefaultThreadCurrentCulture?.Name);
            }
            finally
            {
                FloweryLocalization.CultureChanged -= OnCultureChanged;
                CultureInfo.DefaultThreadCurrentCulture = originalDefaultFormattingCulture;
                CultureInfo.CurrentCulture = originalFormattingCulture;
            }
        }

        [AvaloniaFact]
        public void Every_Key_The_Code_Asks_For_Exists_In_English()
        {
            var english = LoadCatalogs(typeof(FloweryLocalization).Assembly)["en"].Keys
                .Concat(LoadCatalogs(typeof(FlowKanban).Assembly)["en"].Keys)
                .ToHashSet(StringComparer.Ordinal);
            var sources = new[] { "Flowery.NET", "Flowery.NET.Kanban" }
                .SelectMany(project => Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), project), "*.cs", SearchOption.AllDirectories));

            var missing = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var file in sources)
            {
                foreach (Match match in RequestedKey().Matches(File.ReadAllText(file)))
                {
                    var key = match.Groups["key"].Value;
                    // A key ending in an underscore is the prefix of keys composed at run time.
                    if (!key.EndsWith('_') && !english.Contains(key))
                        missing.Add($"{key} ({Path.GetFileName(file)})");
                }
            }

            Assert.True(missing.Count == 0, "English lacks: " + string.Join(", ", missing));
        }

        [AvaloniaFact]
        public void Every_Language_Has_Every_English_Key()
        {
            var gaps = new List<string>();
            foreach (var assembly in new[] { typeof(FloweryLocalization).Assembly, typeof(FlowKanban).Assembly })
            {
                var catalogs = LoadCatalogs(assembly);
                Assert.Equal(12, catalogs.Count);
                foreach (var (language, catalog) in catalogs.Where(pair => pair.Key != "en"))
                {
                    var missing = catalogs["en"].Keys
                        .Where(key => !UntranslatedKeys.Contains(key))
                        .Where(key => !catalog.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                        .ToList();
                    if (missing.Count > 0)
                        gaps.Add($"{assembly.GetName().Name} {language}: {string.Join(", ", missing)}");
                }
            }

            Assert.True(gaps.Count == 0, "Untranslated keys:" + Environment.NewLine + string.Join(Environment.NewLine, gaps));
        }

        [AvaloniaFact]
        public void Every_Shipped_Theme_Has_A_Display_Name()
        {
            FloweryLocalization.SetCulture("en-US");
            // A theme ships with the library when the library holds its palette; others are registered at run time.
            var shipped = Directory.EnumerateFiles(Path.Combine(RepositoryRoot(), "Flowery.NET", "Themes", "Palettes"), "Daisy*.axaml")
                .Select(file => Path.GetFileNameWithoutExtension(file)["Daisy".Length..])
                .ToList();
            Assert.True(shipped.Count >= 35, $"Only {shipped.Count} shipped themes were found.");
            var unnamed = shipped
                .Select(name => $"Theme_{name}")
                .Where(key => FloweryLocalization.GetString(key) == key)
                .ToList();

            Assert.True(unnamed.Count == 0, "No display name for: " + string.Join(", ", unnamed));
        }

        [AvaloniaFact]
        public void Controls_Announce_Themselves_In_The_Current_Language()
        {
            FloweryLocalization.SetCulture("en-US");
            var indicator = new DaisyStatusIndicator { Color = DaisyColor.Error };
            using var window = new TestWindow(indicator);

            // Peers can cache their name, so each language gets a new one.
            foreach (var (culture, name) in new[] { ("en-US", "Error"), ("de", "Fehler"), ("es", "Error") })
            {
                FloweryLocalization.SetCulture(culture);
                Assert.Equal(name, ControlAutomationPeer.CreatePeerForElement(indicator).GetName());
            }

            FloweryLocalization.SetCulture("fr");
            var loading = new DaisyLoading();
            window.Show(loading);
            Assert.Equal("Chargement", ControlAutomationPeer.CreatePeerForElement(loading).GetName());
        }

        [AvaloniaFact]
        public void RegisterAssembly_ConcurrentCallsPublishAtomicSnapshotAndLogResourceErrors()
        {
            const string key = "FloweryLocalization_ConcurrentProbe";
            const string expectedValue = "Concurrent probe";
            const string invalidResourceName = "Flowery.NET.Tests.Localization.de.json";
            var assembly = typeof(LocalizationTests).Assembly;
            Assert.Contains(
                "Flowery.NET.Tests.Localization.en.json",
                assembly.GetManifestResourceNames());
            Assert.Contains(invalidResourceName, assembly.GetManifestResourceNames());
            FloweryLocalization.SetCulture("en-US");
            var observedValues = new ConcurrentBag<string>();
            using var diagnosticWriter = new StringWriter(CultureInfo.InvariantCulture);
            using var diagnosticListener = new TextWriterTraceListener(diagnosticWriter);
            Trace.Listeners.Add(diagnosticListener);

            try
            {
                System.Threading.Tasks.Parallel.For(0, 512, index =>
                {
                    if (index % 8 == 0)
                    {
                        FloweryLocalization.RegisterAssembly(assembly);
                    }
                    else
                    {
                        observedValues.Add(FloweryLocalization.GetString(key));
                    }
                });

                FloweryLocalization.RegisterAssembly(assembly);
                Trace.Flush();
                diagnosticListener.Flush();
            }
            finally
            {
                Trace.Listeners.Remove(diagnosticListener);
            }

            Assert.NotEmpty(observedValues);
            Assert.All(
                observedValues,
                value => Assert.True(
                    string.Equals(value, key, StringComparison.Ordinal) ||
                    string.Equals(value, expectedValue, StringComparison.Ordinal)));
            Assert.Equal(expectedValue, FloweryLocalization.GetString(key));

            FloweryLocalization.SetCulture("de-DE");
            Assert.Equal(expectedValue, FloweryLocalization.GetString(key));

            var diagnostics = diagnosticWriter.ToString();
            Assert.Contains(invalidResourceName, diagnostics, StringComparison.Ordinal);
            Assert.Contains(nameof(JsonException), diagnostics, StringComparison.Ordinal);
            var firstOccurrence = diagnostics.IndexOf(invalidResourceName, StringComparison.Ordinal);
            Assert.Equal(
                firstOccurrence,
                diagnostics.LastIndexOf(invalidResourceName, StringComparison.Ordinal));
        }

        /// <summary>Reads the localization catalogs an assembly ships, keyed by language.</summary>
        private static Dictionary<string, Dictionary<string, string?>> LoadCatalogs(Assembly assembly)
        {
            var prefix = $"{assembly.GetName().Name}.Localization.";
            var catalogs = new Dictionary<string, Dictionary<string, string?>>(StringComparer.Ordinal);
            foreach (var resourceName in assembly.GetManifestResourceNames()
                         .Where(name => name.StartsWith(prefix, StringComparison.Ordinal) && name.EndsWith(".json", StringComparison.Ordinal)))
            {
                using var stream = assembly.GetManifestResourceStream(resourceName)!;
                using var document = JsonDocument.Parse(stream);
                catalogs[resourceName[prefix.Length..^".json".Length]] = document.RootElement.EnumerateObject()
                    .ToDictionary(property => property.Name, property => property.Value.GetString(), StringComparer.Ordinal);
            }

            return catalogs;
        }

        private static string RepositoryRoot([CallerFilePath] string testFile = "") =>
            Path.GetDirectoryName(Path.GetDirectoryName(testFile))!;

        [GeneratedRegex("""GetString(?:Internal)?\("(?<key>[A-Za-z0-9_]+)"[,)]""")]
        private static partial Regex RequestedKey();
    }
}
