using Flowery.Services;

namespace Flowery.NET.PackageTest;

/// <summary>
/// Keeps the saved state of the controls, such as the boards and settings of the Kanban, in memory. The file storage
/// of Flowery.NET would share them with the Gallery, in its folder in the local application data.
/// </summary>
internal sealed class MemoryStateStorage : IStateStorage
{
    private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);

    public IReadOnlyList<string> LoadLines(string key) => _values.TryGetValue(key, out var lines) ? lines : [];

    public void SaveLines(string key, IEnumerable<string> lines) => _values[key] = [.. lines];

    public void Delete(string key) => _values.Remove(key);

    public void Rename(string sourceKey, string targetKey)
    {
        if (!_values.Remove(sourceKey, out var lines))
            throw new InvalidOperationException($"No state is saved under '{sourceKey}'.");
        _values[targetKey] = lines;
    }

    public IEnumerable<string> GetKeys(string prefix)
        => _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
}
