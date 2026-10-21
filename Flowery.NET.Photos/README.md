# Flowery Photos

Flowery Photos is a desktop photo viewer and editor for Windows, macOS, and Linux.
It uses Avalonia 12, Flowery Daisy controls, and SkiaSharp.
The app ports the [Flowery.WinUI Photos demo](https://github.com/tobitege/Flowery.WinUI/tree/main/Flowery.WinUI.Photos).

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
JPEG metadata includes EXIF orientation, dates, camera, lens, exposure, and GPS coordinates when available.
SkiaSharp supplies decoding for supported image codecs.

The gallery supports these operations:

- Justified, virtualized rows or square tiles.
- Groups by day, month, or year, plus an ungrouped view.
- Search by name, folder, camera, year, or month.
- File type and orientation filters.
- Sort by capture date, modified date, name, or size.
- A visible-date indicator and a timeline with month and year buttons.
- Selection with checkboxes, Ctrl-click, Shift-click, or Select all.
- Batch favorites, album assignment, clipboard copy, and Windows Recycle Bin actions.
- Context menus for individual photos.

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
Exports use JPEG or PNG.
Other input formats use a JPEG copy.
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
| Ctrl+wheel | Change gallery thumbnail size |
| Left / Right | Previous / next photo |
| Ctrl+E | Edit |
| Ctrl+Z / Ctrl+Y | Undo / redo |
| Ctrl+S | Save a copy |
| Delete | Move selected photos to the Windows Recycle Bin |
| + / - | Zoom |
| 0 / 1 | Fit / 100% |
| F | Favorite |
| R / Shift+R | Rotate right / left |
| X | Swap markup colors |
| F5 | Slideshow |
| F11 | Full screen |
| Esc | Exit full screen, stop slideshow, or return to the gallery |

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
