using System.ComponentModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Flowery.NET.RuntimeTests;

/// <summary>
/// Places the test window from FLOWERY_MONITOR_DEVICE (for example <c>\\.\DISPLAY2</c>) and
/// FLOWERY_MONITOR_REGION (<c>full</c>, <c>upper-half</c> or <c>lower-half</c>).
/// </summary>
internal static class RuntimeTestWindowPlacement
{
    // Runtime tests must not rely on more room than a 1080p screen offers.
    private const int MaxWidth = 1920;
    private const int MaxHeight = 1080;

    /// <summary>
    /// Positions the window before it is shown and returns the requested region in device pixels,
    /// or null when no monitor was requested.
    /// </summary>
    public static PixelRect? Prepare(Window window)
    {
        var device = Environment.GetEnvironmentVariable("FLOWERY_MONITOR_DEVICE");
        if (string.IsNullOrWhiteSpace(device))
        {
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return null;
        }

        var region = Environment.GetEnvironmentVariable("FLOWERY_MONITOR_REGION");
        region = string.IsNullOrWhiteSpace(region) ? "full" : region.Trim().ToLowerInvariant();
        if (region is not ("full" or "upper-half" or "lower-half"))
            throw new ArgumentException("FLOWERY_MONITOR_REGION must be 'full', 'upper-half' or 'lower-half'.");

        var screen = window.Screens.All.FirstOrDefault(candidate =>
                         string.Equals(GetDeviceName(candidate), device, StringComparison.OrdinalIgnoreCase))
                     ?? throw new InvalidOperationException($"Requested monitor '{device}' is not available. Monitors: "
                                                            + string.Join(", ", window.Screens.All.Select(GetDeviceName)));

        var area = screen.WorkingArea;
        var top = region == "lower-half" ? area.Y + area.Height / 2 : area.Y;
        var height = region == "full" ? area.Height : area.Height / 2;
        var bounds = new PixelRect(area.X, top, Math.Min(area.Width, MaxWidth), Math.Min(height, MaxHeight));

        window.WindowStartupLocation = WindowStartupLocation.Manual;
        window.Position = bounds.Position;
        window.Width = bounds.Width / screen.Scaling;
        window.Height = bounds.Height / screen.Scaling;
        return bounds;
    }

    /// <summary>
    /// Shrinks the shown window so that its frame fits the region, then verifies the result.
    /// </summary>
    public static async Task<string> FitAsync(Window window, PixelRect region)
    {
        var frame = window.FrameSize ?? throw new InvalidOperationException("The window reports no frame size.");
        var scaling = window.DesktopScaling;
        window.Width = region.Width / scaling - (frame.Width - window.ClientSize.Width);
        window.Height = region.Height / scaling - (frame.Height - window.ClientSize.Height);
        window.Position = region.Position;
        await Dispatcher.UIThread.InvokeAsync(window.UpdateLayout, DispatcherPriority.Background);

        frame = window.FrameSize ?? frame;
        var actual = new PixelRect(window.Position, PixelSize.FromSize(frame, scaling));
        // One pixel of rounding is allowed at the far edges.
        if (actual.X < region.X || actual.Y < region.Y
            || actual.Right > region.Right + 1 || actual.Bottom > region.Bottom + 1)
        {
            throw new InvalidOperationException($"Window placement {Format(actual)} exceeds the requested region {Format(region)}.");
        }

        return $"Region={Format(region)}; Window={Format(actual)}; Scaling={scaling}";
    }

    private static string? GetDeviceName(Screen screen)
    {
        if (screen.TryGetPlatformHandle() is { } handle && handle.Handle != IntPtr.Zero)
        {
            var information = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>(), DeviceName = string.Empty };
            if (!GetMonitorInfo(handle.Handle, ref information))
                throw new Win32Exception(Marshal.GetLastWin32Error(), "Unable to read Windows monitor information.");
            return information.DeviceName;
        }

        return screen.DisplayName;
    }

    private static string Format(PixelRect bounds) => $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";

    [DllImport("user32.dll", EntryPoint = "GetMonitorInfoW", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx information);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct MonitorInfoEx
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
