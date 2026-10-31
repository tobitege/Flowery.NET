using System;
using Avalonia;
using Avalonia.Media.Fonts;
using Flowery.Skia;

namespace Flowery.NET.Gallery;

/// <summary>
/// Provides Noto Sans font family with CJK/Arabic fallback support.
/// </summary>
public static class NotoFontProvider
{
    /// <summary>
    /// The key URI for the font collection (uses fonts: scheme).
    /// </summary>
    private static readonly Uri FontKey = new("fonts:Flowery.NET.Gallery");
    
    /// <summary>
    /// The source URI for the embedded fonts (uses avares: scheme).
    /// </summary>
    private static readonly Uri FontSource = new("avares://Flowery.NET.Gallery/Assets/Fonts");
    
    /// <summary>
    /// Configures FontManager to include Noto Sans fonts for multilingual support.
    /// Call this in your AppBuilder chain.
    /// </summary>
    /// <remarks>
    /// This requires Noto Sans font files to be placed in Assets/Fonts:
    /// - NotoSans-Regular.ttf (Latin/Cyrillic/Greek)
    /// - NotoSansSC-Regular.otf (Simplified Chinese)
    /// - NotoSansJP-Regular.otf (Japanese)
    /// - NotoSansKR-Regular.otf (Korean)
    /// - NotoSansArabic-Regular.ttf (Arabic)
    /// 
    /// See Assets/Fonts/README.md for download instructions.
    /// </remarks>
    public static AppBuilder WithNotoFonts(this AppBuilder builder)
    {
        try
        {
            return builder.ConfigureFonts(fontManager =>
            {
                try
                {
                    fontManager.AddFontCollection(CreateCollection());
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[NotoFontProvider] Failed to add font collection: {ex.Message}");
                    // Continue without the fonts - app will fall back to default fonts
                }
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[NotoFontProvider] Failed to configure fonts: {ex.Message}");
            return builder; // Return builder unchanged if configuration fails
        }
    }

    /// <summary>
    /// The Noto font collection that <see cref="WithNotoFonts"/> adds. The fonts have only regular faces, so bold and
    /// italic text needs the stand-ins of a <see cref="SafeEmbeddedFontCollection"/>.
    /// </summary>
    internal static FontCollectionBase CreateCollection() => new SafeEmbeddedFontCollection(FontKey, FontSource);
}
