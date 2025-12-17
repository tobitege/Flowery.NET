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

namespace Flowery.Localization
{
    /// <summary>
    /// Provides JSON-based localization services for Flowery.NET.
    /// Use this class to switch languages at runtime and retrieve localized strings.
    /// Uses embedded JSON files for WASM compatibility (RESX satellite assemblies don't work in browser).
    /// </summary>
    public class FloweryLocalization : INotifyPropertyChanged
    {
        private static CultureInfo _currentCulture = CultureInfo.CurrentUICulture;
        private static readonly Dictionary<string, Dictionary<string, string>> _translations = new();
        private static readonly Lazy<FloweryLocalization> _instance = new(() => new FloweryLocalization());

        /// <summary>
        /// Singleton instance for XAML markup extension bindings.
        /// </summary>
        public static FloweryLocalization Instance => _instance.Value;

        /// <summary>
        /// Event fired when the culture is changed. Subscribe to this to refresh UI bindings.
        /// </summary>
        public static event EventHandler<CultureInfo>? CultureChanged;

        /// <summary>
        /// PropertyChanged event for INotifyPropertyChanged interface (used by XAML bindings).
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        static FloweryLocalization()
        {
            // Load all available translations from embedded JSON resources
            LoadTranslation("en");
            LoadTranslation("de");
            LoadTranslation("fr");
            LoadTranslation("es");
            LoadTranslation("it");
            LoadTranslation("ja");
            LoadTranslation("ko");
            LoadTranslation("zh-CN");
            LoadTranslation("ar");
            LoadTranslation("tr");
            LoadTranslation("uk");
            LoadTranslation("he");
        }

        private FloweryLocalization()
        {
            // Private constructor for singleton
        }

        /// <summary>
        /// Gets the current UI culture used for localization.
        /// </summary>
        public static CultureInfo CurrentCulture => _currentCulture;

        /// <summary>
        /// Gets whether the current culture is Right-To-Left.
        /// </summary>
        public bool IsRtl => _currentCulture.TextInfo.IsRightToLeft;

        /// <summary>
        /// Indexer to support XAML markup extension bindings.
        /// Usage in XAML: {loc:Localize Button_Generate} binds to this[Button_Generate]
        /// </summary>
        public string this[string key] => GetString(key);

        /// <summary>
        /// Sets the current UI culture and notifies subscribers.
        /// </summary>
        /// <param name="culture">The culture to switch to.</param>
        public static void SetCulture(CultureInfo culture)
        {
            if (culture == null)
                throw new ArgumentNullException(nameof(culture));

            if (_currentCulture.Name == culture.Name)
                return;

            _currentCulture = culture;

            // Set culture at multiple levels
            Thread.CurrentThread.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentCulture = culture;

            CultureChanged?.Invoke(null, culture);

            // Notify all bindings that use the indexer to update
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item"));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs("Item[]"));
            Instance.PropertyChanged?.Invoke(Instance, new PropertyChangedEventArgs(nameof(IsRtl)));
        }

        /// <summary>
        /// Sets the current UI culture by name and notifies subscribers.
        /// </summary>
        /// <param name="cultureName">The culture name (e.g., "en-US", "de-DE").</param>
        public static void SetCulture(string cultureName)
        {
            SetCulture(new CultureInfo(cultureName));
        }

        /// <summary>
        /// Optional custom resolver for app-specific localization keys.
        /// When set, GetString will use this resolver for keys not found in the library's translations.
        /// The resolver should return the localized string, or the key itself if not found.
        /// </summary>
        public static Func<string, string>? CustomResolver { get; set; }

        /// <summary>
        /// Gets a localized string by key from the library's internal translations.
        /// This method is used by library controls for their own keys (Size_*, Theme_*, Accessibility_*, etc.)
        /// and is not affected by the CustomResolver.
        /// </summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized string, or the key if not found.</returns>
        internal static string GetStringInternal(string key)
        {
            try
            {
                // Try exact culture match first (e.g., "de-DE")
                if (_translations.TryGetValue(_currentCulture.Name, out var exactDict) && exactDict.TryGetValue(key, out var exactValue))
                    return exactValue;

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

        /// <summary>
        /// Gets a localized string by key from the library's internal translations, with a fallback value.
        /// </summary>
        /// <param name="key">The resource key.</param>
        /// <param name="fallback">The fallback value to return if the key is not found.</param>
        /// <returns>The localized string, or the fallback if not found.</returns>
        internal static string GetStringInternal(string key, string fallback)
        {
            var value = GetStringInternal(key);
            return string.Equals(value, key, StringComparison.Ordinal) ? fallback : value;
        }

        /// <summary>
        /// Gets a localized string by key. Uses the CustomResolver if set, otherwise falls back to library translations.
        /// This is the public method for app-specific keys (like Sidebar_*) that may be provided by the consuming app.
        /// </summary>
        /// <param name="key">The resource key.</param>
        /// <returns>The localized string, or the key if not found.</returns>
        public static string GetString(string key)
        {
            // If a custom resolver is set, use it
            if (CustomResolver != null)
                return CustomResolver(key);

            // Fall back to library's internal translations
            return GetStringInternal(key);
        }

        /// <summary>
        /// Gets the localized display name for a theme.
        /// </summary>
        /// <param name="themeName">The internal theme name (e.g., "Synthwave").</param>
        /// <returns>The localized display name.</returns>
        public static string GetThemeDisplayName(string themeName)
        {
            var key = $"Theme_{themeName}";
            var result = GetStringInternal(key);  // Use internal - theme keys are library keys

            // Final fallback: use the internal theme name if key not found
            return result == key ? themeName : result;
        }

        private static void LoadTranslation(string languageCode)
        {
            try
            {
                var assembly = typeof(FloweryLocalization).Assembly;
                var resourceName = $"Flowery.NET.Localization.{languageCode}.json";
                
                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    return;

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                
                // Use source generator for AOT compatibility
                var dict = JsonSerializer.Deserialize(json, FloweryLocalizationJsonContext.Default.DictionaryStringString);
                
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
    internal partial class FloweryLocalizationJsonContext : JsonSerializerContext
    {
    }
}
