# run-android.ps1

Builds and deploys the Flowery.NET Gallery app to an Android emulator.

## Requirements

- **.NET 9 SDK** with Android workload installed
- **Android SDK** (via Android Studio or standalone)
- **ADB** (Android Debug Bridge) in PATH
- Running Android emulator or connected device

## Usage

```powershell
.\scripts\run-android.ps1
```

## Options

| Parameter | Default | Description |
|-----------|---------|-------------|
| `-DeviceName` | `emulator-5554` | Target device ID (use `adb devices` to list) |
| `-Configuration` | `Debug` | Build configuration (`Debug` or `Release`) |

## Examples

```powershell
# Default: Debug build to emulator-5554
.\scripts\run-android.ps1

# Deploy to a different emulator
.\scripts\run-android.ps1 -DeviceName "emulator-5556"

# Release build
.\scripts\run-android.ps1 -Configuration Release

# Both options
.\scripts\run-android.ps1 -DeviceName "emulator-5556" -Configuration Release
```

## Notes

- The script checks if the target device is connected before building
- Run `adb devices` to find your emulator's device ID
- Emulators typically use IDs like `emulator-5554`, `emulator-5556`, etc.
