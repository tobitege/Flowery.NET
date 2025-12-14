# Noto Sans Fonts for Multilingual Support

This folder contains Noto Sans font files for proper CJK (Chinese, Japanese, Korean) and Arabic character support in the Avalonia WebAssembly browser build.

## Included Fonts

| File | Purpose | Size |
|------|---------|------|
| `NotoSans-Regular.ttf` | Latin, Cyrillic, Greek | ~560KB |
| `NotoSansSC-Regular.otf` | Simplified Chinese (Standalone) | ~16MB |
| `NotoSansJP-Regular.otf` | Japanese (Standalone) | ~16MB |
| `NotoSansKR-Regular.otf` | Korean (Standalone) | ~16MB |
| `NotoSansArabic-Regular.ttf` | Arabic script | ~240KB |

**Total Size:** ~48MB (Note: Browser builds may be large, but this ensures full CJK support)

## License

These fonts are from the [Google Noto Fonts](https://fonts.google.com/noto) project and are licensed under the **SIL Open Font License, Version 1.1 (OFL-1.1)**.

### What This Means

✅ **Free to use** - in personal and commercial projects  
✅ **Free to distribute** - can be bundled with applications  
✅ **Free to modify** - derivative works are allowed  
✅ **No attribution required** - though appreciated  

The OFL is one of the most permissive font licenses available. The full license text is available at:
https://scripts.sil.org/OFL

For a practical guide on using OFL fonts, see:
https://openfontlicense.org/how-to-use-ofl-fonts/

> *"The OFL allows the licensed fonts to be used, studied, modified and redistributed freely as long as they are not sold by themselves."*

## Why These Are Committed

Unlike desktop apps, Avalonia WebAssembly renders via Skia and **cannot access browser/system fonts**. All fonts must be embedded in the application bundle for proper character rendering.

These fonts ensure the language dropdown and all localized UI text display correctly across all 11 supported languages.

## Configuration

Fonts are configured in `App.axaml` as a FontFamily resource with fallback chain:

```xml
<FontFamily x:Key="NotoSansFamily">
    fonts:Flowery.NET.Gallery#Noto Sans,
    fonts:Flowery.NET.Gallery#Noto Sans CJK SC,
    fonts:Flowery.NET.Gallery#Noto Sans CJK JP,
    fonts:Flowery.NET.Gallery#Noto Sans CJK KR,
    fonts:Flowery.NET.Gallery#Noto Sans Arabic
</FontFamily>
```

The font collection is registered in `Program.cs` via `.WithNotoFonts()` (extension method in `NotoFontProvider.cs`).
