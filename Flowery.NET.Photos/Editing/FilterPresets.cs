
namespace Flowery.NET.Photos.Editing
{
    /// <summary>
    /// A 5x4 color matrix in row-vector convention: out = [r g b a 1] * M.
    /// </summary>
    public readonly struct ColorMatrix
    {
        private readonly float[] _m;

        private ColorMatrix(float[] m) => _m = m;

        public float this[int row, int col] => _m[row * 4 + col];

        public static ColorMatrix Identity => new([
            1, 0, 0, 0,
            0, 1, 0, 0,
            0, 0, 1, 0,
            0, 0, 0, 1,
            0, 0, 0, 0]);

        public static ColorMatrix FromRows(float[] values) => new(values);

        public static ColorMatrix Saturation(float s)
        {
            const float lr = 0.2126f, lg = 0.7152f, lb = 0.0722f;
            var i = 1 - s;
            return new([
                lr * i + s, lr * i, lr * i, 0,
                lg * i, lg * i + s, lg * i, 0,
                lb * i, lb * i, lb * i + s, 0,
                0, 0, 0, 1,
                0, 0, 0, 0]);
        }

        public static ColorMatrix Contrast(float c)
        {
            var o = 0.5f * (1 - c);
            return new([
                c, 0, 0, 0,
                0, c, 0, 0,
                0, 0, c, 0,
                0, 0, 0, 1,
                o, o, o, 0]);
        }

        public static ColorMatrix Scale(float r, float g, float b, float offR = 0, float offG = 0, float offB = 0) => new([
            r, 0, 0, 0,
            0, g, 0, 0,
            0, 0, b, 0,
            0, 0, 0, 1,
            offR, offG, offB, 0]);

        public static ColorMatrix Brightness(float offset) => Scale(1, 1, 1, offset, offset, offset);

        /// <summary>Lifts blacks and compresses the tonal range for a faded film look.</summary>
        public static ColorMatrix Fade(float amount)
        {
            var scale = 1 - amount * 0.3f;
            var lift = amount * 0.12f;
            return Scale(scale, scale, scale, lift, lift, lift);
        }

        public static ColorMatrix Sepia => new([
            0.393f, 0.349f, 0.272f, 0,
            0.769f, 0.686f, 0.534f, 0,
            0.189f, 0.168f, 0.131f, 0,
            0, 0, 0, 1,
            0, 0, 0, 0]);

        /// <summary>Applies this matrix first, then <paramref name="next"/>.</summary>
        public ColorMatrix Then(ColorMatrix next)
        {
            var result = new float[20];
            for (var row = 0; row < 5; row++)
            {
                for (var col = 0; col < 4; col++)
                {
                    float sum = 0;
                    for (var k = 0; k < 4; k++)
                        sum += this[row, k] * next[k, col];
                    if (row == 4)
                        sum += next[4, col];
                    result[row * 4 + col] = sum;
                }
            }
            return new ColorMatrix(result);
        }

        public ColorMatrix Lerp(ColorMatrix target, float t)
        {
            var result = new float[20];
            for (var i = 0; i < 20; i++)
                result[i] = _m[i] + (target._m[i] - _m[i]) * t;
            return new ColorMatrix(result);
        }

        // SkiaSharp 3 uses column-vector 4x5 matrices with normalized channel offsets.
        public float[] ToSkiaMatrix() => [
            this[0, 0], this[1, 0], this[2, 0], this[3, 0], this[4, 0],
            this[0, 1], this[1, 1], this[2, 1], this[3, 1], this[4, 1],
            this[0, 2], this[1, 2], this[2, 2], this[3, 2], this[4, 2],
            this[0, 3], this[1, 3], this[2, 3], this[3, 3], this[4, 3]];
    }

    public sealed record FilterPreset(string Id, string LocalizationKey, ColorMatrix Matrix);

    /// <summary>
    /// Built-in "Stylize" filter looks expressed as color matrices.
    /// </summary>
    public static class FilterPresets
    {
        public const string NoneId = "none";

        public static IReadOnlyList<FilterPreset> All { get; } =
        [
            new(NoneId, "Photos_Filter_Original", ColorMatrix.Identity),
            new("vivid", "Photos_Filter_Vivid", ColorMatrix.Saturation(1.35f).Then(ColorMatrix.Contrast(1.08f))),
            new("warm", "Photos_Filter_Warm", ColorMatrix.Scale(1.08f, 1.0f, 0.88f, 0.02f, 0.01f, 0)),
            new("cool", "Photos_Filter_Cool", ColorMatrix.Scale(0.9f, 1.0f, 1.08f, 0, 0.01f, 0.03f)),
            new("chrome", "Photos_Filter_Chrome", ColorMatrix.Contrast(1.18f).Then(ColorMatrix.Saturation(1.2f)).Then(ColorMatrix.Scale(0.98f, 1.0f, 1.04f))),
            new("tealorange", "Photos_Filter_TealOrange", ColorMatrix.Scale(1.12f, 1.0f, 0.9f, -0.03f, 0, 0.06f).Then(ColorMatrix.Contrast(1.1f))),
            new("matte", "Photos_Filter_Matte", ColorMatrix.Saturation(0.85f).Then(ColorMatrix.Fade(0.7f))),
            new("vintage", "Photos_Filter_Vintage", ColorMatrix.Identity.Lerp(ColorMatrix.Sepia, 0.45f).Then(ColorMatrix.Fade(0.5f)).Then(ColorMatrix.Scale(1.05f, 1.0f, 0.92f))),
            new("dramatic", "Photos_Filter_Dramatic", ColorMatrix.Saturation(0.75f).Then(ColorMatrix.Contrast(1.38f))),
            new("lomo", "Photos_Filter_Lomo", ColorMatrix.Contrast(1.3f).Then(ColorMatrix.Saturation(1.3f)).Then(ColorMatrix.Scale(1.04f, 1.02f, 0.94f))),
            new("sepia", "Photos_Filter_Sepia", ColorMatrix.Sepia),
            new("mono", "Photos_Filter_Mono", ColorMatrix.Saturation(0)),
            new("noir", "Photos_Filter_Noir", ColorMatrix.Saturation(0).Then(ColorMatrix.Contrast(1.45f))),
            new("silver", "Photos_Filter_Silver", ColorMatrix.Saturation(0).Then(ColorMatrix.Fade(0.8f)).Then(ColorMatrix.Scale(0.98f, 1.0f, 1.04f)))
        ];

        public static FilterPreset Get(string? id) =>
            All.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase)) ?? All[0];
    }
}
