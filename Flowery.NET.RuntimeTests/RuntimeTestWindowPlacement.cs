using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Flowery.NET.RuntimeTests;

/// <summary>
/// Places the test window from FLOWERY_MONITOR_DEVICE (for example <c>\\.\DISPLAY2</c>) and
/// FLOWERY_MONITOR_REGION (<c>full</c>, <c>upper-half</c> or <c>lower-half</c>). Without a monitor the window
/// is centered on the primary screen.
/// </summary>
internal static partial class RuntimeTestWindowPlacement
{
    // Runtime tests must not rely on more room than a 1080p screen at 100% scaling offers.
    private const double MaxWidth = 1920;
    private const double MaxHeight = 1080;
    // Without a requested monitor the window keeps this size if the screen has room for it.
    private const double DefaultWidth = 1280;
    private const double DefaultHeight = 900;

    /// <summary>
    /// Sizes and positions the window before it is shown. Returns the requested region in device pixels,
    /// or null when no monitor was requested.
    /// </summary>
    public static PixelRect? Prepare(Window window)
    {
        var device = Environment.GetEnvironmentVariable("FLOWERY_MONITOR_DEVICE");
        if (string.IsNullOrWhiteSpace(device))
        {
            // Small desktops, such as the 1024x768 desktop of CI runners, get a window that fits their work area.
            var primary = window.Screens.Primary ?? throw new InvalidOperationException("No screen is available.");
            window.Width = Math.Min(DefaultWidth, primary.WorkingArea.Width / primary.Scaling * 0.9);
            window.Height = Math.Min(DefaultHeight, primary.WorkingArea.Height / primary.Scaling * 0.9);
            window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            return null;
        }

        var region = Environment.GetEnvironmentVariable("FLOWERY_MONITOR_REGION");
        region = string.IsNullOrWhiteSpace(region) ? "full" : region.Trim().ToLowerInvariant();
        if (region is not ("full" or "upper-half" or "lower-half"))
            throw new ArgumentException("FLOWERY_MONITOR_REGION must be 'full', 'upper-half' or 'lower-half'.");

        var screen = FindScreen(window.Screens.All, device)
                     ?? throw new InvalidOperationException($"Requested monitor '{device}' is not available. Monitors: "
                                                            + string.Join(", ", window.Screens.All.Select(GetDeviceName)));

        var area = screen.WorkingArea;
        var top = region == "lower-half" ? area.Y + area.Height / 2 : area.Y;
        var height = region == "full" ? area.Height : area.Height / 2;
        var bounds = new PixelRect(area.X, top,
            Math.Min(area.Width, (int)(MaxWidth * screen.Scaling)),
            Math.Min(height, (int)(MaxHeight * screen.Scaling)));

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

    private static Screen? FindScreen(IReadOnlyList<Screen> screens, string device)
    {
        foreach (var screen in screens)
        {
            if (string.Equals(GetDeviceName(screen), device, StringComparison.OrdinalIgnoreCase))
                return screen;
        }

        return null;
    }

    private static string? GetDeviceName(Screen screen)
    {
        if (screen.TryGetPlatformHandle() is not { } handle || handle.Handle == IntPtr.Zero)
            return screen.DisplayName;

        var information = new MonitorInfoEx { Size = (uint)Marshal.SizeOf<MonitorInfoEx>() };
        if (!GetMonitorInfo(handle.Handle, ref information))
            throw new Win32Exception(Marshal.GetLastPInvokeError(), "Unable to read Windows monitor information.");

        var name = MemoryMarshal.Cast<ushort, char>((ReadOnlySpan<ushort>)information.DeviceName);
        var end = name.IndexOf('\0');
        return (end < 0 ? name : name[..end]).ToString();
    }

    private static string Format(PixelRect bounds) => $"{bounds.X},{bounds.Y},{bounds.Width},{bounds.Height}";

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr monitor, ref MonitorInfoEx information);

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfoEx
    {
        public uint Size;
        public NativeRect Monitor;
        public NativeRect WorkArea;
        public uint Flags;
        public DeviceNameBuffer DeviceName;
    }

    // CCHDEVICENAME UTF-16 code units, inline and as ushort so that the structure stays blittable.
    [InlineArray(32)]
    private struct DeviceNameBuffer
    {
        private ushort _element;
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
