using System;
using Flowery.Capture.Platforms;

namespace Flowery.Capture;

/// <summary>
/// Factory for creating platform-appropriate screen capture service instances.
/// Use this when DI is not available.
/// </summary>
public static class ScreenCapture
{
    private static IScreenCaptureService? _default;
    private static readonly object _lock = new();

    /// <summary>
    /// Creates a new screen capture service appropriate for the current platform.
    /// </summary>
    public static IScreenCaptureService Create()
    {
#if WINDOWS
        if (OperatingSystem.IsWindows())
            return new WindowsScreenCapture();
#endif
        if (OperatingSystem.IsMacOS())
            return new MacOSScreenCapture();
        if (OperatingSystem.IsLinux())
            return new LinuxScreenCapture();

        return new FallbackScreenCapture();
    }

    /// <summary>
    /// Gets or creates a singleton screen capture service for the current platform.
    /// Thread-safe.
    /// </summary>
    public static IScreenCaptureService CreateDefault()
    {
        if (_default != null)
            return _default;

        lock (_lock)
        {
            _default ??= Create();
            return _default;
        }
    }
}

