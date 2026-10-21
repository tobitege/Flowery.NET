using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Platform;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed class FontPackagingTests
{
    [AvaloniaTheory]
    [InlineData("NotoSans-Regular.ttf", "Noto Sans", 0x0041)]
    [InlineData("NotoSansSC-Regular.otf", "Noto Sans CJK SC", 0x4E2D)]
    [InlineData("NotoSansJP-Regular.otf", "Noto Sans CJK JP", 0x3042)]
    [InlineData("NotoSansKR-Regular.otf", "Noto Sans CJK KR", 0xD55C)]
    [InlineData("NotoSansArabic-Regular.ttf", "Noto Sans Arabic", 0x0639)]
    public void ExternalFontsRemainAvailableThroughExistingFamilyAliases(string fileName, string familyName, int codepoint)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", fileName);
        Assert.True(File.Exists(file), file);
        Assert.False(AssetLoader.Exists(new Uri("avares://Flowery.NET.Photos/Assets/Fonts/" + fileName)));

        var typeface = new Typeface(new FontFamily("fonts:Flowery.NET.Photos#" + familyName));
        Assert.True(FontManager.Current.TryGetGlyphTypeface(typeface, out var glyphTypeface));
        Assert.Equal(familyName, glyphTypeface.FamilyName);
        Assert.True(glyphTypeface.CharacterToGlyphMap.TryGetGlyph(codepoint, out var glyph));
        Assert.NotEqual((ushort)0, glyph);
    }

    [Fact]
    public void OutputContainsFontLicenseAndCopyrightNotices()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "OFL.txt");
        var text = File.ReadAllText(path);
        Assert.Contains("SIL OPEN FONT LICENSE Version 1.1", text);
        Assert.Contains("Copyright 2015-2021 Google LLC. All Rights Reserved.", text);
        Assert.Contains("2014-2021 Adobe", text);
    }
}
