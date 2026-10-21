using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Flowery.NET.Photos.Localization
{
    /// <summary>
    /// Provides JSON-based localization services for Flowery Photos.
    /// Mirrors the Gallery localization pattern: bind with {Binding [Key]} and DataContext = Localization.
    /// </summary>
    public class PhotosLocalization : INotifyPropertyChanged
    {
        private static string _currentCultureName = "en";
        private static readonly Dictionary<string, Dictionary<string, string>> _translations = [];
        private static readonly Lazy<PhotosLocalization> _instance = new(() => new PhotosLocalization());

        public static PhotosLocalization Instance => _instance.Value;
        public static event EventHandler<string>? CultureChanged;
        public event PropertyChangedEventHandler? PropertyChanged;

        static PhotosLocalization()
        {
            foreach (var lang in FloweryLocalization.SupportedLanguages)
                LoadTranslation(lang);

            FloweryLocalization.CultureChanged += OnFloweryCultureChanged;

            // Register the app's localization as the custom resolver for Flowery controls.
            FloweryLocalization.CustomResolver = GetString;

            SetCulture(FloweryLocalization.CurrentCulture.Name);
        }

        private static void OnFloweryCultureChanged(object? sender, CultureInfo culture)
        {
            SetCulture(culture.Name);
        }

        public static string CurrentCultureName => _currentCultureName;

        /// <summary>
        /// Indexer for XAML binding: {Binding [KeyName]}
        /// </summary>
        public string this[string key] => GetString(key);

        public static CultureInfo CurrentCulture
        {
            get
            {
                try
                {
                    return CultureInfo.GetCultureInfo(_currentCultureName);
                }
                catch (CultureNotFoundException)
                {
                    return CultureInfo.InvariantCulture;
                }
            }
        }

        public static void SetCulture(string cultureName)
        {
            if (string.IsNullOrEmpty(cultureName))
                return;

            if (string.Equals(_currentCultureName, cultureName, StringComparison.OrdinalIgnoreCase))
                return;

            _currentCultureName = cultureName;

            CultureChanged?.Invoke(null, cultureName);
            NotifyPropertyChangedOnUIThread();
        }

        private static void NotifyPropertyChangedOnUIThread()
        {
            var instance = Instance;
            var handler = instance.PropertyChanged;
            if (handler == null)
                return;

            void RaiseNotifications()
            {
                handler.Invoke(instance, new PropertyChangedEventArgs("Item"));
                handler.Invoke(instance, new PropertyChangedEventArgs("Item[]"));
            }

            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                RaiseNotifications();
            else
                Avalonia.Threading.Dispatcher.UIThread.Post(RaiseNotifications);
        }

        public static string GetString(string key)
        {
            try
            {
                if (_translations.TryGetValue(_currentCultureName, out var exactDict) &&
                    exactDict.TryGetValue(key, out var exactValue))
                {
                    return exactValue;
                }

                var dashIndex = _currentCultureName.IndexOf('-');
                if (dashIndex > 0)
                {
                    var twoLetter = _currentCultureName[..dashIndex];
                    if (_translations.TryGetValue(twoLetter, out var langDict) &&
                        langDict.TryGetValue(key, out var langValue))
                    {
                        return langValue;
                    }
                }

                if (_translations.TryGetValue("en", out var enDict) &&
                    enDict.TryGetValue(key, out var enValue))
                {
                    return enValue;
                }

                return key;
            }
            catch
            {
                return key;
            }
        }

        /// <summary>
        /// Resolves a key and formats it with the current culture.
        /// </summary>
        public static string Format(string key, params object[] args)
        {
            try
            {
                return string.Format(CurrentCulture, GetString(key), args);
            }
            catch (FormatException)
            {
                return GetString(key);
            }
        }

        public static string FormatLibraryStats(int photos, int folders, string fileSize)
        {
            var suffix = (photos == 1, folders == 1) switch
            {
                (true, true) => "OnePhotoOneFolder",
                (true, false) => "OnePhoto",
                (false, true) => "OneFolder",
                _ => ""
            };
            return Format("Photos_Settings_LibraryStats" + suffix, photos, folders, fileSize);
        }

        private static void LoadTranslation(string languageCode)
        {
            try
            {
                var assembly = typeof(PhotosLocalization).Assembly;
                var resourceName = $"{assembly.GetName().Name}.Localization.{languageCode}.json";

                using var stream = assembly.GetManifestResourceStream(resourceName);
                if (stream == null)
                    return;

                using var reader = new StreamReader(stream);
                var json = reader.ReadToEnd();
                if (string.IsNullOrWhiteSpace(json))
                    return;

                var dict = JsonSerializer.Deserialize(json, PhotosLocalizationJsonContext.Default.DictionaryStringString);
                if (dict != null)
                    _translations[languageCode] = dict;
            }
            catch
            {
                // Fallback to English will be used.
            }
        }
    }

    [JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
    [JsonSerializable(typeof(Dictionary<string, string>))]
    internal partial class PhotosLocalizationJsonContext : JsonSerializerContext
    {
    }
}
