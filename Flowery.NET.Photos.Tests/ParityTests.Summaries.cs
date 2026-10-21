using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Localization;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaTheory]
    [InlineData(1, 1, "1 photo in 1 folder", "1 Foto in 1 Ordner")]
    [InlineData(1, 2, "1 photo in 2 folders", "1 Foto in 2 Ordnern")]
    [InlineData(2, 1, "2 photos in 1 folder", "2 Fotos in 1 Ordner")]
    [InlineData(2, 2, "2 photos in 2 folders", "2 Fotos in 2 Ordnern")]
    [InlineData(0, 0, "0 photos in 0 folders", "0 Fotos in 0 Ordnern")]
    public async Task HomeAndSettingsUseIndependentPhotoAndFolderCounts(int photos, int folders, string english, string german)
    {
        var oldState = PhotosSettings.StateDirectory;
        var oldCulture = FloweryLocalization.CurrentCulture.Name;
        var state = Path.Combine(Path.GetTempPath(), "flowery-photos-parity", Guid.NewGuid().ToString("N"));
        PhotosSettings.Initialize(state);
        PhotosSettings.SaveFolders([]);
        var library = new PhotoLibrary();
        library.Initialize(Dispatcher.UIThread);
        using var shell = new PhotosShell(library);
        var owner = new Window { Content = shell, Width = 1396, Height = 1198 };
        SettingsView? settings = null;
        try
        {
            for (var index = 0; index < folders; index++)
            {
                var folder = Path.Combine(state, "images", index.ToString());
                Directory.CreateDirectory(folder);
                if (index == 0)
                    for (var photo = 0; photo < photos; photo++)
                        File.Copy(CreateImage(), Path.Combine(folder, $"photo-{photo}.png"));
                library.AddFolder(folder);
            }
            await library.RescanAsync();
            Assert.Equal(photos, library.Items.Count);
            Assert.Equal(folders, library.Folders.Count);
            owner.Show();
            settings = new SettingsView(library);
            var dialog = settings.ShowDialog(owner);
            foreach (var (culture, expected) in new[] { ("en", english), ("de", german) })
            {
                FloweryLocalization.SetCulture(culture);
                owner.UpdateLayout(); settings.UpdateLayout();
                var summary = expected + " · " + FileOperations.FormatFileSize(library.Items.Sum(item => item.FileSize));
                Assert.Equal(summary, settings.FindControl<TextBlock>("LibraryStats")!.Text);
                var home = shell.FindControl<StackPanel>("Home")!;
                Assert.Contains(home.GetVisualDescendants().OfType<TextBlock>(), text =>
                    text.Text == (photos == 0 ? PhotosLocalization.GetString("Photos_Home_WelcomeText") : summary));
            }
            settings.Close(); await dialog;
        }
        finally
        {
            settings?.Close(); owner.Close(); await library.DisposeAsync();
            FloweryLocalization.SetCulture(oldCulture); PhotosSettings.Initialize(oldState);
        }
    }
}
