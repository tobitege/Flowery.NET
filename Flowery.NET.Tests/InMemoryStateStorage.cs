using Flowery.Services;

namespace Flowery.NET.Tests
{
    /// <summary>
    /// Keeps saved control state in memory, away from user data. Shared with Flowery.NET.RuntimeTests and
    /// Flowery.NET.PackageTest.
    /// Like <see cref="FileStateStorage"/>, renaming a missing key fails.
    /// </summary>
    internal sealed class InMemoryStateStorage : IStateStorage
    {
        private readonly Dictionary<string, List<string>> _store = new(StringComparer.Ordinal);

        public int SaveCount { get; private set; }

        public IReadOnlyList<string> LoadLines(string key) =>
            _store.TryGetValue(key, out var lines) ? lines : Array.Empty<string>();

        public void SaveLines(string key, IEnumerable<string> lines)
        {
            SaveCount++;
            _store[key] = new List<string>(lines);
        }

        public void ResetSaveCount()
        {
            SaveCount = 0;
        }

        public void Delete(string key)
        {
            _store.Remove(key);
        }

        public void Rename(string sourceKey, string targetKey)
        {
            if (!_store.Remove(sourceKey, out var lines))
            {
                throw new InvalidOperationException($"Storage key '{sourceKey}' does not exist.");
            }

            _store[targetKey] = lines;
        }

        public IEnumerable<string> GetKeys(string prefix)
        {
            var keys = new List<string>();
            foreach (var key in _store.Keys)
            {
                if (key.StartsWith(prefix, StringComparison.Ordinal))
                    keys.Add(key);
            }

            return keys;
        }
    }
}
