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
            // The local path selects the replace through a temporary file.
            Assert.Equal(path, file.TryGetLocalPath(), ignoreCase: true);

            await TourExamples.WriteProfileAsync(file, Profile);

            Assert.Equal(Profile, await File.ReadAllTextAsync(path));
            Assert.Empty(Directory.GetFiles(directory, "*.partial"));
            Assert.Equal(Profile, await TourExamples.ReadProfileAsync(file));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task When_An_Export_Has_No_Local_Path_The_Longer_Old_Content_Is_Truncated()
    {
        // A platform stream of a file without a local path, such as a browser download, opens with the old content.
        using var stream = new MemoryStream();
        stream.Write(Encoding.UTF8.GetBytes(new string('x', 4096)));
        stream.Position = 0;

        await TourExamples.WriteProfileStreamAsync(stream, Profile);

        Assert.Equal(Profile, Encoding.UTF8.GetString(stream.ToArray()));
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
    public async Task When_The_Temporary_File_Cannot_Be_Created_The_Error_Names_No_Temporary_File()
    {
        var directory = CreateDirectory();
        try
        {
            var path = Path.Combine(directory, "missing", "tour.json");

            var error = await Assert.ThrowsAnyAsync<IOException>(() => TourExamples.WriteProfileFileAsync(path, Profile));

            // The original error passes unchanged; no temporary path is appended for a file that was never created.
            Assert.IsType<DirectoryNotFoundException>(error);
            Assert.DoesNotContain(Environment.NewLine, error.Message, StringComparison.Ordinal);
            Assert.False(Directory.Exists(Path.Combine(directory, "missing")));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), "flowery-tour-files-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }
}
