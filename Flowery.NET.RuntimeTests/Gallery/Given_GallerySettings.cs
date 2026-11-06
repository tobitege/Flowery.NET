using Avalonia.Media;
using Flowery.NET.Gallery;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

/// <summary>The settings the Gallery keeps between starts.</summary>
public class Given_GallerySettings
{
    [Fact]
    public void When_A_Backdrop_And_A_Base_Color_Are_Saved_They_Go_Through_The_State_Storage()
    {
        // Regression: both went to files, which the browser keeps in memory only, so a reload lost them. The runtime
        // tests run on a state storage in memory.
        GallerySettings.SaveBackdrop("effect:Aurora");
        GallerySettings.SaveBaseColor(Color.Parse("#204060"));

        Assert.Equal(new[] { "effect:Aurora" }, StateStorageProvider.Instance.LoadLines("gallery.backdrop"));
        Assert.Equal("effect:Aurora", GallerySettings.LoadBackdrop());
        Assert.Equal(Color.Parse("#204060"), GallerySettings.LoadBaseColor());

        // No choice is saved as none.
        GallerySettings.SaveBackdrop(null);
        GallerySettings.SaveBaseColor(null);
        Assert.Null(GallerySettings.LoadBackdrop());
        Assert.Null(GallerySettings.LoadBaseColor());
    }
}
