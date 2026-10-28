using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Flowery.Effects;
using SkiaSharp;

namespace Flowery.NET.Gallery.Effects
{
    /// <summary>
    /// The engine of <c>CarouselGlTransitionHost</c>: the C# port of flowery-carousel-gl-transitions.js. It draws the
    /// gl-transitions catalog, the SkSL ports of its GLSL files (GL/Shaders/*.sksl), in the wrapper of
    /// <see cref="BuildFragmentShader"/>.
    /// </summary>
    internal sealed class FloweryCarouselGLTransitions : CarouselGlEngine
    {
        // The overlay key of the native bridge.
        private const string Key = "native-host";

        // DEFAULT_OVERLAY_Z_INDEX and getOverlayZIndex: no counterpart; the canvas fills its host instead of lying over the page.
        internal const double DefaultTransitionSec = 0.6;
        internal const double TransitionDurationScale = 1.25;
        internal const double CircleTransitionScale = 2.0;

        internal static double GetTransitionDurationScale(string? transitionName)
        {
            if (string.IsNullOrEmpty(transitionName))
            {
                return TransitionDurationScale;
            }

            var key = transitionName.Trim().ToLowerInvariant();
            if (key == "circle")
            {
                return TransitionDurationScale * CircleTransitionScale;
            }

            return TransitionDurationScale;
        }

        internal static double NormalizeTransitionSeconds(double value, double fallback, string? transitionName)
        {
            var seconds = value;
            var scale = GetTransitionDurationScale(transitionName);
            if (double.IsFinite(seconds) && seconds > 0)
            {
                return seconds * scale;
            }
            return fallback * scale;
        }

        internal static string ResolveAssetPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }
            if (path.StartsWith("//", StringComparison.Ordinal)
                || path.StartsWith("http://", StringComparison.Ordinal)
                || path.StartsWith("https://", StringComparison.Ordinal)
                || path.StartsWith("data:", StringComparison.Ordinal))
            {
                return path;
            }
            throw new InvalidOperationException("Image asset must be resolved by the native host: " + path);
        }

        private static readonly string[] DefaultImages =
        [
            "Assets/wallpaper_blue.jpg",
            "Assets/wallpaper_green.jpg",
            "Assets/wallpaper_red.jpg",
            "Assets/wallpaper_yellow.jpg"
        ];

        // The catalog in the order of the JS table. The sources are the flat GL/Shaders/<name>.sksl files; the path is
        // the key of the shader cache, as in the JS, and the cache-busting "?v=" suffixes of the JS paths are dropped.
        internal static readonly TransitionEntry[] GlTransitions = CreateTransitions(
            "angular", "BookFlip", "Bounce", "BowTieHorizontal", "BowTieVertical", "BowTieWithParameter", "burn",
            "ButterflyWaveScrawler", "cannabisleaf", "circle", "CircleCrop", "circleopen", "colorphase",
            "ColourDistance", "coord-from-in", "CrazyParametricFun", "crosshatch", "crosswarp", "CrossZoom", "cube",
            "Directional", "directional-easing", "DirectionalScaled", "directionalwarp", "directionalwipe",
            "displacement", "dissolve", "DoomScreenTransition", "doorway", "Dreamy", "DreamyZoom", "EdgeTransition",
            "fade", "fadecolor", "fadegrayscale", "FilmBurn", "flyeye", "GlitchDisplace", "GlitchMemories",
            "GridFlip", "heart", "hexagonalize", "HorizontalClose", "HorizontalOpen", "InvertedPageCurl",
            "kaleidoscope", "LeftRight", "LinearBlur", "luma", "luminance_melt", "morph", "Mosaic",
            "mosaic_transition", "multiply_blend", "Overexposure", "perlin", "pinwheel", "pixelize",
            "polar_function", "PolkaDotsCurtain", "powerKaleido", "Radial", "randomNoisex", "randomsquares",
            "Rectangle", "RectangleCrop", "ripple", "Rolls", "rotate_scale_fade", "RotateScaleVanish",
            "rotateTransition", "scale-in", "SimpleZoom", "SimpleZoomOut", "Slides", "squareswire", "squeeze",
            "static_wipe", "StaticFade", "StereoViewer", "swap", "Swirl", "tangentMotionBlur", "TopBottom",
            "TVStatic", "undulatingBurnOut", "VerticalClose", "VerticalOpen", "WaterDrop", "wind", "windowblinds",
            "windowslice", "wipeDown", "wipeLeft", "wipeRight", "wipeUp", "x_axis_translation", "ZoomInCircles",
            "ZoomLeftWipe", "ZoomRigthWipe");

        private static TransitionEntry[] CreateTransitions(params string[] names)
        {
            var entries = new TransitionEntry[names.Length];
            for (var i = 0; i < names.Length; i++)
            {
                entries[i] = new TransitionEntry(names[i], "GL/Shaders/" + names[i] + ".sksl");
            }
            return entries;
        }

        private static readonly Dictionary<string, TransitionEntry> TransitionMap = CreateTransitionMap();

        private static Dictionary<string, TransitionEntry> CreateTransitionMap()
        {
            var map = new Dictionary<string, TransitionEntry>(StringComparer.Ordinal);
            foreach (var entry in GlTransitions)
            {
                map[entry.Name.ToLowerInvariant()] = entry;
            }
            return map;
        }

        // shaderCache and imageCache: the JS module of a page holds them, so they are fields of the engine of a session.
        private readonly Dictionary<string, Task<TransitionDefinition>> _shaderCache = new(StringComparer.Ordinal);
        private readonly Dictionary<string, Task<SKImage>> _imageCache = new(StringComparer.Ordinal);

        internal static TransitionEntry? ResolveTransitionEntry(string? name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return GlTransitions.Length > 0 ? GlTransitions[0] : null;
            }

            var key = name.Trim();
            if (key.Length == 0)
            {
                return GlTransitions.Length > 0 ? GlTransitions[0] : null;
            }

            if (key.ToLowerInvariant() == "random")
            {
                return GlTransitions.Length > 0
                    ? GlTransitions[(int)Math.Floor(Random.Shared.NextDouble() * GlTransitions.Length)]
                    : null;
            }

            return TransitionMap.TryGetValue(key.ToLowerInvariant(), out var entry)
                ? entry
                : GlTransitions.Length > 0 ? GlTransitions[0] : null;
        }

        // The JS returns { list }.
        internal static List<string> ResolveImages(IReadOnlyDictionary<string, string>? assets)
        {
            var merged = new Dictionary<string, string>(StringComparer.Ordinal);
            if (assets is not null)
            {
                foreach (var (name, value) in assets)
                {
                    merged[name] = value;
                }
            }
            var list = new List<string>();

            // merged.images: no counterpart; the assets are strings, so they hold no array of images.

            var from = Or(merged, "from", "transitionA");
            var to = Or(merged, "to", "transitionB");
            if (!string.IsNullOrEmpty(from))
            {
                list.Add(from);
            }
            if (!string.IsNullOrEmpty(to) && to != from)
            {
                list.Add(to);
            }

            if (list.Count < 2)
            {
                return [.. DefaultImages];
            }

            return list;
        }

        // merged[first] || merged[second]: an empty string counts as missing, as in the JS.
        private static string? Or(Dictionary<string, string> merged, string first, string second)
        {
            if (merged.TryGetValue(first, out var value) && !string.IsNullOrEmpty(value))
            {
                return value;
            }
            return merged.TryGetValue(second, out value) ? value : null;
        }

        // Throws at once for an unresolved path, as the JS throws before its Promise.
        private Task<SKImage> LoadImage(string src)
        {
            var resolved = ResolveAssetPath(src);
            if (_imageCache.TryGetValue(resolved, out var cached))
            {
                return cached;
            }

            var promise = DecodeImageAsync(resolved);

            _imageCache[resolved] = promise;
            return promise;
        }

        // createShader and createProgram: CarouselGlEngine.CreateProgram compiles each wrapped source once per process.
        private static readonly ConcurrentDictionary<string, Lazy<SKRuntimeEffect>> Programs = new(StringComparer.Ordinal);

        private static SKRuntimeEffect CreateCachedProgram(string fragmentSource) =>
            Programs.GetOrAdd(fragmentSource, static source => new Lazy<SKRuntimeEffect>(() => CreateProgram(source))).Value;

        // createTexture: the decoded image is the texture. The image cache owns it, as the page holds its Image
        // elements; each frame samples it through a child shader (CarouselGlEngine.CreateTextureShader).
        private static SKImage CreateTexture(SKImage image)
        {
            return image;
        }

        // createSolidTexture: one pixel, not premultiplied, as the data of texImage2D. The state, as its GL context,
        // releases it in stop.
        private static SKImage CreateSolidTexture(State state, byte r, byte g, byte b, byte a)
        {
            var info = new SKImageInfo(1, 1, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            var data = new[] { r, g, b, a };
            var texture = SKImage.FromPixelCopy(info, data, 4)
                ?? throw new InvalidOperationException("Skia could not create the solid texture.");
            state.resources.Add(texture);
            return texture;
        }

        // createQuad: no counterpart; a frame shader covers the canvas.

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

        internal static double ComputeTransitionProgress(double elapsedSec, double transitionSec, double intervalSec)
        {
            var holdSec = intervalSec > 0 ? intervalSec : 0;
            var transition = Math.Max(0.001, transitionSec);
            var cycle = holdSec + transition + holdSec;
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
            return 1;
        }

        internal static double GetTransitionCycleSeconds(double transitionSec, double intervalSec)
        {
            var holdSec = intervalSec > 0 ? intervalSec : 0;
            var transition = Math.Max(0.001, transitionSec);
            return holdSec + transition + holdSec;
        }

        // The regular expressions of the JS; ECMAScript keeps \d and \w to ASCII, as in JS.
        private static readonly Regex NumberPattern =
            new(@"-?\d*\.?\d+(?:e[-+]?\d+)?", RegexOptions.ECMAScript | RegexOptions.IgnoreCase);

        private static readonly Regex ConstructorPattern = new(@"^[a-zA-Z_]\w*\s*\((.*)\)$", RegexOptions.ECMAScript);
        private static readonly Regex TruePattern = new("true", RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
        private static readonly Regex FalsePattern = new("false", RegexOptions.ECMAScript | RegexOptions.IgnoreCase);
        private static readonly Regex LineSplitPattern = new(@"\r?\n", RegexOptions.ECMAScript);
        private static readonly Regex UniformPattern = new(@"^uniform\s+(\w+)\s+([^;]+);", RegexOptions.ECMAScript);
        private static readonly Regex BlockCommentPattern = new(@"\/\*.*?\*\/", RegexOptions.ECMAScript);
        private static readonly Regex WhitespacePattern = new(@"\s+", RegexOptions.ECMAScript);
        private static readonly Regex ArraySuffixPattern = new(@"\[.*\]$", RegexOptions.ECMAScript);

        internal static string? ExtractDefaultExpression(string line)
        {
            string? comment = null;
            var inlineIndex = line.IndexOf("//", StringComparison.Ordinal);
            var blockIndex = line.IndexOf("/*", StringComparison.Ordinal);

            if (inlineIndex >= 0)
            {
                comment = line[(inlineIndex + 2)..];
            }
            else if (blockIndex >= 0)
            {
                var end = line.IndexOf("*/", blockIndex + 2, StringComparison.Ordinal);
                comment = end >= 0 ? line[(blockIndex + 2)..end] : line[(blockIndex + 2)..];
            }

            if (string.IsNullOrEmpty(comment))
            {
                return null;
            }

            var eqIndex = comment.IndexOf('=');
            if (eqIndex < 0)
            {
                return null;
            }

            var expr = comment[(eqIndex + 1)..].Trim();
            var extraIndex = expr.IndexOf("//", StringComparison.Ordinal);
            if (extraIndex >= 0)
            {
                expr = expr[..extraIndex].Trim();
            }
            if (expr.EndsWith(';'))
            {
                expr = expr[..^1].Trim();
            }
            return expr.Length > 0 ? expr : null;
        }

        internal static List<double> ExtractNumbers(string text)
        {
            var matches = NumberPattern.Matches(text);
            var numbers = new List<double>();
            for (var i = 0; i < matches.Count; i++)
            {
                // Number(text): every match is a decimal literal.
                if (double.TryParse(matches[i].Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
                    && double.IsFinite(value))
                {
                    numbers.Add(value);
                }
            }
            return numbers;
        }

        internal static string ExtractConstructorArgs(string text)
        {
            var trimmed = text.Trim();
            var match = ConstructorPattern.Match(trimmed);
            return match.Success ? match.Groups[1].Value : trimmed;
        }

        /// <summary>
        /// Returns the default as the JS value: a <see cref="double"/> for float and int, a <see cref="bool"/> for
        /// bool and a <see cref="double"/> array for the vector types; null when there is none.
        /// </summary>
        internal static object? ParseDefaultValue(string type, string? expr)
        {
            if (string.IsNullOrEmpty(expr))
            {
                return null;
            }

            var cleaned = expr.Trim();
            if (cleaned.Length == 0)
            {
                return null;
            }

            if (type == "float")
            {
                var numbers = ExtractNumbers(cleaned);
                return numbers.Count > 0 ? numbers[0] : null;
            }

            if (type == "int")
            {
                var numbers = ExtractNumbers(cleaned);
                if (numbers.Count == 0)
                {
                    // SkSL has no bool uniforms: the GLSL bool uniforms are declared int and keep their true or false
                    // default, which reads as 1 or 0 (GL binds a bool with uniform1i(value ? 1 : 0)).
                    return ParseDefaultValue("bool", cleaned) is bool flag ? (flag ? 1.0 : 0.0) : null;
                }
                return Math.Truncate(numbers[0]);
            }

            if (type == "bool")
            {
                if (TruePattern.IsMatch(cleaned))
                {
                    return true;
                }
                if (FalsePattern.IsMatch(cleaned))
                {
                    return false;
                }
                var numbers = ExtractNumbers(cleaned);
                if (numbers.Count > 0)
                {
                    return numbers[0] != 0;
                }
                return null;
            }

            if (type is "vec2" or "vec3" or "vec4" or "ivec2" or "ivec3" or "ivec4")
            {
                var args = ExtractConstructorArgs(cleaned);
                var numbers = ExtractNumbers(args);
                if (numbers.Count == 0)
                {
                    return null;
                }
                var expected = type[^1] - '0';
                var values = new double[expected];
                if (numbers.Count == 1)
                {
                    for (var i = 0; i < expected; i++)
                    {
                        values[i] = numbers[0];
                    }
                }
                else
                {
                    for (var i = 0; i < expected; i++)
                    {
                        values[i] = i < numbers.Count ? numbers[i] : numbers[^1];
                    }
                }
                return values;
            }

            return null;
        }

        internal static ParsedUniforms ParseUniforms(string source)
        {
            var uniforms = new List<UniformDefinition>();
            var samplers = new List<string>();
            var lines = LineSplitPattern.Split(source);

            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].Trim();
                if (!line.StartsWith("uniform ", StringComparison.Ordinal))
                {
                    continue;
                }

                var match = UniformPattern.Match(line);
                if (!match.Success)
                {
                    continue;
                }

                var type = match.Groups[1].Value;
                var namesPart = match.Groups[2].Value;
                namesPart = BlockCommentPattern.Replace(namesPart, "");
                namesPart = namesPart.Split("//")[0];

                var names = new List<string>();
                foreach (var entry in namesPart.Split(','))
                {
                    var trimmed = entry.Trim();
                    if (trimmed.Length == 0)
                    {
                        continue;
                    }
                    var name = ArraySuffixPattern.Replace(WhitespacePattern.Split(trimmed)[0], "");
                    if (name.Length > 0)
                    {
                        names.Add(name);
                    }
                }

                if (names.Count == 0)
                {
                    continue;
                }

                // A GLSL sampler2D is a child shader in SkSL, declared "uniform shader <name>;".
                if (type == "shader")
                {
                    samplers.AddRange(names);
                    continue;
                }

                var expr = ExtractDefaultExpression(line);
                if (expr is null)
                {
                    continue;
                }

                var value = ParseDefaultValue(type, expr);
                if (value is null)
                {
                    continue;
                }

                foreach (var name in names)
                {
                    if (name is "progress" or "ratio")
                    {
                        continue;
                    }
                    uniforms.Add(new UniformDefinition(name, type, value));
                }
            }

            return new ParsedUniforms(uniforms, samplers);
        }

        // The SkSL wrapper of a transition. Where it differs from the GLSL one, SkSL needs it:
        // - The children from and to are image shaders in normalized coordinates from the top left. GL uploads the
        //   images flipped (UNPACK_FLIP_Y_WEBGL) and keeps them unpremultiplied, so texture2D(t, uv) is
        //   unpremul(t.eval(vec2(uv.x, 1.0 - uv.y))).
        // - main gets local coordinates; v_texCoord (GL, from the bottom left) follows from u_resolution.
        // - The canvas is not premultiplied (premultipliedAlpha: false) and GL clamps the output; Skia gets it
        //   clamped and premultiplied.
        // - SkSL has no precision statement.
        internal static string BuildFragmentShader(string source)
        {
            return string.Join("\n",
                "uniform shader from;",
                "uniform shader to;",
                "uniform float progress;",
                "uniform float ratio;",
                "uniform float2 u_resolution;",
                "vec4 getFromColor(vec2 uv) {",
                "  return unpremul(from.eval(vec2(uv.x, 1.0 - uv.y)));",
                "}",
                "vec4 getToColor(vec2 uv) {",
                "  return unpremul(to.eval(vec2(uv.x, 1.0 - uv.y)));",
                "}",
                source,
                "half4 main(float2 p) {",
                "  vec2 uv = vec2(p.x / u_resolution.x, 1.0 - p.y / u_resolution.y);",
                "  float clampedProgress = clamp(progress, 0.0, 1.0);",
                "  vec4 fragColor;",
                "  if (clampedProgress <= 0.0001) {",
                "    fragColor = getFromColor(uv);",
                "  } else if (clampedProgress >= 0.9999) {",
                "    fragColor = getToColor(uv);",
                "  } else {",
                "    fragColor = transition(uv);",
                "  }",
                "  fragColor = clamp(fragColor, 0.0, 1.0);",
                "  return half4(fragColor.rgb * fragColor.a, fragColor.a);",
                "}");
        }

        private Task<TransitionDefinition> LoadTransitionDefinition(TransitionEntry? entry)
        {
            if (entry is null)
            {
                return Task.FromException<TransitionDefinition>(new InvalidOperationException("Transition not found"));
            }

            if (_shaderCache.TryGetValue(entry.Path, out var cached))
            {
                return cached;
            }

            // Promise.resolve(window.FloweryNativeShaders[entry.name]).then(...)
            Task<TransitionDefinition> promise;
            try
            {
                if (!NativeShaders.TryGetValue(entry.Name, out var source))
                {
                    throw new InvalidOperationException("Embedded shader missing: " + entry.Name);
                }
                var parsed = ParseUniforms(source);
                promise = Task.FromResult(new TransitionDefinition(source, parsed.Uniforms, parsed.Samplers));
            }
            catch (Exception error)
            {
                promise = Task.FromException<TransitionDefinition>(error);
            }

            _shaderCache[entry.Path] = promise;
            return promise;
        }

        // GL keeps the uniform values in the program; SkSL builds the uniforms of every frame, so each frame gets the
        // defaults. Contains is the null check of getUniformLocation. GL starts uniforms at 0, while a new
        // SKRuntimeEffectUniforms is uninitialized: every uniform of the catalog has a default.
        internal static void ApplyUniformDefaults(SKRuntimeEffectUniforms target, IReadOnlyList<UniformDefinition> uniforms)
        {
            for (var i = 0; i < uniforms.Count; i++)
            {
                var def = uniforms[i];
                if (def.Name is "progress" or "ratio")
                {
                    continue;
                }

                if (!target.Contains(def.Name))
                {
                    continue;
                }

                var value = def.Value;
                switch (def.Type)
                {
                    case "float":
                        target[def.Name] = (float)(double)value;
                        break;
                    case "int":
                        target[def.Name] = (int)(double)value;
                        break;
                    case "bool":
                        target[def.Name] = (bool)value ? 1 : 0;
                        break;
                    case "vec2":
                    case "vec3":
                    case "vec4":
                        target[def.Name] = ToFloats((double[])value);
                        break;
                    case "ivec2":
                    case "ivec3":
                    case "ivec4":
                        target[def.Name] = ToInts((double[])value);
                        break;
                    default:
                        break;
                }
            }
        }

        private static float[] ToFloats(double[] values)
        {
            var result = new float[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                result[i] = (float)values[i];
            }
            return result;
        }

        private static int[] ToInts(double[] values)
        {
            var result = new int[values.Length];
            for (var i = 0; i < values.Length; i++)
            {
                result[i] = (int)values[i];
            }
            return result;
        }

        // SkSL has no texture units: each extra sampler is a child that every frame binds to the texture (DrawArrays).
        // The children of the program are the null check of getUniformLocation.
        internal static List<(string Name, SKImage Image)> SetupExtraSamplers(SKRuntimeEffect program, IReadOnlyList<string> samplers, SKImage texture)
        {
            var bound = new List<(string Name, SKImage Image)>();
            for (var i = 0; i < samplers.Count; i++)
            {
                var name = samplers[i];
                if (string.IsNullOrEmpty(name) || name == "from" || name == "to")
                {
                    continue;
                }

                if (!program.Children.Contains(name))
                {
                    continue;
                }

                bound.Add((name, texture));
            }
            return bound;
        }

        private static void ApplyTransitionDefinition(State state, TransitionDefinition definition)
        {
            var fs = BuildFragmentShader(definition.Source);
            var program = CreateCachedProgram(fs);
            // createQuad, the from and to locations and useProgram: the frame shader binds its children by name.
            var uProgress = program.Uniforms.Contains("progress");
            var uRatio = program.Uniforms.Contains("ratio");

            // applyUniformDefaults: the render applies definition.uniforms to the uniforms of each frame.
            state.extraTexture ??= CreateSolidTexture(state, 128, 128, 128, 255);
            var extraSamplers = SetupExtraSamplers(program, definition.Samplers, state.extraTexture);

            state.program = program;
            state.uProgress = uProgress;
            state.uRatio = uRatio;
            state.extraSamplers = extraSamplers;
            state.definition = definition;

            // gl.deleteProgram(previousProgram): the programs stay compiled for the process.
        }

        // In JS the new program arrives after the configure request that asked for it has drawn its frame. A paused
        // loop draws no next frame and would keep showing the old program and clock, so it draws the change once.
        private static void DrawIfPaused(State state)
        {
            if (!state.running && state._render is { } render)
            {
                render(CarouselGlCanvas.Now);
            }
        }

        private static void SetCycleStart(State state, int imageIndex, double nowMs)
        {
            var cycleSeconds = GetTransitionCycleSeconds(state.transitionSec, state.intervalSec);
            var offsetMs = double.IsFinite(cycleSeconds) && cycleSeconds > 0
                ? imageIndex * cycleSeconds * 1000
                : 0;
            state.startTime = nowMs - offsetMs;
            state.lastCycleIndex = -1;
        }

        private State? InitTransition(CarouselGlCanvas canvas, IReadOnlyDictionary<string, string>? assets, string? transitionName, double transitionSec, double intervalSec, int initialIndex)
        {
            // FloweryNativeBridge.createContext: Skia needs no context of its own, so "WebGL unavailable" cannot occur.

            var assetsKey = assets is not null ? Stringify(assets) : "";
            // vsSource: no counterpart; a frame shader covers the canvas.

            var state = new State(canvas)
            {
                intervalSec = double.IsFinite(intervalSec) ? intervalSec : 0,
                baseTransitionSec = double.IsFinite(transitionSec) ? transitionSec : 0,
                transitionSec = NormalizeTransitionSeconds(transitionSec, DefaultTransitionSec, transitionName),
                defaultTransitionSec = DefaultTransitionSec,
                assetsKey = assetsKey
            };

            var images = ResolveImages(assets);
            if (images.Count < 2)
            {
                canvas.DrawFallback("Not supported (assets missing)");
                return state;
            }

            var entry = ResolveTransitionEntry(transitionName);
            if (entry is null)
            {
                canvas.DrawFallback("Not supported (transition missing)");
                return state;
            }

            state.transitionName = entry.Name;
            state.transitionEntry = entry;
            state.transitionSec = NormalizeTransitionSeconds(state.baseTransitionSec, DefaultTransitionSec, entry.Name);

            state.updateTransition = UpdateTransition;

            var imagePromises = images.Select(LoadImage).ToArray();
            var definitionPromise = LoadTransitionDefinition(entry);
            _ = ThenAsync();
            return state;

            void UpdateTransition(string? nextName, double nextTransitionSec, double nextIntervalSec)
            {
                var nextEntry = ResolveTransitionEntry(nextName);
                if (nextEntry is null)
                {
                    return;
                }

                var previousStartTime = state.startTime;
                var previousFromTexture = state.fromTexture;
                var previousToTexture = state.toTexture;
                var previousImageIndex = state.imageIndex;
                var previousIntervalSec = state.intervalSec;
                var previousTransitionSec = state.transitionSec;
                var previousTransitionName = state.transitionName;
                var transitionUpdateTime = CarouselGlCanvas.Now;

                var nextInterval = nextIntervalSec;
                state.intervalSec = double.IsFinite(nextInterval) ? nextInterval : 0;

                var nextTransitionValue = nextTransitionSec;
                state.baseTransitionSec = double.IsFinite(nextTransitionValue) ? nextTransitionValue : 0;
                var fallback = double.IsFinite(state.defaultTransitionSec) ? state.defaultTransitionSec : DefaultTransitionSec;
                state.transitionEntry = nextEntry;
                state.transitionName = nextEntry.Name;
                state.transitionSec = NormalizeTransitionSeconds(state.baseTransitionSec, fallback, nextEntry.Name);

                var shouldPreserveCycle = previousStartTime is not null
                    && previousFromTexture is not null
                    && previousToTexture is not null
                    && previousIntervalSec == state.intervalSec
                    && previousTransitionSec == state.transitionSec
                    && (previousTransitionName ?? "").Trim().ToLowerInvariant() == nextEntry.Name.Trim().ToLowerInvariant();

                var requestId = ++state.transitionRequestId;
                _ = UpdateThenAsync();

                int ResolveCurrentIndex(int count)
                {
                    var baseIndex = previousImageIndex;
                    if (count <= 0 || previousStartTime is not { } start)
                    {
                        return baseIndex;
                    }

                    var elapsedSec = Math.Max(0, (transitionUpdateTime - start) * 0.001);
                    var cycleSeconds = GetTransitionCycleSeconds(previousTransitionSec, previousIntervalSec);
                    if (cycleSeconds <= 0)
                    {
                        return baseIndex;
                    }

                    var cycleIndex = (int)Math.Floor(elapsedSec / cycleSeconds);
                    var fromIndex = ((cycleIndex % count) + count) % count;
                    var toIndex = (fromIndex + 1) % count;
                    var progress = ComputeTransitionProgress(elapsedSec, previousTransitionSec, previousIntervalSec);
                    baseIndex = progress >= 0.5 ? toIndex : fromIndex;
                    return baseIndex;
                }

                // loadTransitionDefinition(nextEntry).then(...).catch(...)
                async Task UpdateThenAsync()
                {
                    try
                    {
                        var definition = await LoadTransitionDefinition(nextEntry);
                        if (!state.alive || state.transitionRequestId != requestId)
                        {
                            return;
                        }

                        try
                        {
                            ApplyTransitionDefinition(state, definition);
                        }
                        catch (Exception err)
                        {
                            canvas.Fail(err);
                            return;
                        }

                        var imageCount = state.textures.Count;
                        if (imageCount > 0 && shouldPreserveCycle)
                        {
                            state.imageIndex = ((previousImageIndex % imageCount) + imageCount) % imageCount;
                            state.fromTexture = previousFromTexture;
                            state.toTexture = previousToTexture;
                            state.startTime = previousStartTime;
                            DrawIfPaused(state);
                            return;
                        }

                        if (imageCount > 0)
                        {
                            var baseIndex = ResolveCurrentIndex(imageCount);
                            var normalizedIndex = ((baseIndex % imageCount) + imageCount) % imageCount;
                            state.imageIndex = normalizedIndex;
                            state.fromTexture = state.textures[normalizedIndex];
                            state.toTexture = state.textures[(normalizedIndex + 1) % imageCount];
                            SetCycleStart(state, normalizedIndex, transitionUpdateTime);
                        }
                        else
                        {
                            SetCycleStart(state, 0, CarouselGlCanvas.Now);
                        }
                        DrawIfPaused(state);
                    }
                    catch (Exception err)
                    {
                        canvas.Fail(err);
                    }
                }
            }

            // Promise.all([loadTransitionDefinition(entry)].concat(imagePromises)).then(...).catch(...)
            async Task ThenAsync()
            {
                try
                {
                    await Task.WhenAll(new Task[] { definitionPromise }.Concat(imagePromises));
                    if (!state.alive)
                    {
                        return;
                    }

                    var definition = definitionPromise.Result;
                    var loadedImages = imagePromises.Select(promise => promise.Result);

                    // gl.clearColor: the frame shader replaces the whole frame.
                    state.textures = loadedImages.Select(CreateTexture).ToList();

                    try
                    {
                        ApplyTransitionDefinition(state, definition);
                    }
                    catch (Exception err)
                    {
                        canvas.Fail(err);
                        canvas.DrawFallback("Not supported (shader load failed)");
                        return;
                    }

                    var imageCount = state.textures.Count;
                    var requestedIndex = initialIndex;
                    var normalizedIndex = imageCount > 0
                        ? ((requestedIndex % imageCount) + imageCount) % imageCount
                        : 0;
                    state.imageIndex = normalizedIndex;
                    state.fromTexture = imageCount > 0 ? state.textures[normalizedIndex] : null;
                    state.toTexture = imageCount > 0
                        ? state.textures[(normalizedIndex + 1) % imageCount]
                        : null;
                    SetCycleStart(state, normalizedIndex, CarouselGlCanvas.Now);

                    StartLoop(state, (time) =>
                    {
                        // !state.bindQuad: the definition, which is set with the program.
                        if (state.program is null || state.definition is null || state.textures.Count == 0)
                        {
                            return;
                        }

                        canvas.ResizeCanvasToDisplaySize();
                        // gl.clear, gl.useProgram and bindQuad: the frame shader replaces the whole frame.

                        if (state.startTime is null)
                        {
                            state.startTime = time;
                        }

                        var elapsed = (time - state.startTime.Value) * 0.001;
                        var progress = ComputeTransitionProgress(elapsed, state.transitionSec, state.intervalSec);
                        using var uniforms = new SKRuntimeEffectUniforms(state.program)
                        {
                            ["u_resolution"] = new[] { (float)canvas.Resolution.Width, (float)canvas.Resolution.Height }
                        };
                        ApplyUniformDefaults(uniforms, state.definition.Uniforms);
                        if (state.uProgress)
                        {
                            uniforms["progress"] = (float)progress;
                        }
                        if (state.uRatio)
                        {
                            var ratio = canvas.PixelHeight > 0 ? canvas.PixelWidth / (double)canvas.PixelHeight : 1;
                            uniforms["ratio"] = (float)ratio;
                        }

                        var textures = state.textures;
                        var fromTexture = state.fromTexture ?? textures[0];
                        var toTexture = state.toTexture ?? (textures.Count > 1 ? textures[1] : fromTexture);
                        var fromIndex = state.imageIndex;
                        var toIndex = textures.Count > 0 ? (fromIndex + 1) % textures.Count : 0;

                        if (textures.Count > 1)
                        {
                            var cycleSeconds = GetTransitionCycleSeconds(state.transitionSec, state.intervalSec);
                            var cycleIndex = cycleSeconds > 0 ? Math.Floor(elapsed / cycleSeconds) : 0;
                            if (cycleIndex != state.lastCycleIndex)
                            {
                                state.lastCycleIndex = cycleIndex;
                                fromIndex = (int)(((cycleIndex % textures.Count) + textures.Count) % textures.Count);
                                toIndex = (fromIndex + 1) % textures.Count;
                                fromTexture = textures[fromIndex];
                                toTexture = textures[toIndex];
                                state.imageIndex = fromIndex;
                                state.fromTexture = fromTexture;
                                state.toTexture = toTexture;
                            }
                        }

                        if (progress <= 0.0001)
                        {
                            state.imageIndex = fromIndex;
                            state.fromTexture = fromTexture;
                            state.toTexture = toTexture;
                        }
                        else if (progress >= 0.9999)
                        {
                            state.imageIndex = toIndex;
                            state.fromTexture = fromTexture;
                            state.toTexture = toTexture;
                        }

                        // gl.activeTexture and gl.bindTexture: the textures are the children from and to, and the extra
                        // samplers get the solid texture.
                        DrawArrays(canvas, state.program, uniforms, [("from", fromTexture), ("to", toTexture), .. state.extraSamplers]);
                        canvas.Frame();
                    });
                }
                catch (Exception err)
                {
                    canvas.Fail(err);
                    canvas.DrawFallback("Not supported (shader load failed)");
                }
            }
        }

        // releaseContext: deletes what the context of the state created, its solid texture.
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

        protected override void Resize(CarouselGlCanvas canvas)
        {
            if (canvas.State is not State state)
            {
                return;
            }
            state.Resize();
        }

        private void Init(CarouselGlCanvas canvas, IReadOnlyDictionary<string, string>? assets, string? transitionName, double transitionSec, double intervalSec)
        {
            var assetsKey = assets is not null ? Stringify(assets) : "";
            var previousState = canvas.State as State;
            if (previousState is not null
                && previousState.alive
                && previousState.assetsKey == assetsKey
                && previousState.updateTransition is not null)
            {
                previousState.updateTransition(transitionName, transitionSec, intervalSec);
                return;
            }

            var previousIndex = previousState?.imageIndex ?? 0;

            Stop(canvas);

            State? state = null;
            try
            {
                state = InitTransition(canvas, assets, transitionName, transitionSec, intervalSec, previousIndex);
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

        private readonly Dictionary<string, OverlayEntry> _overlayMap = new(StringComparer.Ordinal);

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

        protected override void AttachOverlay(CarouselGlCanvas canvas, CarouselGlConfiguration configuration) =>
            AttachOverlay(Key, canvas, configuration.Assets, configuration.Variant, configuration.TransitionSeconds,
                configuration.IntervalSeconds);

        private void AttachOverlay(string key, CarouselGlCanvas canvas, IReadOnlyDictionary<string, string>? assets, string? transitionName, double transitionSec, double intervalSec)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            var entry = EnsureOverlayCanvas(key, canvas);
            var assetsKey = assets is not null ? Stringify(assets) : "";
            var transitionKey = transitionName ?? "";
            var transitionValue = transitionSec;
            var nextTransitionSec = double.IsFinite(transitionValue) ? transitionValue : 0;
            var intervalValue = intervalSec;
            var nextIntervalSec = double.IsFinite(intervalValue) ? intervalValue : 0;

            if (entry.assetsKey != assetsKey || entry.transitionName != transitionKey)
            {
                entry.assetsKey = assetsKey;
                entry.transitionName = transitionKey;
                entry.transitionSec = nextTransitionSec;
                entry.intervalSec = nextIntervalSec;
                Init(entry.canvas, assets, string.IsNullOrEmpty(transitionName) ? null : transitionName, nextTransitionSec, nextIntervalSec);
            }
            else if (entry.transitionSec != nextTransitionSec || entry.intervalSec != nextIntervalSec)
            {
                entry.transitionSec = nextTransitionSec;
                entry.intervalSec = nextIntervalSec;
                SetOverlayTransitionDuration(key, nextTransitionSec);
                SetOverlayTiming(key, nextIntervalSec);
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

        protected override void DetachAllOverlays()
        {
            foreach (var key in _overlayMap.Keys.ToArray()) DetachOverlay(key);
            // The page goes away after pagehide and its Image elements with it: the cached images are released.
            foreach (var image in _imageCache.Values)
            {
                if (image.IsCompletedSuccessfully)
                    image.Result.Dispose();
            }
            _imageCache.Clear();
        }

        protected override void SetOverlayTiming(double intervalSec) => SetOverlayTiming(Key, intervalSec);

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

        protected override void SetOverlayTransitionDuration(double transitionSec) => SetOverlayTransitionDuration(Key, transitionSec);

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
                var fallback = double.IsFinite(state.defaultTransitionSec) ? state.defaultTransitionSec : DefaultTransitionSec;
                state.baseTransitionSec = entry.transitionSec;
                state.transitionSec = NormalizeTransitionSeconds(entry.transitionSec, fallback, state.transitionName);
            }
        }

        protected override void SetOverlayActive(bool isActive) => SetOverlayActive(Key, isActive);

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

        // The compile canvas and its context (createContext, destroyContext): no counterpart; Skia compiles without one.
        protected override async Task<IReadOnlyList<string>> CompileAsync()
        {
            var result = new List<string>();
            foreach (var entry in GlTransitions)
            {
                var definition = await LoadTransitionDefinition(entry);
                CreateCachedProgram(BuildFragmentShader(definition.Source));
                // gl.deleteProgram: the programs stay compiled for the process.
                result.Add(entry.Name);
            }
            return result;
        }

        // An entry of the catalog (the JS { name, path }).
        internal sealed record TransitionEntry(string Name, string Path);

        // A parsed uniform default (the JS { name, type, value }): the value is a double (float, int), a bool or a
        // double array (vectors).
        internal sealed record UniformDefinition(string Name, string Type, object Value);

        // The result of parseUniforms (the JS { uniforms, samplers }).
        internal sealed record ParsedUniforms(List<UniformDefinition> Uniforms, List<string> Samplers);

        // A loaded transition (the JS { source, uniforms, samplers }).
        internal sealed record TransitionDefinition(string Source, IReadOnlyList<UniformDefinition> Uniforms, IReadOnlyList<string> Samplers);

        private sealed class OverlayEntry
        {
            public OverlayEntry(CarouselGlCanvas canvas)
            {
                this.canvas = canvas;
            }

            public readonly CarouselGlCanvas canvas;
            public string? assetsKey;
            public string? transitionName;
            public double intervalSec;
            public double transitionSec;
        }

        // The state of a transition on a canvas, with the field names of the JS object.
        internal sealed class State : ICarouselGlState
        {
            public State(CarouselGlCanvas canvas)
            {
                this.canvas = canvas;
            }

            public readonly CarouselGlCanvas canvas;
            // gl: no counterpart; the resources its context tracked for releaseContext are the textures it created.
            public readonly List<IDisposable> resources = [];
            public bool alive = true;
            public bool running;
            public int raf;
            public double intervalSec;
            public double baseTransitionSec;
            public double transitionSec;
            public double defaultTransitionSec;
            public string assetsKey = "";
            public string transitionName = "";
            public TransitionEntry? transitionEntry;
            public SKRuntimeEffect? program;
            // bindQuad and the from and to locations: no counterpart; uProgress and uRatio tell whether the program
            // has the uniform.
            public bool uProgress;
            public bool uRatio;
            public List<SKImage> textures = [];
            public SKImage? extraTexture;
            // The extra samplers of the program with the texture they sample (setupExtraSamplers).
            public List<(string Name, SKImage Image)> extraSamplers = [];
            public double? startTime;
            public double lastCycleIndex = -1;
            public SKImage? fromTexture;
            public SKImage? toTexture;
            public int transitionRequestId;
            public int imageIndex;
            public TransitionDefinition? definition;
            public Action<string?, double, double>? updateTransition;
            public Action<double>? _render;
            public Action<double>? _frame;

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
                alive = false;
            }

            public void Resize()
            {
                canvas.ResizeCanvasToDisplaySize();
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

            // The transitions state has no nativeStart; the bridge samples from startTime.
            double? ICarouselGlState.NativeStart => null;

            double? ICarouselGlState.StartTime => startTime;
        }
    }
}
