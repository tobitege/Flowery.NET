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
    // Neighboring pixels of the stripes differ by 765, the sum of three channels from black to white. Behind a glass that
    // blurs them, they barely differ.
    private const int StripeStep = 300;
    private const int BlurredStep = 45;

    public static int Run()
    {
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false })
            .WithInterFont()
            .UseFlowerySkia()
            .SetupWithoutStarting();

        var window = new MainWindow();
        window.Show();
        // A glass redraws its backdrop in full for a few frames after it appeared.
        for (var frame = 0; frame < 10; frame++)
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

        using var frameBitmap = window.CaptureRenderedFrame();
        if (frameBitmap is null)
        {
            failures.Add("The window rendered no frame.");
        }
        else
        {
            var scale = window.RenderScaling;
            var origin = glass.TranslatePoint(default, window) ?? default;
            // A row across the stripes above the glass, and one through the lower part of the glass, below its text and
            // away from its rim.
            var beside = MaxStep(frameBitmap, 8, 8, (int)((window.Bounds.Width - 8) * scale));
            var behind = MaxStep(frameBitmap,
                (int)((origin.Y + glass.Bounds.Height * 0.8) * scale),
                (int)((origin.X + glass.Bounds.Width * 0.25) * scale),
                (int)((origin.X + glass.Bounds.Width * 0.75) * scale));
            Console.WriteLine($"Largest step between neighboring pixels: {beside} beside the glass, {behind} behind it.");

            if (beside < StripeStep)
                failures.Add("The stripes beside the glass did not render.");
            else if (behind > BlurredStep)
                failures.Add("The glass did not blur the stripes behind it.");
        }

        window.Close();
        foreach (var failure in failures)
            Console.Error.WriteLine($"FAILED: {failure}");
        if (failures.Count == 0)
            Console.WriteLine("OK: the glass blurs its live backdrop with Flowery.NET.Skia.");
        return failures.Count == 0 ? 0 : 1;
    }

    /// <summary>
    /// Returns the largest brightness difference between neighboring pixels of row <paramref name="y"/>, from
    /// <paramref name="left"/> to <paramref name="right"/>.
    /// </summary>
    private static int MaxStep(WriteableBitmap frame, int y, int left, int right)
    {
        using var buffer = frame.Lock();
        var row = new byte[buffer.RowBytes];
        Marshal.Copy(buffer.Address + y * buffer.RowBytes, row, 0, row.Length);

        var max = 0;
        for (var x = left + 1; x < right; x++)
            max = Math.Max(max, Math.Abs(Brightness(row, x) - Brightness(row, x - 1)));
        return max;
    }

    // The sum of the three color channels, the same for BGRA and RGBA.
    private static int Brightness(byte[] row, int x) => row[x * 4] + row[x * 4 + 1] + row[x * 4 + 2];
}
