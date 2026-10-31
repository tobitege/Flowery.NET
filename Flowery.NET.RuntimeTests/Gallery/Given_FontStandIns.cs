using Avalonia.Media;
using Flowery.NET.Gallery;
using Flowery.Skia;
using Xunit;

namespace Flowery.NET.RuntimeTests.Gallery;

/// <summary>
/// The Gallery's Noto fonts have only regular faces and Inter has no italic ones, so bold or italic text needs a
/// stand-in. Avalonia reads the bytes for one through a Skia stream that optimized code (ReadyToRun, NativeAOT, a hot
/// method) can finalize during the read, which ended the app with an access violation.
/// </summary>
public class Given_FontStandIns
{
    private static readonly Uri TestKey = new("fonts:FloweryStandInTest");
    private static readonly Uri NotoSource = new("avares://Flowery.NET.Gallery/Assets/Fonts");

    [Fact]
    public void When_The_Gallery_Adds_Its_Fonts_Their_Collections_Make_Their_Stand_Ins_Themselves()
    {
        Assert.IsType<SafeEmbeddedFontCollection>(NotoFontProvider.CreateCollection());
        var inter = SafeEmbeddedFontCollection.CreateInter();
        Assert.Equal("fonts:Inter", inter.Key.ToString().TrimEnd('/'));
    }

    [Fact]
    public void When_A_Bold_Italic_Face_Is_Missing_The_Stand_In_Simulates_Both_And_Is_Kept()
    {
        var collection = new SafeEmbeddedFontCollection(TestKey, NotoSource);
        FontManager.Current.AddFontCollection(collection);
        try
        {
            Assert.True(collection.TryGetGlyphTypeface("Noto Sans", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out var regular));
            Assert.True(collection.TryGetGlyphTypeface("Noto Sans", FontStyle.Italic, FontWeight.Bold, FontStretch.Normal, out var boldItalic));

            Assert.Equal(FontSimulations.None, regular.FontSimulations);
            Assert.Equal(FontSimulations.Bold | FontSimulations.Oblique, boldItalic.FontSimulations);
            Assert.NotSame(regular.PlatformTypeface, boldItalic.PlatformTypeface);
            Assert.Equal(regular.GlyphCount, boldItalic.GlyphCount);

            Assert.True(collection.TryGetGlyphTypeface("Noto Sans", FontStyle.Italic, FontWeight.Bold, FontStretch.Normal, out var again));
            Assert.Same(boldItalic, again);
        }
        finally
        {
            FontManager.Current.RemoveFontCollection(TestKey);
        }
    }

    [Fact]
    public void When_Inter_Is_Italic_Its_Oblique_Stand_In_Comes_From_The_Collection()
    {
        var collection = new SafeEmbeddedFontCollection(TestKey, new Uri("avares://Avalonia.Fonts.Inter/Assets"));
        FontManager.Current.AddFontCollection(collection);
        try
        {
            Assert.True(collection.TryGetGlyphTypeface("Inter", FontStyle.Italic, FontWeight.Normal, FontStretch.Normal, out var italic));
            Assert.Equal(FontSimulations.Oblique, italic.FontSimulations);
        }
        finally
        {
            FontManager.Current.RemoveFontCollection(TestKey);
        }
    }
}
