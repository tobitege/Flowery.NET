# Flowery.Capture.NET

A cross-platform screen capture library for Avalonia UI applications. Provides high-quality, pixel-perfect screenshots of controls with support for smart chunking of tall content.

## Features

- **High-quality capture on Windows**: Uses GDI+ `CopyFromScreen` with `PointToScreen` for accurate DPI-aware coordinates
- **Cross-platform support**: Native capture tools on macOS and Linux with automatic fallback
- **Smart chunking**: Automatically splits tall content at child element boundaries
- **Viewport clipping**: Captures only visible content within ScrollViewers
- **Screen boundary awareness**: Clips to working area (excludes taskbar)
- **Dependency Injection**: Easy integration with `IServiceCollection`
- **Factory pattern**: Simple usage without DI via `ScreenCapture.CreateDefault()`

## Installation

Add a project reference or install from NuGet:

```xml
<PackageReference Include="Flowery.Capture.NET" Version="1.0.0" />
```

## Requirements

| Platform | Requirements |
| -------- | ----------- |
| Windows  | .NET 8.0, Windows Forms (included automatically) |
| macOS    | .NET 8.0, Screen Recording permission in System Preferences |
| Linux    | .NET 8.0, `scrot` (X11) or `grim` (Wayland) |

### Linux Tool Installation

**X11 (most distributions):**

```bash
# Ubuntu/Debian
sudo apt install scrot

# Fedora
sudo dnf install scrot

# Arch
sudo pacman -S scrot
```

**Wayland:**

```bash
# Ubuntu/Debian
sudo apt install grim

# Fedora
sudo dnf install grim

# Arch
sudo pacman -S grim
```

## Usage

### Basic Usage (without DI)

```csharp
using Flowery.Capture;

// Get the platform-appropriate capture service
var captureService = ScreenCapture.CreateDefault();

// Capture a control
var result = await captureService.CaptureControlAsync(myControl);

if (result.Success)
{
    // Single chunk for small controls, multiple for tall content
    foreach (var chunk in result.Chunks)
    {
        await File.WriteAllBytesAsync("screenshot.png", chunk);
    }
}
```

### With Dependency Injection

```csharp
using Flowery.Capture.Extensions;

// In your service configuration
services.AddScreenCapture();

// In your class
public class MyViewModel
{
    private readonly IScreenCaptureService _captureService;

    public MyViewModel(IScreenCaptureService captureService)
    {
        _captureService = captureService;
    }

    public async Task CaptureAsync(Control control)
    {
        var result = await _captureService.CaptureControlAsync(control);
        // ...
    }
}
```

### With Options

```csharp
var options = new ScreenCaptureOptions
{
    ScrollViewer = myScrollViewer,           // Clip to viewport
    MaxChunkHeight = 800,                    // Max height per chunk
    EnableSmartChunking = true,              // Split at child boundaries
    CaptureMargin = new Thickness(-8, 0, 0, 0), // Adjust capture region
    ScrollSettleDelayMs = 500,               // Wait after scrolling
    ContentPanel = myWrapPanel               // Panel for smart chunking
};

var result = await captureService.CaptureControlAsync(control, options);
```

### Capture a Screen Region

```csharp
var region = new PixelRect(100, 100, 800, 600);
byte[] pngBytes = await captureService.CaptureRegionAsync(region);
```

## API Reference

### IScreenCaptureService

```csharp
public interface IScreenCaptureService
{
    Task<ScreenCaptureResult> CaptureControlAsync(
        Control control,
        ScreenCaptureOptions? options = null,
        CancellationToken ct = default);

    Task<byte[]> CaptureRegionAsync(
        PixelRect region,
        CancellationToken ct = default);

    bool IsHighQualityAvailable { get; }
}
```

### ScreenCaptureOptions

| Property | Type | Default | Description |
| -------- | ---- | ------- | ----------- |
| `ScrollViewer` | `ScrollViewer?` | `null` | Clips capture to visible viewport |
| `MaxChunkHeight` | `double` | `800` | Maximum height per chunk |
| `EnableSmartChunking` | `bool` | `true` | Split at child boundaries vs fixed height |
| `CaptureMargin` | `Thickness` | `(-8,0,0,0)` | Adjust capture region |
| `ScrollSettleDelayMs` | `int` | `500` | Delay after scrolling (ms) |
| `ContentPanel` | `Control?` | `null` | Panel for smart chunking |

### ScreenCaptureResult

| Property | Type | Description |
| -------- | ---- | ----------- |
| `Success` | `bool` | Whether capture succeeded |
| `Chunks` | `IReadOnlyList<byte[]>` | PNG-encoded image data |
| `ErrorMessage` | `string?` | Error details if failed |

## Platform Implementations

### Windows (WindowsScreenCapture)

- Uses `Graphics.CopyFromScreen` for pixel-perfect capture
- Handles high-DPI displays correctly via `PointToScreen`
- Clips to ScrollViewer viewport and screen working area
- Includes `CopyToClipboard` helper for Windows clipboard

### macOS (MacOSScreenCapture)

- Uses `screencapture -R x,y,w,h` CLI tool
- Requires Screen Recording permission
- Falls back to RenderTargetBitmap if permission denied

### Linux (LinuxScreenCapture)

- Auto-detects X11 vs Wayland via `XDG_SESSION_TYPE`
- X11: Uses `scrot -a x,y,w,h`
- Wayland: Uses `grim -g "x,y wxh"`
- Falls back to RenderTargetBitmap if tools not installed

### Fallback (FallbackScreenCapture)

- Uses Avalonia's `RenderTargetBitmap.Render()`
- Works on all platforms
- Lower quality on high-DPI displays (renders at 96 DPI)
- Cannot capture arbitrary screen regions

## Limitations

1. **RenderTargetBitmap fallback**: Produces lower quality on high-DPI displays. Text may appear blurry.

2. **macOS permissions**: Users must grant Screen Recording permission in System Preferences > Security & Privacy > Privacy > Screen Recording.

3. **Linux tool dependency**: Requires `scrot` or `grim` to be installed for high-quality capture.

4. **Wayland limitations**: Some Wayland compositors may restrict screen capture. XWayland apps may work with `scrot` as fallback.

5. **Region capture**: `CaptureRegionAsync` is not supported by the fallback implementation (returns empty array).

6. **Animated content**: Captures a single frame. For animated content, consider using GIF recording tools.

7. **Overlay windows**: Cannot capture content from other windows overlapping the target control.

## Troubleshooting

### Black images on Windows

- Ensure the control is fully visible on screen
- Check that the control is not behind the taskbar
- Increase `ScrollSettleDelayMs` if content is still rendering

### Blurry text

- You're likely using the fallback implementation
- On Windows: Ensure the `net10.0-windows` target is used
- On macOS: Grant Screen Recording permission
- On Linux: Install `scrot` or `grim`

### Empty capture result

- Check `result.ErrorMessage` for details
- Ensure the control has non-zero bounds
- Verify the control is attached to the visual tree

### macOS permission denied

1. Open System Preferences > Security & Privacy > Privacy
2. Select "Screen Recording" in the left panel
3. Add your application to the allowed list
4. Restart the application

## License

MIT License - see the main Flowery.NET repository for details.
