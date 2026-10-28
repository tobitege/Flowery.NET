using System.Text;
using Avalonia.Platform.Storage;
using Flowery.NET.Gallery.Examples;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

/// <summary>The tour designer's profile files: reading, the replace through a temporary file, and the stream fallback.</summary>
public class Given_TourExamplesFiles
{
    private const string Profile = "{\"schemaVersion\":1,\"tourId\":\"gallery-tour\"}";

    [Fact]
    public async Task When_An_Export_Replaces_A_Longer_File_Only_The_New_Profile_Remains()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "tour.json");
            await File.WriteAllTextAsync(path, new string('x', 4096));
            using var file = await RuntimeTestHelpers.GetWindow().StorageProvider.TryGetFileFromPathAsync(path);
            Assert.NotNull(file);
            // A picked file with a local path reaches the export with that path.
            Assert.Equal(path, file.TryGetLocalPath(), ignoreCase: true);

            await TourExamples.WriteProfileAsync(file, Profile);

            Assert.Equal(Profile, await File.ReadAllTextAsync(path));
            Assert.Empty(Directory.GetFiles(directory, "*.partial"));
            Assert.Equal(Profile, await TourExamples.ReadProfileAsync(file));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task When_The_Replace_Fails_The_Error_Names_The_Remaining_Temporary_File()
    {
        var directory = CreateDirectory();
        try
        {
            // A folder at the destination lets the temporary file be written and the replace fail.
            var path = Path.Combine(directory, "tour.json");
            Directory.CreateDirectory(path);

            var error = await Assert.ThrowsAnyAsync<IOException>(() => TourExamples.WriteProfileFileAsync(path, Profile));

            var partial = Assert.Single(Directory.GetFiles(directory, "*.partial"));
            Assert.Contains(partial, error.Message, StringComparison.Ordinal);
            Assert.Equal(Profile, await File.ReadAllTextAsync(partial));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task When_The_Temporary_File_Cannot_Be_Created_The_Original_Error_Passes_Unchanged()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "missing", "tour.json");

            var error = await Assert.ThrowsAnyAsync<IOException>(() => TourExamples.WriteProfileFileAsync(path, Profile));

            // The original error passes unchanged; no temporary path is appended for a file that was never created.
            Assert.IsType<DirectoryNotFoundException>(error);
            Assert.DoesNotContain(Environment.NewLine, error.Message, StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task When_The_Folder_Takes_The_Temporary_File_The_Export_Replaces_The_File_Without_The_Stream()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "tour.json");
            await File.WriteAllTextAsync(path, new string('x', 4096));
            var opened = false;

            await TourExamples.WriteProfileAsync(path, () =>
            {
                opened = true;
                return Task.FromResult<Stream>(new MemoryStream());
            }, Profile);

            // A writable folder takes the replace through a temporary file; the stream is only the fallback.
            Assert.False(opened);
            Assert.Equal(Profile, await File.ReadAllTextAsync(path));
            Assert.Empty(Directory.GetFiles(directory, "*.partial"));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task When_The_Folder_Cannot_Take_The_Temporary_File_The_Export_Writes_Through_The_File_Stream()
    {
        var directory = CreateDirectory();
        try
        {
            // A local path whose folder cannot take the temporary file, like a sandboxed folder, uses the file's stream.
            var path = Path.Combine(directory, "missing", "tour.json");
            var stream = OldContent();
            var opened = false;

            await TourExamples.WriteProfileAsync(path, () =>
            {
                opened = true;
                return Task.FromResult<Stream>(stream);
            }, Profile);

            // MemoryStream keeps its buffer readable after the export disposes it.
            Assert.True(opened);
            Assert.Equal(Profile, Encoding.UTF8.GetString(stream.ToArray()));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task When_The_Replace_Fails_After_The_Temporary_File_Exists_The_Export_Reports_It_Without_The_Stream()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "tour.json");
            Directory.CreateDirectory(path);
            var opened = false;

            var error = await Assert.ThrowsAnyAsync<IOException>(() => TourExamples.WriteProfileAsync(path, () =>
            {
                opened = true;
                return Task.FromResult<Stream>(new MemoryStream());
            }, Profile));

            // The incomplete temporary file stays for diagnosis; writing elsewhere would hide the failed replace.
            Assert.False(opened);
            Assert.Contains(Assert.Single(Directory.GetFiles(directory, "*.partial")), error.Message, StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task When_An_Export_Has_No_Path_It_Writes_Through_The_File_Stream()
    {
        var stream = OldContent();
        var opened = false;

        await TourExamples.WriteProfileAsync(null, () =>
        {
            opened = true;
            return Task.FromResult<Stream>(stream);
        }, Profile);

        Assert.True(opened);
        Assert.Equal(Profile, Encoding.UTF8.GetString(stream.ToArray()));
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "flowery-tour-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    // A platform stream that opens with the previous, longer content.
    private static MemoryStream OldContent()
    {
        var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes(new string('x', 4096)));
        stream.Position = 0;
        return stream;
    }
}
