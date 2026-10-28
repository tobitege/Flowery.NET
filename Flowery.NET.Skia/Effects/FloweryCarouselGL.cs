using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;

namespace Flowery.Effects
{
    /// <summary>
    /// The engine of <see cref="CarouselGlEffectHost"/>: the C# port of flowery-carousel-gl.js. Its mask, flip,
    /// transition, effect and text programs are SkSL ports of the GLSL fragment shaders.
    /// </summary>
    internal sealed class FloweryCarouselGL : CarouselGlEngine
    {
        // The overlay key of the native bridge.
        private const string Key = "native-host";

        // DEFAULT_OVERLAY_Z_INDEX and getOverlayZIndex: no counterpart; the canvas fills its host instead of lying over the page.

        internal static double NormalizeTransitionSeconds(double value, double fallback)
        {
            var seconds = value;
            if (double.IsFinite(seconds) && seconds > 0)
            {
                return seconds;
            }
            return fallback;
        }

        internal static string ResolveAssetPath(string path)
        {
            if (path.StartsWith("//", StringComparison.Ordinal)
                || path.StartsWith("http://", StringComparison.Ordinal)
                || path.StartsWith("https://", StringComparison.Ordinal)
                || path.StartsWith("data:", StringComparison.Ordinal))
            {
                return path;
            }
            throw new InvalidOperationException("Image asset must be resolved by the native host: " + path);
        }

        private static readonly IReadOnlyDictionary<string, string> DefaultImages = new Dictionary<string, string>();

        internal static readonly IReadOnlyDictionary<string, MaskSettings> MaskVariants = new Dictionary<string, MaskSettings>
        {
            ["checkerboard"] = new(0, 8),
            ["blinds-h"] = new(1, 10),
            ["blinds-v"] = new(2, 10),
            ["slices-h"] = new(3, 12),
            ["slices-v"] = new(4, 12),
            ["spiral"] = new(5, 8),
            ["matrix"] = new(6, 16),
            ["wormhole"] = new(7, 8),
            ["dissolve"] = new(8, 12),
            ["pixelate"] = new(9, 10)
        };

        internal static readonly IReadOnlyDictionary<string, FlipSettings> FlipVariants = new Dictionary<string, FlipSettings>
        {
            ["flip-plane"] = new(0, 0, 0, 2.2, 0),
            ["flip-v"] = new(0, 0, 0, 2.2, 0),
            ["flip-h"] = new(1, 0, 0, 2.2, 1),
            ["cube-left"] = new(0, -1, 0, 1.8, 0, 180),
            ["cube-right"] = new(0, 1, 0, 1.8, 0, 180)
        };

        internal static readonly IReadOnlyDictionary<string, TransitionSettings> TransitionVariants = new Dictionary<string, TransitionSettings>
        {
            ["fade"] = new(0, [0, 0]),
            ["fade-black"] = new(1, [0, 0]),
            ["fade-white"] = new(2, [0, 0]),
            ["slide-left"] = new(3, [-1, 0]),
            ["slide-right"] = new(3, [1, 0]),
            ["slide-up"] = new(3, [0, -1]),
            ["slide-down"] = new(3, [0, 1]),
            ["push-left"] = new(4, [-1, 0]),
            ["push-right"] = new(4, [1, 0]),
            ["push-up"] = new(4, [0, -1]),
            ["push-down"] = new(4, [0, 1]),
            ["cover-left"] = new(5, [-1, 0]),
            ["cover-right"] = new(5, [1, 0]),
            ["reveal-left"] = new(6, [-1, 0]),
            ["reveal-right"] = new(6, [1, 0]),
            ["wipe-left"] = new(7, [-1, 0]),
            ["wipe-right"] = new(7, [1, 0]),
            ["wipe-up"] = new(7, [0, -1]),
            ["wipe-down"] = new(7, [0, 1]),
            ["zoom-in"] = new(8, [0, 0]),
            ["zoom-out"] = new(9, [0, 0]),
            ["zoom-cross"] = new(10, [0, 0])
        };

        private static readonly string[] TransitionVariantKeys = [.. TransitionVariants.Keys];

        internal static readonly IReadOnlyDictionary<string, EffectSettings> EffectVariants = new Dictionary<string, EffectSettings>
        {
            ["pan-zoom"] = new("pan-zoom"),
            ["zoom-in"] = new("zoom-in"),
            ["zoom-out"] = new("zoom-out"),
            ["pan-left"] = new("pan-left"),
            ["pan-right"] = new("pan-right"),
            ["pan-up"] = new("pan-up"),
            ["pan-down"] = new("pan-down"),
            ["drift"] = new("drift"),
            ["pulse"] = new("pulse"),
            ["breath"] = new("breath"),
            ["throw"] = new("throw")
        };

        internal static IReadOnlyDictionary<string, string?> ResolveImages(string? mode, IReadOnlyDictionary<string, string>? assets)
        {
            var merged = new Dictionary<string, string?>(StringComparer.Ordinal);
            foreach (var (key, value) in DefaultImages) merged[key] = value;
            if (assets is not null) foreach (var (key, value) in assets) merged[key] = value;
            if (mode == "mask")
            {
                return new Dictionary<string, string?> { ["maskA"] = merged.GetValueOrDefault("maskA"), ["maskB"] = merged.GetValueOrDefault("maskB") };
            }
            if (mode == "flip")
            {
                return new Dictionary<string, string?> { ["flipA"] = merged.GetValueOrDefault("flipA"), ["flipB"] = merged.GetValueOrDefault("flipB") };
            }
            if (mode == "transition")
            {
                return new Dictionary<string, string?> { ["transitionA"] = merged.GetValueOrDefault("transitionA"), ["transitionB"] = merged.GetValueOrDefault("transitionB") };
            }
            if (mode == "effect")
            {
                return new Dictionary<string, string?> { ["effect"] = merged.GetValueOrDefault("effect") };
            }
            if (mode == "text")
            {
                return new Dictionary<string, string?> { ["text"] = merged.GetValueOrDefault("text") };
            }
            return merged;
        }

        internal static (string A, string B)? GetPairKeys(string? mode)
        {
            if (mode == "mask")
            {
                return ("maskA", "maskB");
            }
            if (mode == "flip")
            {
                return ("flipA", "flipB");
            }
            if (mode == "transition")
            {
                return ("transitionA", "transitionB");
            }
            return null;
        }

        internal static IReadOnlyDictionary<string, string>? ResolveAssetsForCurrentImage(string? mode, IReadOnlyDictionary<string, string>? assets, State? state)
        {
            var pair = GetPairKeys(mode);
            if (pair is not { } keys || assets is null || state is null)
            {
                return assets;
            }

            var assetA = assets.GetValueOrDefault(keys.A);
            var assetB = assets.GetValueOrDefault(keys.B);
            if (string.IsNullOrEmpty(assetA) || string.IsNullOrEmpty(assetB) || assetA == assetB)
            {
                return assets;
            }

            var currentImage = state.currentImage;
            var shouldSwap = false;
            if (!string.IsNullOrEmpty(currentImage))
            {
                if (currentImage == assetB && currentImage != assetA)
                {
                    shouldSwap = true;
                }
            }
            else if (state.currentSwap)
            {
                shouldSwap = true;
            }

            if (!shouldSwap)
            {
                return assets;
            }

            var swapped = new Dictionary<string, string>(assets);
            swapped[keys.A] = assetB;
            swapped[keys.B] = assetA;
            return swapped;
        }

        internal static MaskSettings GetMaskSettings(string? variant)
        {
            if (!string.IsNullOrEmpty(variant) && MaskVariants.TryGetValue(variant, out var settings))
            {
                return settings;
            }
            return MaskVariants["checkerboard"];
        }

        internal static FlipSettings GetFlipSettings(string? variant)
        {
            if (!string.IsNullOrEmpty(variant) && FlipVariants.TryGetValue(variant, out var settings))
            {
                return settings;
            }
            return FlipVariants["flip-plane"];
        }

        internal static TransitionSettings GetTransitionSettings(string? variant)
        {
            if (variant == "random" && TransitionVariantKeys.Length > 0)
            {
                var choice = TransitionVariantKeys[(int)Math.Floor(Random.Shared.NextDouble() * TransitionVariantKeys.Length)];
                return TransitionVariants[choice];
            }
            if (!string.IsNullOrEmpty(variant) && TransitionVariants.TryGetValue(variant, out var settings))
            {
                return settings;
            }
            return TransitionVariants["fade"];
        }

        internal static EffectSettings GetEffectSettings(string? variant)
        {
            if (!string.IsNullOrEmpty(variant) && EffectVariants.TryGetValue(variant, out var settings))
            {
                return settings;
            }
            return EffectVariants["pan-zoom"];
        }

        internal static EffectParams GetEffectParams(string? type, double timeSec)
        {
            double offsetX = 0;
            double offsetY = 0;
            double scale = 1;
            double rotation = 0;
            double alpha = 1;
            var t = timeSec;

            switch (type)
            {
                case "zoom-in":
                {
                    var pulse = 0.5 - 0.5 * Math.Cos(t * 0.6);
                    scale = 1.0 + 0.18 * pulse;
                    break;
                }
                case "zoom-out":
                {
                    var pulse = 0.5 - 0.5 * Math.Cos(t * 0.6);
                    scale = 1.18 - 0.18 * pulse;
                    break;
                }
                case "pan-left":
                    scale = 1.12;
                    offsetX = -0.12 + 0.03 * Math.Sin(t * 0.5);
                    offsetY = 0.02 * Math.Cos(t * 0.4);
                    break;
                case "pan-right":
                    scale = 1.12;
                    offsetX = 0.12 + 0.03 * Math.Sin(t * 0.5);
                    offsetY = 0.02 * Math.Cos(t * 0.4);
                    break;
                case "pan-up":
                    scale = 1.12;
                    offsetY = -0.12 + 0.03 * Math.Cos(t * 0.5);
                    offsetX = 0.02 * Math.Sin(t * 0.4);
                    break;
                case "pan-down":
                    scale = 1.12;
                    offsetY = 0.12 + 0.03 * Math.Cos(t * 0.5);
                    offsetX = 0.02 * Math.Sin(t * 0.4);
                    break;
                case "drift":
                    scale = 1.08;
                    offsetX = 0.06 * Math.Sin(t * 0.25);
                    offsetY = 0.06 * Math.Cos(t * 0.22);
                    rotation = 0.05 * Math.Sin(t * 0.18);
                    break;
                case "pulse":
                    scale = 1.0 + 0.08 * Math.Sin(t * 1.2);
                    alpha = 0.9 + 0.1 * Math.Sin(t * 1.2);
                    break;
                case "breath":
                    scale = 1.0 + 0.06 * Math.Sin(t * 0.6);
                    alpha = 0.92 + 0.08 * Math.Sin(t * 0.6);
                    break;
                case "throw":
                {
                    var phase = (t * 0.25) % 1;
                    var ease = 0.5 - 0.5 * Math.Cos(phase * Math.PI * 2);
                    scale = 1.1;
                    offsetX = -0.15 + 0.3 * ease;
                    offsetY = 0.12 - 0.24 * ease;
                    rotation = -0.12 + 0.24 * ease;
                    break;
                }
                case "pan-zoom":
                default:
                    scale = 1.12 + 0.06 * Math.Sin(t * 0.35);
                    offsetX = 0.08 * Math.Sin(t * 0.25);
                    offsetY = 0.06 * Math.Cos(t * 0.2);
                    break;
            }

            scale = Math.Max(0.2, scale);
            alpha = Math.Min(1, Math.Max(0, alpha));

            return new EffectParams(offsetX, offsetY, scale, rotation, alpha);
        }

        // Throws at once for an unresolved path, as the JS throws before its Promise.
        private static Task<SKImage> LoadImage(string src)
        {
            var resolved = ResolveAssetPath(src);
            return DecodeImageAsync(resolved);
        }

        // createShader and createProgram: CarouselGlEngine.CreateProgram compiles each SkSL program once per process.

        // createTexture: the decoded image is the texture; the state, as its GL context, releases it in stop. Each frame
        // samples it through a child shader (CarouselGlEngine.CreateTextureShader).
        private static SKImage CreateTexture(State state, SKImage image)
        {
            state.resources.Add(image);
            return image;
        }

        // createQuad: no counterpart; a frame shader covers the canvas, and the flip program interpolates its quad itself.

        // getCanvasDisplaySize, resizeCanvasToDisplaySize and drawFallback: CarouselGlCanvas.

        private static void StartLoop(State state, Action<double> render)
        {
            state._render = render;
            if (state._frame is null)
            {
                state._frame = state.Frame;
            }
            ResumeState(state);
        }

        internal static double ComputeTransitionProgress(double elapsedSec, double transitionSec, double intervalSec)
        {
            var holdSec = intervalSec > 0 ? intervalSec : 0;
            var transition = Math.Max(0.001, transitionSec);
            var cycle = holdSec + transition + holdSec + transition;
            if (cycle <= 0)
            {
                return 0;
            }
            var t = elapsedSec % cycle;
            if (t < holdSec)
            {
                return 0;
            }
            if (t < holdSec + transition)
            {
                return (t - holdSec) / transition;
            }
            if (t < holdSec + transition + holdSec)
            {
                return 1;
            }
            return 1 - (t - (holdSec + transition + holdSec)) / transition;
        }

        internal static double ResolvePixelateSize(double value)
        {
            var size = value;
            return double.IsFinite(size) && size > 0 ? size : 20;
        }

        internal static double ResolveSliceCount(double value)
        {
            var count = value;
            if (!double.IsFinite(count))
            {
                return 0;
            }
            var rounded = Math.Floor(count);
            return rounded >= 2 ? rounded : 0;
        }

        internal static bool ResolveStaggerFlag(object? value)
        {
            return value is true or "true" or 1 or 1d;
        }

        internal static double ResolveStaggerMs(double value)
        {
            var ms = value;
            return double.IsFinite(ms) && ms > 0 ? ms : 0;
        }

        internal static double ResolveDissolveDensity(double value)
        {
            var density = value;
            if (!double.IsFinite(density))
            {
                return 0.5;
            }
            return Math.Min(1, Math.Max(0, density));
        }

        internal static double ResolveFlipAngle(double value)
        {
            var angle = value;
            if (!double.IsFinite(angle))
            {
                return 180;
            }
            return Math.Min(180, Math.Max(30, angle));
        }

        internal static (double minBlocks, double maxBlocks) GetPixelateBlocks(CarouselGlCanvas canvas, double pixelateSize)
        {
            var display = canvas.GetCanvasDisplaySize();
            var displayWidth = Math.Max(1, display.Width);
            var dpr = Math.Max(1, canvas.PixelWidth / displayWidth);
            double minSidePx = Math.Max(1, Math.Min(canvas.PixelWidth, canvas.PixelHeight));
            var pixelSizePx = Math.Max(1, pixelateSize * dpr);
            var minBlocks = Math.Max(2, Math.Floor(minSidePx / pixelSizePx));
            var maxBlocks = Math.Max(minBlocks + 1, Math.Floor(minSidePx / Math.Max(1, pixelSizePx * 0.25)));
            return (minBlocks, maxBlocks);
        }

        internal static double GetPixelateTransitionSeconds(State state, double minBlocks)
        {
            var @base = NormalizeTransitionSeconds(state.baseTransitionSec, state.defaultTransitionSec);
            const double referenceBlocks = 12;
            var scale = Math.Min(5.0, Math.Max(1.6, minBlocks / referenceBlocks));
            return @base * scale;
        }

        internal static double GetSpiralWormholeTransitionSeconds(State state)
        {
            var @base = NormalizeTransitionSeconds(state.baseTransitionSec, state.defaultTransitionSec);
            return @base * 1.25;
        }

        private static void PauseState(State state)
        {
            if (!state.running)
            {
                return;
            }
            state.running = false;
            if (state.raf != 0)
            {
                state.canvas.CancelAnimationFrame(state.raf);
                state.raf = 0;
            }
        }

        private static void ResumeState(State state)
        {
            if (state.running && state.raf != 0)
            {
                return;
            }
            if (!state.alive) return;
            state.running = true;
            if (state._frame is not null)
            {
                state.raf = state.canvas.RequestAnimationFrame(state._frame);
            }
        }

        // The GLSL vertex shaders have no counterpart: a frame shader covers the canvas, and SkSL derives v_texCoord from
        // the pixel position (GL texture coordinates start at the bottom left). Each fragment shader ends with fragColor:
        // GL clamps the output, and the canvas is not premultiplied (premultipliedAlpha: false), so Skia gets it
        // premultiplied. SkSL cannot pass a sampler to a function, so each texture has a sampling helper; GL uploads the
        // images flipped (UNPACK_FLIP_Y_WEBGL) and keeps them unpremultiplied.
        internal const string MaskFragmentShader = """
            uniform shader u_tex1;
            uniform shader u_tex2;
            uniform vec2 u_resolution;
            uniform float u_progress;
            uniform float u_seed;
            uniform float u_tiles;
            uniform float u_pattern;
            uniform vec2 u_effectOffset;
            uniform float u_effectScale;
            uniform float u_effectRotation;
            uniform float u_effectAlpha;
            uniform float u_blockMin;
            uniform float u_blockMax;
            uniform float u_stagger;
            uniform float u_dissolveDensity;
            vec4 texture1(vec2 uv) {
              return unpremul(u_tex1.eval(vec2(uv.x, 1.0 - uv.y)));
            }
            vec4 texture2(vec2 uv) {
              return unpremul(u_tex2.eval(vec2(uv.x, 1.0 - uv.y)));
            }
            half4 fragColor(vec4 color) {
              color = clamp(color, 0.0, 1.0);
              return half4(color.rgb * color.a, color.a);
            }
            float rand(vec2 co) {
              vec2 seeded = co + vec2(u_seed, u_seed * 1.37);
              return fract(sin(dot(seeded, vec2(12.9898, 78.233))) * 43758.5453);
            }
            float patternMask(vec2 uv, float progress) {
              if (u_pattern < 0.5) {
                vec2 grid = floor(uv * u_tiles);
                float checker = mod(grid.x + grid.y, 2.0);
                float edge = checker * 0.5;
                return smoothstep(edge, edge + 0.5, progress);
              }
              if (u_pattern < 1.5) {
                if (progress <= 0.0) {
                  return 0.0;
                }
                float slice = floor(uv.y * u_tiles);
                float delay = rand(vec2(slice, 0.91)) * u_stagger;
                float local = clamp((progress - delay) / (1.0 - delay), 0.0, 1.0);
                float stripe = fract(uv.y * u_tiles);
                float edge0 = stripe - 0.08;
                float edge1 = min(stripe + 0.08, 1.0);
                return smoothstep(edge0, edge1, local);
              }
              if (u_pattern < 2.5) {
                if (progress <= 0.0) {
                  return 0.0;
                }
                float slice = floor(uv.x * u_tiles);
                float delay = rand(vec2(slice, 0.63)) * u_stagger;
                float local = clamp((progress - delay) / (1.0 - delay), 0.0, 1.0);
                float stripe = fract(uv.x * u_tiles);
                float edge0 = stripe - 0.08;
                float edge1 = min(stripe + 0.08, 1.0);
                return smoothstep(edge0, edge1, local);
              }
              if (u_pattern < 3.5) {
                float slice = floor(uv.y * u_tiles);
                float delay = rand(vec2(slice, 0.13)) * u_stagger;
                float local = clamp((progress - delay) / (1.0 - delay), 0.0, 1.0);
                float speed = mix(0.5, 1.7, rand(vec2(slice, 1.21)));
                float eased = pow(local, 1.0 / speed);
                float jitter = mix(-0.25, 0.25, rand(vec2(slice, 2.71)));
                float jitterAmt = jitter * (eased * (1.0 - eased) * 2.0);
                float edge = clamp(eased + jitterAmt, 0.0, 1.0);
                return step(uv.x, edge);
              }
              if (u_pattern < 4.5) {
                float slice = floor(uv.x * u_tiles);
                float delay = rand(vec2(slice, 0.37)) * u_stagger;
                float local = clamp((progress - delay) / (1.0 - delay), 0.0, 1.0);
                float speed = mix(0.5, 1.7, rand(vec2(slice, 1.57)));
                float eased = pow(local, 1.0 / speed);
                float jitter = mix(-0.25, 0.25, rand(vec2(slice, 2.11)));
                float jitterAmt = jitter * (eased * (1.0 - eased) * 2.0);
                float edge = clamp(eased + jitterAmt, 0.0, 1.0);
                return step(uv.y, edge);
              }
              if (u_pattern < 5.5) {
                if (progress <= 0.0) {
                  return 0.0;
                }
                if (progress >= 1.0) {
                  return 1.0;
                }
                vec2 p = uv - 0.5;
                float angle = atan(p.y, p.x);
                float angleNorm = (angle + 3.14159265) / 6.2831853;
                float radius = length(p) * 1.2;
                float spiral = angleNorm + radius;
                float t = progress * 2.0;
                float mask = smoothstep(spiral - 0.15, spiral + 0.15, t);
                mask = mix(mask, 1.0, step(0.999, progress));
                mask = mix(mask, 0.0, 1.0 - step(0.001, progress));
                return mask;
              }
              if (u_pattern < 6.5) {
                float col = floor(uv.x * u_tiles);
                float delay = rand(vec2(col, 0.73)) * u_stagger;
                float local = clamp((progress - delay) / (1.0 - delay), 0.0, 1.0);
                float fall = 1.0 - uv.y;
                return step(fall, local);
              }
              if (u_pattern < 7.5) {
                vec2 p = uv - 0.5;
                float radius = length(p);
                float angle = atan(p.y, p.x);
                float swirl = radius + 0.12 * sin(angle * 6.0 + radius * 12.0);
                float maxSwirl = 0.827;
                float normalized = clamp(swirl / maxSwirl, 0.0, 1.0);
                float feather = 0.1;
                float mask = smoothstep(normalized - feather, normalized + feather, progress);
                mask = mix(mask, 1.0, step(0.999, progress));
                mask = mix(mask, 0.0, 1.0 - step(0.001, progress));
                return mask;
              }
              if (u_pattern < 8.5) {
                float grain = mix(2.0, 8.0, u_dissolveDensity);
                vec2 noiseUv = floor(uv * u_tiles * grain) + vec2(0.123, 0.456);
                float noise = max(0.001, rand(noiseUv));
                return step(noise, progress);
              }
              return progress;
            }
            vec2 applyEffect(vec2 uv) {
              vec2 centered = uv - 0.5;
              float c = cos(u_effectRotation);
              float s = sin(u_effectRotation);
              vec2 rot = vec2(centered.x * c - centered.y * s, centered.x * s + centered.y * c);
              return rot / u_effectScale + 0.5 + u_effectOffset;
            }
            half4 main(vec2 fragCoord) {
              vec2 v_texCoord = vec2(fragCoord.x / u_resolution.x, 1.0 - fragCoord.y / u_resolution.y);
              vec2 sampleUv = v_texCoord;
              float pixelPhase = u_progress * 2.0;
              float pixelT = pixelPhase < 1.0 ? pixelPhase : (2.0 - pixelPhase);
              bool isPixelate = u_pattern > 8.5;
              if (isPixelate) {
                float blocks = mix(u_blockMax, u_blockMin, pixelT);
                vec2 pixelUv = floor(sampleUv * blocks) / blocks;
                sampleUv = mix(sampleUv, pixelUv, pixelT);
              }
              vec2 uv1 = applyEffect(sampleUv);
              vec2 uv2 = applyEffect(sampleUv);
              vec4 c1 = texture1(clamp(uv1, 0.0, 1.0));
              vec4 c2 = texture2(clamp(uv2, 0.0, 1.0));
              c1.rgb *= u_effectAlpha;
              c2.rgb *= u_effectAlpha;
              c1.a *= u_effectAlpha;
              c2.a *= u_effectAlpha;
              if (isPixelate) {
                float swap = step(1.0, pixelPhase);
                return fragColor(mix(c1, c2, swap));
              } else {
                float mask = patternMask(v_texCoord, u_progress);
                return fragColor(mix(c1, c2, mask));
              }
            }
            """;

        // The flip vertex shader runs in C# (ProjectFlipVertex): GL writes gl_Position with w = 1, so it interpolates
        // v_texCoord affinely over each triangle of the strip; the fragment shader finds the triangle of its pixel.
        internal const string FlipFragmentShader = """
            uniform shader u_tex1;
            uniform shader u_tex2;
            uniform vec2 u_resolution;
            uniform vec2 u_position0;
            uniform vec2 u_position1;
            uniform vec2 u_position2;
            uniform vec2 u_position3;
            uniform float u_facing;
            uniform float u_angle;
            uniform float u_flipAxis;
            uniform vec2 u_effectOffset;
            uniform float u_effectScale;
            uniform float u_effectRotation;
            uniform float u_effectAlpha;
            vec4 texture1(vec2 uv) {
              return unpremul(u_tex1.eval(vec2(uv.x, 1.0 - uv.y)));
            }
            vec4 texture2(vec2 uv) {
              return unpremul(u_tex2.eval(vec2(uv.x, 1.0 - uv.y)));
            }
            half4 fragColor(vec4 color) {
              color = clamp(color, 0.0, 1.0);
              return half4(color.rgb * color.a, color.a);
            }
            bool interpolateTexCoord(vec2 position, vec2 a, vec2 b, vec2 c, vec2 ta, vec2 tb, vec2 tc, out vec2 texCoord) {
              texCoord = vec2(0.0);
              float area = (b.x - a.x) * (c.y - a.y) - (c.x - a.x) * (b.y - a.y);
              if (!(abs(area) > 0.000001)) {
                return false;
              }
              float wb = ((position.x - a.x) * (c.y - a.y) - (c.x - a.x) * (position.y - a.y)) / area;
              float wc = ((b.x - a.x) * (position.y - a.y) - (position.x - a.x) * (b.y - a.y)) / area;
              float wa = 1.0 - wb - wc;
              if (!(wa >= 0.0 && wb >= 0.0 && wc >= 0.0)) {
                return false;
              }
              texCoord = ta * wa + tb * wb + tc * wc;
              return true;
            }
            vec2 applyEffect(vec2 uv) {
              vec2 centered = uv - 0.5;
              float c = cos(u_effectRotation);
              float s = sin(u_effectRotation);
              vec2 rot = vec2(centered.x * c - centered.y * s, centered.x * s + centered.y * c);
              return rot / u_effectScale + 0.5 + u_effectOffset;
            }
            half4 main(vec2 fragCoord) {
              vec2 position = vec2(2.0 * fragCoord.x / u_resolution.x - 1.0, 1.0 - 2.0 * fragCoord.y / u_resolution.y);
              vec2 v_texCoord;
              bool covered = interpolateTexCoord(position, u_position2, u_position1, u_position3, vec2(0.0, 1.0), vec2(1.0, 0.0), vec2(1.0, 1.0), v_texCoord);
              if (!covered) {
                covered = interpolateTexCoord(position, u_position0, u_position1, u_position2, vec2(0.0, 0.0), vec2(1.0, 0.0), vec2(0.0, 1.0), v_texCoord);
              }
              if (!covered) {
                return half4(0.0);
              }
              float v_facing = u_facing;
              vec2 uv = v_texCoord;
              if (v_facing < 0.5) {
                if (u_flipAxis < 0.5) {
                  uv.x = 1.0 - uv.x;
                } else {
                  uv.y = 1.0 - uv.y;
                }
              }
              vec2 sampleUv = applyEffect(uv);
              vec4 front = texture1(clamp(sampleUv, 0.0, 1.0));
              vec4 back = texture2(clamp(sampleUv, 0.0, 1.0));
              front.rgb *= u_effectAlpha;
              back.rgb *= u_effectAlpha;
              front.a *= u_effectAlpha;
              back.a *= u_effectAlpha;
              vec4 color = mix(back, front, v_facing);
              float shade = 0.6 + 0.4 * abs(cos(u_angle));
              return fragColor(vec4(color.rgb * shade, color.a));
            }
            """;

        internal const string TransitionFragmentShader = """
            uniform shader u_tex1;
            uniform shader u_tex2;
            uniform vec2 u_resolution;
            uniform float u_progress;
            uniform float u_variant;
            uniform vec2 u_direction;
            uniform vec2 u_effectOffset;
            uniform float u_effectScale;
            uniform float u_effectRotation;
            uniform float u_effectAlpha;
            vec4 texture1(vec2 uv) {
              return unpremul(u_tex1.eval(vec2(uv.x, 1.0 - uv.y)));
            }
            vec4 texture2(vec2 uv) {
              return unpremul(u_tex2.eval(vec2(uv.x, 1.0 - uv.y)));
            }
            half4 fragColor(vec4 color) {
              color = clamp(color, 0.0, 1.0);
              return half4(color.rgb * color.a, color.a);
            }
            vec2 applyEffect(vec2 uv) {
              vec2 centered = uv - 0.5;
              float c = cos(u_effectRotation);
              float s = sin(u_effectRotation);
              vec2 rot = vec2(centered.x * c - centered.y * s, centered.x * s + centered.y * c);
              return rot / u_effectScale + 0.5 + u_effectOffset;
            }
            float inside(vec2 uv) {
              return step(0.0, uv.x) * step(0.0, uv.y) * step(uv.x, 1.0) * step(uv.y, 1.0);
            }
            vec4 sampleTexture1(vec2 uv) {
              vec2 transformed = applyEffect(uv);
              vec4 color = texture1(clamp(transformed, 0.0, 1.0));
              color.rgb *= u_effectAlpha;
              color.a *= u_effectAlpha;
              return color;
            }
            vec4 sampleTexture2(vec2 uv) {
              vec2 transformed = applyEffect(uv);
              vec4 color = texture2(clamp(transformed, 0.0, 1.0));
              color.rgb *= u_effectAlpha;
              color.a *= u_effectAlpha;
              return color;
            }
            half4 main(vec2 fragCoord) {
              vec2 v_texCoord = vec2(fragCoord.x / u_resolution.x, 1.0 - fragCoord.y / u_resolution.y);
              vec2 uv = v_texCoord;
              vec4 c1 = sampleTexture1(uv);
              vec4 c2 = sampleTexture2(uv);
              float p = clamp(u_progress, 0.0, 1.0);
              vec2 dir = u_direction;
              if (u_variant < 0.5) {
                return fragColor(mix(c1, c2, p));
              }
              if (u_variant < 1.5) {
                float t = p * 2.0;
                vec3 mid = vec3(0.0);
                vec3 color = t < 1.0 ? mix(c1.rgb, mid, t) : mix(mid, c2.rgb, t - 1.0);
                return fragColor(vec4(color, 1.0));
              }
              if (u_variant < 2.5) {
                float t = p * 2.0;
                vec3 mid = vec3(1.0);
                vec3 color = t < 1.0 ? mix(c1.rgb, mid, t) : mix(mid, c2.rgb, t - 1.0);
                return fragColor(vec4(color, 1.0));
              }
              if (u_variant < 3.5) {
                vec2 uvOld = uv + dir * p;
                vec2 uvNew = uv + dir * (p - 1.0);
                float maskOld = inside(uvOld);
                float maskNew = inside(uvNew);
                vec4 old = sampleTexture1(uvOld) * maskOld;
                vec4 next = sampleTexture2(uvNew) * maskNew;
                return fragColor(mix(old, next, p));
              }
              if (u_variant < 4.5) {
                vec2 uvOld = uv + dir * p;
                vec2 uvNew = uv + dir * (p - 1.0);
                float maskOld = inside(uvOld);
                float maskNew = inside(uvNew);
                vec4 old = sampleTexture1(uvOld) * maskOld;
                vec4 next = sampleTexture2(uvNew) * maskNew;
                return fragColor(old + next);
              }
              if (u_variant < 5.5) {
                vec2 uvNew = uv + dir * (p - 1.0);
                float maskNew = inside(uvNew);
                vec4 next = sampleTexture2(uvNew) * maskNew;
                return fragColor(mix(c1, next, maskNew));
              }
              if (u_variant < 6.5) {
                vec2 uvOld = uv + dir * p;
                float maskOld = inside(uvOld);
                vec4 old = sampleTexture1(uvOld) * maskOld;
                return fragColor(mix(c2, old, maskOld));
              }
              if (u_variant < 7.5) {
                float edge = 0.0;
                if (abs(dir.x) > 0.5) {
                  edge = dir.x < 0.0 ? 1.0 - uv.x : uv.x;
                } else {
                  edge = dir.y < 0.0 ? 1.0 - uv.y : uv.y;
                }
                float edge0 = p - 0.02;
                float edge1 = min(p + 0.02, 1.0);
                float mask = smoothstep(edge0, edge1, edge);
                return fragColor(mix(c1, c2, mask));
              }
              if (u_variant < 8.5) {
                float scaleNew = mix(0.85, 1.0, p);
                vec2 uvNew = (uv - 0.5) / scaleNew + 0.5;
                vec4 next = sampleTexture2(uvNew);
                return fragColor(mix(c1, next, p));
              }
              if (u_variant < 9.5) {
                float scaleNew = mix(1.15, 1.0, p);
                vec2 uvNew = (uv - 0.5) / scaleNew + 0.5;
                vec4 next = sampleTexture2(uvNew);
                return fragColor(mix(c1, next, p));
              }
              float scaleOld = mix(1.0, 1.12, p);
              float scaleNew = mix(0.88, 1.0, p);
              vec2 uvOld = (uv - 0.5) / scaleOld + 0.5;
              vec2 uvNew = (uv - 0.5) / scaleNew + 0.5;
              vec4 old = sampleTexture1(uvOld);
              vec4 next = sampleTexture2(uvNew);
              return fragColor(mix(old, next, p));
            }
            """;

        internal const string EffectFragmentShader = """
            uniform shader u_image;
            uniform vec2 u_resolution;
            uniform vec2 u_offset;
            uniform float u_scale;
            uniform float u_rotation;
            uniform float u_alpha;
            vec4 textureImage(vec2 uv) {
              return unpremul(u_image.eval(vec2(uv.x, 1.0 - uv.y)));
            }
            half4 fragColor(vec4 color) {
              color = clamp(color, 0.0, 1.0);
              return half4(color.rgb * color.a, color.a);
            }
            half4 main(vec2 fragCoord) {
              vec2 v_texCoord = vec2(fragCoord.x / u_resolution.x, 1.0 - fragCoord.y / u_resolution.y);
              vec2 uv = v_texCoord - 0.5;
              float c = cos(u_rotation);
              float s = sin(u_rotation);
              vec2 rot = vec2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);
              vec2 sampleUv = rot / u_scale + 0.5 + u_offset;
              vec4 color = textureImage(sampleUv);
              return fragColor(vec4(color.rgb, color.a * u_alpha));
            }
            """;

        internal const string TextFillFragmentShader = """
            uniform shader u_image;
            uniform shader u_mask;
            uniform vec2 u_resolution;
            uniform vec2 u_offset;
            uniform float u_scale;
            vec4 textureImage(vec2 uv) {
              return unpremul(u_image.eval(vec2(uv.x, 1.0 - uv.y)));
            }
            vec4 textureMask(vec2 uv) {
              return unpremul(u_mask.eval(vec2(uv.x, 1.0 - uv.y)));
            }
            half4 fragColor(vec4 color) {
              color = clamp(color, 0.0, 1.0);
              return half4(color.rgb * color.a, color.a);
            }
            half4 main(vec2 fragCoord) {
              vec2 v_texCoord = vec2(fragCoord.x / u_resolution.x, 1.0 - fragCoord.y / u_resolution.y);
              vec2 uv = v_texCoord;
              vec2 imgUv = (uv - 0.5) / u_scale + 0.5 + u_offset;
              vec4 img = textureImage(imgUv);
              float mask = textureMask(uv).a;
              return fragColor(vec4(img.rgb, img.a * mask));
            }
            """;

        internal static readonly Lazy<SKRuntimeEffect> MaskProgram = new(() => CreateProgram(MaskFragmentShader));
        internal static readonly Lazy<SKRuntimeEffect> FlipProgram = new(() => CreateProgram(FlipFragmentShader));
        internal static readonly Lazy<SKRuntimeEffect> TransitionProgram = new(() => CreateProgram(TransitionFragmentShader));
        internal static readonly Lazy<SKRuntimeEffect> EffectProgram = new(() => CreateProgram(EffectFragmentShader));
        internal static readonly Lazy<SKRuntimeEffect> TextFillProgram = new(() => CreateProgram(TextFillFragmentShader));

        private static State? InitMask(
            CarouselGlCanvas canvas,
            IReadOnlyDictionary<string, string>? assets,
            string? variant,
            string? effectVariant,
            double transitionSec,
            double sliceCount,
            object? stagger,
            double staggerMs,
            double pixelateSize,
            double dissolveDensity)
        {
            // FloweryNativeBridge.createContext: Skia needs no context of its own, so "WebGL unavailable" cannot occur.

            var program = MaskProgram.Value;

            const double defaultTransitionSec = 2.2;
            var state = new State(canvas)
            {
                intervalSec = 0,
                baseTransitionSec = transitionSec,
                transitionSec = NormalizeTransitionSeconds(transitionSec, defaultTransitionSec),
                sliceCount = ResolveSliceCount(sliceCount),
                stagger = ResolveStaggerFlag(stagger),
                staggerMs = ResolveStaggerMs(staggerMs),
                pixelateSize = ResolvePixelateSize(pixelateSize),
                dissolveDensity = ResolveDissolveDensity(dissolveDensity),
                assetA = null,
                assetB = null,
                currentImage = null,
                currentSwap = false,
                defaultTransitionSec = defaultTransitionSec,
                seed = Random.Shared.NextDouble() * 1000,
                seedPhase = -1,
                wormholeStart = 0,
                wormholeSwap = false
            };

            var images = ResolveImages("mask", assets);
            if (string.IsNullOrEmpty(images["maskA"]) || string.IsNullOrEmpty(images["maskB"]))
            {
                canvas.DrawFallback("Not supported (assets missing)");
                return state;
            }
            state.assetA = images["maskA"];
            state.assetB = images["maskB"];
            state.currentImage = images["maskA"];
            state.currentSwap = false;
            var settings = GetMaskSettings(variant);
            var effectSettings = !string.IsNullOrEmpty(effectVariant) ? GetEffectSettings(effectVariant) : null;
            var loadA = LoadImage(images["maskA"]!);
            var loadB = LoadImage(images["maskB"]!);
            _ = ThenAsync();
            return state;

            // Promise.all([...]).then(...).catch(...)
            async Task ThenAsync()
            {
                try
                {
                    var loaded = await Task.WhenAll(loadA, loadB);
                    if (!state.alive)
                    {
                        ReleaseUnused(state, loadA, loadB);
                        return;
                    }
                    var texA = CreateTexture(state, loaded[0]);
                    var texB = CreateTexture(state, loaded[1]);

                    // gl.clearColor, gl.useProgram and the uniforms set once: every frame shader gets all its uniforms.

                    var start = CarouselGlCanvas.Now;
                    state.nativeStart = start;
                    state.wormholeStart = start;
                    state.wormholeSwap = false;

                    StartLoop(state, (time) =>
                    {
                        canvas.ResizeCanvasToDisplaySize();
                        // gl.clear: the frame shader replaces the whole frame.

                        var elapsedSec = (time - start) * 0.001;
                        var transitionSeconds = state.transitionSec;
                        var tiles = state.sliceCount > 1 ? state.sliceCount : settings.Tiles;
                        var staggerFactor = state.stagger && transitionSeconds > 0
                            ? Math.Min(0.9, Math.Max(0, state.staggerMs / (transitionSeconds * 1000)))
                            : 0;
                        double blockMin = 0;
                        double blockMax = 0;
                        if (settings.Pattern > 4.5 && settings.Pattern < 5.5)
                        {
                            transitionSeconds = GetSpiralWormholeTransitionSeconds(state);
                        }
                        else if (settings.Pattern > 6.5 && settings.Pattern < 7.5)
                        {
                            transitionSeconds = GetSpiralWormholeTransitionSeconds(state);
                        }
                        else if (settings.Pattern > 8.5)
                        {
                            var blocks = GetPixelateBlocks(canvas, ResolvePixelateSize(state.pixelateSize));
                            blockMin = blocks.minBlocks;
                            blockMax = blocks.maxBlocks;
                            transitionSeconds = GetPixelateTransitionSeconds(state, blockMin);
                        }
                        var isSpiral = settings.Pattern > 4.5 && settings.Pattern < 5.5;
                        var isWormhole = settings.Pattern > 6.5 && settings.Pattern < 7.5;
                        var progress = ComputeTransitionProgress(elapsedSec, transitionSeconds, state.intervalSec);
                        var useSwap = false;
                        if (isSpiral || isWormhole)
                        {
                            // Renamed from holdSec, transitionSec and cycleSec: C# locals cannot shadow the outer ones.
                            var swapHoldSec = state.intervalSec > 0 ? state.intervalSec : 0;
                            var swapTransitionSec = Math.Max(0.001, transitionSeconds);
                            var swapCycleSec = swapHoldSec + swapTransitionSec;
                            if (swapCycleSec > 0)
                            {
                                var elapsedMs = time - state.wormholeStart;
                                var elapsedCycleSec = elapsedMs * 0.001;
                                if (elapsedCycleSec >= swapCycleSec)
                                {
                                    var cycles = Math.Floor(elapsedCycleSec / swapCycleSec);
                                    state.wormholeStart += cycles * swapCycleSec * 1000;
                                    if (cycles % 2 == 1)
                                    {
                                        state.wormholeSwap = !state.wormholeSwap;
                                    }
                                }
                                var localSec = Math.Max(0, (time - state.wormholeStart) * 0.001);
                                if (localSec < swapHoldSec)
                                {
                                    progress = 0;
                                }
                                else
                                {
                                    progress = (localSec - swapHoldSec) / swapTransitionSec;
                                }
                                progress = Math.Min(1, Math.Max(0, progress));
                                useSwap = state.wormholeSwap;
                            }
                        }
                        var holdSec = state.intervalSec > 0 ? state.intervalSec : 0;
                        // Renamed from transitionSec, the parameter of initMask.
                        var phaseTransitionSec = Math.Max(0.001, transitionSeconds);
                        var cycleSec = holdSec + phaseTransitionSec + holdSec + phaseTransitionSec;
                        var phase = 0;
                        if (cycleSec > 0)
                        {
                            var t = elapsedSec % cycleSec;
                            if (t < holdSec)
                            {
                                phase = 0;
                            }
                            else if (t < holdSec + phaseTransitionSec)
                            {
                                phase = 1;
                            }
                            else if (t < holdSec + phaseTransitionSec + holdSec)
                            {
                                phase = 2;
                            }
                            else
                            {
                                phase = 3;
                            }
                        }
                        if (phase != state.seedPhase)
                        {
                            state.seedPhase = phase;
                            if (phase == 1 || phase == 3)
                            {
                                state.seed = Random.Shared.NextDouble() * 1000;
                            }
                        }
                        if (!string.IsNullOrEmpty(state.assetA) && !string.IsNullOrEmpty(state.assetB))
                        {
                            var isCurrentB = useSwap ? progress < 0.5 : progress >= 0.5;
                            state.currentSwap = isCurrentB;
                            state.currentImage = isCurrentB ? state.assetB : state.assetA;
                        }
                        EffectParams? effectParams = effectSettings is not null ? GetEffectParams(effectSettings.Type, elapsedSec) : null;
                        var effectOffsetX = effectParams?.OffsetX ?? 0;
                        var effectOffsetY = effectParams?.OffsetY ?? 0;
                        var effectScale = effectParams?.Scale ?? 1;
                        var effectRotation = effectParams?.Rotation ?? 0;
                        var effectAlpha = effectParams?.Alpha ?? 1;

                        // gl.activeTexture and gl.bindTexture: the textures are the children u_tex1 and u_tex2.
                        using var uniforms = new SKRuntimeEffectUniforms(program)
                        {
                            ["u_resolution"] = Vec2(canvas.Resolution.Width, canvas.Resolution.Height),
                            ["u_pattern"] = (float)settings.Pattern,
                            ["u_progress"] = (float)progress,
                            ["u_seed"] = (float)state.seed,
                            ["u_tiles"] = (float)tiles,
                            ["u_effectOffset"] = Vec2(effectOffsetX, effectOffsetY),
                            ["u_effectScale"] = (float)effectScale,
                            ["u_effectRotation"] = (float)effectRotation,
                            ["u_effectAlpha"] = (float)effectAlpha
                        };
                        if (settings.Pattern > 8.5)
                        {
                            uniforms["u_blockMin"] = (float)blockMin;
                            uniforms["u_blockMax"] = (float)blockMax;
                        }
                        else
                        {
                            uniforms["u_blockMin"] = 1.0f;
                            uniforms["u_blockMax"] = 1.0f;
                        }
                        uniforms["u_stagger"] = (float)staggerFactor;
                        uniforms["u_dissolveDensity"] = (float)state.dissolveDensity;
                        DrawArrays(canvas, program, uniforms, ("u_tex1", useSwap ? texB : texA), ("u_tex2", useSwap ? texA : texB));
                        canvas.Frame();
                    });
                }
                catch (Exception err)
                {
                    ReleaseUnused(state, loadA, loadB);
                    Debug.WriteLine("[CarouselGL] mask init failed " + err);
                    canvas.DrawFallback("Not supported (asset load failed)");
                }
            }
        }

        // The flip vertex shader for one corner of the strip, in single precision as on the GPU.
        internal static float[] ProjectFlipVertex(float x, float y, float angle, FlipSettings settings)
        {
            var c = MathF.Cos(angle);
            var s = MathF.Sin(angle);
            float posX = x, posY = y, posZ = 0f;
            posX -= (float)settings.PivotX;
            posY -= (float)settings.PivotY;
            float rotX, rotY, rotZ;
            if (settings.Axis < 0.5)
            {
                rotX = posX * c + posZ * s;
                rotY = posY;
                rotZ = -posX * s + posZ * c;
            }
            else
            {
                rotX = posX;
                rotY = posY * c - posZ * s;
                rotZ = posY * s + posZ * c;
            }
            rotX += (float)settings.PivotX;
            rotY += (float)settings.PivotY;
            var z = rotZ + (float)settings.Depth;
            return [rotX / z, rotY / z];
        }

        private static State? InitFlip(CarouselGlCanvas canvas, IReadOnlyDictionary<string, string>? assets, string? variant, string? effectVariant, double transitionSec, double flipAngle)
        {
            // FloweryNativeBridge.createContext: Skia needs no context of its own, so "WebGL unavailable" cannot occur.

            var program = FlipProgram.Value;

            const double defaultTransitionSec = 2.6;
            var state = new State(canvas)
            {
                intervalSec = 0,
                transitionSec = NormalizeTransitionSeconds(transitionSec, defaultTransitionSec),
                flipAngle = ResolveFlipAngle(flipAngle),
                assetA = null,
                assetB = null,
                currentImage = null,
                currentSwap = false,
                defaultTransitionSec = defaultTransitionSec
            };

            var images = ResolveImages("flip", assets);
            if (string.IsNullOrEmpty(images["flipA"]) || string.IsNullOrEmpty(images["flipB"]))
            {
                canvas.DrawFallback("Not supported (assets missing)");
                return state;
            }
            state.assetA = images["flipA"];
            state.assetB = images["flipB"];
            state.currentImage = images["flipA"];
            state.currentSwap = false;
            var settings = GetFlipSettings(variant);
            var effectSettings = !string.IsNullOrEmpty(effectVariant) ? GetEffectSettings(effectVariant) : null;
            var loadA = LoadImage(images["flipA"]!);
            var loadB = LoadImage(images["flipB"]!);
            _ = ThenAsync();
            return state;

            // Promise.all([...]).then(...).catch(...)
            async Task ThenAsync()
            {
                try
                {
                    var loaded = await Task.WhenAll(loadA, loadB);
                    if (!state.alive)
                    {
                        ReleaseUnused(state, loadA, loadB);
                        return;
                    }
                    var texA = CreateTexture(state, loaded[0]);
                    var texB = CreateTexture(state, loaded[1]);

                    // gl.clearColor, gl.useProgram and the uniforms set once: every frame shader gets all its uniforms.

                    var start = CarouselGlCanvas.Now;
                    state.nativeStart = start;

                    StartLoop(state, (time) =>
                    {
                        canvas.ResizeCanvasToDisplaySize();
                        // gl.clear: the frame shader replaces the whole frame.

                        var elapsedSec = (time - start) * 0.001;
                        var progress = ComputeTransitionProgress(elapsedSec, state.transitionSec, state.intervalSec);
                        if (!string.IsNullOrEmpty(state.assetA) && !string.IsNullOrEmpty(state.assetB))
                        {
                            var isCurrentB = progress >= 0.5;
                            state.currentSwap = isCurrentB;
                            state.currentImage = isCurrentB ? state.assetB : state.assetA;
                        }
                        var minAngle = settings.MinAngle;
                        var angleDegrees = Math.Max(state.flipAngle, minAngle);
                        var angle = progress * (angleDegrees * Math.PI / 180);
                        EffectParams? effectParams = effectSettings is not null ? GetEffectParams(effectSettings.Type, elapsedSec) : null;
                        var effectOffsetX = effectParams?.OffsetX ?? 0;
                        var effectOffsetY = effectParams?.OffsetY ?? 0;
                        var effectScale = effectParams?.Scale ?? 1;
                        var effectRotation = effectParams?.Rotation ?? 0;
                        var effectAlpha = effectParams?.Alpha ?? 1;

                        // The vertex shader for the strip v0 (-1, -1), v1 (1, -1), v2 (-1, 1), v3 (1, 1), and its
                        // v_facing = step(0.0, cos(u_angle)).
                        var glAngle = (float)angle;
                        using var uniforms = new SKRuntimeEffectUniforms(program)
                        {
                            ["u_resolution"] = Vec2(canvas.Resolution.Width, canvas.Resolution.Height),
                            ["u_position0"] = ProjectFlipVertex(-1, -1, glAngle, settings),
                            ["u_position1"] = ProjectFlipVertex(1, -1, glAngle, settings),
                            ["u_position2"] = ProjectFlipVertex(-1, 1, glAngle, settings),
                            ["u_position3"] = ProjectFlipVertex(1, 1, glAngle, settings),
                            ["u_facing"] = MathF.Cos(glAngle) >= 0 ? 1f : 0f,
                            ["u_flipAxis"] = (float)settings.FlipAxis,
                            ["u_angle"] = glAngle,
                            ["u_effectOffset"] = Vec2(effectOffsetX, effectOffsetY),
                            ["u_effectScale"] = (float)effectScale,
                            ["u_effectRotation"] = (float)effectRotation,
                            ["u_effectAlpha"] = (float)effectAlpha
                        };
                        DrawArrays(canvas, program, uniforms, ("u_tex1", texA), ("u_tex2", texB));
                        canvas.Frame();
                    });
                }
                catch (Exception err)
                {
                    ReleaseUnused(state, loadA, loadB);
                    Debug.WriteLine("[CarouselGL] flip init failed " + err);
                    canvas.DrawFallback("Not supported (asset load failed)");
                }
            }
        }

        private static State? InitTransition(CarouselGlCanvas canvas, IReadOnlyDictionary<string, string>? assets, string? variant, string? effectVariant, double transitionSec)
        {
            // FloweryNativeBridge.createContext: Skia needs no context of its own, so "WebGL unavailable" cannot occur.

            var program = TransitionProgram.Value;

            const double defaultTransitionSec = 2.4;
            var state = new State(canvas)
            {
                intervalSec = 0,
                transitionSec = NormalizeTransitionSeconds(transitionSec, defaultTransitionSec),
                assetA = null,
                assetB = null,
                currentImage = null,
                currentSwap = false,
                defaultTransitionSec = defaultTransitionSec
            };

            var images = ResolveImages("transition", assets);
            if (string.IsNullOrEmpty(images["transitionA"]) || string.IsNullOrEmpty(images["transitionB"]))
            {
                canvas.DrawFallback("Not supported (assets missing)");
                return state;
            }
            state.assetA = images["transitionA"];
            state.assetB = images["transitionB"];
            state.currentImage = images["transitionA"];
            state.currentSwap = false;
            var settings = GetTransitionSettings(variant);
            var effectSettings = !string.IsNullOrEmpty(effectVariant) ? GetEffectSettings(effectVariant) : null;
            var loadA = LoadImage(images["transitionA"]!);
            var loadB = LoadImage(images["transitionB"]!);
            _ = ThenAsync();
            return state;

            // Promise.all([...]).then(...).catch(...)
            async Task ThenAsync()
            {
                try
                {
                    var loaded = await Task.WhenAll(loadA, loadB);
                    if (!state.alive)
                    {
                        ReleaseUnused(state, loadA, loadB);
                        return;
                    }
                    var texA = CreateTexture(state, loaded[0]);
                    var texB = CreateTexture(state, loaded[1]);

                    // gl.clearColor, gl.useProgram and the uniforms set once: every frame shader gets all its uniforms.
                    // settings.dir || [0, 0]: every variant has its direction.
                    var dir = settings.Dir;

                    var start = CarouselGlCanvas.Now;
                    state.nativeStart = start;

                    StartLoop(state, (time) =>
                    {
                        canvas.ResizeCanvasToDisplaySize();
                        // gl.clear: the frame shader replaces the whole frame.

                        var elapsedSec = (time - start) * 0.001;
                        var progress = ComputeTransitionProgress(elapsedSec, state.transitionSec, state.intervalSec);
                        if (!string.IsNullOrEmpty(state.assetA) && !string.IsNullOrEmpty(state.assetB))
                        {
                            var isCurrentB = progress >= 0.5;
                            state.currentSwap = isCurrentB;
                            state.currentImage = isCurrentB ? state.assetB : state.assetA;
                        }
                        EffectParams? effectParams = effectSettings is not null ? GetEffectParams(effectSettings.Type, elapsedSec) : null;
                        var effectOffsetX = effectParams?.OffsetX ?? 0;
                        var effectOffsetY = effectParams?.OffsetY ?? 0;
                        var effectScale = effectParams?.Scale ?? 1;
                        var effectRotation = effectParams?.Rotation ?? 0;
                        var effectAlpha = effectParams?.Alpha ?? 1;

                        // gl.activeTexture and gl.bindTexture: the textures are the children u_tex1 and u_tex2.
                        using var uniforms = new SKRuntimeEffectUniforms(program)
                        {
                            ["u_resolution"] = Vec2(canvas.Resolution.Width, canvas.Resolution.Height),
                            ["u_variant"] = (float)settings.Type,
                            ["u_direction"] = Vec2(dir[0], dir[1]),
                            ["u_progress"] = (float)progress,
                            ["u_effectOffset"] = Vec2(effectOffsetX, effectOffsetY),
                            ["u_effectScale"] = (float)effectScale,
                            ["u_effectRotation"] = (float)effectRotation,
                            ["u_effectAlpha"] = (float)effectAlpha
                        };
                        DrawArrays(canvas, program, uniforms, ("u_tex1", texA), ("u_tex2", texB));
                        canvas.Frame();
                    });
                }
                catch (Exception err)
                {
                    ReleaseUnused(state, loadA, loadB);
                    Debug.WriteLine("[CarouselGL] transition init failed " + err);
                    canvas.DrawFallback("Not supported (asset load failed)");
                }
            }
        }

        private static State? InitEffect(CarouselGlCanvas canvas, IReadOnlyDictionary<string, string>? assets, string? variant)
        {
            // FloweryNativeBridge.createContext: Skia needs no context of its own, so "WebGL unavailable" cannot occur.

            var program = EffectProgram.Value;

            var state = new State(canvas)
            {
                intervalSec = 0
            };

            var images = ResolveImages("effect", assets);
            if (string.IsNullOrEmpty(images["effect"]))
            {
                canvas.DrawFallback("Not supported (assets missing)");
                return state;
            }
            var settings = GetEffectSettings(variant);
            var load = LoadImage(images["effect"]!);
            _ = ThenAsync();
            return state;

            // loadImage(...).then(...).catch(...)
            async Task ThenAsync()
            {
                try
                {
                    var img = await load;
                    if (!state.alive)
                    {
                        ReleaseUnused(state, load);
                        return;
                    }
                    var imageTexture = CreateTexture(state, img);

                    // gl.clearColor, gl.useProgram and the uniforms set once: every frame shader gets all its uniforms.

                    var start = CarouselGlCanvas.Now;
                    state.nativeStart = start;

                    StartLoop(state, (time) =>
                    {
                        canvas.ResizeCanvasToDisplaySize();
                        // gl.clear: the frame shader replaces the whole frame.

                        var elapsed = (time - start) * 0.001;
                        var @params = GetEffectParams(settings.Type, elapsed);

                        // gl.activeTexture and gl.bindTexture: the texture is the child u_image.
                        using var uniforms = new SKRuntimeEffectUniforms(program)
                        {
                            ["u_resolution"] = Vec2(canvas.Resolution.Width, canvas.Resolution.Height),
                            ["u_offset"] = Vec2(@params.OffsetX, @params.OffsetY),
                            ["u_scale"] = (float)@params.Scale,
                            ["u_rotation"] = (float)@params.Rotation,
                            ["u_alpha"] = (float)@params.Alpha
                        };
                        DrawArrays(canvas, program, uniforms, ("u_image", imageTexture));
                        canvas.Frame();
                    });
                }
                catch (Exception err)
                {
                    ReleaseUnused(state, load);
                    Debug.WriteLine("[CarouselGL] effect init failed " + err);
                    canvas.DrawFallback("Not supported (asset load failed)");
                }
            }
        }

        private static State? InitTextFill(CarouselGlCanvas canvas, IReadOnlyDictionary<string, string>? assets, string? variant)
        {
            // FloweryNativeBridge.createContext: Skia needs no context of its own, so "WebGL unavailable" cannot occur.

            var program = TextFillProgram.Value;

            // maskCanvas and maskCtx: Avalonia draws the mask text (CreateTextMask); maskTexture is the image it made.
            SKImage? maskTexture = null;

            var state = new State(canvas)
            {
                intervalSec = 0
            };
            // resize: if (resizeCanvasToDisplaySize(canvas, gl)) updateMask();
            state.updateMask = UpdateMask;

            var images = ResolveImages("text", assets);
            if (string.IsNullOrEmpty(images["text"]))
            {
                canvas.DrawFallback("Not supported (assets missing)");
                return state;
            }
            var load = LoadImage(images["text"]!);
            _ = ThenAsync();
            return state;

            void UpdateMask()
            {
                var width = canvas.PixelWidth;
                var height = canvas.PixelHeight;
                if (width <= 0 || height <= 0)
                {
                    return;
                }
                var fontSize = Math.Max(32, Math.Floor(height * 0.45));
                var mask = CreateTextMask(width, height, fontSize);

                // gl.texImage2D: the new mask replaces the old one in the texture.
                var previous = maskTexture;
                maskTexture = CreateTexture(state, mask);
                if (previous is not null)
                {
                    state.resources.Remove(previous);
                    previous.Dispose();
                }
            }

            // loadImage(...).then(...).catch(...)
            async Task ThenAsync()
            {
                try
                {
                    var img = await load;
                    if (!state.alive)
                    {
                        ReleaseUnused(state, load);
                        return;
                    }
                    var imageTexture = CreateTexture(state, img);

                    // gl.clearColor, gl.useProgram and the uniforms set once: every frame shader gets all its uniforms.

                    var start = CarouselGlCanvas.Now;
                    state.nativeStart = start;
                    const double scale = 1.2;
                    UpdateMask();

                    StartLoop(state, (time) =>
                    {
                        if (canvas.ResizeCanvasToDisplaySize()) UpdateMask();
                        // gl.clear: the frame shader replaces the whole frame.

                        var elapsed = (time - start) * 0.001;
                        var offsetX = Math.Sin(elapsed * 0.6) * 0.08;
                        var offsetY = Math.Cos(elapsed * 0.4) * 0.06;

                        // gl.activeTexture and gl.bindTexture: the textures are the children u_image and u_mask.
                        using var uniforms = new SKRuntimeEffectUniforms(program)
                        {
                            ["u_resolution"] = Vec2(canvas.Resolution.Width, canvas.Resolution.Height),
                            ["u_offset"] = Vec2(offsetX, offsetY),
                            ["u_scale"] = (float)scale
                        };
                        DrawArrays(canvas, program, uniforms, ("u_image", imageTexture), ("u_mask", maskTexture!));
                        canvas.Frame();
                    });
                }
                catch (Exception err)
                {
                    ReleaseUnused(state, load);
                    Debug.WriteLine("[CarouselGL] text init failed " + err);
                    canvas.DrawFallback("Not supported (asset load failed)");
                }
            }
        }

        // The mask canvas of initTextFill: "FLOWERY" in bold sans-serif at fontSize device pixels, white and centered on
        // a transparent canvas of width by height pixels. Avalonia draws it, so every target has its fonts (SkiaSharp
        // finds no system fonts in the browser). Canvas text centers its em box on the middle; Avalonia centers the line.
        internal static SKImage CreateTextMask(int width, int height, double fontSize)
        {
            using var bitmap = new RenderTargetBitmap(new PixelSize(width, height), new Vector(96, 96));
            var text = new FormattedText("FLOWERY", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
                new Typeface(FontFamily.Default, FontStyle.Normal, FontWeight.Bold), fontSize, Brushes.White);
            using (var context = bitmap.CreateDrawingContext())
            // A transparent canvas antialiases text in gray; subpixel text would color the edges and make them opaque.
            using (context.PushTextOptions(new TextOptions { TextRenderingMode = TextRenderingMode.Antialias }))
            {
                context.DrawText(text, new Point(width / 2.0 - text.Width / 2, height / 2.0 - text.Height / 2));
            }

            var colorType = bitmap.Format == PixelFormat.Rgba8888 ? SKColorType.Rgba8888 : SKColorType.Bgra8888;
            var alphaType = bitmap.AlphaFormat == AlphaFormat.Unpremul ? SKAlphaType.Unpremul : SKAlphaType.Premul;
            var info = new SKImageInfo(width, height, colorType, alphaType);
            var pixels = new byte[info.BytesSize];
            var handle = GCHandle.Alloc(pixels, GCHandleType.Pinned);
            try
            {
                bitmap.CopyPixels(new PixelRect(0, 0, width, height), handle.AddrOfPinnedObject(), pixels.Length, info.RowBytes);
            }
            finally
            {
                handle.Free();
            }
            return SKImage.FromPixelCopy(info, pixels, info.RowBytes)
                ?? throw new InvalidOperationException("The text mask could not be created.");
        }

        private static float[] Vec2(double x, double y) => [(float)x, (float)y];

        // An image that loaded after stop, or with a failed sibling, never became a texture: the JS leaves it to the
        // garbage collector, the native image is released here.
        private static void ReleaseUnused(State state, params Task<SKImage>[] loads)
        {
            foreach (var load in loads)
            {
                if (load.IsCompletedSuccessfully && !state.resources.Contains(load.Result))
                    load.Result.Dispose();
            }
        }

        // releaseContext: deletes what the context of the state created, its textures.
        private static void ReleaseContext(State state)
        {
            foreach (var resource in state.resources)
                resource.Dispose();
            state.resources.Clear();
        }

        private static void Stop(CarouselGlCanvas canvas)
        {
            if (canvas.State is not State state)
            {
                return;
            }
            state.Stop();
            canvas.State = null;
        }

        protected internal override void Resize(CarouselGlCanvas canvas)
        {
            if (canvas.State is not State state)
            {
                return;
            }
            state.Resize();
        }

        private static void Init(
            CarouselGlCanvas canvas,
            string? mode,
            IReadOnlyDictionary<string, string>? assets,
            string? variant,
            string? effectVariant,
            double transitionSec,
            double sliceCount,
            object? stagger,
            double staggerMs,
            double pixelateSize,
            double dissolveDensity,
            double flipAngle)
        {
            Stop(canvas);

            State? state = null;
            try
            {
                if (mode == "mask")
                {
                    state = InitMask(
                        canvas,
                        assets,
                        variant,
                        effectVariant,
                        transitionSec,
                        sliceCount,
                        stagger,
                        staggerMs,
                        pixelateSize,
                        dissolveDensity);
                }
                else if (mode == "flip")
                {
                    state = InitFlip(canvas, assets, variant, effectVariant, transitionSec, flipAngle);
                }
                else if (mode == "transition")
                {
                    state = InitTransition(canvas, assets, variant, effectVariant, transitionSec);
                }
                else if (mode == "effect")
                {
                    state = InitEffect(canvas, assets, variant);
                }
                else if (mode == "text")
                {
                    state = InitTextFill(canvas, assets, variant);
                }
                else
                {
                    Debug.WriteLine("[CarouselGL] unknown mode " + mode);
                }
            }
            catch (Exception err)
            {
                canvas.Fail(err);
            }

            if (state is not null)
            {
                canvas.State = state;
            }
        }

        // ensureDebugHost, createDebugCanvas, debugStart and debugStop: no counterpart; the debug panel is a page element.

        private readonly Dictionary<string, OverlayEntry> _overlayMap = new();

        private OverlayEntry EnsureOverlayCanvas(string key, CarouselGlCanvas canvas)
        {
            if (_overlayMap.TryGetValue(key, out var entry))
            {
                return entry;
            }

            // The session's canvas takes the place of the page canvas; its fixed position, z-index, rounded corners and
            // transparent background have no counterpart, since it fills its host.
            entry = new OverlayEntry(canvas);
            _overlayMap[key] = entry;
            return entry;
        }

        // updateOverlayRect: no counterpart; the canvas fills its host, so attachOverlay only resizes it.

        protected internal override void AttachOverlay(CarouselGlCanvas canvas, CarouselGlConfiguration configuration) =>
            AttachOverlay(Key, canvas, configuration.Mode, configuration.Assets, configuration.Variant,
                configuration.OverlayVariant, configuration.TransitionSeconds, configuration.SliceCount, configuration.Stagger,
                configuration.StaggerMs, configuration.PixelateSize, configuration.DissolveDensity, configuration.FlipAngle);

        private void AttachOverlay(
            string key,
            CarouselGlCanvas canvas,
            string? mode,
            IReadOnlyDictionary<string, string>? assets,
            string? variant,
            string? effectVariant,
            double transitionSec,
            double sliceCount,
            object? stagger,
            double staggerMs,
            double pixelateSize,
            double dissolveDensity,
            double flipAngle)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(mode))
            {
                return;
            }

            var entry = EnsureOverlayCanvas(key, canvas);
            var assetsKey = assets is not null ? Stringify(assets) : "";
            var variantKey = variant ?? "";
            var effectKey = effectVariant ?? "";
            var transitionValue = transitionSec;
            var nextTransitionSec = double.IsFinite(transitionValue) ? transitionValue : 0;
            var nextSliceCount = ResolveSliceCount(sliceCount);
            var nextStagger = ResolveStaggerFlag(stagger);
            var nextStaggerMs = ResolveStaggerMs(staggerMs);
            var pixelValue = ResolvePixelateSize(pixelateSize);
            var nextDissolveDensity = ResolveDissolveDensity(dissolveDensity);
            var nextFlipAngle = ResolveFlipAngle(flipAngle);
            var previousState = entry.canvas.State as State;
            var resolvedAssets = ResolveAssetsForCurrentImage(mode, assets, previousState);
            if (entry.mode != mode || entry.assetsKey != assetsKey || entry.variant != variantKey || entry.effectVariant != effectKey)
            {
                entry.mode = mode;
                entry.assetsKey = assetsKey;
                entry.variant = variantKey;
                entry.effectVariant = effectKey;
                entry.transitionSec = nextTransitionSec;
                entry.sliceCount = nextSliceCount;
                entry.stagger = nextStagger;
                entry.staggerMs = nextStaggerMs;
                entry.pixelateSize = pixelValue;
                entry.dissolveDensity = nextDissolveDensity;
                entry.flipAngle = nextFlipAngle;
                Init(
                    entry.canvas,
                    mode,
                    resolvedAssets,
                    string.IsNullOrEmpty(variant) ? null : variant,
                    string.IsNullOrEmpty(effectVariant) ? null : effectVariant,
                    nextTransitionSec,
                    nextSliceCount,
                    nextStagger,
                    nextStaggerMs,
                    pixelValue,
                    nextDissolveDensity,
                    nextFlipAngle);
            }
            else if (entry.transitionSec != nextTransitionSec
                || entry.sliceCount != nextSliceCount
                || entry.stagger != nextStagger
                || entry.staggerMs != nextStaggerMs
                || entry.pixelateSize != pixelValue
                || entry.dissolveDensity != nextDissolveDensity
                || entry.flipAngle != nextFlipAngle)
            {
                entry.transitionSec = nextTransitionSec;
                entry.sliceCount = nextSliceCount;
                entry.stagger = nextStagger;
                entry.staggerMs = nextStaggerMs;
                entry.pixelateSize = pixelValue;
                entry.dissolveDensity = nextDissolveDensity;
                entry.flipAngle = nextFlipAngle;
                SetOverlayTransitionDuration(key, nextTransitionSec);
                SetOverlayTransitionParams(key, nextSliceCount, nextStagger, nextStaggerMs, pixelValue, nextDissolveDensity, nextFlipAngle);
            }
            // updateOverlayRect(entry.canvas, left, top, width, height)
            Resize(entry.canvas);
        }

        // JSON.stringify(assets), written without reflection.
        private static string Stringify(IReadOnlyDictionary<string, string> assets)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                foreach (var (name, value) in assets)
                    writer.WriteString(name, value);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private void DetachOverlay(string key)
        {
            if (!_overlayMap.TryGetValue(key, out var entry))
            {
                return;
            }

            Stop(entry.canvas);
            // entry.canvas.remove(): the session owns its canvas.
            _overlayMap.Remove(key);
        }

        protected internal override void DetachAllOverlays()
        {
            foreach (var key in _overlayMap.Keys.ToArray()) DetachOverlay(key);
        }

        protected internal override void SetOverlayTiming(double intervalSec) => SetOverlayTiming(Key, intervalSec);

        private void SetOverlayTiming(string key, double intervalSec)
        {
            if (!_overlayMap.TryGetValue(key, out var entry))
            {
                return;
            }

            var seconds = intervalSec;
            entry.intervalSec = double.IsFinite(seconds) && seconds > 0 ? seconds : 0;
            if (entry.canvas.State is State state)
            {
                state.intervalSec = entry.intervalSec;
            }
        }

        protected internal override void SetOverlayTransitionDuration(double transitionSec) => SetOverlayTransitionDuration(Key, transitionSec);

        private void SetOverlayTransitionDuration(string key, double transitionSec)
        {
            if (!_overlayMap.TryGetValue(key, out var entry))
            {
                return;
            }

            var seconds = transitionSec;
            entry.transitionSec = double.IsFinite(seconds) ? seconds : 0;
            if (entry.canvas.State is State state)
            {
                var fallback = double.IsFinite(state.defaultTransitionSec) ? state.defaultTransitionSec : 0;
                state.baseTransitionSec = entry.transitionSec;
                state.transitionSec = NormalizeTransitionSeconds(entry.transitionSec, fallback);
            }
        }

        private void SetOverlayTransitionParams(
            string key,
            double sliceCount,
            object? stagger,
            double staggerMs,
            double pixelateSize,
            double dissolveDensity,
            double flipAngle)
        {
            if (!_overlayMap.TryGetValue(key, out var entry))
            {
                return;
            }

            var nextSliceCount = ResolveSliceCount(sliceCount);
            var nextStagger = ResolveStaggerFlag(stagger);
            var nextStaggerMs = ResolveStaggerMs(staggerMs);
            var nextPixelateSize = ResolvePixelateSize(pixelateSize);
            var nextDissolveDensity = ResolveDissolveDensity(dissolveDensity);
            var nextFlipAngle = ResolveFlipAngle(flipAngle);

            entry.sliceCount = nextSliceCount;
            entry.stagger = nextStagger;
            entry.staggerMs = nextStaggerMs;
            entry.pixelateSize = nextPixelateSize;
            entry.dissolveDensity = nextDissolveDensity;
            entry.flipAngle = nextFlipAngle;

            if (entry.canvas.State is State state)
            {
                state.sliceCount = nextSliceCount;
                state.stagger = nextStagger;
                state.staggerMs = nextStaggerMs;
                state.pixelateSize = nextPixelateSize;
                state.dissolveDensity = nextDissolveDensity;
                state.flipAngle = nextFlipAngle;
            }
        }

        protected internal override void SetOverlayActive(bool isActive) => SetOverlayActive(Key, isActive);

        private void SetOverlayActive(string key, bool isActive)
        {
            if (!_overlayMap.TryGetValue(key, out var entry))
            {
                return;
            }

            // canvas.style.display: not ported. WinUI hid its overlay canvas while paused; this canvas fills its host,
            // so a hidden canvas would leave the host blank. Paused, it keeps its last frame as a still image.
            if (isActive)
            {
                if (entry.canvas.State is State state)
                {
                    state.Resume();
                }
                Resize(entry.canvas);
            }
            else
            {
                if (entry.canvas.State is State state)
                {
                    state.Pause();
                }
            }
        }

        // The compile assets: 16x16 PNGs instead of the SVG data URIs of the JS, since Skia decodes no SVG.
        private static readonly Lazy<string> RedImage = new(() => CreateSolidImage(SKColors.Red));
        private static readonly Lazy<string> BlueImage = new(() => CreateSolidImage(SKColors.Blue));

        private static string CreateSolidImage(SKColor color)
        {
            using var bitmap = new SKBitmap(16, 16);
            bitmap.Erase(color);
            using var data = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            return "data:image/png;base64," + Convert.ToBase64String(data.ToArray());
        }

        protected internal override async Task<IReadOnlyList<string>> CompileAsync()
        {
            var assets = new Dictionary<string, string> { ["maskA"] = RedImage.Value };
            assets["maskB"] = assets["flipB"] = assets["transitionB"] = BlueImage.Value;
            assets["flipA"] = assets["transitionA"] = assets["effect"] = assets["text"] = assets["maskA"];
            (string mode, string variant)[] definitions =
            [
                .. MaskVariants.Keys.Select(variant => ("mask", variant)),
                .. FlipVariants.Keys.Select(variant => ("flip", variant)),
                .. TransitionVariants.Keys.Select(variant => ("transition", variant)),
                .. EffectVariants.Keys.Select(variant => ("effect", variant)),
                ("text", "text-fill")
            ];
            var result = new List<string>();
            foreach (var definition in definitions)
            {
                var canvas = new CarouselGlCanvas { Width = 64, Height = 64 };
                try
                {
                    Init(canvas, definition.mode, assets, definition.variant, null, 1, 8, true, 50, 20, 0.5, 180);
                    await canvas.WaitFrameAsync();
                    result.Add(definition.mode + ":" + definition.variant);
                }
                finally
                {
                    var state = canvas.State;
                    Stop(canvas);
                    if (state is not null) canvas.Clear();
                }
            }
            return result;
        }

        internal sealed record MaskSettings(double Pattern, double Tiles);

        internal sealed record FlipSettings(double Axis, double PivotX, double PivotY, double Depth, double FlipAxis, double MinAngle = 0);

        internal sealed record TransitionSettings(double Type, double[] Dir);

        internal sealed record EffectSettings(string Type);

        internal readonly record struct EffectParams(double OffsetX, double OffsetY, double Scale, double Rotation, double Alpha);

        private sealed class OverlayEntry
        {
            public OverlayEntry(CarouselGlCanvas canvas)
            {
                this.canvas = canvas;
            }

            public readonly CarouselGlCanvas canvas;
            public string? mode;
            public string? assetsKey;
            public string? variant;
            public string? effectVariant;
            public double intervalSec;
            public double transitionSec;
            public double sliceCount;
            public bool stagger = true;
            public double staggerMs = 50;
            public double pixelateSize = 20;
            public double dissolveDensity = 0.5;
            public double flipAngle = 180;
        }

        // The state of an effect on a canvas, with the field names of the JS object. Fields a mode does not set keep
        // their defaults, as the JS reads them as undefined.
        internal sealed class State : ICarouselGlState
        {
            public State(CarouselGlCanvas canvas)
            {
                this.canvas = canvas;
            }

            public readonly CarouselGlCanvas canvas;
            // gl: no counterpart; the resources its context tracked for releaseContext are the textures.
            public readonly List<IDisposable> resources = [];
            public bool alive = true;
            public bool running;
            public int raf;
            public double intervalSec;
            public double baseTransitionSec;
            public double transitionSec;
            public double sliceCount;
            public bool stagger;
            public double staggerMs;
            public double pixelateSize;
            public double dissolveDensity;
            public double flipAngle;
            public string? assetA;
            public string? assetB;
            public string? currentImage;
            public bool currentSwap;
            public double defaultTransitionSec = double.NaN;
            public double seed;
            public double seedPhase = -1;
            public double wormholeStart;
            public bool wormholeSwap;
            public double? nativeStart;
            public Action<double>? _render;
            public Action<double>? _frame;
            // The text mode's resize also redraws its mask.
            public Action? updateMask;

            public void Pause()
            {
                PauseState(this);
            }

            public void Resume()
            {
                ResumeState(this);
            }

            public void Stop()
            {
                alive = false;
                PauseState(this);
                ReleaseContext(this);
                _frame = null;
                _render = null;
            }

            public void Resize()
            {
                if (canvas.ResizeCanvasToDisplaySize())
                {
                    updateMask?.Invoke();
                }
            }

            // startLoop's frame function.
            public void Frame(double time)
            {
                if (!running)
                {
                    return;
                }
                try { _render!(time); }
                catch (Exception error) { canvas.Fail(error); PauseState(this); return; }
                raf = canvas.RequestAnimationFrame(_frame!);
            }

            Action<double>? ICarouselGlState.Render => _render;

            bool ICarouselGlState.Running => running;

            double? ICarouselGlState.NativeStart => nativeStart;

            double? ICarouselGlState.StartTime => null;
        }
    }
}
