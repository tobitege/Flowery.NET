using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Flowery.Services
{
    /// <summary>
    /// File-based state storage for Desktop platforms.
    /// Stores state in LocalApplicationData folder.
    /// </summary>
    public class FileStateStorage : IStateStorage
    {
        private readonly string _baseDir;

        public FileStateStorage(string appName = "FloweryGallery")
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            _baseDir = Path.Combine(localAppData, appName);
        }

        public IReadOnlyList<string> LoadLines(string key)
        {
            try
            {
                var filePath = GetFilePath(key);
                if (!File.Exists(filePath))
                    return Array.Empty<string>();

                return File.ReadAllLines(filePath);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        public void SaveLines(string key, IEnumerable<string> lines)
        {
            Directory.CreateDirectory(_baseDir);
            var filePath = GetFilePath(key);
            File.WriteAllLines(filePath, lines.ToArray());
        }

        public void Delete(string key)
        {
            File.Delete(GetFilePath(key));
        }

        public void Rename(string sourceKey, string targetKey)
        {
            var sourcePath = GetFilePath(sourceKey);
            Directory.CreateDirectory(_baseDir);
            var targetPath = GetFilePath(targetKey);
            File.Move(sourcePath, targetPath, true);
        }

        public IEnumerable<string> GetKeys(string prefix)
        {
            try
            {
                if (!Directory.Exists(_baseDir))
                    return Array.Empty<string>();

                var prefixValue = prefix ?? string.Empty;
                return Directory.EnumerateFiles(_baseDir, "*.state")
                    .Select(Path.GetFileNameWithoutExtension)
                    .Where(key => !string.IsNullOrWhiteSpace(key)
                        && key.StartsWith(prefixValue, StringComparison.Ordinal)
                        && !key.EndsWith(".tmp", StringComparison.Ordinal))
                    .Select(key => key!)
                    .ToArray();
            }
            catch
            {
                return Array.Empty<string>();
            }
        }

        private string GetFilePath(string key)
        {
            var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_baseDir, safeKey + ".state");
        }
    }
}
