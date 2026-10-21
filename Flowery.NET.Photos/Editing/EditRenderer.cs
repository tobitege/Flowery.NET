using SkiaSharp;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Editing;

/// <summary>The same Skia pipeline renders the live surface and full-resolution exports.</summary>
public static class EditRenderer
{
    private static readonly Lazy<SKRuntimeEffect> Tone = new(() =>
        SKRuntimeEffect.CreateShader(ToneSource, out var error)
        ?? throw new InvalidOperationException(error));
    private static readonly Lazy<SKRuntimeEffect> Vignette = new(() =>
        SKRuntimeEffect.CreateShader(VignetteSource, out var error)
        ?? throw new InvalidOperationException(error));

    private const string ToneSource = """
        uniform shader image;
        uniform float exposure;
        uniform float highlights;
        uniform float shadows;
        uniform float clarity;
        uniform float contrast;
        uniform float warmth;
        uniform float tint;
        float3 curve(float3 rgb, float amount) {
            return rgb + amount * (2.0 * rgb - 1.0) * min(rgb, 1.0 - rgb);
        }
        half4 main(float2 p) {
            half4 pixel = image.eval(p);
            float3 rgb = pixel.rgb / max(pixel.a, 0.0001);
            rgb *= pow(2.0, exposure);
            float l = dot(rgb, float3(0.2126, 0.7152, 0.0722));
            rgb += shadows * pow(1.0 - clamp(l, 0.0, 1.0), 2.0) * 0.4;
            rgb += highlights * pow(clamp(l, 0.0, 1.0), 2.0) * 0.4;
            rgb = curve(clamp(rgb, 0.0, 1.0), clarity * 4.0 * clamp(l, 0.0, 1.0) * (1.0 - clamp(l, 0.0, 1.0)));
            rgb = curve(clamp(rgb, 0.0, 1.0), contrast);
            rgb *= float3(1.0 + warmth * 0.16, 1.0 - tint * 0.12, 1.0 - warmth * 0.16);
            return half4(clamp(rgb, 0.0, 1.0) * pixel.a, pixel.a);
        }
        """;
    private const string VignetteSource = """
        uniform float2 size;
        uniform float amount;
        uniform float curve;
        uniform float white;
        half4 main(float2 p) {
            float distance = length((p / size - 0.5) * 1.41421356);
            float alpha = pow(smoothstep(1.0 - amount, 1.0, distance), curve);
            return half4(float3(white * alpha), alpha);
        }
        """;

    public static Size GetTransformedSize(Size source, EditState state) =>
        Math.Abs(state.Rotation % 2) == 1 ? new Size(source.Height, source.Width) : source;

    public static Size GetOutputSize(Size source, EditState state)
    {
        var size = GetTransformedSize(source, state);
        return new Size(Math.Max(1, Math.Round(size.Width * state.Crop.Width)),
            Math.Max(1, Math.Round(size.Height * state.Crop.Height)));
    }

    public static void Draw(SKCanvas canvas, SKImage source, EditState state)
    {
        var transformed = GetTransformedSize(new Size(source.Width, source.Height), state);
        var cropX = (float)(state.Crop.X * transformed.Width);
        var cropY = (float)(state.Crop.Y * transformed.Height);
        var output = GetOutputSize(new Size(source.Width, source.Height), state);
        var theta = Math.Abs(state.Straighten) * Math.PI / 180;
        var fill = Math.Cos(theta) + Math.Max(transformed.Width / transformed.Height,
            transformed.Height / transformed.Width) * Math.Sin(theta);
        var geometry = SKMatrix.Concat(SKMatrix.CreateTranslation((float)transformed.Width / 2 - cropX, (float)transformed.Height / 2 - cropY),
            SKMatrix.Concat(SKMatrix.CreateScale((float)fill, (float)fill),
            SKMatrix.Concat(SKMatrix.CreateRotationDegrees(state.Rotation * 90 + (float)state.Straighten),
            SKMatrix.Concat(SKMatrix.CreateScale(state.FlipH ? -1 : 1, state.FlipV ? -1 : 1),
                SKMatrix.CreateTranslation(-source.Width / 2f, -source.Height / 2f)))));

        using var child = source.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
            new SKSamplingOptions(SKFilterMode.Linear), geometry);
        var uniforms = new SKRuntimeEffectUniforms(Tone.Value)
        {
            ["exposure"] = (float)(state.Exposure / 100 * 1.5),
            ["highlights"] = (float)(state.Highlights / 100),
            ["shadows"] = (float)(state.Shadows / 100),
            ["clarity"] = (float)(state.Clarity / 100),
            ["contrast"] = (float)(state.Contrast / 100 * .75),
            ["warmth"] = (float)(state.Warmth / 100 * .8),
            ["tint"] = (float)(state.Tint / 100 * .8)
        };
        var children = new SKRuntimeEffectChildren(Tone.Value) { ["image"] = child };
        using var shader = Tone.Value.ToShader(uniforms, children);
        var matrix = ColorMatrix.Brightness((float)(state.Brightness / 400))
            .Then(ColorMatrix.Saturation((float)(1 + state.Saturation / 100)));
        if (state.Filter != FilterPresets.NoneId)
            matrix = matrix.Then(ColorMatrix.Identity.Lerp(FilterPresets.Get(state.Filter).Matrix,
                (float)Math.Clamp(state.FilterIntensity / 100, 0, 1)));
        matrix = matrix.Then(ColorMatrix.Fade((float)(state.Fade / 100)));
        using var color = SKColorFilter.CreateColorMatrix(matrix.ToSkiaMatrix());
        using var detail = CreateDetailFilter(state, output);
        using var paint = new SKPaint { Shader = shader, ColorFilter = color, ImageFilter = detail, IsAntialias = true };
        var bounds = SKRect.Create((float)output.Width, (float)output.Height);
        canvas.Save();
        canvas.ClipRect(bounds);
        canvas.SaveLayer(bounds, null);
        canvas.DrawRect(bounds, paint);
        if (state.Vignette != 0)
        {
            var strength = (float)Math.Clamp(Math.Abs(state.Vignette) / 100, 0, 1);
            var vignetteUniforms = new SKRuntimeEffectUniforms(Vignette.Value)
            {
                ["size"] = new float[] { (float)output.Width, (float)output.Height },
                ["amount"] = .15f + strength * .65f,
                ["curve"] = .35f + (1 - strength) * .4f,
                ["white"] = state.Vignette < 0 ? 1f : 0f
            };
            using var vignetteShader = Vignette.Value.ToShader(vignetteUniforms);
            using var vignettePaint = new SKPaint { Shader = vignetteShader, BlendMode = SKBlendMode.SrcATop };
            canvas.DrawRect(bounds, vignettePaint);
        }
        canvas.Restore();
        canvas.Translate(-cropX, -cropY);
        DrawStrokes(canvas, state.Strokes, transformed);
        canvas.Restore();
    }

    private static SKImageFilter? CreateDetailFilter(EditState state, Size output)
    {
        if (state.Sharpness < 0)
        {
            var radius = (float)(-state.Sharpness / 100 * Math.Max(output.Width, output.Height) / 400);
            return SKImageFilter.CreateBlur(radius, radius, SKShaderTileMode.Clamp);
        }
        var strength = (float)(state.Sharpness / 100 * 6);
        if (strength == 0) return null;
        float[] kernel = [0, -strength, 0, -strength, 1 + 4 * strength, -strength, 0, -strength, 0];
        return SKImageFilter.CreateMatrixConvolution(new SKSizeI(3, 3), kernel, 1, 0,
            new SKPointI(1, 1), SKShaderTileMode.Clamp, false);
    }

    public static void DrawStrokes(SKCanvas canvas, IEnumerable<MarkupStroke> strokes, Size size)
    {
        foreach (var stroke in strokes)
        {
            if (stroke.Points.IsEmpty) continue;
            using var paint = new SKPaint
            {
                Color = new SKColor(stroke.Color.R, stroke.Color.G, stroke.Color.B,
                    stroke.Highlighter ? (byte)Math.Min(110, (int)stroke.Color.A) : stroke.Color.A),
                StrokeWidth = Math.Max(1, stroke.Width * (float)Math.Max(size.Width, size.Height)),
                IsAntialias = true, Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round, StrokeJoin = SKStrokeJoin.Round
            };
            using var path = new SKPath();
            var first = stroke.Points[0];
            path.MoveTo(first.X * (float)size.Width, first.Y * (float)size.Height);
            for (var index = 1; index < stroke.Points.Length; index++)
            {
                var previous = stroke.Points[index - 1];
                var next = stroke.Points[index];
                path.QuadTo(previous.X * (float)size.Width, previous.Y * (float)size.Height,
                    (previous.X + next.X) / 2 * (float)size.Width, (previous.Y + next.Y) / 2 * (float)size.Height);
            }
            var last = stroke.Points[^1];
            path.LineTo(last.X * (float)size.Width, last.Y * (float)size.Height);
            if (stroke.Points.Length == 1)
                canvas.DrawPoint(first.X * (float)size.Width, first.Y * (float)size.Height, paint);
            else canvas.DrawPath(path, paint);
        }
    }

    public static SKBitmap Render(SKBitmap source, EditState state)
    {
        var size = GetOutputSize(new Size(source.Width, source.Height), state);
        var result = new SKBitmap((int)size.Width, (int)size.Height);
        using var canvas = new SKCanvas(result);
        using var image = SKImage.FromBitmap(source);
        canvas.Clear(SKColors.Transparent);
        Draw(canvas, image, state);
        return result;
    }

    public static bool CanEncode(string extension)
    {
        if (extension.ToLowerInvariant() is ".jpg" or ".jpeg" or ".jpe" or ".jfif" or ".png") return true;
#if WINDOWS
        return WindowsPhotoCodec.CanEncode(extension);
#else
        return false;
#endif
    }

    public static async Task SaveAsync(string sourcePath, string targetPath, EditState state, int quality, bool overwrite = true)
    {
        await Task.Run(async () =>
        {
            using var source = PhotoDecoder.Load(sourcePath);
            using var rendered = Render(source, state);
            using var image = SKImage.FromBitmap(rendered);
            var extension = Path.GetExtension(targetPath);
            if (!CanEncode(extension)) throw new NotSupportedException(extension);
            byte[] bytes;
#if WINDOWS
            if (WindowsPhotoCodec.CanEncode(extension)) bytes = await WindowsPhotoCodec.EncodeAsync(rendered, extension);
            else
#endif
            {
                var format = extension.Equals(".png", StringComparison.OrdinalIgnoreCase) ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
                using var encoded = image.Encode(format, quality);
                bytes = encoded.ToArray();
            }
            var temporary = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    await stream.WriteAsync(bytes);
                    stream.Flush(true);
                }
                File.Move(temporary, targetPath, overwrite);
            }
            catch (Exception error)
            {
                if (created && File.Exists(temporary) && !FileOperations.MoveToRecycleBin(temporary))
                    throw new IOException($"Export failed and its temporary file could not be moved to the Recycle Bin: {temporary}", error);
                throw;
            }
        });
    }
}
