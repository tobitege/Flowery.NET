using Flowery.Services;

namespace Flowery.NET.RuntimeTests;

/// <summary>
/// Keeps saved control state (for example the sidebar's last item) out of the tests and away from user data.
/// </summary>
internal sealed class InMemoryStateStorage : IStateStorage
{
    private readonly Dictionary<string, List<string>> _store = new(StringComparer.Ordinal);

    public IReadOnlyList<string> LoadLines(string key) => _store.TryGetValue(key, out var lines) ? lines : [];

    public void SaveLines(string key, IEnumerable<string> lines) => _store[key] = [.. lines];

    public void Delete(string key) => _store.Remove(key);

    public void Rename(string sourceKey, string targetKey)
    {
        if (_store.Remove(sourceKey, out var lines))
            _store[targetKey] = lines;
    }

    public IEnumerable<string> GetKeys(string prefix) =>
        _store.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToList();
}
