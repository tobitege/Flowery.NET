# Flowery.NET Build & Run Scripts

PowerShell scripts for building and running the multi-platform Flowery.NET.Gallery application.

## Understanding .NET Workloads

This project targets multiple platforms (Desktop, Browser/WASM, Android, iOS), each requiring specific .NET workloads. Workloads are optional SDK components that add platform-specific build capabilities.

### When to Restore Workloads

You need to run `dotnet workload restore` (or use `-RestoreWorkloads`) when:

1. **First-time clone** - After cloning the repository on a new machine
2. **After .NET SDK upgrade** - Workloads are tied to SDK versions; upgrading the SDK may require reinstalling them
3. **Build errors mentioning missing workloads** - e.g., `error NETSDK1147: To build this project, the following workloads must be installed: wasm-tools`
4. **After pulling changes that add new platform targets** - If someone adds iOS support and you didn't have the `ios` workload

### How Workload Restore Works

Run these commands from the **solution root folder** (where `Flowery.NET.sln` is located):

```powershell
cd path/to/Flowery.NET

# Restores workloads based on what the solution/projects require
dotnet workload restore

# Or install specific workloads manually
dotnet workload install wasm-tools    # For Browser/WASM
dotnet workload install android       # For Android
dotnet workload install ios           # For iOS (macOS only)
```

Running from the solution root allows `dotnet workload restore` to scan all projects and determine which workloads are needed.

The `build_all.ps1` script handles this automatically - it resolves the repo root and runs `dotnet workload restore` from there when you use the `-RestoreWorkloads` flag.

---

## Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0) or later
- PowerShell 7+ (`pwsh`)
- For Android builds: Android SDK (auto-detected or specify path)
- For Browser builds: `wasm-tools` workload

### Installing Workloads

```powershell
# Install all required workloads (first-time setup)
dotnet workload install wasm-tools
dotnet workload install android

# Or use the build script's -RestoreWorkloads flag
pwsh ./scripts/build_all.ps1 -RestoreWorkloads
```

---

## build_all.ps1

Builds all Flowery.NET projects in the correct order with proper per-project settings.

### Usage

```powershell
# Basic build (Debug configuration)
pwsh ./scripts/build_all.ps1

# Release build
pwsh ./scripts/build_all.ps1 -Configuration Release

# With workload restore (first-time setup)
pwsh ./scripts/build_all.ps1 -RestoreWorkloads

# Specify Android SDK path manually
pwsh ./scripts/build_all.ps1 -AndroidSdkDirectory "C:\Users\YOURUSER\AppData\Local\Android\Sdk"
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-Configuration` | string | `Debug` | Build configuration (`Debug` or `Release`) |
| `-AndroidSdkDirectory` | string | auto-detect | Path to Android SDK. Auto-detected from `ANDROID_SDK_ROOT`, `ANDROID_HOME`, or `%LOCALAPPDATA%\Android\Sdk` |
| `-RestoreWorkloads` | switch | false | Run `dotnet workload restore` before building |

### Build Order

The script builds projects in dependency order:

1. `Flowery.Capture.NET` - Screenshot capture library
2. `Flowery.NET` - Core UI component library
3. `Flowery.NET.Gallery` - Shared gallery library (no entry point)
4. `Flowery.NET.Gallery.Desktop` - Desktop host (Windows/Linux/macOS)
5. `Flowery.NET.Gallery.Browser` - WebAssembly host
6. `Flowery.NET.Tests` - Unit tests
7. `Flowery.NET.Gallery.Android` - Android host (with `InstallAndroidDependencies`)

### Output

The script displays a build summary at the end:

```log
═══════════════════════════════════════════════════════════
 BUILD SUMMARY
═══════════════════════════════════════════════════════════

  [OK] Build: Flowery.Capture.NET                  00:02.34
  [OK] Build: Flowery.NET                          00:03.12
  [OK] Build: Flowery.NET.Gallery                  00:04.56
  [OK] Build: Flowery.NET.Gallery.Desktop          00:01.23
  [OK] Build: Flowery.NET.Gallery.Browser          00:05.67
  [OK] Build: Flowery.NET.Tests                    00:02.89
  [OK] Android: InstallAndroidDependencies         00:08.45
  [OK] Android: Build                              00:12.34

  Total time: 00:40.60
  Projects built: 8

All builds completed successfully.
```

If any step fails, the script stops immediately and shows `[FAILED]` status.

---

## run_browser.ps1

Starts the pre-compiled Flowery.NET.Gallery.Browser WASM application and optionally opens it in your default browser.

### Browser: Usage

```powershell
# Run and open browser automatically
pwsh ./scripts/run_browser.ps1

# Run without opening browser
pwsh ./scripts/run_browser.ps1 -NoBrowser

# Use a custom port
pwsh ./scripts/run_browser.ps1 -Port 8080
```

### Browser: Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `-NoBrowser` | switch | false | If specified, does not automatically open the browser |
| `-Port` | int | `5235` | HTTP port to use for the local server |

### Notes

- Uses `--no-build` flag, so run `build_all.ps1` first if you haven't built yet
- Browser opens after a 3-second delay to give the server time to start
- Press `Ctrl+C` to stop the server
- Default URL: `http://localhost:5235`

### Example Workflow

```powershell
# First time: build everything
pwsh ./scripts/build_all.ps1

# Then run the browser version
pwsh ./scripts/run_browser.ps1
```

---

## Troubleshooting

### Android SDK not found

```log
AndroidSdkDirectory not set. Pass -AndroidSdkDirectory or set ANDROID_SDK_ROOT / ANDROID_HOME.
```

**Solution:** Set environment variable or pass the path:

```powershell
# Option 1: Set environment variable
$env:ANDROID_SDK_ROOT = "C:\Users\YOURUSER\AppData\Local\Android\Sdk"

# Option 2: Pass as parameter
pwsh ./scripts/build_all.ps1 -AndroidSdkDirectory "C:\Users\YOURUSER\AppData\Local\Android\Sdk"
```

### Browser build fails with SkiaSharp error

Ensure `WasmBuildNative` is enabled in `Flowery.NET.Gallery.Browser.csproj`:

```xml
<PropertyGroup>
    <WasmBuildNative>true</WasmBuildNative>
</PropertyGroup>
```

### Workload not installed

```log
error NETSDK1147: To build this project, the following workloads must be installed: wasm-tools
```

**Solution:**

```powershell
dotnet workload install wasm-tools
# Or use:
pwsh ./scripts/build_all.ps1 -RestoreWorkloads
```
