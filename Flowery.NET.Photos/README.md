# Flowery Photos

Flowery Photos is a desktop photo viewer and editor for Windows, macOS, and Linux. It also shows videos.
It uses Avalonia 12, Flowery Daisy controls, and SkiaSharp.
The app ports the [Flowery.WinUI Photos demo](https://github.com/tobitege/Flowery.WinUI/tree/main/Flowery.WinUI.Photos).

Font files ship in `Assets/Fonts`, and sample photos ship in `Assets/Samples`.
Distribute both folders with the desktop app.
Keep `Assets/Fonts/OFL.txt` with the fonts. It contains their copyright notices and the SIL Open Font License 1.1.
The app loads the fonts from its output directory instead of embedding them in `Flowery.NET.Photos.dll`.

## Standalone releases

The manual **Release Photos** workflow creates a Photos-only GitHub release without publishing NuGet packages or changing the library version.
With library version `4.0.0`, its tags are `v4.0.0-photos.1`, `v4.0.0-photos.2`, and so on.
The `main` and `avalonia12` branches share this counter.
The `alpha` branch uses separate prerelease tags such as `v4.0.0-alpha-photos.1`.
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
| Windows ARM64 | `Flowery.Photos-Windows-arm64.zip` | `Flowery.NET.Photos.Desktop.exe` |
| Linux x64 | `Flowery.Photos-Linux-x64.tar.gz` | `Flowery.NET.Photos.Desktop` |
| Linux ARM64 | `Flowery.Photos-Linux-arm64.tar.gz` | `Flowery.NET.Photos.Desktop` |
| macOS ARM64 | `Flowery.Photos-macOS-arm64.tar.gz` | `Flowery.NET.Photos.Desktop` |
| macOS x64 | `Flowery.Photos-macOS-x64.tar.gz` | `Flowery.NET.Photos.Desktop` |

Extract the complete archive before starting the app.
Keep `Assets/Fonts`, its license file, and `Assets/Samples` beside the executable.
The .NET runtime is included. Linux still requires the system libraries used by Avalonia.
The macOS archives contain a standalone executable, without an `.app` bundle, signing, or notarization.
The Unix archives preserve executable permissions.

**Release Gallery** also provides these six desktop platforms, four Android APKs, and a Browser (WebAssembly) zip.
Its desktop archives use the prefix `Flowery.Gallery`.

## Build and run

Use the .NET SDK selected by the repository's `global.json`.

```powershell
dotnet build Flowery.NET.Photos.Desktop/Flowery.NET.Photos.Desktop.csproj -c Debug
dotnet run --project Flowery.NET.Photos.Desktop -c Debug --no-build
```

Pass an image or video path to open that file.

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
Browse… opens the gallery with one folder without adding it to the library.
The gallery shows the photos and videos of that folder, and of its subfolders when the library includes subfolders.
A browsed folder outside the library is not saved or watched.
Imports create copies inside the first source folder.
Drag an image or video onto the window to open it.
Drag a folder onto the window to add it as a source.
Removing a source folder preserves its files.

The scanner reads file headers and stores metadata in a JSON cache.
File watchers refresh changed folders.
JPEG, PNG, WebP, HEIC, and AVIF metadata includes EXIF dates, camera, lens, exposure, ISO, focal length, and GPS coordinates when available.
JPEG metadata also includes the EXIF orientation.
Windows also supplies image codecs and metadata for formats such as TIFF and JPEG XR.
Other platforms use the SkiaSharp codecs and the metadata readers.

The library also contains MP4, MOV, MKV, and WebM videos.
Their headers supply the size, length, frame rate, codec, recording date, camera, and GPS coordinates when available.
Phone videos recorded upright keep their orientation.
On Windows, the Windows media player takes video stills from a second into the video.
On other platforms, they come from ffmpeg when it is installed.

The gallery supports these operations:

- Justified, virtualized rows or square tiles.
- On Windows, a click plays a video in its thumbnail; a double click or Enter opens it in full screen.
  Up to ten thumbnails play at once, each with its own player and a small bar for stop, play, position, time, and sound; starting one more stops the one that started first.
  When the gallery has videos, a button in its header mutes every video that plays in a thumbnail.
  Deleting a playing video stops it first.
- Buttons over a video thumbnail open it in the viewer or in full screen; the file name shows next to them while the pointer is over a thumbnail.
- Groups by day, month, or year, plus an ungrouped view.
- Search by name, folder, camera, year, or month.
- File type filters, including videos, and orientation filters.
- Sort by capture date, modified date, name, or size.
- Full scrollbars, the month and year of the topmost photo in the header, and an optional draggable timeline with proportional month and year positions.
- With name sorting, a bar of first letters (0–9, A–Z, Ä, Ö, Ü, #) that goes to the first file with a letter; it can be switched off, and so can its digits and umlauts.
- Selection with checkboxes, Ctrl-click, Shift-click, or Select all.
- Keyboard navigation: the arrow keys move between thumbnails, Page Up and Page Down move a screen, Home and End go to the start and end of the row, and Ctrl with Home, End, Page Up, or Page Down goes to the first or last thumbnail.
  The album and folder pages work the same way with their cards.
  On the home page the arrow keys move to the nearest card or button, and in the side bar the up and down arrow keys move between its buttons.
- Batch favorites, album assignment, clipboard copy, and Windows Recycle Bin actions.
- Context menus for individual photos or the visible selection.

Date groups and the timeline appear only with capture-date sorting; the timeline is off by default and switches on in the view options.
Search retains the current folder, album, or favorites source.
Album and folder cards load their covers only in the visible part of the collection list.
Thumbnails on the screen load first, from the top, and videos load their stills alongside the photos.
A thumbnail that takes a moment says "Waiting…" until its turn comes and shows a loading spinner while its file is read.
Thumbnails are cached in the app's data folder, up to 512 MB, so they appear at once after the first time.
A cached thumbnail that no longer decodes is made again from its file.

Albums reference the original paths.
They do not copy or move the images.

## Viewer and editor

The viewer supports zoom, pan, fit, slideshow, full screen, and file information.
Animated GIF and WebP files play with their frame times and repeat count.

On Windows, the viewer plays videos with the Windows media player, which supplies the installed codecs and the sound.
The playback bar offers play and pause, the position, the time, loop, and mute.
On Linux and macOS, the viewer shows the video still and opens the video in the default player.
Videos zoom, pan, and turn like photos while they play; a click on a video plays or pauses it.
A double click or Enter takes a video to full screen and back; Enter also leaves full screen for a photo.
On Windows, the left and right arrow keys jump ten seconds back or forward in a video; Page Up and Page Down go to the previous or next file.
Videos cannot be edited or used as wallpaper.
Outside the editor, turning changes only the view and asks for nothing to be saved.
Opening the editor takes the turn over as an edit.
Slideshows skip videos.
View options include rulers, gridlines, a navigator, a status bar, and an automatically hidden toolbar.
Move the pointer to the top edge to show a hidden toolbar.
In full screen, the toolbar, the status bar, the arrows, and the video controls hide after three seconds without a pointer move.
Any move shows them again, and they stay while the pointer is on one of them.
The file name in the status bar uses the room that the other entries leave.
In a narrow window, the other entries move below the file name and wrap.

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
Editing an animated image changes and saves its first frame.

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
| Ctrl+wheel / Ctrl+Plus / Ctrl+Minus | Change the thumbnail size on every page (50 pixels up to the window height) |
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
| Space / M | Play or pause / mute a video |
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
