using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.Skia;

namespace Flowery.NET.PackageTest;

/// <summary>
/// Renders the main window headless with SkiaSharp and checks that its glass blurs the live backdrop through the
/// renderer of Flowery.NET.Skia.
/// </summary>
internal static class PackageCheck
{
    // Neighboring pixels of the stripes differ by 765, the sum of three channels from black to white. Behind a glass
    // that blurs them, they barely differ.
    private const int StripeStep = 300;
    private const int BlurredStep = 45;

    public static int Run()
    {
        // The setup of the app, on the headless platform instead of the desktop.
        Program.BuildAvaloniaApp()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .SetupWithoutStarting();

        var window = new MainWindow();
        window.Show();
        // A glass redraws its backdrop in full for a few frames after it appeared.
        for (var tick = 0; tick < 10; tick++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }

        Console.WriteLine(MainWindow.Describe(typeof(DaisyGlass).Assembly));
        Console.WriteLine(MainWindow.Describe(typeof(FlowerySkia).Assembly));

        var failures = new List<string>();
        var glass = window.LiveGlass;
        if (glass.EffectiveBlurMode != GlassBlurMode.SkiaSharp)
            failures.Add($"The glass uses {glass.EffectiveBlurMode} instead of the live SkiaSharp blur.");

        using var frame = window.CaptureRenderedFrame()
            ?? throw new InvalidOperationException("The window rendered no frame.");
        var origin = glass.TranslatePoint(default, window)
            ?? throw new InvalidOperationException("The glass is not in the window.");
        // A row across the stripes above the glass, and one through the middle of the glass, away from its rim.
        var beside = MaxStep(frame, window.RenderScaling, 8, 8, window.Bounds.Width - 8);
        var behind = MaxStep(frame, window.RenderScaling, origin.Y + glass.Bounds.Height / 2,
            origin.X + glass.Bounds.Width / 4, origin.X + glass.Bounds.Width * 3 / 4);
        Console.WriteLine($"Largest step between neighboring pixels: {beside} beside the glass, {behind} behind it.");

        if (beside < StripeStep)
            failures.Add("The stripes beside the glass did not render.");
        else if (behind > BlurredStep)
            failures.Add("The glass did not blur the stripes behind it.");

        window.Close();
        foreach (var failure in failures)
            Console.Error.WriteLine($"FAILED: {failure}");
        if (failures.Count == 0)
            Console.WriteLine("OK: the glass blurs its live backdrop with Flowery.NET.Skia.");
        return failures.Count == 0 ? 0 : 1;
    }

    /// <summary>
    /// Returns the largest brightness difference between neighboring pixels of the row at <paramref name="y"/>, from
    /// <paramref name="left"/> to <paramref name="right"/>, in device-independent pixels at <paramref name="scale"/>.
    /// </summary>
    private static int MaxStep(WriteableBitmap frame, double scale, double y, double left, double right)
    {
        using var buffer = frame.Lock();
        var (row, first, last) = ((int)(y * scale), (int)(left * scale), (int)(right * scale));
        if (row < 0 || row >= buffer.Size.Height || first < 0 || last > buffer.Size.Width)
        {
            throw new InvalidOperationException(
                $"Row {row} from {first} to {last} is outside the frame of {buffer.Size}.");
        }

        var pixels = new byte[buffer.RowBytes];
        Marshal.Copy(buffer.Address + row * buffer.RowBytes, pixels, 0, pixels.Length);
        var max = 0;
        for (var x = first + 1; x < last; x++)
            max = Math.Max(max, Math.Abs(Brightness(pixels, x) - Brightness(pixels, x - 1)));
        return max;
    }

    // The sum of the three color channels, the same for BGRA and RGBA.
    private static int Brightness(byte[] pixels, int x) => pixels[x * 4] + pixels[x * 4 + 1] + pixels[x * 4 + 2];
}
