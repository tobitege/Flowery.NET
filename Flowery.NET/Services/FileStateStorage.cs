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
            try
            {
                Directory.CreateDirectory(_baseDir);
                var filePath = GetFilePath(key);
                File.WriteAllLines(filePath, lines.ToArray());
            }
            catch
            {
            }
        }

        private string GetFilePath(string key)
        {
            var safeKey = string.Join("_", key.Split(Path.GetInvalidFileNameChars()));
            return Path.Combine(_baseDir, safeKey + ".state");
        }
    }
}
