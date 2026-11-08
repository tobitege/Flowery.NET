using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Flowery.Controls;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaFact]
    public void ViewerConstructionDoesNotOverwriteSavedViewOptions()
    {
        var previous = (PhotosSettings.ShowRulers, PhotosSettings.ShowGrid, PhotosSettings.ShowNavigator, PhotosSettings.ShowStatusBar, PhotosSettings.AutoHideToolbar);
        try
        {
            PhotosSettings.ShowRulers = PhotosSettings.ShowGrid = PhotosSettings.ShowNavigator = PhotosSettings.ShowStatusBar = PhotosSettings.AutoHideToolbar = true;
            using var viewer = new ViewerView();
            Assert.True(PhotosSettings.ShowGrid);
            Assert.True(PhotosSettings.ShowNavigator);
            Assert.True(PhotosSettings.ShowStatusBar);
            Assert.True(PhotosSettings.AutoHideToolbar);
            foreach (var name in new[] { "Rulers", "Gridlines", "Navigator", "StatusBar", "AutoHide" })
                Assert.True(viewer.FindControl<DaisyCheckBox>(name)!.IsChecked);
        }
        finally
        {
            (PhotosSettings.ShowRulers, PhotosSettings.ShowGrid, PhotosSettings.ShowNavigator, PhotosSettings.ShowStatusBar, PhotosSettings.AutoHideToolbar) = previous;
        }
    }

    [AvaloniaFact]
    public async Task MalformedNativeImageHasNoThumbnailAndDoesNotEscapeIntoTheUi()
    {
        var path = Path.ChangeExtension(CreateImage(), ".tiff");
        await File.WriteAllTextAsync(path, "not a TIFF image", TestContext.Current.CancellationToken);
        using var thumbnail = await ThumbnailLoader.LoadAsync(new PhotoItem(path), 180, TestContext.Current.CancellationToken);
        Assert.Null(thumbnail);
    }
}
