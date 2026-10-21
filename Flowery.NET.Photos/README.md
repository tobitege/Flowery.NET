# Flowery Photos

Flowery Photos is a desktop photo viewer and editor for Windows, macOS, and Linux.
It uses Avalonia 12, Flowery Daisy controls, and SkiaSharp.
The app ports the [Flowery.WinUI Photos demo](https://github.com/tobitege/Flowery.WinUI/tree/main/Flowery.WinUI.Photos).

Font files ship in `Assets/Fonts`, and sample photos ship in `Assets/Samples`.
Distribute both folders with the desktop app.
Keep `Assets/Fonts/OFL.txt` with the fonts. It contains their copyright notices and the SIL Open Font License 1.1.
The app loads the fonts from its output directory instead of embedding them in `Flowery.NET.Photos.dll`.

## Standalone releases

The manual **Release Photos** workflow creates a Photos-only GitHub release without publishing NuGet packages or changing the library version.
With library version `3.8.0`, its tags are `v3.8.0-photos.1`, `v3.8.0-photos.2`, and so on.
The `main` and `avalonia12` branches share this counter.
The `alpha` branch uses separate prerelease tags such as `v3.8.0-alpha-photos.1`.
Photos releases do not replace the latest library release on GitHub.

After the workflow is merged into the default branch:

1. Open **Actions → Release Photos → Run workflow**.
2. Select `avalonia12` under **Use workflow from**. The other supported branches are `main` and `alpha`.
3. Enter an optional description.
4. Start the workflow to build the packages, create the tag, and publish the release.

Every package and the release tag use the same source commit.
The release contains these downloads:

| Platform | Archive | Executable |
| --- | --- | --- |
| Windows x64 | `Flowery.Photos-Windows-x64.zip` | `Flowery.NET.Photos.Desktop.exe` |
| Linux x64 | `Flowery.Photos-Linux-x64.tar.gz` | `Flowery.NET.Photos.Desktop` |
| macOS ARM64 | `Flowery.Photos-macOS-arm64.tar.gz` | `Flowery.NET.Photos.Desktop` |

Extract the complete archive before starting the app.
Keep `Assets/Fonts`, its license file, and `Assets/Samples` beside the executable.
The .NET runtime is included. Linux still requires the system libraries used by Avalonia.
The macOS archive contains a standalone executable, without an `.app` bundle, signing, or notarization.
The Unix archives preserve executable permissions.

**Release Gallery** also provides these three desktop platforms and retains its four Android APKs.
Its desktop archives use the prefix `Flowery.Gallery`.

## Build and run

Use the .NET SDK selected by the repository's `global.json`.

```powershell
dotnet build Flowery.NET.Photos.Desktop/Flowery.NET.Photos.Desktop.csproj -c Debug
dotnet run --project Flowery.NET.Photos.Desktop -c Debug --no-build
```

Pass an image path to open that image.

The solution and the repository's desktop and full build scripts include Photos.
Run the Photos tests with this command:

```powershell
dotnet test Flowery.NET.Photos.Tests/Flowery.NET.Photos.Tests.csproj -c Debug
```

### Debug and demo options

Use `--screen=2` for manual UI tests or demonstrations on a secondary display.
The app selects the first non-primary display reported by Avalonia.
It places a normal, resizable window there and sizes it to the full working area.
The flag keeps the current display resolution, scaling, and orientation.
It does not enable full-screen mode.

With only one display, the app ignores the flag.
This placement helper is available in both Debug and Release builds.

```powershell
dotnet run --project Flowery.NET.Photos.Desktop -c Debug --no-build -- --screen=2
```

Pass `--cdp` in a Debug build to enable the CDP server on `http://127.0.0.1:9222`.
The server stops when the app exits. Release builds exclude the CDP dependency.

```powershell
dotnet run --project Flowery.NET.Photos.Desktop -c Debug --no-build -- --cdp
```

## Library

The home page shows recent photos, favorites, folders, albums, and library statistics.
An empty library shows introductory actions and feature descriptions.

The app starts with the Pictures folder.
Add other folders or import image files with the navigation buttons.
Imports create copies inside the first source folder.
Drag an image onto the window to open it.
Drag a folder onto the window to add it as a source.
Removing a source folder preserves its files.

The scanner reads image headers and stores metadata in a JSON cache.
File watchers refresh changed folders.
JPEG metadata includes EXIF orientation, dates, camera, lens, exposure, ISO, focal length, and GPS coordinates when available.
Windows also supplies image codecs and metadata for formats such as TIFF and JPEG XR.
Other platforms use the SkiaSharp codecs and the JPEG metadata reader.

The gallery supports these operations:

- Justified, virtualized rows or square tiles.
- Groups by day, month, or year, plus an ungrouped view.
- Search by name, folder, camera, year, or month.
- File type and orientation filters.
- Sort by capture date, modified date, name, or size.
- A visible-date indicator and a draggable timeline with proportional month and year positions.
- Selection with checkboxes, Ctrl-click, Shift-click, or Select all.
- Batch favorites, album assignment, clipboard copy, and Windows Recycle Bin actions.
- Context menus for individual photos or the visible selection.

Date groups and the timeline appear only with capture-date sorting.
Search retains the current folder, album, or favorites source.
Album and folder cards load their covers only in the visible part of the collection list.

Albums reference the original paths.
They do not copy or move the images.

## Viewer and editor

The viewer supports zoom, pan, fit, slideshow, full screen, and file information.
View options include rulers, gridlines, a navigator, a status bar, and an automatically hidden toolbar.
Move the pointer to the top edge to show a hidden toolbar.

Edits remain separate from the source image until you save:

- Crop with handles and aspect ratios.
- Rotate, flip, and straighten.
- Brightness, exposure, contrast, highlights, shadows, saturation, warmth, and tint.
- Sharpness, clarity, vignette, fade, and automatic enhancement.
- Fourteen filter presets with previews and intensity.
- Pen and highlighter strokes with primary and secondary colors.
- An eyedropper, RGB/HSL color editor, nine custom colors, recent colors, and color harmonies.
- Image palette extraction and JASC-PAL or GIMP palette import and export.
- A draggable before/after divider.
- Undo, redo, and revert.
- Save a copy, replace the original after confirmation, or copy the edited image.

The same Skia rendering pipeline supplies the preview and full-resolution export.
Detail zoom uses the full-resolution image and accounts for display scaling.
The blur radius scales with the output dimensions.
The pipeline applies geometry before color and detail effects, then the vignette, then markup.
Skia effects preserve the original control ranges and processing order, but do not guarantee identical Win2D pixels.

Copies retain the source format when an encoder is available.
All platforms export JPEG and PNG.
Windows also exports BMP, GIF, TIFF, and JPEG XR through its native codecs.
Other input formats use a JPEG copy. Saved copies appear next to their originals in the viewer list.
Exports do not preserve EXIF metadata.
Animated images show their first frame.

Failed exports send their temporary image to the Recycle Bin or Trash.
Linux uses `gio trash`; macOS uses the Foundation trash API through `osascript`.
If recycling fails, the error includes the retained temporary file's path.

Windows supplies Recycle Bin, wallpaper, and Open With actions.
Unavailable Windows actions remain hidden on other systems.
File reveal uses the platform's file manager.

## Settings

Theme, global size, and all twelve languages change immediately.
The settings dialog previews changes.
Cancel restores the previous theme, size, language, folders, and preferences.
The app stores window position, library configuration, and preferences between sessions.

Controls expose stable UI Automation IDs and localized names.
Photo tiles support Invoke and Toggle patterns.
Editor sliders expose RangeValue, and the tests exercise these patterns against the actual controls.

## Keyboard

| Keys | Action |
| --- | --- |
| Ctrl+F | Search |
| Ctrl+, | Settings |
| Ctrl+A | Select all gallery photos |
| Ctrl+wheel / Ctrl+Plus / Ctrl+Minus | Change gallery thumbnail size |
| Left / Right or PageUp / PageDown | Previous / next photo |
| Home / End | First / last photo |
| Ctrl+E | Edit |
| Ctrl+Z / Ctrl+Y or Ctrl+Shift+Z | Undo / redo |
| Ctrl+S | Save a copy |
| Ctrl+Shift+S | Replace the original after confirmation |
| Ctrl+C | Copy the selection or the current photo, including its edits |
| I | Toggle file information |
| Delete | Move selected photos to the Windows Recycle Bin |
| + / - | Zoom |
| 0 / 1 | Fit / 100% |
| F | Favorite |
| R / Shift+R | Rotate right / left |
| X | Swap markup colors |
| F5 | Start or stop slideshow |
| Space | Advance during slideshow |
| F11 | Full screen |
| Esc | Clear gallery selection, stop slideshow, exit full screen, close comparison or the side panel, then return to the previous page |

## Isolated demonstrations

Set `FLOWERY_STATE_ROOT` to an absolute directory before you start the app.
With this variable set, a new library starts empty and does not scan your Pictures folder.
Choose **Use sample photos** to load the ten bundled demonstration images.

```powershell
$env:FLOWERY_STATE_ROOT = Join-Path $env:TEMP 'flowery-photos-demo'
dotnet run --project Flowery.NET.Photos.Desktop -- --screen=2
```

Tests use generated images and isolated temporary directories.
The Photos CI job builds the desktop app and runs its tests on Windows, Linux, and macOS.
