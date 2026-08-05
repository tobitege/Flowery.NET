using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Text.Json.Serialization;
using Flowery.Services;

namespace Flowery.NET.Gallery.Browser
{
    /// <summary>
    /// Browser localStorage-based state storage for WASM platforms.
    /// Uses JavaScript interop to access localStorage.
    /// </summary>
    [SupportedOSPlatform("browser")]
    public partial class BrowserStateStorage : IStateStorage
    {
        private const string LineSeparator = "\n";
        private const string StoragePrefix = "flowery_";

        public IReadOnlyList<string> LoadLines(string key)
        {
            try
            {
                var data = GetLocalStorageItem(StoragePrefix + key);
                if (string.IsNullOrEmpty(data))
                    return Array.Empty<string>();

                return data.Split(new[] { LineSeparator }, StringSplitOptions.None);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public void SaveLines(string key, IEnumerable<string> lines)
        {
            var data = string.Join(LineSeparator, lines);
            SetLocalStorageItem(StoragePrefix + key, data);
        }

        public void Delete(string key)
        {
            RemoveLocalStorageItem(StoragePrefix + key);
        }

        public void Rename(string sourceKey, string targetKey)
        {
            var data = GetLocalStorageItem(StoragePrefix + sourceKey)
                ?? throw new InvalidOperationException($"Storage key '{sourceKey}' does not exist.");

            SetLocalStorageItem(StoragePrefix + targetKey, data);
            RemoveLocalStorageItem(StoragePrefix + sourceKey);
        }

        public IEnumerable<string> GetKeys(string prefix)
        {
            try
            {
                var json = GetLocalStorageKeys(StoragePrefix, prefix ?? string.Empty);
                return JsonSerializer.Deserialize(
                    json,
                    BrowserStateStorageJsonContext.Default.StringArray) ?? Array.Empty<string>();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

#if DEBUG
        internal static void RunMutationFailureProbe()
        {
            var storage = new BrowserStateStorage();
            var saveError = CaptureSaveFailure(storage);
            var renameError = CaptureRenameFailure(storage);
            var deleteError = CaptureDeleteFailure(storage);
            var passed = saveError.Length > 0
                && renameError.Length > 0
                && deleteError.Length > 0;

            ReportMutationProbe(passed, saveError, renameError, deleteError);
        }

        private static string CaptureSaveFailure(BrowserStateStorage storage)
        {
            try
            {
                storage.SaveLines("mutation-probe-save", new[] { "probe" });
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        private static string CaptureRenameFailure(BrowserStateStorage storage)
        {
            try
            {
                storage.Rename("mutation-probe-save", "mutation-probe-rename");
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        private static string CaptureDeleteFailure(BrowserStateStorage storage)
        {
            try
            {
                storage.Delete("mutation-probe-rename");
                return string.Empty;
            }
            catch (Exception ex)
            {
                return $"{ex.GetType().Name}: {ex.Message}";
            }
        }

        [JSImport("globalThis.floweryStorage.reportMutationProbe")]
        private static partial void ReportMutationProbe(
            bool passed,
            string saveError,
            string renameError,
            string deleteError);
#endif

        [JSImport("globalThis.localStorage.getItem")]
        private static partial string? GetLocalStorageItem(string key);

        [JSImport("globalThis.localStorage.setItem")]
        private static partial void SetLocalStorageItem(string key, string value);

        [JSImport("globalThis.localStorage.removeItem")]
        private static partial void RemoveLocalStorageItem(string key);

        [JSImport("globalThis.floweryStorage.getKeys")]
        private static partial string GetLocalStorageKeys(string storagePrefix, string keyPrefix);
    }

    [JsonSerializable(typeof(string[]))]
    internal partial class BrowserStateStorageJsonContext : JsonSerializerContext
    {
    }
}
