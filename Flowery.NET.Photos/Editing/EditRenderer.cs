using SkiaSharp;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Editing;

/// <summary>The same Skia pipeline renders the live surface and full-resolution exports.</summary>
public static class EditRenderer
{
    private static readonly Lazy<SKRuntimeEffect> Tone = new(() =>
        SKRuntimeEffect.CreateShader(ToneSource, out var error)
        ?? throw new InvalidOperationException(error));

    private const string ToneSource = """
        uniform shader image;
        uniform float exposure;
        uniform float highlights;
        uniform float shadows;
        uniform float warmth;
        uniform float tint;
        uniform float vignette;
        uniform float2 size;
        half4 main(float2 p) {
            half4 pixel = image.eval(p);
            float3 rgb = pixel.rgb / max(pixel.a, 0.0001);
            rgb *= pow(2.0, exposure);
            float l = dot(rgb, float3(0.2126, 0.7152, 0.0722));
            rgb += shadows * pow(1.0 - clamp(l, 0.0, 1.0), 2.0) * 0.4;
            rgb += highlights * pow(clamp(l, 0.0, 1.0), 2.0) * 0.4;
            rgb *= float3(1.0 + warmth * 0.16, 1.0 - tint * 0.12, 1.0 - warmth * 0.16);
            float distance = length((p / size - 0.5) * 1.4142);
            float amount = smoothstep(0.3, 0.95, distance) * abs(vignette) * 0.8;
            rgb = mix(rgb, vignette > 0.0 ? float3(0) : float3(1), amount);
            return half4(clamp(rgb, 0.0, 1.0) * pixel.a, pixel.a);
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
        canvas.Save();
        canvas.Translate(-cropX, -cropY);
        canvas.Save();
        canvas.Translate((float)transformed.Width / 2, (float)transformed.Height / 2);
        var theta = Math.Abs(state.Straighten) * Math.PI / 180;
        var fill = Math.Cos(theta) + Math.Max(transformed.Width / transformed.Height,
            transformed.Height / transformed.Width) * Math.Sin(theta);
        canvas.Scale((float)fill);
        canvas.RotateDegrees(state.Rotation * 90 + (float)state.Straighten);
        canvas.Scale(state.FlipH ? -1 : 1, state.FlipV ? -1 : 1);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);

        using var child = source.ToShader(SKShaderTileMode.Clamp, SKShaderTileMode.Clamp,
            new SKSamplingOptions(SKFilterMode.Linear));
        var uniforms = new SKRuntimeEffectUniforms(Tone.Value)
        {
            ["exposure"] = (float)(state.Exposure / 100 * 1.5),
            ["highlights"] = (float)(state.Highlights / 100),
            ["shadows"] = (float)(state.Shadows / 100),
            ["warmth"] = (float)(state.Warmth / 100),
            ["tint"] = (float)(state.Tint / 100),
            ["vignette"] = (float)(state.Vignette / 100),
            ["size"] = new float[] { source.Width, source.Height }
        };
        var children = new SKRuntimeEffectChildren(Tone.Value) { ["image"] = child };
        using var shader = Tone.Value.ToShader(uniforms, children);
        var matrix = ColorMatrix.Contrast((float)(1 + state.Contrast / 100 * 0.75))
            .Then(ColorMatrix.Brightness((float)(state.Brightness / 400)))
            .Then(ColorMatrix.Saturation((float)(1 + state.Saturation / 100)));
        if (state.Filter != FilterPresets.NoneId)
            matrix = matrix.Then(ColorMatrix.Identity.Lerp(FilterPresets.Get(state.Filter).Matrix,
                (float)Math.Clamp(state.FilterIntensity / 100, 0, 1)));
        matrix = matrix.Then(ColorMatrix.Fade((float)(state.Fade / 100)));
        using var color = SKColorFilter.CreateColorMatrix(matrix.ToSkiaMatrix());
        using var detail = CreateDetailFilter(state);
        using var paint = new SKPaint { Shader = shader, ColorFilter = color, ImageFilter = detail, IsAntialias = true };
        canvas.DrawRect(0, 0, source.Width, source.Height, paint);
        canvas.Restore();
        DrawStrokes(canvas, state.Strokes, transformed);
        canvas.Restore();
    }

    private static SKImageFilter? CreateDetailFilter(EditState state)
    {
        if (state.Sharpness < 0)
            return SKImageFilter.CreateBlur((float)(-state.Sharpness / 100 * 4), (float)(-state.Sharpness / 100 * 4));
        var strength = (float)((state.Sharpness + state.Clarity * 0.4) / 100);
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
            foreach (var point in stroke.Points.Skip(1))
                path.LineTo(point.X * (float)size.Width, point.Y * (float)size.Height);
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

    public static async Task SaveAsync(string sourcePath, string targetPath, EditState state, int quality)
    {
        await Task.Run(() =>
        {
            using var source = PhotoDecoder.Load(sourcePath);
            using var rendered = Render(source, state);
            using var image = SKImage.FromBitmap(rendered);
            var format = Path.GetExtension(targetPath).Equals(".png", StringComparison.OrdinalIgnoreCase)
                ? SKEncodedImageFormat.Png : SKEncodedImageFormat.Jpeg;
            using var encoded = image.Encode(format, quality);
            var temporary = targetPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
            var created = false;
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    created = true;
                    encoded.SaveTo(stream);
                    stream.Flush(true);
                }
                File.Move(temporary, targetPath, true);
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
