using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Media;
using Flowery.Enums;
using Flowery.Helpers;

namespace Flowery.Controls
{
    /// <summary>
    /// How a <see cref="DaisyGlassButtonDesign"/> fills its glass.
    /// </summary>
    public enum DaisyGlassFillKind
    {
        /// <summary>No fill.</summary>
        None,
        /// <summary>A <see cref="DaisyGlassGradient"/> preset.</summary>
        Preset,
        /// <summary>A linear gradient along <see cref="DaisyGlassButtonDesign.FillAngle"/>.</summary>
        Linear,
        /// <summary>A radial gradient from the top center.</summary>
        Radial,
        /// <summary>A conic gradient around the center, starting at <see cref="DaisyGlassButtonDesign.FillAngle"/>.</summary>
        Conic
    }

    /// <summary>
    /// Which implementation draws the animated background of a <see cref="DaisyGlassButtonDesign"/>.
    /// </summary>
    public enum DaisyGlassBackgroundRenderer
    {
        /// <summary>No animated background.</summary>
        None,
        /// <summary><see cref="DaisyGlassGradientBackground"/>: moving gradients, no SkiaSharp needed.</summary>
        Gradient,
        /// <summary><c>DaisyGlassShaderBackground</c> from Flowery.NET.Skia: per-pixel shaders.</summary>
        Shader
    }

    /// <summary>
    /// A color stop of a custom fill gradient.
    /// </summary>
    public sealed class DaisyGlassFillStop
    {
        /// <summary>Gets or sets the color of the stop.</summary>
        public Color Color { get; set; }

        /// <summary>Gets or sets the position of the stop, from 0 to 1.</summary>
        public double Offset { get; set; }
    }

    /// <summary>
    /// The look of a <see cref="DaisyGlassButton"/> as plain values: what <see cref="DaisyGlassButtonDesigner"/> edits.
    /// A design applies to a button, reads from one, and converts to XAML, C# and JSON. The defaults are those of a
    /// <see cref="DaisyGlassButton"/> in the Flowery theme.
    /// </summary>
    public sealed class DaisyGlassButtonDesign
    {
        private const double Tolerance = 1e-6;

        /// <summary>Gets or sets the button text.</summary>
        public string Content { get; set; } = "Get started";

        /// <summary>Gets or sets the variant, whose color tints the glass.</summary>
        public DaisyButtonVariant Variant { get; set; }

        /// <summary>Gets or sets the button style.</summary>
        public DaisyButtonStyle ButtonStyle { get; set; }

        /// <summary>Gets or sets the button shape.</summary>
        public DaisyButtonShape Shape { get; set; }

        /// <summary>Gets or sets the button size.</summary>
        public DaisySize Size { get; set; } = DaisySize.Medium;

        /// <summary>Gets or sets whether the glass blurs its backdrop.</summary>
        public bool EnableBackdropBlur { get; set; } = true;

        /// <summary>Gets or sets how the backdrop is blurred.</summary>
        public GlassBlurMode BlurMode { get; set; } = GlassBlurMode.SkiaSharp;

        /// <summary>Gets or sets the blur amount.</summary>
        public double GlassBlur { get; set; } = 30;

        /// <summary>Gets or sets the saturation of the blurred backdrop.</summary>
        public double GlassSaturation { get; set; } = 1;

        /// <summary>Gets or sets the opacity of the simulated glass.</summary>
        public double GlassOpacity { get; set; } = 0.25;

        /// <summary>Gets or sets the tint color.</summary>
        public Color GlassTint { get; set; } = Colors.White;

        /// <summary>Gets or sets the tint opacity.</summary>
        public double GlassTintOpacity { get; set; } = 0.15;

        /// <summary>Gets or sets the reflection angle in degrees.</summary>
        public double GlassReflectDegree { get; set; } = 100;

        /// <summary>Gets or sets the reflection opacity.</summary>
        public double GlassReflectOpacity { get; set; } = 0.15;

        /// <summary>Gets or sets how far the liquid-glass rim reaches inward.</summary>
        public double GlassDepth { get; set; } = 0.65;

        /// <summary>Gets or sets the lens curvature of the liquid-glass highlight.</summary>
        public double GlassCurvature { get; set; } = 0.6;

        /// <summary>Gets or sets the inner rim bend.</summary>
        public double GlassBend { get; set; } = 0.35;

        /// <summary>Gets or sets the chromatic edge split.</summary>
        public double GlassDispersion { get; set; } = 0.18;

        /// <summary>Gets or sets how the glass is filled.</summary>
        public DaisyGlassFillKind FillKind { get; set; }

        /// <summary>Gets or sets the preset used when <see cref="FillKind"/> is <see cref="DaisyGlassFillKind.Preset"/>.</summary>
        public DaisyGlassGradient FillPreset { get; set; }

        /// <summary>
        /// Gets or sets the angle of a linear or conic fill in degrees. A linear fill runs clockwise from the left edge,
        /// so 0 is left to right and 90 is top to bottom.
        /// </summary>
        public double FillAngle { get; set; } = 30;

        /// <summary>Gets or sets the stops of a linear, radial or conic fill.</summary>
        public List<DaisyGlassFillStop> FillStops { get; set; } = CreateDefaultStops();

        /// <summary>Gets or sets the fill opacity.</summary>
        public double GlassFillOpacity { get; set; } = 0.6;

        /// <summary>Gets or sets the pattern over the fill.</summary>
        public DaisyCardPattern GlassPattern { get; set; }

        /// <summary>Gets or sets the pattern color.</summary>
        public Color GlassPatternColor { get; set; } = Colors.White;

        /// <summary>Gets or sets the pattern opacity.</summary>
        public double GlassPatternOpacity { get; set; } = 0.4;

        /// <summary>Gets or sets which implementation draws the animated background.</summary>
        public DaisyGlassBackgroundRenderer BackgroundRenderer { get; set; }

        /// <summary>Gets or sets the effect of the animated background.</summary>
        public DaisyGlassAnimation BackgroundEffect { get; set; }

        /// <summary>Gets or sets the speed of the animated background.</summary>
        public double BackgroundSpeed { get; set; } = 1;

        /// <summary>Gets or sets the redraws per second of the animated background (0 redraws on every display frame).</summary>
        public double BackgroundFrameRate { get; set; } = 30;

        /// <summary>Gets or sets the opacity of the animated background.</summary>
        public double GlassBackgroundOpacity { get; set; } = 0.6;

        /// <summary>Gets or sets the opacity of the variant color over the glass.</summary>
        public double GlassColorOpacity { get; set; } = 0.35;

        /// <summary>Gets or sets the opacity of the text shadow.</summary>
        public double GlassTextShadowOpacity { get; set; } = 0.5;

        /// <summary>Gets or sets whether the text keeps its contrast over the backdrop.</summary>
        public bool GlassTextContrast { get; set; } = true;

        /// <summary>
        /// Gets whether a shader background can be created, which needs the renderer of Flowery.NET.Skia.
        /// </summary>
        public static bool ShaderBackgroundAvailable => DaisyGlassLiveRenderer.Current?.CreateShaderBackground() is not null;

        /// <summary>
        /// Creates a deep copy.
        /// </summary>
        public DaisyGlassButtonDesign Clone()
        {
            var copy = (DaisyGlassButtonDesign)MemberwiseClone();
            copy.FillStops = FillStops.Select(stop => new DaisyGlassFillStop { Color = stop.Color, Offset = stop.Offset }).ToList();
            return copy;
        }

        /// <summary>
        /// Creates the fill brush, or <c>null</c> when the design has no custom fill (a preset is applied as
        /// <see cref="DaisyGlass.GlassGradient"/>).
        /// </summary>
        public IBrush? CreateFill()
        {
            if (FillKind is DaisyGlassFillKind.None or DaisyGlassFillKind.Preset || FillStops.Count == 0)
                return null;

            var stops = new GradientStops();
            foreach (var stop in FillStops)
                stops.Add(new GradientStop(stop.Color, stop.Offset));

            switch (FillKind)
            {
                case DaisyGlassFillKind.Radial:
                    return new RadialGradientBrush
                    {
                        Center = RadialCenter,
                        GradientOrigin = RadialCenter,
                        RadiusX = RadialRadius,
                        RadiusY = RadialRadius,
                        GradientStops = stops
                    };
                case DaisyGlassFillKind.Conic:
                    return new ConicGradientBrush { Center = RelativePoint.Center, Angle = FillAngle, GradientStops = stops };
                default:
                    var (start, end) = LinearPoints(FillAngle);
                    return new LinearGradientBrush { StartPoint = start, EndPoint = end, GradientStops = stops };
            }
        }

        /// <summary>
        /// Creates the animated background, or <c>null</c> when the design has none. A shader background falls back to
        /// the gradient implementation while Flowery.NET.Skia is not registered.
        /// </summary>
        public DaisyGlassBackground? CreateBackground()
        {
            DaisyGlassBackground? background = BackgroundRenderer switch
            {
                DaisyGlassBackgroundRenderer.Shader => DaisyGlassLiveRenderer.Current?.CreateShaderBackground() ?? new DaisyGlassGradientBackground(),
                DaisyGlassBackgroundRenderer.Gradient => new DaisyGlassGradientBackground(),
                _ => null
            };
            if (background is null)
                return null;

            background.Effect = BackgroundEffect;
            background.Speed = BackgroundSpeed;
            background.FrameRate = BackgroundFrameRate;
            return background;
        }

        /// <summary>
        /// Sets the look of <paramref name="button"/> to this design. The content stays unless
        /// <paramref name="includeContent"/> is true.
        /// </summary>
        public void ApplyTo(DaisyGlassButton button, bool includeContent = false) => ApplyTo(button, includeContent, reuseBackground: false);

        // With reuseBackground, a background of the same kind is updated in place instead of replaced, so its
        // animation keeps running while the designer changes other values. Only for a background nothing else shares.
        internal void ApplyTo(DaisyGlassButton button, bool includeContent, bool reuseBackground)
        {
            ArgumentNullException.ThrowIfNull(button);
            if (includeContent)
                button.Content = Content;

            button.Variant = Variant;
            button.ButtonStyle = ButtonStyle;
            button.Shape = Shape;
            button.Size = Size;
            button.EnableBackdropBlur = EnableBackdropBlur;
            button.BlurMode = BlurMode;
            button.GlassBlur = GlassBlur;
            button.GlassSaturation = GlassSaturation;
            button.GlassOpacity = GlassOpacity;
            button.GlassTint = GlassTint;
            button.GlassTintOpacity = GlassTintOpacity;
            button.GlassReflectDegree = GlassReflectDegree;
            button.GlassReflectOpacity = GlassReflectOpacity;
            button.GlassDepth = GlassDepth;
            button.GlassCurvature = GlassCurvature;
            button.GlassBend = GlassBend;
            button.GlassDispersion = GlassDispersion;
            button.GlassFill = CreateFill();
            button.GlassGradient = FillKind == DaisyGlassFillKind.Preset ? FillPreset : DaisyGlassGradient.None;
            button.GlassFillOpacity = GlassFillOpacity;
            button.GlassPattern = GlassPattern;
            button.GlassPatternColor = GlassPatternColor;
            button.GlassPatternOpacity = GlassPatternOpacity;
            if (reuseBackground && button.GlassBackground is { } current && IsSameRenderer(current))
            {
                current.Effect = BackgroundEffect;
                current.Speed = BackgroundSpeed;
                current.FrameRate = BackgroundFrameRate;
            }
            else
            {
                button.GlassBackground = CreateBackground();
            }
            button.GlassBackgroundOpacity = GlassBackgroundOpacity;
            button.GlassColorOpacity = GlassColorOpacity;
            button.GlassTextShadowOpacity = GlassTextShadowOpacity;
            button.GlassTextContrast = GlassTextContrast;
        }

        private bool IsSameRenderer(DaisyGlassBackground background) => BackgroundRenderer switch
        {
            DaisyGlassBackgroundRenderer.Gradient => background is DaisyGlassGradientBackground,
            DaisyGlassBackgroundRenderer.Shader => background is not DaisyGlassGradientBackground,
            _ => false
        };

        /// <summary>
        /// Reads the look of <paramref name="button"/>. A fill brush other than a linear, radial or conic gradient is
        /// read as no fill; a radial fill keeps its stops but not its geometry.
        /// </summary>
        public static DaisyGlassButtonDesign FromButton(DaisyGlassButton button)
        {
            ArgumentNullException.ThrowIfNull(button);
            var design = new DaisyGlassButtonDesign
            {
                Content = button.Content as string ?? "Get started",
                Variant = button.Variant,
                ButtonStyle = button.ButtonStyle,
                Shape = button.Shape,
                Size = button.Size,
                EnableBackdropBlur = button.EnableBackdropBlur,
                BlurMode = button.BlurMode,
                GlassBlur = button.GlassBlur,
                GlassSaturation = button.GlassSaturation,
                GlassOpacity = button.GlassOpacity,
                GlassTint = button.GlassTint,
                GlassTintOpacity = button.GlassTintOpacity,
                GlassReflectDegree = button.GlassReflectDegree,
                GlassReflectOpacity = button.GlassReflectOpacity,
                GlassDepth = button.GlassDepth,
                GlassCurvature = button.GlassCurvature,
                GlassBend = button.GlassBend,
                GlassDispersion = button.GlassDispersion,
                GlassFillOpacity = button.GlassFillOpacity,
                GlassPattern = button.GlassPattern,
                GlassPatternColor = button.GlassPatternColor,
                GlassPatternOpacity = button.GlassPatternOpacity,
                GlassBackgroundOpacity = button.GlassBackgroundOpacity,
                GlassColorOpacity = button.GlassColorOpacity,
                GlassTextShadowOpacity = button.GlassTextShadowOpacity,
                GlassTextContrast = button.GlassTextContrast
            };

            switch (button.GlassFill)
            {
                case LinearGradientBrush linear:
                    design.FillKind = DaisyGlassFillKind.Linear;
                    design.FillAngle = LinearAngle(linear.StartPoint, linear.EndPoint);
                    design.FillStops = ReadStops(linear);
                    break;
                case RadialGradientBrush radial:
                    design.FillKind = DaisyGlassFillKind.Radial;
                    design.FillStops = ReadStops(radial);
                    break;
                case ConicGradientBrush conic:
                    design.FillKind = DaisyGlassFillKind.Conic;
                    design.FillAngle = conic.Angle;
                    design.FillStops = ReadStops(conic);
                    break;
                case null when button.GlassGradient != DaisyGlassGradient.None:
                    design.FillKind = DaisyGlassFillKind.Preset;
                    design.FillPreset = button.GlassGradient;
                    break;
            }

            if (button.GlassBackground is { } background)
            {
                design.BackgroundRenderer = background is DaisyGlassGradientBackground
                    ? DaisyGlassBackgroundRenderer.Gradient
                    : DaisyGlassBackgroundRenderer.Shader;
                design.BackgroundEffect = background.Effect;
                design.BackgroundSpeed = background.Speed;
                design.BackgroundFrameRate = background.FrameRate;
            }

            return design;
        }

        /// <summary>
        /// Serializes the design to indented JSON.
        /// </summary>
        public string ToJson() => JsonSerializer.Serialize(this, DaisyGlassButtonDesignJsonContext.Default.DaisyGlassButtonDesign);

        /// <summary>
        /// Reads a design from JSON written by <see cref="ToJson"/>. Missing values keep their defaults.
        /// </summary>
        /// <exception cref="JsonException">The text is not a valid design.</exception>
        public static DaisyGlassButtonDesign FromJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            var design = JsonSerializer.Deserialize(json, DaisyGlassButtonDesignJsonContext.Default.DaisyGlassButtonDesign)
                         ?? throw new JsonException("The JSON holds no design.");
            design.FillStops ??= CreateDefaultStops();
            design.Content ??= "";
            return design;
        }

        /// <summary>
        /// Writes the design as a <c>DaisyGlassButton</c> element with the values that differ from the defaults.
        /// </summary>
        /// <param name="prefix">The XML namespace prefix of Flowery.Controls.</param>
        public string ToXaml(string prefix = "controls")
        {
            var defaults = new DaisyGlassButtonDesign();
            var element = $"{prefix}:DaisyGlassButton";
            var attributes = new List<string> { $"Content=\"{EscapeXml(Content)}\"" };
            foreach (var (name, value, isDefault) in Values(defaults))
            {
                if (!isDefault)
                    attributes.Add($"{name}=\"{FormatXaml(value)}\"");
            }
            if (FillKind == DaisyGlassFillKind.Preset && FillPreset != DaisyGlassGradient.None)
                attributes.Add($"GlassGradient=\"{FillPreset}\"");

            var children = new List<string>();
            if (CreateFill() is { } fill)
                children.Add(PropertyElement(element, "GlassFill", FillXaml(fill)));
            if (BackgroundRenderer != DaisyGlassBackgroundRenderer.None)
                children.Add(PropertyElement(element, "GlassBackground", BackgroundXaml(prefix)));

            var builder = new StringBuilder();
            if (BackgroundRenderer == DaisyGlassBackgroundRenderer.Shader)
                builder.AppendLine("<!-- xmlns:skia=\"clr-namespace:Flowery.Skia;assembly=Flowery.NET.Skia\" -->");
            var indent = new string(' ', element.Length + 2);
            builder.Append('<').Append(element).Append(' ').Append(attributes[0]);
            foreach (var attribute in attributes.Skip(1))
                builder.AppendLine().Append(indent).Append(attribute);

            if (children.Count == 0)
            {
                builder.Append(" />");
                return builder.ToString();
            }

            builder.AppendLine(">");
            foreach (var child in children)
                builder.Append(child);
            builder.Append("</").Append(element).Append('>');
            return builder.ToString();
        }

        /// <summary>
        /// Writes the design as a C# object initializer with the values that differ from the defaults.
        /// </summary>
        public string ToCSharp()
        {
            var defaults = new DaisyGlassButtonDesign();
            var lines = new List<string> { $"Content = \"{EscapeCSharp(Content)}\"" };
            foreach (var (name, value, isDefault) in Values(defaults))
            {
                if (!isDefault)
                    lines.Add($"{name} = {FormatCSharp(value)}");
            }
            if (FillKind == DaisyGlassFillKind.Preset && FillPreset != DaisyGlassGradient.None)
                lines.Add($"GlassGradient = DaisyGlassGradient.{FillPreset}");
            if (CreateFill() is { } fill)
                lines.Add("GlassFill = " + FillCSharp(fill));
            if (BackgroundRenderer != DaisyGlassBackgroundRenderer.None)
                lines.Add("GlassBackground = " + BackgroundCSharp());

            var builder = new StringBuilder();
            if (BackgroundRenderer == DaisyGlassBackgroundRenderer.Shader)
                builder.AppendLine("// DaisyGlassShaderBackground is in Flowery.Skia (package Flowery.NET.Skia).");
            builder.AppendLine("var button = new DaisyGlassButton");
            builder.AppendLine("{");
            for (var i = 0; i < lines.Count; i++)
                builder.Append("    ").Append(lines[i]).AppendLine(i < lines.Count - 1 ? "," : "");
            builder.Append("};");
            return builder.ToString();
        }

        // The scalar properties written as attributes, in a fixed order, with whether each equals its default.
        private IEnumerable<(string Name, object Value, bool IsDefault)> Values(DaisyGlassButtonDesign defaults)
        {
            yield return (nameof(Variant), Variant, Variant == defaults.Variant);
            yield return (nameof(ButtonStyle), ButtonStyle, ButtonStyle == defaults.ButtonStyle);
            yield return (nameof(Shape), Shape, Shape == defaults.Shape);
            yield return (nameof(Size), Size, Size == defaults.Size);
            yield return (nameof(EnableBackdropBlur), EnableBackdropBlur, EnableBackdropBlur == defaults.EnableBackdropBlur);
            yield return (nameof(BlurMode), BlurMode, BlurMode == defaults.BlurMode);
            yield return Number(nameof(GlassBlur), GlassBlur, defaults.GlassBlur);
            yield return Number(nameof(GlassSaturation), GlassSaturation, defaults.GlassSaturation);
            yield return Number(nameof(GlassOpacity), GlassOpacity, defaults.GlassOpacity);
            yield return (nameof(GlassTint), GlassTint, GlassTint == defaults.GlassTint);
            yield return Number(nameof(GlassTintOpacity), GlassTintOpacity, defaults.GlassTintOpacity);
            yield return Number(nameof(GlassReflectDegree), GlassReflectDegree, defaults.GlassReflectDegree);
            yield return Number(nameof(GlassReflectOpacity), GlassReflectOpacity, defaults.GlassReflectOpacity);
            yield return Number(nameof(GlassDepth), GlassDepth, defaults.GlassDepth);
            yield return Number(nameof(GlassCurvature), GlassCurvature, defaults.GlassCurvature);
            yield return Number(nameof(GlassBend), GlassBend, defaults.GlassBend);
            yield return Number(nameof(GlassDispersion), GlassDispersion, defaults.GlassDispersion);
            yield return Number(nameof(GlassFillOpacity), GlassFillOpacity, defaults.GlassFillOpacity);
            yield return (nameof(GlassPattern), GlassPattern, GlassPattern == defaults.GlassPattern);
            yield return (nameof(GlassPatternColor), GlassPatternColor, GlassPatternColor == defaults.GlassPatternColor);
            yield return Number(nameof(GlassPatternOpacity), GlassPatternOpacity, defaults.GlassPatternOpacity);
            yield return Number(nameof(GlassBackgroundOpacity), GlassBackgroundOpacity, defaults.GlassBackgroundOpacity);
            yield return Number(nameof(GlassColorOpacity), GlassColorOpacity, defaults.GlassColorOpacity);
            yield return Number(nameof(GlassTextShadowOpacity), GlassTextShadowOpacity, defaults.GlassTextShadowOpacity);
            yield return (nameof(GlassTextContrast), GlassTextContrast, GlassTextContrast == defaults.GlassTextContrast);
        }

        private static (string, object, bool) Number(string name, double value, double defaultValue) =>
            (name, value, Math.Abs(value - defaultValue) < Tolerance);

        private string BackgroundXaml(string prefix)
        {
            var element = BackgroundRenderer == DaisyGlassBackgroundRenderer.Shader
                ? "skia:DaisyGlassShaderBackground"
                : $"{prefix}:DaisyGlassGradientBackground";
            var builder = new StringBuilder($"<{element} Effect=\"{BackgroundEffect}\"");
            if (Math.Abs(BackgroundSpeed - 1) >= Tolerance)
                builder.Append($" Speed=\"{FormatNumber(BackgroundSpeed)}\"");
            if (Math.Abs(BackgroundFrameRate - 30) >= Tolerance)
                builder.Append($" FrameRate=\"{FormatNumber(BackgroundFrameRate)}\"");
            builder.Append(" />");
            return builder.ToString();
        }

        private string BackgroundCSharp()
        {
            var type = BackgroundRenderer == DaisyGlassBackgroundRenderer.Shader ? "DaisyGlassShaderBackground" : "DaisyGlassGradientBackground";
            var values = new List<string> { $"Effect = DaisyGlassAnimation.{BackgroundEffect}" };
            if (Math.Abs(BackgroundSpeed - 1) >= Tolerance)
                values.Add($"Speed = {FormatNumber(BackgroundSpeed)}");
            if (Math.Abs(BackgroundFrameRate - 30) >= Tolerance)
                values.Add($"FrameRate = {FormatNumber(BackgroundFrameRate)}");
            return $"new {type} {{ {string.Join(", ", values)} }}";
        }

        private static string FillXaml(IBrush fill)
        {
            var builder = new StringBuilder();
            string element;
            switch (fill)
            {
                case LinearGradientBrush linear:
                    element = "LinearGradientBrush";
                    builder.Append($"<{element} StartPoint=\"{FormatPoint(linear.StartPoint)}\" EndPoint=\"{FormatPoint(linear.EndPoint)}\">");
                    break;
                case RadialGradientBrush:
                    element = "RadialGradientBrush";
                    builder.Append($"<{element} Center=\"50%,0%\" GradientOrigin=\"50%,0%\" RadiusX=\"120%\" RadiusY=\"120%\">");
                    break;
                default:
                    element = "ConicGradientBrush";
                    builder.Append($"<{element} Center=\"50%,50%\" Angle=\"{FormatNumber(((ConicGradientBrush)fill).Angle)}\">");
                    break;
            }
            builder.AppendLine();
            foreach (var stop in ((GradientBrush)fill).GradientStops)
                builder.AppendLine($"    <GradientStop Color=\"{FormatColor(stop.Color)}\" Offset=\"{FormatNumber(stop.Offset)}\" />");
            builder.Append($"</{element}>");
            return builder.ToString();
        }

        private static string FillCSharp(IBrush fill)
        {
            var stops = string.Join(", ", ((GradientBrush)fill).GradientStops
                .Select(stop => $"new GradientStop(Color.Parse(\"{FormatColor(stop.Color)}\"), {FormatNumber(stop.Offset)})"));
            return fill switch
            {
                LinearGradientBrush linear =>
                    $"new LinearGradientBrush {{ StartPoint = {PointCSharp(linear.StartPoint)}, EndPoint = {PointCSharp(linear.EndPoint)}, GradientStops = {{ {stops} }} }}",
                RadialGradientBrush =>
                    "new RadialGradientBrush { Center = RelativePoint.Parse(\"50%,0%\"), GradientOrigin = RelativePoint.Parse(\"50%,0%\"), " +
                    $"RadiusX = RelativeScalar.Parse(\"120%\"), RadiusY = RelativeScalar.Parse(\"120%\"), GradientStops = {{ {stops} }} }}",
                _ => $"new ConicGradientBrush {{ Center = RelativePoint.Center, Angle = {FormatNumber(((ConicGradientBrush)fill).Angle)}, GradientStops = {{ {stops} }} }}"
            };
        }

        private static string PropertyElement(string element, string property, string value)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"    <{element}.{property}>");
            foreach (var line in value.Split('\n'))
                builder.AppendLine("        " + line.TrimEnd('\r'));
            builder.AppendLine($"    </{element}.{property}>");
            return builder.ToString();
        }

        private static readonly RelativePoint RadialCenter = new(0.5, 0, RelativeUnit.Relative);
        private static readonly RelativeScalar RadialRadius = new(1.2, RelativeUnit.Relative);

        // Same geometry as the linear presets of DaisyGlassGradients: the angle runs clockwise from the left edge.
        private static (RelativePoint Start, RelativePoint End) LinearPoints(double angle)
        {
            var radians = angle * Math.PI / 180;
            var dx = Math.Round(Math.Cos(radians) / 2, 4);
            var dy = Math.Round(Math.Sin(radians) / 2, 4);
            return (new RelativePoint(0.5 - dx, 0.5 - dy, RelativeUnit.Relative),
                    new RelativePoint(0.5 + dx, 0.5 + dy, RelativeUnit.Relative));
        }

        private static double LinearAngle(RelativePoint start, RelativePoint end)
        {
            var degrees = Math.Atan2(end.Point.Y - start.Point.Y, end.Point.X - start.Point.X) * 180 / Math.PI;
            return Math.Round(degrees < 0 ? degrees + 360 : degrees, 2);
        }

        private static List<DaisyGlassFillStop> ReadStops(GradientBrush brush) =>
            brush.GradientStops.Select(stop => new DaisyGlassFillStop { Color = stop.Color, Offset = stop.Offset }).ToList();

        private static List<DaisyGlassFillStop> CreateDefaultStops() =>
        [
            new() { Color = Color.FromUInt32(0xFF22D3EE), Offset = 0 },
            new() { Color = Color.FromUInt32(0xFFA78BFA), Offset = 0.5 },
            new() { Color = Color.FromUInt32(0xFFF472B6), Offset = 1 }
        ];

        private static string FormatXaml(object value) => value switch
        {
            double number => FormatNumber(number),
            bool flag => flag ? "True" : "False",
            Color color => FormatColor(color),
            _ => value.ToString() ?? ""
        };

        private static string FormatCSharp(object value) => value switch
        {
            double number => FormatNumber(number),
            bool flag => flag ? "true" : "false",
            Color color => $"Color.Parse(\"{FormatColor(color)}\")",
            Enum enumValue => $"{enumValue.GetType().Name}.{enumValue}",
            _ => value.ToString() ?? ""
        };

        private static string FormatNumber(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

        internal static string FormatColor(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

        private static string FormatPoint(RelativePoint point) =>
            $"{FormatNumber(point.Point.X * 100)}%,{FormatNumber(point.Point.Y * 100)}%";

        private static string PointCSharp(RelativePoint point) =>
            $"new RelativePoint({FormatNumber(point.Point.X)}, {FormatNumber(point.Point.Y)}, RelativeUnit.Relative)";

        private static string EscapeXml(string text) => text
            .Replace("&", "&amp;").Replace("\"", "&quot;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string EscapeCSharp(string text) => text.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }

    /// <summary>
    /// Writes a <see cref="Color"/> as <c>#AARRGGBB</c>.
    /// </summary>
    internal sealed class DaisyGlassColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Color.TryParse(reader.GetString(), out var color) ? color : throw new JsonException("Expected a color such as #FFFFFFFF.");

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) =>
            writer.WriteStringValue(DaisyGlassButtonDesign.FormatColor(value));
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        UseStringEnumConverter = true,
        Converters = [typeof(DaisyGlassColorJsonConverter)])]
    [JsonSerializable(typeof(DaisyGlassButtonDesign))]
    internal partial class DaisyGlassButtonDesignJsonContext : JsonSerializerContext
    {
    }
}
