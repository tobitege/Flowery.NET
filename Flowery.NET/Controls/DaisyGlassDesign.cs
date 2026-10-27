using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// How a <see cref="DaisyGlassDesign"/> fills its glass.
    /// </summary>
    public enum DaisyGlassFillKind
    {
        /// <summary>No fill.</summary>
        None,
        /// <summary>A <see cref="DaisyGlassGradient"/> preset.</summary>
        Preset,
        /// <summary>A linear gradient along <see cref="DaisyGlassDesign.FillAngle"/>.</summary>
        Linear,
        /// <summary>A radial gradient from the top center.</summary>
        Radial,
        /// <summary>A conic gradient around the center, starting at <see cref="DaisyGlassDesign.FillAngle"/>.</summary>
        Conic,
        /// <summary>
        /// The fill of the control stays as it is: <see cref="DaisyGlassDesign.FromControl"/> reads a fill this design
        /// cannot describe, such as a solid or image brush, as Keep, so applying the design does not replace it.
        /// </summary>
        Keep
    }

    /// <summary>
    /// Which implementation draws the animated background of a <see cref="DaisyGlassDesign"/>.
    /// </summary>
    public enum DaisyGlassBackgroundRenderer
    {
        /// <summary>No animated background.</summary>
        None,
        /// <summary><see cref="DaisyGlassGradientBackground"/>: moving gradients, no SkiaSharp needed.</summary>
        Gradient,
        /// <summary><c>DaisyGlassShaderBackground</c> from Flowery.NET.Skia: per-pixel shaders.</summary>
        Shader,
        /// <summary>
        /// The animated background of the control stays as it is: <see cref="DaisyGlassDesign.FromControl"/> reads a
        /// background of another kind as Keep, so applying the design does not replace it.
        /// </summary>
        Keep
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
    /// A glass look as plain values: what <see cref="DaisyGlassDesigner"/> edits. It holds tint, blur, the optics, the
    /// fill with its stops, the pattern and the animated background, not the control the glass sits on. A design applies
    /// to every control on glass (<see cref="DaisyGlassSurface"/>), such as a button, badge, menu, card or input, reads
    /// from one, and converts to XAML and C# with the attached glass properties, and to JSON. The defaults are those of
    /// <c>DaisyGlassButtonTheme</c>.
    /// </summary>
    public sealed class DaisyGlassDesign
    {
        private const double Tolerance = 1e-6;

        /// <summary>Gets or sets whether the glass blurs its backdrop.</summary>
        public bool EnableBackdropBlur { get; set; } = true;

        /// <summary>Gets or sets how the backdrop is blurred.</summary>
        public GlassBlurMode BlurMode { get; set; } = GlassBlurMode.SkiaSharp;

        /// <summary>Gets or sets the blur amount.</summary>
        public double Blur { get; set; } = 30;

        /// <summary>Gets or sets the saturation of the blurred backdrop.</summary>
        public double Saturation { get; set; } = 1;

        /// <summary>Gets or sets the opacity of the simulated glass.</summary>
        public double Opacity { get; set; } = 0.25;

        /// <summary>Gets or sets the tint color.</summary>
        public Color Tint { get; set; } = Colors.White;

        /// <summary>Gets or sets the tint opacity.</summary>
        public double TintOpacity { get; set; } = 0.15;

        /// <summary>Gets or sets the reflection angle in degrees.</summary>
        public double ReflectDegree { get; set; } = 100;

        /// <summary>Gets or sets the reflection opacity.</summary>
        public double ReflectOpacity { get; set; } = 0.15;

        /// <summary>Gets or sets how far the liquid-glass rim reaches inward.</summary>
        public double Depth { get; set; } = 0.65;

        /// <summary>Gets or sets the lens curvature of the liquid-glass highlight.</summary>
        public double Curvature { get; set; } = 0.6;

        /// <summary>Gets or sets the inner rim bend.</summary>
        public double Bend { get; set; } = 0.35;

        /// <summary>Gets or sets the chromatic edge split.</summary>
        public double Dispersion { get; set; } = 0.18;

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
        public double FillOpacity { get; set; } = 0.6;

        /// <summary>Gets or sets the pattern over the fill.</summary>
        public DaisyCardPattern Pattern { get; set; }

        /// <summary>Gets or sets the pattern color.</summary>
        public Color PatternColor { get; set; } = Colors.White;

        /// <summary>Gets or sets the pattern opacity.</summary>
        public double PatternOpacity { get; set; } = 0.4;

        /// <summary>Gets or sets which implementation draws the animated background.</summary>
        public DaisyGlassBackgroundRenderer BackgroundRenderer { get; set; }

        /// <summary>Gets or sets the effect of the animated background.</summary>
        public DaisyGlassAnimation BackgroundEffect { get; set; }

        /// <summary>
        /// Gets or sets the color of the <see cref="DaisyGlassAnimation.LiquidColor"/> effect; <c>null</c> takes the accent
        /// color of the system.
        /// </summary>
        public Color? BackgroundColor { get; set; }

        /// <summary>Gets or sets the speed of the animated background.</summary>
        public double BackgroundSpeed { get; set; } = 1;

        /// <summary>Gets or sets the redraws per second of the animated background (0 redraws on every display frame).</summary>
        public double BackgroundFrameRate { get; set; } = 30;

        /// <summary>Gets or sets the opacity of the animated background.</summary>
        public double BackgroundOpacity { get; set; } = 0.6;

        /// <summary>Gets or sets the opacity of the variant color over the glass, for buttons and badges.</summary>
        public double ColorOpacity { get; set; } = 0.35;

        /// <summary>Gets or sets the opacity of the text shadow.</summary>
        public double TextShadowOpacity { get; set; } = 0.5;

        /// <summary>Gets or sets whether the text keeps its contrast over the backdrop.</summary>
        public bool TextContrast { get; set; } = true;

        // The fill and whether an animated background a Keep design leaves on the control, as read by FromControl. They
        // are not serialized; the designer shows the fill in its preview and offers Keep again after another kind was picked.
        internal IBrush? KeptFill { get; set; }

        internal bool KeptBackground { get; set; }

        /// <summary>
        /// Gets whether a shader background can be created, which needs the renderer of Flowery.NET.Skia.
        /// </summary>
        public static bool ShaderBackgroundAvailable => DaisyGlassLiveRenderer.Current?.SupportsShaderBackground == true;

        // The values that map one to one to an attached glass property, in a fixed order. The fill and the animated
        // background are written apart.
        private static readonly Setting[] Settings =
        [
            new(nameof(EnableBackdropBlur), DaisyGlassSurface.EnableBackdropBlurProperty, d => d.EnableBackdropBlur, (d, v) => d.EnableBackdropBlur = (bool)v),
            new(nameof(BlurMode), DaisyGlassSurface.BlurModeProperty, d => d.BlurMode, (d, v) => d.BlurMode = (GlassBlurMode)v),
            new(nameof(Blur), DaisyGlassSurface.BlurProperty, d => d.Blur, (d, v) => d.Blur = (double)v),
            new(nameof(Saturation), DaisyGlassSurface.SaturationProperty, d => d.Saturation, (d, v) => d.Saturation = (double)v),
            new(nameof(Opacity), DaisyGlassSurface.OpacityProperty, d => d.Opacity, (d, v) => d.Opacity = (double)v),
            new(nameof(Tint), DaisyGlassSurface.TintProperty, d => d.Tint, (d, v) => d.Tint = (Color)v),
            new(nameof(TintOpacity), DaisyGlassSurface.TintOpacityProperty, d => d.TintOpacity, (d, v) => d.TintOpacity = (double)v),
            new(nameof(ReflectDegree), DaisyGlassSurface.ReflectDegreeProperty, d => d.ReflectDegree, (d, v) => d.ReflectDegree = (double)v),
            new(nameof(ReflectOpacity), DaisyGlassSurface.ReflectOpacityProperty, d => d.ReflectOpacity, (d, v) => d.ReflectOpacity = (double)v),
            new(nameof(Depth), DaisyGlassSurface.DepthProperty, d => d.Depth, (d, v) => d.Depth = (double)v),
            new(nameof(Curvature), DaisyGlassSurface.CurvatureProperty, d => d.Curvature, (d, v) => d.Curvature = (double)v),
            new(nameof(Bend), DaisyGlassSurface.BendProperty, d => d.Bend, (d, v) => d.Bend = (double)v),
            new(nameof(Dispersion), DaisyGlassSurface.DispersionProperty, d => d.Dispersion, (d, v) => d.Dispersion = (double)v),
            new(nameof(FillOpacity), DaisyGlassSurface.FillOpacityProperty, d => d.FillOpacity, (d, v) => d.FillOpacity = (double)v),
            new(nameof(Pattern), DaisyGlassSurface.PatternProperty, d => d.Pattern, (d, v) => d.Pattern = (DaisyCardPattern)v),
            new(nameof(PatternColor), DaisyGlassSurface.PatternColorProperty, d => d.PatternColor, (d, v) => d.PatternColor = (Color)v),
            new(nameof(PatternOpacity), DaisyGlassSurface.PatternOpacityProperty, d => d.PatternOpacity, (d, v) => d.PatternOpacity = (double)v),
            new(nameof(BackgroundOpacity), DaisyGlassSurface.BackgroundOpacityProperty, d => d.BackgroundOpacity, (d, v) => d.BackgroundOpacity = (double)v),
            new(nameof(ColorOpacity), DaisyGlassSurface.ColorOpacityProperty, d => d.ColorOpacity, (d, v) => d.ColorOpacity = (double)v),
            new(nameof(TextShadowOpacity), DaisyGlassSurface.TextShadowOpacityProperty, d => d.TextShadowOpacity, (d, v) => d.TextShadowOpacity = (double)v),
            new(nameof(TextContrast), DaisyGlassSurface.TextContrastProperty, d => d.TextContrast, (d, v) => d.TextContrast = (bool)v)
        ];

        private sealed record Setting(string Name, AvaloniaProperty Attached, Func<DaisyGlassDesign, object> Get, Action<DaisyGlassDesign, object> Set);

        /// <summary>
        /// Creates a deep copy.
        /// </summary>
        public DaisyGlassDesign Clone()
        {
            var copy = (DaisyGlassDesign)MemberwiseClone();
            copy.FillStops = FillStops.Select(stop => new DaisyGlassFillStop { Color = stop.Color, Offset = stop.Offset }).ToList();
            return copy;
        }

        /// <summary>
        /// Creates the fill brush, or <c>null</c> when the design has no custom fill (a preset is applied as
        /// <see cref="DaisyGlassSurface.GradientProperty"/>).
        /// </summary>
        public IBrush? CreateFill()
        {
            if (FillKind is DaisyGlassFillKind.None or DaisyGlassFillKind.Preset or DaisyGlassFillKind.Keep || FillStops.Count == 0)
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
            background.EffectColor = BackgroundColor;
            background.Speed = BackgroundSpeed;
            background.FrameRate = BackgroundFrameRate;
            return background;
        }

        /// <summary>
        /// Puts <paramref name="control"/> on glass with this design: it turns <see cref="DaisyGlassSurface.IsEnabledProperty"/>
        /// on and sets the attached glass properties. A fill or background of kind Keep stays as it is.
        /// </summary>
        public void ApplyTo(Control control) => ApplyTo(control, reuseBackground: false);

        // With reuseBackground, a background of the same kind is updated in place instead of replaced, so its animation
        // keeps running while the designer changes other values. Only for a background nothing else shares. With an
        // original design, as read from the control, only the values that differ from it are written: the local values
        // and styles of the control stay, and an unchanged fill or background stays the same instance.
        internal void ApplyTo(Control control, bool reuseBackground, DaisyGlassDesign? original = null)
        {
            ArgumentNullException.ThrowIfNull(control);
            if (DaisyGlassSurface.GetIsEnabled(control) != true)
                DaisyGlassSurface.SetIsEnabled(control, true);

            foreach (var setting in Settings)
            {
                var value = setting.Get(this);
                if (original is null || !SameValue(value, setting.Get(original)))
                    control.SetValue(setting.Attached, value);
            }

            // A Keep fill stays as it is.
            if (FillKind != DaisyGlassFillKind.Keep && (original is null || !SameFill(original)))
            {
                DaisyGlassSurface.SetFill(control, CreateFill());
                DaisyGlassSurface.SetGradient(control, FillKind == DaisyGlassFillKind.Preset ? FillPreset : DaisyGlassGradient.None);
            }

            // A Keep background stays as it is.
            if (BackgroundRenderer != DaisyGlassBackgroundRenderer.Keep && (original is null || !SameBackground(original)))
            {
                if (reuseBackground && DaisyGlassSurface.GetBackground(control) is { } current && IsSameRenderer(current))
                {
                    current.Effect = BackgroundEffect;
                    current.EffectColor = BackgroundColor;
                    current.Speed = BackgroundSpeed;
                    current.FrameRate = BackgroundFrameRate;
                }
                else
                {
                    DaisyGlassSurface.SetBackground(control, CreateBackground());
                }
            }
        }

        private static bool SameValue(object value, object other) => value is double number && other is double otherNumber
            ? Math.Abs(number - otherNumber) < Tolerance
            : Equals(value, other);

        private bool SameFill(DaisyGlassDesign other) =>
            FillKind == other.FillKind && FillPreset == other.FillPreset && Math.Abs(FillAngle - other.FillAngle) < Tolerance
            && FillStops.Count == other.FillStops.Count
            && FillStops.Zip(other.FillStops).All(static pair => pair.First.Color == pair.Second.Color
                                                                   && Math.Abs(pair.First.Offset - pair.Second.Offset) < Tolerance);

        private bool SameBackground(DaisyGlassDesign other) =>
            BackgroundRenderer == other.BackgroundRenderer && BackgroundEffect == other.BackgroundEffect
            && BackgroundColor == other.BackgroundColor && Math.Abs(BackgroundSpeed - other.BackgroundSpeed) < Tolerance
            && Math.Abs(BackgroundFrameRate - other.BackgroundFrameRate) < Tolerance;

        // Whether the background is what CreateBackground makes for this design: without the Skia renderer a shader design
        // gets a gradient background, which must be updated in place too, or its animation restarts on every change.
        private bool IsSameRenderer(DaisyGlassBackground background) => BackgroundRenderer switch
        {
            DaisyGlassBackgroundRenderer.Gradient => background is DaisyGlassGradientBackground,
            DaisyGlassBackgroundRenderer.Shader => DaisyGlassLiveRenderer.Current is { SupportsShaderBackground: true } renderer
                ? renderer.IsShaderBackground(background)
                : background is DaisyGlassGradientBackground,
            _ => false
        };

        /// <summary>
        /// Reads the glass look of <paramref name="control"/>. A setting the control does not set is read from its glass
        /// (<see cref="DaisyGlassSurface.GetGlass"/>), else it keeps the default of the design. A fill or animated
        /// background this design cannot describe, such as a solid or image brush, a gradient with another geometry,
        /// opacity, transform or spread, or a custom background, is read as Keep, so applying the design leaves it as it is.
        /// </summary>
        public static DaisyGlassDesign FromControl(Control control)
        {
            ArgumentNullException.ThrowIfNull(control);
            var design = new DaisyGlassDesign();
            var glass = DaisyGlassSurface.GetGlass(control);
            foreach (var setting in Settings)
            {
                var value = control.GetValue(setting.Attached);
                if (value is null && glass != null && DaisyGlassSurface.Settings.TryGetValue(setting.Attached, out var glassProperty))
                    value = glass.GetValue(glassProperty);
                if (value is not null)
                    setting.Set(design, value);
            }

            var fill = DaisyGlassSurface.GetFill(control) ?? glass?.GlassFill;
            var gradient = DaisyGlassSurface.GetGradient(control) ?? glass?.GlassGradient ?? DaisyGlassGradient.None;
            switch (fill)
            {
                case LinearGradientBrush linear when IsPlain(linear) && IsOwnLinear(linear.StartPoint, linear.EndPoint):
                    design.FillKind = DaisyGlassFillKind.Linear;
                    design.FillAngle = LinearAngle(linear.StartPoint, linear.EndPoint);
                    design.FillStops = ReadStops(linear);
                    break;
                case RadialGradientBrush radial when IsPlain(radial) && radial.Center == RadialCenter && radial.GradientOrigin == RadialCenter
                                                     && radial.RadiusX == RadialRadius && radial.RadiusY == RadialRadius:
                    design.FillKind = DaisyGlassFillKind.Radial;
                    design.FillStops = ReadStops(radial);
                    break;
                case ConicGradientBrush conic when IsPlain(conic) && conic.Center == RelativePoint.Center:
                    design.FillKind = DaisyGlassFillKind.Conic;
                    design.FillAngle = conic.Angle;
                    design.FillStops = ReadStops(conic);
                    break;
                case null when gradient != DaisyGlassGradient.None:
                    design.FillKind = DaisyGlassFillKind.Preset;
                    design.FillPreset = gradient;
                    break;
                case null:
                    break;
                default:
                    design.FillKind = DaisyGlassFillKind.Keep;
                    design.KeptFill = fill;
                    break;
            }

            if ((DaisyGlassSurface.GetBackground(control) ?? glass?.GlassBackground) is { } background)
            {
                design.BackgroundRenderer = background is DaisyGlassGradientBackground ? DaisyGlassBackgroundRenderer.Gradient
                    : DaisyGlassLiveRenderer.Current?.IsShaderBackground(background) == true ? DaisyGlassBackgroundRenderer.Shader
                    : DaisyGlassBackgroundRenderer.Keep;
                design.KeptBackground = design.BackgroundRenderer == DaisyGlassBackgroundRenderer.Keep;
                design.BackgroundEffect = background.Effect;
                design.BackgroundColor = background.EffectColor;
                design.BackgroundSpeed = background.Speed;
                design.BackgroundFrameRate = background.FrameRate;
            }

            return design;
        }

        /// <summary>
        /// Serializes the design to indented JSON.
        /// </summary>
        public string ToJson() => JsonSerializer.Serialize(this, DaisyGlassDesignJsonContext.Default.DaisyGlassDesign);

        /// <summary>
        /// Reads a design from JSON written by <see cref="ToJson"/>. Missing values keep their defaults.
        /// </summary>
        /// <exception cref="JsonException">The text is not a valid design: not JSON, an unknown enum value, an empty fill
        /// stop, or a number that is not finite or out of range.</exception>
        public static DaisyGlassDesign FromJson(string json)
        {
            ArgumentNullException.ThrowIfNull(json);
            var design = JsonSerializer.Deserialize(json, DaisyGlassDesignJsonContext.Default.DaisyGlassDesign)
                         ?? throw new JsonException("The JSON holds no design.");
            design.FillStops ??= CreateDefaultStops();
            design.Validate();
            return design;
        }

        // JSON can name any enum number and any value; a design that reaches a control holds only values it can apply.
        private void Validate()
        {
            Defined(BlurMode);
            Defined(FillKind);
            Defined(FillPreset);
            Defined(Pattern);
            Defined(BackgroundRenderer);
            Defined(BackgroundEffect);
            foreach (var (name, value) in new[]
                     {
                         (nameof(Blur), Blur), (nameof(Saturation), Saturation),
                         (nameof(BackgroundSpeed), BackgroundSpeed), (nameof(BackgroundFrameRate), BackgroundFrameRate)
                     })
                InRange(name, value, 0, double.MaxValue);
            foreach (var (name, value) in new[]
                     {
                         (nameof(Opacity), Opacity), (nameof(TintOpacity), TintOpacity),
                         (nameof(ReflectOpacity), ReflectOpacity), (nameof(FillOpacity), FillOpacity),
                         (nameof(PatternOpacity), PatternOpacity), (nameof(BackgroundOpacity), BackgroundOpacity),
                         (nameof(ColorOpacity), ColorOpacity), (nameof(TextShadowOpacity), TextShadowOpacity)
                     })
                InRange(name, value, 0, 1);
            foreach (var (name, value) in new[]
                     {
                         (nameof(ReflectDegree), ReflectDegree), (nameof(Depth), Depth),
                         (nameof(Curvature), Curvature), (nameof(Bend), Bend),
                         (nameof(Dispersion), Dispersion), (nameof(FillAngle), FillAngle)
                     })
                InRange(name, value, double.MinValue, double.MaxValue);
            foreach (var stop in FillStops)
            {
                if (stop is null)
                    throw new JsonException("A fill stop is empty.");
                InRange("fill stop offset", stop.Offset, 0, 1);
            }
        }

        private static void Defined<T>(T value) where T : struct, Enum
        {
            if (!Enum.IsDefined(value))
                throw new JsonException($"{value} is not a {typeof(T).Name}.");
        }

        private static void InRange(string name, double value, double minimum, double maximum)
        {
            if (!double.IsFinite(value) || value < minimum || value > maximum)
                throw new JsonException(string.Format(CultureInfo.InvariantCulture, "{0} is {1}, outside {2} to {3}.", name, value, minimum, maximum));
        }

        /// <summary>
        /// Writes the design as an element with the attached glass properties. On the glass button, the default, only the
        /// values that differ from its theme are written; on another element all of them, since nothing else gives the
        /// glass the look of the design.
        /// </summary>
        /// <param name="prefix">The XML namespace prefix of Flowery.Controls.</param>
        /// <param name="element">The element that gets the glass, <c>DaisyButton</c> with the glass button theme when not set.</param>
        public string ToXaml(string prefix = "controls", string? element = null)
        {
            var defaults = new DaisyGlassDesign();
            var attributes = new List<string>();
            var onGlassButton = element is null;
            if (onGlassButton)
            {
                element = $"{prefix}:DaisyButton";
                attributes.Add("Theme=\"{StaticResource DaisyGlassButtonTheme}\"");
            }
            var surface = $"{prefix}:DaisyGlassSurface";
            attributes.Add($"{surface}.IsEnabled=\"True\"");
            foreach (var (name, value, isDefault) in Values(defaults))
            {
                if (!isDefault || !onGlassButton)
                    attributes.Add($"{surface}.{name}=\"{FormatXaml(value)}\"");
            }
            if (FillKind == DaisyGlassFillKind.Preset && FillPreset != DaisyGlassGradient.None)
                attributes.Add($"{surface}.Gradient=\"{FillPreset}\"");

            var children = new List<string>();
            if (CreateFill() is { } fill)
                children.Add(PropertyElement(surface, "Fill", FillXaml(fill)));
            if (BackgroundRenderer is DaisyGlassBackgroundRenderer.Gradient or DaisyGlassBackgroundRenderer.Shader)
                children.Add(PropertyElement(surface, "Background", BackgroundXaml(prefix, defaults)));

            var builder = new StringBuilder();
            if (BackgroundRenderer == DaisyGlassBackgroundRenderer.Shader)
                builder.AppendLine("<!-- xmlns:skia=\"clr-namespace:Flowery.Skia;assembly=Flowery.NET.Skia\" -->");
            var indent = new string(' ', element!.Length + 2);
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
        /// Writes the design as C# statements that put a control on glass. Without <paramref name="target"/> they create
        /// a glass button and set only the values that differ from its theme; on another control they set all of them,
        /// since nothing else gives the glass the look of the design.
        /// </summary>
        /// <param name="target">The name of the control variable, or <c>null</c> for a new glass button.</param>
        public string ToCSharp(string? target = null)
        {
            var defaults = new DaisyGlassDesign();
            var onGlassButton = target is null;
            target ??= "button";
            var lines = new List<string>();
            if (onGlassButton)
                lines.Add($"var {target} = new DaisyButton {{ Theme = (ControlTheme)Application.Current!.FindResource(\"DaisyGlassButtonTheme\")! }};");
            lines.Add($"DaisyGlassSurface.SetIsEnabled({target}, true);");
            foreach (var (name, value, isDefault) in Values(defaults))
            {
                if (!isDefault || !onGlassButton)
                    lines.Add($"DaisyGlassSurface.Set{name}({target}, {FormatCSharp(value)});");
            }
            if (FillKind == DaisyGlassFillKind.Preset && FillPreset != DaisyGlassGradient.None)
                lines.Add($"DaisyGlassSurface.SetGradient({target}, DaisyGlassGradient.{FillPreset});");
            if (CreateFill() is { } fill)
                lines.Add($"DaisyGlassSurface.SetFill({target}, {FillCSharp(fill)});");
            if (BackgroundRenderer is DaisyGlassBackgroundRenderer.Gradient or DaisyGlassBackgroundRenderer.Shader)
                lines.Add($"DaisyGlassSurface.SetBackground({target}, {BackgroundCSharp(defaults)});");

            var builder = new StringBuilder();
            builder.AppendLine("// using Avalonia;");
            builder.AppendLine("// using Avalonia.Media;");
            if (onGlassButton)
                builder.AppendLine("// using Avalonia.Styling;");
            builder.AppendLine("// using Flowery.Controls;");
            builder.AppendLine("// using Flowery.Enums;");
            if (BackgroundRenderer == DaisyGlassBackgroundRenderer.Shader)
                builder.AppendLine("// using Flowery.Skia; // DaisyGlassShaderBackground, from the package Flowery.NET.Skia");
            builder.Append(string.Join(Environment.NewLine, lines));
            return builder.ToString();
        }

        // The scalar values written as attached properties, in a fixed order, with whether each equals its default. The
        // tint never counts as a default: the glass button theme does not set it, so the glass takes the tint of the color
        // theme (navy in Glassy), while the design and its preview always set one.
        private IEnumerable<(string Name, object Value, bool IsDefault)> Values(DaisyGlassDesign defaults) =>
            Settings.Select(setting => (setting.Name, setting.Get(this),
                setting.Attached != DaisyGlassSurface.TintProperty && SameValue(setting.Get(this), setting.Get(defaults))));

        private string BackgroundXaml(string prefix, DaisyGlassDesign defaults)
        {
            var element = BackgroundRenderer == DaisyGlassBackgroundRenderer.Shader
                ? "skia:DaisyGlassShaderBackground"
                : $"{prefix}:DaisyGlassGradientBackground";
            var builder = new StringBuilder($"<{element} Effect=\"{BackgroundEffect}\"");
            if (BackgroundColor is { } color)
                builder.Append($" EffectColor=\"{FormatColor(color)}\"");
            if (Math.Abs(BackgroundSpeed - defaults.BackgroundSpeed) >= Tolerance)
                builder.Append($" Speed=\"{FormatNumber(BackgroundSpeed)}\"");
            if (Math.Abs(BackgroundFrameRate - defaults.BackgroundFrameRate) >= Tolerance)
                builder.Append($" FrameRate=\"{FormatNumber(BackgroundFrameRate)}\"");
            builder.Append(" />");
            return builder.ToString();
        }

        private string BackgroundCSharp(DaisyGlassDesign defaults)
        {
            var type = BackgroundRenderer == DaisyGlassBackgroundRenderer.Shader ? "DaisyGlassShaderBackground" : "DaisyGlassGradientBackground";
            var values = new List<string> { $"Effect = DaisyGlassAnimation.{BackgroundEffect}" };
            if (BackgroundColor is { } color)
                values.Add($"EffectColor = Color.Parse(\"{FormatColor(color)}\")");
            if (Math.Abs(BackgroundSpeed - defaults.BackgroundSpeed) >= Tolerance)
                values.Add($"Speed = {FormatNumber(BackgroundSpeed)}");
            if (Math.Abs(BackgroundFrameRate - defaults.BackgroundFrameRate) >= Tolerance)
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

        private static string PropertyElement(string owner, string property, string value)
        {
            var builder = new StringBuilder();
            builder.AppendLine($"    <{owner}.{property}>");
            foreach (var line in value.Split('\n'))
                builder.AppendLine("        " + line.TrimEnd('\r'));
            builder.AppendLine($"    </{owner}.{property}>");
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

        // A gradient brush with nothing a design cannot write: full opacity, no transform, padded ends.
        private static bool IsPlain(GradientBrush brush) =>
            Math.Abs(brush.Opacity - 1) < Tolerance && brush.Transform is null && brush.SpreadMethod == GradientSpreadMethod.Pad;

        // Whether the points are those LinearPoints makes for their angle: centered, half a unit long, relative.
        private static bool IsOwnLinear(RelativePoint start, RelativePoint end)
        {
            if (start.Unit != RelativeUnit.Relative || end.Unit != RelativeUnit.Relative)
                return false;
            var (ownStart, ownEnd) = LinearPoints(LinearAngle(start, end));
            return Near(ownStart.Point, start.Point) && Near(ownEnd.Point, end.Point);
        }

        private static bool Near(Point a, Point b) => Math.Abs(a.X - b.X) < 1e-3 && Math.Abs(a.Y - b.Y) < 1e-3;

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
            Enum enumValue => enumValue.GetType().Name + "." + enumValue,
            _ => value.ToString() ?? ""
        };

        private static string FormatNumber(double value) => Math.Round(value, 3).ToString("0.###", CultureInfo.InvariantCulture);

        internal static string FormatColor(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

        private static string FormatPoint(RelativePoint point) =>
            $"{FormatNumber(point.Point.X * 100)}%,{FormatNumber(point.Point.Y * 100)}%";

        private static string PointCSharp(RelativePoint point) =>
            $"new RelativePoint({FormatNumber(point.Point.X)}, {FormatNumber(point.Point.Y)}, RelativeUnit.Relative)";
    }

    /// <summary>
    /// Writes a <see cref="Color"/> as <c>#AARRGGBB</c>.
    /// </summary>
    internal sealed class DaisyGlassColorJsonConverter : JsonConverter<Color>
    {
        public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
            Color.TryParse(reader.GetString(), out var color) ? color : throw new JsonException("Expected a color such as #FFFFFFFF.");

        public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options) =>
            writer.WriteStringValue(DaisyGlassDesign.FormatColor(value));
    }

    [JsonSourceGenerationOptions(
        WriteIndented = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        UseStringEnumConverter = true,
        Converters = [typeof(DaisyGlassColorJsonConverter)])]
    [JsonSerializable(typeof(DaisyGlassDesign))]
    internal partial class DaisyGlassDesignJsonContext : JsonSerializerContext
    {
    }
}
