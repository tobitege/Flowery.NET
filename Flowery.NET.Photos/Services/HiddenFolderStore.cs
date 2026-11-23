using System.IO;
using System.Text.Json;

namespace Flowery.NET.Photos.Services;

internal sealed class HiddenFolderStore(string directory)
{
    private const string FileName = "photos.hidden-folders.state";
    private readonly string _path = Path.Combine(directory, FileName);

    public HashSet<string> Read()
    {
        try
        {
            using var stream = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            var folders = JsonSerializer.Deserialize(stream, PhotosJsonContext.Default.StringArray);
            if (folders == null || folders.Any(folder => folder == null || !Path.IsPathFullyQualified(folder)))
                throw new JsonException("Invalid hidden folder list.");
            return new HashSet<string>(folders, PhotoLibrary.PathComparer);
        }
        catch (FileNotFoundException)
        {
            return new HashSet<string>(PhotoLibrary.PathComparer);
        }
    }

    public HashSet<string> SetHidden(string folder, bool hidden)
    {
        // The lock file stays in place. Every writer locks the same file and reads the current state under that lock.
        using var writeLock = new FileStream(_path + ".lock", FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
        var folders = Read();
        if (!(hidden ? folders.Add(folder) : folders.Remove(folder))) return folders;

        var temporary = $"{_path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(folders.ToArray(), PhotosJsonContext.Default.StringArray));
        File.Move(temporary, _path, overwrite: true);
        return folders;
    }
}
