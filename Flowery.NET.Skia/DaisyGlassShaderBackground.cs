using Avalonia;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Flowery.Controls;
using Flowery.Enums;
using SkiaSharp;

namespace Flowery.Skia
{
    /// <summary>
    /// An animated glass background drawn with SkiaSharp runtime shaders: per-pixel plasma, aurora curtains and molten
    /// gold. It draws in every blur mode, but needs the Skia renderer; <see cref="DaisyGlassGradientBackground"/> in
    /// Flowery.NET draws the same effects with gradients.
    /// </summary>
    public class DaisyGlassShaderBackground : DaisyGlassBackground
    {
        /// <inheritdoc/>
        public override DaisyGlassAnimationLayer CreateLayer() => new ShaderLayer(this);

        /// <summary>
        /// Returns the compiled shader of an effect. The shaders compile once per process.
        /// </summary>
        internal static SKRuntimeEffect GetEffect(DaisyGlassAnimation effect) => effect switch
        {
            DaisyGlassAnimation.Aurora => Aurora.Value,
            DaisyGlassAnimation.LiquidGold => LiquidGold.Value,
            _ => Plasma.Value
        };

        private static Lazy<SKRuntimeEffect> Compile(string source) => new(() =>
            SKRuntimeEffect.CreateShader(source, out var error)
            ?? throw new InvalidOperationException("A glass background shader did not compile: " + error));

        // Shared value noise for the aurora shimmer and the gold surface.
        private const string Noise = """
            float hash(float2 p) { return fract(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float noise(float2 p) {
                float2 i = floor(p);
                float2 f = fract(p);
                float2 u = f * f * (3.0 - 2.0 * f);
                return mix(mix(hash(i), hash(i + float2(1.0, 0.0)), u.x),
                           mix(hash(i + float2(0.0, 1.0)), hash(i + float2(1.0, 1.0)), u.x), u.y);
            }
            """;

        private static readonly Lazy<SKRuntimeEffect> Plasma = Compile("""
            uniform float2 size;
            uniform float time;
            half4 main(float2 p) {
                // One pattern unit per 140 DIPs, so a small button shows a calm section and a large panel more of it.
                float2 uv = p / 140.0;
                float t = time * 0.6;
                float v = sin(uv.x * 6.0 + t)
                        + sin((uv.y * 6.0 + t) * 0.8)
                        + sin(uv.x * 4.0 + uv.y * 4.0 + t * 1.3)
                        + sin(length(uv * 6.0 - float2(3.0 + 2.0 * sin(t * 0.7), 2.0 + 2.0 * cos(t * 0.5))) + t);
                float3 c = float3(0.55 + 0.3 * sin(v * 1.6),
                                  0.45 + 0.3 * sin(v * 1.6 + 2.094),
                                  0.65 + 0.3 * sin(v * 1.6 + 4.188));
                return half4(half3(c), 1.0);
            }
            """);

        private static readonly Lazy<SKRuntimeEffect> Aurora = Compile("""
            uniform float2 size;
            uniform float time;
            """ + Noise + """
            half4 main(float2 p) {
                float2 uv = p / max(size, float2(1.0));
                float t = time * 0.3;
                float3 color = mix(float3(0.02, 0.05, 0.12), float3(0.03, 0.12, 0.18), uv.y);
                for (int i = 0; i < 3; i++) {
                    float fi = float(i);
                    float x = p.x / 160.0;
                    float y = 0.3 + 0.18 * fi + 0.12 * sin(x * 3.0 + t * (1.0 + fi * 0.3) + fi)
                            + 0.08 * (noise(float2(x * 4.0 + t + fi * 10.0, t * 0.5)) - 0.5);
                    float d = (uv.y - y) * (9.0 - fi * 2.0);
                    float band = exp(-d * d);
                    float shimmer = 0.75 + 0.25 * noise(float2(p.x / 12.0 + t * 2.0, fi * 7.0));
                    float3 tint = i == 0 ? float3(0.2, 0.95, 0.6) : (i == 1 ? float3(0.15, 0.8, 0.95) : float3(0.65, 0.45, 0.98));
                    color += tint * band * shimmer * 0.9;
                }
                return half4(half3(color), 1.0);
            }
            """);

        private static readonly Lazy<SKRuntimeEffect> LiquidGold = Compile("""
            uniform float2 size;
            uniform float time;
            """ + Noise + """
            half4 main(float2 p) {
                float2 q = p / 70.0;
                float t = time * 0.4;
                float n = noise(q + float2(t * 0.4, t * 0.2));
                float w = sin(q.x * 1.7 + n * 4.0 + t) * cos(q.y * 1.3 - n * 3.0 + t * 0.7);
                float h = 0.5 + 0.5 * w;
                float specular = pow(clamp(1.0 - abs(w - 0.6) * 2.5, 0.0, 1.0), 8.0);
                float3 color = mix(float3(0.35, 0.22, 0.02), float3(0.83, 0.62, 0.12), smoothstep(0.0, 0.6, h));
                color = mix(color, float3(1.0, 0.9, 0.55), smoothstep(0.6, 1.0, h));
                color += float3(1.0, 0.95, 0.8) * specular * 0.5;
                return half4(half3(color), 1.0);
            }
            """);

        private sealed class ShaderLayer(DaisyGlassBackground source) : DaisyGlassAnimationLayer(source)
        {
            public override void Render(DrawingContext context)
            {
                if (Bounds.Width > 0 && Bounds.Height > 0)
                    context.Custom(new ShaderOperation(new Rect(Bounds.Size), Source.Effect, (float)Time));
            }
        }

        private sealed class ShaderOperation : ICustomDrawOperation
        {
            private readonly DaisyGlassAnimation _effect;
            private readonly float _time;

            public ShaderOperation(Rect bounds, DaisyGlassAnimation effect, float time)
            {
                Bounds = bounds;
                _effect = effect;
                _time = time;
            }

            public Rect Bounds { get; }

            public bool HitTest(Point p) => false;

            public bool Equals(ICustomDrawOperation? other) =>
                other is ShaderOperation op && op.Bounds == Bounds && op._effect == _effect && op._time == _time;

            public void Dispose() { }

            public void Render(ImmediateDrawingContext context)
            {
                if (context.TryGetFeature<ISkiaSharpApiLeaseFeature>() is not { } feature)
                    return;
                using var lease = feature.Lease();
                Draw(lease.SkCanvas, (float)Bounds.Width, (float)Bounds.Height, _effect, _time);
            }
        }

        /// <summary>
        /// Draws <paramref name="effect"/> at <paramref name="time"/> into a <paramref name="width"/> by
        /// <paramref name="height"/> area at the canvas origin.
        /// </summary>
        internal static void Draw(SKCanvas canvas, float width, float height, DaisyGlassAnimation effect, float time)
        {
            var shaderEffect = GetEffect(effect);
            using var uniforms = new SKRuntimeEffectUniforms(shaderEffect)
            {
                ["size"] = new[] { width, height },
                ["time"] = time
            };
            using var shader = shaderEffect.ToShader(uniforms);
            using var paint = new SKPaint { Shader = shader };
            canvas.DrawRect(SKRect.Create(width, height), paint);
        }
    }
}
