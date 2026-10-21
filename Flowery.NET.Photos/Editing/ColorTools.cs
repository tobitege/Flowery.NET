using System.IO;
using System.Text;
using SkiaSharp;

namespace Flowery.NET.Photos.Editing
{
    /// <summary>
    /// Color math and palette helpers for the markup color management (HSL adjustments, harmonies,
    /// dominant colors from a photo, JASC-PAL / GIMP palette files).
    /// </summary>
    public static class ColorTools
    {
        public static (double H, double S, double L) ToHsl(Color c)
        {
            double r = c.R / 255.0, g = c.G / 255.0, b = c.B / 255.0;
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var l = (max + min) / 2;
            if (max - min < 1e-9)
                return (0, 0, l);
            var d = max - min;
            var s = l > 0.5 ? d / (2 - max - min) : d / (max + min);
            double h;
            if (max == r)
                h = (g - b) / d + (g < b ? 6 : 0);
            else if (max == g)
                h = (b - r) / d + 2;
            else
                h = (r - g) / d + 4;
            return (h * 60, s, l);
        }

        public static Color FromHsl(double h, double s, double l, byte alpha = 255)
        {
            h = ((h % 360) + 360) % 360 / 360;
            s = Math.Clamp(s, 0, 1);
            l = Math.Clamp(l, 0, 1);
            if (s < 1e-9)
            {
                var v = (byte)Math.Round(l * 255);
                return Color.FromArgb(alpha, v, v, v);
            }
            var q = l < 0.5 ? l * (1 + s) : l + s - l * s;
            var p = 2 * l - q;
            double Hue(double t)
            {
                if (t < 0) t += 1;
                if (t > 1) t -= 1;
                if (t < 1.0 / 6) return p + (q - p) * 6 * t;
                if (t < 0.5) return q;
                if (t < 2.0 / 3) return p + (q - p) * (2.0 / 3 - t) * 6;
                return p;
            }
            return Color.FromArgb(alpha,
                (byte)Math.Round(Hue(h + 1.0 / 3) * 255),
                (byte)Math.Round(Hue(h) * 255),
                (byte)Math.Round(Hue(h - 1.0 / 3) * 255));
        }

        public static Color AdjustLightness(Color c, double delta)
        {
            var (h, s, l) = ToHsl(c);
            return FromHsl(h, s, l + delta, c.A);
        }

        public static Color AdjustSaturation(Color c, double delta)
        {
            var (h, s, l) = ToHsl(c);
            return FromHsl(h, s + delta, l, c.A);
        }

        public static Color RotateHue(Color c, double degrees)
        {
            var (h, s, l) = ToHsl(c);
            return FromHsl(h + degrees, s, l, c.A);
        }

        public static Color Invert(Color c) => Color.FromArgb(c.A, (byte)(255 - c.R), (byte)(255 - c.G), (byte)(255 - c.B));

        public static Color Grayscale(Color c)
        {
            var y = (byte)Math.Round(0.2126 * c.R + 0.7152 * c.G + 0.0722 * c.B);
            return Color.FromArgb(c.A, y, y, y);
        }

        public static Color Warmer(Color c) =>
            Color.FromArgb(c.A, (byte)Math.Min(255, c.R + 18), c.G, (byte)Math.Max(0, c.B - 18));

        public enum Harmony
        {
            Complementary,
            Triad,
            Analogous,
            Monochrome
        }

        /// <summary>Builds a 9-color harmony palette around a base color.</summary>
        public static Color[] CreateHarmony(Color baseColor, Harmony harmony)
        {
            var (h, s, l) = ToHsl(baseColor);
            if (s < 0.05)
                s = 0.6;
            double[] hues = harmony switch
            {
                Harmony.Complementary => [h, h + 180],
                Harmony.Triad => [h, h + 120, h + 240],
                Harmony.Analogous => [h - 30, h, h + 30],
                _ => [h]
            };
            double[] lights = harmony == Harmony.Monochrome
                ? [0.15, 0.25, 0.35, 0.45, 0.55, 0.65, 0.75, 0.85, 0.93]
                : [Math.Clamp(l - 0.2, 0.1, 0.9), Math.Clamp(l, 0.15, 0.85), Math.Clamp(l + 0.2, 0.1, 0.92)];

            List<Color> result = [];
            foreach (var hue in hues)
            {
                foreach (var light in lights)
                    result.Add(FromHsl(hue, s, light));
            }
            while (result.Count < 9)
                result.Add(FromHsl(h, s * 0.5, 0.5 + result.Count * 0.04));
            return [.. result.Take(9)];
        }

        /// <summary>
        /// Extracts dominant colors from an image with a small k-means over a downsampled render.
        /// </summary>
        public static Color[] ExtractDominantColors(SKBitmap source, int count = 9)
        {
            var scale = 64.0 / Math.Max(source.Width, source.Height);
            using var sample = source.Resize(new SKImageInfo(Math.Max(1, (int)(source.Width * scale)),
                Math.Max(1, (int)(source.Height * scale)), SKColorType.Bgra8888),
                new SKSamplingOptions(SKFilterMode.Linear));
            if (sample == null) return [];
            var pixels = sample.Bytes;

            List<Vector3> points = [];
            for (var i = 0; i < pixels.Length; i += 4)
                points.Add(new Vector3(pixels[i + 2], pixels[i + 1], pixels[i]));
            if (points.Count == 0)
                return [];

            // Deterministic seeding spread over the sample.
            var k = Math.Min(count, points.Count);
            var centers = Enumerable.Range(0, k).Select(i => points[i * points.Count / k]).ToArray();
            var assignment = new int[points.Count];
            for (var iteration = 0; iteration < 12; iteration++)
            {
                for (var p = 0; p < points.Count; p++)
                {
                    var best = 0;
                    var bestDistance = float.MaxValue;
                    for (var c = 0; c < k; c++)
                    {
                        var d = Vector3.DistanceSquared(points[p], centers[c]);
                        if (d < bestDistance)
                        {
                            bestDistance = d;
                            best = c;
                        }
                    }
                    assignment[p] = best;
                }
                for (var c = 0; c < k; c++)
                {
                    var members = points.Where((_, idx) => assignment[idx] == c).ToList();
                    if (members.Count > 0)
                        centers[c] = members.Aggregate(Vector3.Zero, (a, v) => a + v) / members.Count;
                }
            }

            return [.. Enumerable.Range(0, k)
                .OrderByDescending(c => assignment.Count(a => a == c))
                .Select(c => Color.FromArgb(255, (byte)centers[c].X, (byte)centers[c].Y, (byte)centers[c].Z))];
        }

        /// <summary>Writes a Paint Shop Pro (JASC-PAL) palette.</summary>
        public static void WriteJascPalette(string path, IEnumerable<Color> colors)
        {
            var list = colors.ToList();
            var sb = new StringBuilder();
            sb.Append("JASC-PAL\r\n0100\r\n").Append(list.Count.ToString(CultureInfo.InvariantCulture)).Append("\r\n");
            foreach (var c in list)
                sb.Append(CultureInfo.InvariantCulture, $"{c.R} {c.G} {c.B}\r\n");
            File.WriteAllText(path, sb.ToString(), Encoding.ASCII);
        }

        /// <summary>Reads JASC-PAL (Paint Shop Pro) or GIMP (.gpl) palettes.</summary>
        public static List<Color> ReadPalette(string path)
        {
            List<Color> colors = [];
            var lines = File.ReadAllLines(path);
            var isJasc = lines.Length > 0 && lines[0].Trim().Equals("JASC-PAL", StringComparison.OrdinalIgnoreCase);
            var start = isJasc ? 3 : 0;
            for (var i = start; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (line.Length == 0 || line.StartsWith('#') || line.StartsWith("GIMP", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Name:", StringComparison.OrdinalIgnoreCase) || line.StartsWith("Columns:", StringComparison.OrdinalIgnoreCase))
                    continue;
                var parts = line.Split((char[])[' ', '\t'], StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 3
                    && byte.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var r)
                    && byte.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var g)
                    && byte.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var b))
                {
                    colors.Add(Color.FromArgb(255, r, g, b));
                }
            }
            return colors;
        }
    }
}
