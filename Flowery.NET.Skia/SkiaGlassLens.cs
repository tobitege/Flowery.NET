using SkiaSharp;

namespace Flowery.Skia
{
    /// <summary>
    /// How a SkiaSharp glass bends, magnifies and saturates its blurred backdrop, like a lens with a curved rim. The
    /// default does none of it, and the glass draws its backdrop as frosted glass.
    /// </summary>
    /// <param name="Refraction">How strongly the rim bends the backdrop, from 0 to 1 (<c>DaisyGlass.GlassRefraction</c>).</param>
    /// <param name="Bezel">The width of the curved rim as a share of half the shorter side (<c>DaisyGlass.GlassBezel</c>).</param>
    /// <param name="Zoom">The magnification of the whole glass (<c>DaisyGlass.GlassZoom</c>).</param>
    /// <param name="RimSaturation">The saturation of the backdrop at the edge (<c>DaisyGlass.GlassRimSaturation</c>).</param>
    internal readonly record struct SkiaGlassLens(double Refraction, double Bezel, double Zoom, double RimSaturation)
    {
        /// <summary>
        /// A lens that changes nothing.
        /// </summary>
        public static SkiaGlassLens None => new(0, 0.5, 1, 1);

        /// <summary>
        /// Gets whether the lens changes the backdrop; otherwise the glass draws it as frosted glass, without a shader.
        /// </summary>
        public bool IsActive => Refraction > 0.001 || Math.Abs(Zoom - 1) > 0.001 || Math.Abs(RimSaturation - 1) > 0.01;

        private float BendShare => (float)Math.Clamp(Refraction, 0, 1);

        private float BezelShare => (float)Math.Clamp(Bezel, 0.05, 1);

        /// <summary>
        /// Returns the bezel width in pixels for a glass of the given size in pixels.
        /// </summary>
        public float BezelWidth(SKRect glass) => BezelShare * Math.Min(glass.Width, glass.Height) / 2;

        /// <summary>
        /// Returns how far beyond its edge the glass samples its backdrop, in pixels: the bend plus the split of red and
        /// blue. The backdrop snapshot reaches that much further.
        /// </summary>
        public float Reach(SKRect glass, float split) => IsActive ? BendShare * BezelWidth(glass) + split + 1 : 0;

        /// <summary>
        /// Creates the shader that draws <paramref name="backdrop"/>, the blurred backdrop in pixels of the surface,
        /// through the lens over <paramref name="glass"/>, a rectangle in the same pixels with corners of
        /// <paramref name="radius"/>; <paramref name="split"/> offsets red and blue at the edge, in pixels.
        /// </summary>
        public SKShader CreateShader(SKShader backdrop, SKRect glass, float radius, float split)
        {
            var effect = Effect.Value;
            var bezel = BezelWidth(glass);
            using var uniforms = new SKRuntimeEffectUniforms(effect)
            {
                ["glass"] = new[] { glass.Left, glass.Top, glass.Right, glass.Bottom },
                ["radius"] = radius,
                ["bezel"] = bezel,
                ["bend"] = BendShare * bezel,
                ["zoom"] = (float)Math.Clamp(Zoom, 0.5, 3),
                ["split"] = split,
                ["rimSaturation"] = (float)Math.Clamp(RimSaturation, 0, 3)
            };
            using var children = new SKRuntimeEffectChildren(effect) { ["backdrop"] = backdrop };
            return effect.ToShader(uniforms, children);
        }

        // The rim is a quarter circle in profile: flat inside the bezel, steepest at the edge. A point on the rim shows the
        // backdrop from further out along the normal of the edge, so the edge reads as a lens that gathers what lies
        // around the glass; red and blue go a little further and less far, and the colors grow stronger toward the edge.
        private static readonly Lazy<SKRuntimeEffect> Effect = new(() =>
            SKRuntimeEffect.CreateShader("""
                uniform shader backdrop;
                uniform float4 glass;
                uniform float radius;
                uniform float bezel;
                uniform float bend;
                uniform float zoom;
                uniform float split;
                uniform float rimSaturation;

                // Signed distance to the rounded edge: negative inside the glass.
                float edgeDistance(float2 p, float2 halfSize) {
                    float r = min(radius, min(halfSize.x, halfSize.y));
                    float2 q = abs(p) - halfSize + r;
                    return length(max(q, 0.0)) + min(max(q.x, q.y), 0.0) - r;
                }

                half4 main(float2 coord) {
                    float2 center = (glass.xy + glass.zw) * 0.5;
                    float2 halfSize = (glass.zw - glass.xy) * 0.5;
                    float2 p = coord - center;
                    float t = clamp(1.0 + edgeDistance(p, halfSize) / max(bezel, 1.0), 0.0, 1.0);
                    float slope = 1.0 - sqrt(max(1.0 - t * t, 0.0));
                    float2 normal = float2(
                        edgeDistance(p + float2(0.5, 0.0), halfSize) - edgeDistance(p - float2(0.5, 0.0), halfSize),
                        edgeDistance(p + float2(0.0, 0.5), halfSize) - edgeDistance(p - float2(0.0, 0.5), halfSize));
                    float size = length(normal);
                    normal = size > 0.0001 ? normal / size : float2(0.0);
                    float2 source = center + p / zoom + normal * bend * slope;
                    float2 chroma = normal * split * slope;
                    half4 color = backdrop.eval(source);
                    color.r = backdrop.eval(source + chroma).r;
                    color.b = backdrop.eval(source - chroma).b;
                    half luminance = dot(color.rgb, half3(0.2126, 0.7152, 0.0722));
                    color.rgb = clamp(mix(half3(luminance), color.rgb, half(1.0 + (rimSaturation - 1.0) * slope)), 0.0, color.a);
                    return color;
                }
                """, out var error) ?? throw new InvalidOperationException("The glass lens shader does not compile: " + error));
    }
}
