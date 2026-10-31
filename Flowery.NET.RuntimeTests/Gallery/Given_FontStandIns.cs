using System.Reflection;
using Avalonia.Media;
using Avalonia.Platform;
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
    private static readonly Uri OtherKey = new("fonts:FloweryStandInOther");
    private static readonly Uri NotoSource = new("avares://Flowery.NET.Gallery/Assets/Fonts");
    private static readonly Uri InterSource = new("avares://Avalonia.Fonts.Inter/Assets");

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
        var collection = new SafeEmbeddedFontCollection(TestKey, InterSource);
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

    [Fact]
    public void When_A_Skia_Typeface_Needs_A_Bold_Stand_In_The_Collection_Copies_The_Font_Without_Avalonias_Stream()
    {
        // Avalonia's SkiaTypeface.TryGetStream copies the whole font, 16 MB here, into a managed array; the collection
        // copies it in native memory.
        using var file = AssetLoader.Open(new Uri(NotoSource.OriginalString + "/NotoSansJP-Regular.otf"));
        var fontLength = file.Length;
        var collection = new SafeEmbeddedFontCollection(TestKey, NotoSource);
        FontManager.Current.AddFontCollection(collection);
        try
        {
            Assert.True(collection.TryGetGlyphTypeface("Noto Sans CJK JP", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out var regular));
            Assert.Equal("Avalonia.Skia.SkiaTypeface", regular.PlatformTypeface.GetType().FullName);

            var before = GC.GetAllocatedBytesForCurrentThread();
            Assert.True(collection.TryGetGlyphTypeface("Noto Sans CJK JP", FontStyle.Normal, FontWeight.Bold, FontStretch.Normal, out var bold));
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(FontSimulations.Bold, bold.FontSimulations);
            Assert.True(allocated < fontLength / 2, $"{allocated} bytes allocated for a stand-in of a font of {fontLength} bytes.");
        }
        finally
        {
            FontManager.Current.RemoveFontCollection(TestKey);
        }
    }

    [Fact]
    public void When_Another_Typeface_Needs_A_Bold_Stand_In_Avalonia_Makes_It_From_Its_Stream()
    {
        var noto = new SafeEmbeddedFontCollection(TestKey, NotoSource);
        var inter = new SafeEmbeddedFontCollection(OtherKey, InterSource);
        FontManager.Current.AddFontCollection(noto);
        FontManager.Current.AddFontCollection(inter);
        try
        {
            Assert.True(noto.TryGetGlyphTypeface("Noto Sans", FontStyle.Normal, FontWeight.Normal, FontStretch.Normal, out var regular));
            var other = DispatchProxy.Create<IPlatformTypeface, CountingTypeface>();
            ((CountingTypeface)(object)other).Target = regular.PlatformTypeface;

            // The Inter collection has no Noto Sans, so the face of the test is the only one of its family there.
            Assert.True(inter.TryAddGlyphTypeface(new GlyphTypeface(other)));
            Assert.True(inter.TryGetGlyphTypeface("Noto Sans", FontStyle.Normal, FontWeight.Bold, FontStretch.Normal, out var bold));

            Assert.Equal(FontSimulations.Bold, bold.FontSimulations);
            Assert.Equal(1, ((CountingTypeface)(object)other).StreamReads);
        }
        finally
        {
            FontManager.Current.RemoveFontCollection(OtherKey);
            FontManager.Current.RemoveFontCollection(TestKey);
        }
    }

    /// <summary>
    /// A platform typeface of another kind than Avalonia.Skia's: it forwards to one and counts the reads of its stream.
    /// User code cannot implement <see cref="IPlatformTypeface"/>, but a proxy made at run time can.
    /// </summary>
    public class CountingTypeface : DispatchProxy
    {
        public IPlatformTypeface? Target { get; set; }

        public int StreamReads { get; private set; }

        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            if (targetMethod!.Name == nameof(IPlatformTypeface.TryGetStream))
                StreamReads++;
            // The typeface belongs to the Noto collection, which disposes it.
            if (targetMethod.Name == nameof(IDisposable.Dispose))
                return null;
            return targetMethod.Invoke(Target, args);
        }
    }
}
