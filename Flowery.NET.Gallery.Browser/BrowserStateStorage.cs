using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.JavaScript;
using System.Runtime.Versioning;
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
            try
            {
                var data = string.Join(LineSeparator, lines);
                SetLocalStorageItem(StoragePrefix + key, data);
            }
            catch
            {
            }
        }

        [JSImport("globalThis.localStorage.getItem")]
        private static partial string? GetLocalStorageItem(string key);

        [JSImport("globalThis.localStorage.setItem")]
        private static partial void SetLocalStorageItem(string key, string value);
    }
}
