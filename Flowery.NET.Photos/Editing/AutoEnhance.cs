using SkiaSharp;

namespace Flowery.NET.Photos.Editing
{
    /// <summary>
    /// Histogram-based one-click enhancement: balances exposure, contrast, highlights/shadows,
    /// white balance and saturation from a small render of the current (cropped) image.
    /// </summary>
    public static class AutoEnhance
    {
        public static EditState Apply(SKBitmap source, EditState state)
        {
            using var geometry = EditRenderer.Render(source, state.ResetAdjustments() with { Filter = FilterPresets.NoneId });
            var scale = Math.Min(1.0, 256.0 / Math.Max(geometry.Width, geometry.Height));
            using var sample = geometry.Resize(new SKImageInfo(Math.Max(1, (int)(geometry.Width * scale)),
                Math.Max(1, (int)(geometry.Height * scale)), SKColorType.Bgra8888),
                new SKSamplingOptions(SKFilterMode.Linear));
            if (sample == null) return state;
            var pixels = sample.Bytes;

            var histogram = new int[256];
            double sumR = 0, sumG = 0, sumB = 0, sumSat = 0;
            var count = pixels.Length / 4;
            for (var i = 0; i < pixels.Length; i += 4)
            {
                double b = pixels[i] / 255.0, g = pixels[i + 1] / 255.0, r = pixels[i + 2] / 255.0;
                var lum = 0.2126 * r + 0.7152 * g + 0.0722 * b;
                histogram[Math.Clamp((int)(lum * 255), 0, 255)]++;
                sumR += r;
                sumG += g;
                sumB += b;
                var max = Math.Max(r, Math.Max(g, b));
                var min = Math.Min(r, Math.Min(g, b));
                sumSat += max <= 0 ? 0 : (max - min) / max;
            }

            if (count == 0)
                return state;

            double Percentile(double p)
            {
                var targetCount = count * p;
                var acc = 0;
                for (var i = 0; i < 256; i++)
                {
                    acc += histogram[i];
                    if (acc >= targetCount)
                        return i / 255.0;
                }
                return 1;
            }

            var p02 = Percentile(0.02);
            var p10 = Percentile(0.10);
            var p50 = Percentile(0.50);
            var p98 = Percentile(0.98);
            var meanR = sumR / count;
            var meanB = sumB / count;
            var meanSat = sumSat / count;

            var exposure = Math.Clamp(Math.Log2(0.46 / Math.Max(0.04, p50)) / 1.5 * 100 * 0.6, -45, 45);
            var spread = p98 - p02;
            var contrast = spread < 0.85 ? Math.Clamp((0.85 - spread) * 90, 0, 35) : 0;
            var shadows = p10 < 0.12 ? Math.Clamp((0.12 - p10) * 250, 0, 35) : 0;
            var highlights = p98 > 0.96 ? -Math.Clamp((p98 - 0.96) * 600, 0, 35) : 0;
            var warmth = Math.Clamp((meanB - meanR) * 160, -25, 25);
            var saturation = meanSat < 0.3 ? Math.Clamp((0.3 - meanSat) * 80, 0, 20) : 0;

            return state with
            {
                Exposure = Math.Round(exposure),
                Contrast = Math.Round(contrast),
                Shadows = Math.Round(shadows),
                Highlights = Math.Round(highlights),
                Warmth = Math.Round(warmth),
                Saturation = Math.Round(saturation),
                Sharpness = Math.Max(state.Sharpness, 12),
                Brightness = 0
            };
        }
    }
}
