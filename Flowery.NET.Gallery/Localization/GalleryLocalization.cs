using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

#nullable enable

namespace Flowery.NET.Gallery.Localization
{
    /// <summary>
    /// Provides JSON-based localization services for Flowery.NET.Gallery application.
    /// </summary>
    public class GalleryLocalization : INotifyPropertyChanged
    {
        private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;
        private static readonly Dictionary<string, Dictionary<string, string>> _translations = new();
        private static readonly Lazy<GalleryLocalization> _instance = new Lazy<GalleryLocalization>(() => new GalleryLocalization());


        public static GalleryLocalization Instance => _instance.Value;
        public static event EventHandler<CultureInfo>? CultureChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        static GalleryLocalization()
        {
            // Load available translations at startup - use library's list to stay in sync
            foreach (var lang in Flowery.Localization.FloweryLocalization.SupportedLanguages)
                LoadTranslation(lang);

            // Subscribe to FloweryLocalization culture changes
            Flowery.Localization.FloweryLocalization.CultureChanged += OnFloweryCultureChanged;

            // Register Gallery's localization as the custom resolver for FloweryComponentSidebar
            // This allows sidebar items to use Gallery-specific keys (Sidebar_*, Effects_*, etc.)
            Flowery.Localization.FloweryLocalization.CustomResolver = GetString;
        }

        private static void OnFloweryCultureChanged(object? sender, CultureInfo culture)
        {
            SetCulture(culture);
        }

        private GalleryLocalization() { }

        public static CultureInfo CurrentCulture => _currentCulture;

        /// <summary>
        /// Indexer for XAML binding: {Binding [KeyName]}
        /// </summary>
        public string this[string key] => GetString(key);

        public static void SetCulture(CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));

            if (_currentCulture.Name == culture.Name)
                return;

            _currentCulture = culture;

            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            CultureChanged?.Invoke(null, culture);

            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item"));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
        }

        public static void SetCulture(string cultureName)
        {
            SetCulture(new CultureInfo(cultureName));
        }

        public static string GetString(string key)
        {
            try
            {
                // Try exact culture match first (e.g., "de-DE")
                if (_translations.TryGetValue(_currentCulture.Name, out var exactDict) && exactDict.TryGetValue(key, out var exactValue))
                {
                    return exactValue;
                }

                // Try language-only match (e.g., "de")
                var languageCode = _currentCulture.TwoLetterISOLanguageName;
                if (_translations.TryGetValue(languageCode, out var langDict) && langDict.TryGetValue(key, out var langValue))
                    return langValue;

                // Fallback to English
                if (_translations.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var enValue))
                    return enValue;

                // Return key if not found
                return key;
            }
            catch
            {
                return key;
            }
        }

        private static void LoadTranslation(string languageCode)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resourceName = $"Flowery.NET.Gallery.Localization.{languageCode}.json";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    return;

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();

                // Use source generator context for AOT compatibility
                var dict = JsonSerializer.Deserialize(json, LocalizationJsonContext.Default.DictionaryStringString);

                if (dict != null)
                    _translations[languageCode] = dict;
            }
            catch
            {
                // Silently ignore - fallback to English will be used
            }
        }
    }

    /// <summary>
    /// JSON source generator context for AOT/WASM compatibility.
    /// </summary>
    [JsonSourceGenerationOptions(
        WriteIndented = false,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class LocalizationJsonContext : JsonSerializerContext
    {
    }
}
