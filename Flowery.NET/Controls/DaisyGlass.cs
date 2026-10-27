using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Logging;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Blur rendering mode for DaisyGlass.
    /// </summary>
    public enum GlassBlurMode
    {
        /// <summary>
        /// Simulated glass using gradient overlays (no real blur).
        /// </summary>
        Simulated,

        /// <summary>
        /// Captures bitmap and applies BlurEffect (one-time capture).
        /// </summary>
        BitmapCapture,

        /// <summary>
        /// Blurs the live backdrop on every frame with SkiaSharp, including the liquid-glass optics.
        /// Needs the Flowery.NET.Skia package (<c>FlowerySkia.Register()</c>); without it the glass uses BitmapCapture.
        /// </summary>
        SkiaSharp
    }

    /// <summary>
    /// A glass/frosted effect container control styled after DaisyUI's glass effect.
    /// Supports multiple blur modes: Simulated, BitmapCapture, and SkiaSharp.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyGlass : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyGlass);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        private bool _isCapturing;
        private IImage? _capturedBitmap;
        private Image? _blurImage;
        private Canvas? _patternLayer;
        private Panel? _backgroundHost;
        private bool _needsUpdate = true;
        private bool _captureScheduled;
        private int _captureGeneration;
        private CancellationTokenSource? _captureCancellation;

        // Most card pattern tiles draw their strokes at this opacity; GlassPatternOpacity scales from it.
        private const double CardPatternOpacity = 0.15;

        // The missing-renderer warning is logged once per process.
        private static int _fallbackWarned;

        private bool ShouldCaptureBackdrop => EnableBackdropBlur && EffectiveBlurMode == GlassBlurMode.BitmapCapture && !DaisyAccessibility.IsHighContrast;

        /// <summary>
        /// Gets or sets the blur amount for the glass effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassBlurProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassBlur), 40.0);

        public double GlassBlur
        {
            get => GetValue(GlassBlurProperty);
            set => SetValue(GlassBlurProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the glass effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassOpacity), 0.25);

        public double GlassOpacity
        {
            get => GetValue(GlassOpacityProperty);
            set => SetValue(GlassOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the tint color for the glass effect.
        /// </summary>
        public static readonly StyledProperty<Color> GlassTintProperty =
            AvaloniaProperty.Register<DaisyGlass, Color>(nameof(GlassTint), Colors.White);

        public Color GlassTint
        {
            get => GetValue(GlassTintProperty);
            set => SetValue(GlassTintProperty, value);
        }

        /// <summary>
        /// Gets or sets the tint opacity for the glass effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassTintOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassTintOpacity), 0.5);

        public double GlassTintOpacity
        {
            get => GetValue(GlassTintOpacityProperty);
            set => SetValue(GlassTintOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the glass border.
        /// </summary>
        public static readonly StyledProperty<double> GlassBorderOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassBorderOpacity), 0.2);

        public double GlassBorderOpacity
        {
            get => GetValue(GlassBorderOpacityProperty);
            set => SetValue(GlassBorderOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the degree of the glass reflection.
        /// </summary>
        public static readonly StyledProperty<double> GlassReflectDegreeProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassReflectDegree), 100.0);

        public double GlassReflectDegree
        {
            get => GetValue(GlassReflectDegreeProperty);
            set => SetValue(GlassReflectDegreeProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the glass reflection.
        /// </summary>
        public static readonly StyledProperty<double> GlassReflectOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassReflectOpacity), 0.1);

        public double GlassReflectOpacity
        {
            get => GetValue(GlassReflectOpacityProperty);
            set => SetValue(GlassReflectOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the text shadow effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassTextShadowOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassTextShadowOpacity), 0.5);

        public double GlassTextShadowOpacity
        {
            get => GetValue(GlassTextShadowOpacityProperty);
            set => SetValue(GlassTextShadowOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets the saturation of the glass background (0.0 = grayscale, 1.0 = normal).
        /// </summary>
        public static readonly StyledProperty<double> GlassSaturationProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassSaturation), 1.0);

        public double GlassSaturation
        {
            get => GetValue(GlassSaturationProperty);
            set => SetValue(GlassSaturationProperty, value);
        }

        /// <summary>
        /// Gets or sets how far the liquid-glass rim and its highlights reach inward from the edge.
        /// </summary>
        public static readonly StyledProperty<double> GlassDepthProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassDepth), 0.65);

        public double GlassDepth
        {
            get => GetValue(GlassDepthProperty);
            set => SetValue(GlassDepthProperty, value);
        }

        /// <summary>
        /// Gets or sets the convex lens curvature used by the liquid-glass highlight model.
        /// </summary>
        public static readonly StyledProperty<double> GlassCurvatureProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassCurvature), 0.6);

        public double GlassCurvature
        {
            get => GetValue(GlassCurvatureProperty);
            set => SetValue(GlassCurvatureProperty, value);
        }

        /// <summary>
        /// Gets or sets the inner rim bend for the liquid-glass meniscus effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassBendProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassBend), 0.35);

        public double GlassBend
        {
            get => GetValue(GlassBendProperty);
            set => SetValue(GlassBendProperty, value);
        }

        /// <summary>
        /// Gets or sets the chromatic edge split used by the liquid-glass effect.
        /// </summary>
        public static readonly StyledProperty<double> GlassDispersionProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassDispersion), 0.18);

        public double GlassDispersion
        {
            get => GetValue(GlassDispersionProperty);
            set => SetValue(GlassDispersionProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassRefraction"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassRefractionProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassRefraction));

        /// <summary>
        /// Gets or sets how strongly the glass bends its backdrop like a lens, from 0 to 1: at 1 the edge shows what
        /// lies one bezel width (<see cref="GlassBezel"/>) beyond it, and the bend fades to nothing past the bezel. 0, the
        /// default, keeps the frosted glass that bends nothing. With <see cref="GlassDispersion"/> the red and blue of the
        /// backdrop split at the rim. Only the <see cref="GlassBlurMode.SkiaSharp"/> mode of Flowery.NET.Skia draws it.
        /// </summary>
        public double GlassRefraction
        {
            get => GetValue(GlassRefractionProperty);
            set => SetValue(GlassRefractionProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassBezel"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassBezelProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassBezel), 0.5);

        /// <summary>
        /// Gets or sets the width of the curved rim that bends the backdrop, as a share of half the shorter side, from 0 to
        /// 1 (default 0.5). A wide bezel bends gently over a long way, a narrow one sharply at the edge.
        /// </summary>
        public double GlassBezel
        {
            get => GetValue(GlassBezelProperty);
            set => SetValue(GlassBezelProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassZoom"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassZoomProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassZoom), 1.0);

        /// <summary>
        /// Gets or sets how much the glass magnifies its whole backdrop; 1, the default, does not, and 1.1 shows it 10 %
        /// larger, like the lens of a magnifier. Only the <see cref="GlassBlurMode.SkiaSharp"/> mode draws it.
        /// </summary>
        public double GlassZoom
        {
            get => GetValue(GlassZoomProperty);
            set => SetValue(GlassZoomProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="GlassRimSaturation"/> property.
        /// </summary>
        public static readonly StyledProperty<double> GlassRimSaturationProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassRimSaturation), 1.0);

        /// <summary>
        /// Gets or sets the saturation of the backdrop at the rim, rising from the inner end of the bezel to the edge; 1,
        /// the default, keeps the saturation of the glass, and 1.5 makes the colors at the edge half as strong again. Only
        /// the <see cref="GlassBlurMode.SkiaSharp"/> mode draws it.
        /// </summary>
        public double GlassRimSaturation
        {
            get => GetValue(GlassRimSaturationProperty);
            set => SetValue(GlassRimSaturationProperty, value);
        }

        /// <summary>
        /// Gets or sets a brush, such as a gradient, painted over the blurred backdrop at <see cref="GlassFillOpacity"/>.
        /// It takes precedence over <see cref="GlassGradient"/>.
        /// </summary>
        public static readonly StyledProperty<IBrush?> GlassFillProperty =
            AvaloniaProperty.Register<DaisyGlass, IBrush?>(nameof(GlassFill));

        public IBrush? GlassFill
        {
            get => GetValue(GlassFillProperty);
            set => SetValue(GlassFillProperty, value);
        }

        /// <summary>
        /// Gets or sets a gradient preset painted over the blurred backdrop when <see cref="GlassFill"/> is not set.
        /// </summary>
        public static readonly StyledProperty<DaisyGlassGradient> GlassGradientProperty =
            AvaloniaProperty.Register<DaisyGlass, DaisyGlassGradient>(nameof(GlassGradient));

        public DaisyGlassGradient GlassGradient
        {
            get => GetValue(GlassGradientProperty);
            set => SetValue(GlassGradientProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the fill over the blurred backdrop.
        /// </summary>
        public static readonly StyledProperty<double> GlassFillOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassFillOpacity), 0.6);

        public double GlassFillOpacity
        {
            get => GetValue(GlassFillOpacityProperty);
            set => SetValue(GlassFillOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets a pattern drawn over the fill, using the same patterns as <see cref="DaisyPatternedCard"/>.
        /// </summary>
        public static readonly StyledProperty<DaisyCardPattern> GlassPatternProperty =
            AvaloniaProperty.Register<DaisyGlass, DaisyCardPattern>(nameof(GlassPattern));

        public DaisyCardPattern GlassPattern
        {
            get => GetValue(GlassPatternProperty);
            set => SetValue(GlassPatternProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the glass pattern.
        /// </summary>
        public static readonly StyledProperty<Color> GlassPatternColorProperty =
            AvaloniaProperty.Register<DaisyGlass, Color>(nameof(GlassPatternColor), Colors.White);

        public Color GlassPatternColor
        {
            get => GetValue(GlassPatternColorProperty);
            set => SetValue(GlassPatternColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of a typical stroke of the glass pattern. Lighter and darker parts of a pattern keep their proportion.
        /// </summary>
        public static readonly StyledProperty<double> GlassPatternOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassPatternOpacity), 0.4);

        public double GlassPatternOpacity
        {
            get => GetValue(GlassPatternOpacityProperty);
            set => SetValue(GlassPatternOpacityProperty, value);
        }

        /// <summary>
        /// Gets or sets an animated background drawn over the fill and below the pattern, such as a
        /// <see cref="DaisyGlassGradientBackground"/> or, from Flowery.NET.Skia, a DaisyGlassShaderBackground.
        /// </summary>
        public static readonly StyledProperty<DaisyGlassBackground?> GlassBackgroundProperty =
            AvaloniaProperty.Register<DaisyGlass, DaisyGlassBackground?>(nameof(GlassBackground));

        public DaisyGlassBackground? GlassBackground
        {
            get => GetValue(GlassBackgroundProperty);
            set => SetValue(GlassBackgroundProperty, value);
        }

        /// <summary>
        /// Gets or sets the opacity of the animated background.
        /// </summary>
        public static readonly StyledProperty<double> GlassBackgroundOpacityProperty =
            AvaloniaProperty.Register<DaisyGlass, double>(nameof(GlassBackgroundOpacity), 0.6);

        public double GlassBackgroundOpacity
        {
            get => GetValue(GlassBackgroundOpacityProperty);
            set => SetValue(GlassBackgroundOpacityProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="EffectiveGlassFill"/> property.
        /// </summary>
        public static readonly DirectProperty<DaisyGlass, IBrush?> EffectiveGlassFillProperty =
            AvaloniaProperty.RegisterDirect<DaisyGlass, IBrush?>(nameof(EffectiveGlassFill), o => o.EffectiveGlassFill);

        private IBrush? _effectiveGlassFill;

        /// <summary>
        /// Gets the brush painted over the blurred backdrop: <see cref="GlassFill"/>, else the <see cref="GlassGradient"/> preset.
        /// </summary>
        public IBrush? EffectiveGlassFill
        {
            get => _effectiveGlassFill;
            private set => SetAndRaise(EffectiveGlassFillProperty, ref _effectiveGlassFill, value);
        }

        /// <summary>
        /// Gets or sets whether to enable real backdrop blur (performance intensive).
        /// When false, uses the simulated glass effect.
        /// </summary>
        public static readonly StyledProperty<bool> EnableBackdropBlurProperty =
            AvaloniaProperty.Register<DaisyGlass, bool>(nameof(EnableBackdropBlur), false);

        public bool EnableBackdropBlur
        {
            get => GetValue(EnableBackdropBlurProperty);
            set => SetValue(EnableBackdropBlurProperty, value);
        }

        /// <summary>
        /// Gets or sets the blur rendering mode.
        /// </summary>
        public static readonly StyledProperty<GlassBlurMode> BlurModeProperty =
            AvaloniaProperty.Register<DaisyGlass, GlassBlurMode>(nameof(BlurMode), GlassBlurMode.BitmapCapture);

        public GlassBlurMode BlurMode
        {
            get => GetValue(BlurModeProperty);
            set => SetValue(BlurModeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="EffectiveBlurMode"/> property.
        /// </summary>
        public static readonly DirectProperty<DaisyGlass, GlassBlurMode> EffectiveBlurModeProperty =
            AvaloniaProperty.RegisterDirect<DaisyGlass, GlassBlurMode>(nameof(EffectiveBlurMode), o => o.EffectiveBlurMode);

        private GlassBlurMode _effectiveBlurMode = GlassBlurMode.BitmapCapture;

        /// <summary>
        /// Gets the blur mode in use. <see cref="GlassBlurMode.SkiaSharp"/> needs a live renderer, which the
        /// Flowery.NET.Skia package registers; without one the glass uses <see cref="GlassBlurMode.BitmapCapture"/>.
        /// </summary>
        public GlassBlurMode EffectiveBlurMode
        {
            get => _effectiveBlurMode;
            private set => SetAndRaise(EffectiveBlurModeProperty, ref _effectiveBlurMode, value);
        }

        // Also run when the glass enters the visual tree, so a renderer registered after the glass was created still counts.
        private void UpdateEffectiveBlurMode()
        {
            var fallback = BlurMode == GlassBlurMode.SkiaSharp && DaisyGlassLiveRenderer.Current == null;
            EffectiveBlurMode = fallback ? GlassBlurMode.BitmapCapture : BlurMode;
            if (fallback && Interlocked.Exchange(ref _fallbackWarned, 1) == 0)
            {
                Logger.TryGet(LogEventLevel.Warning, LogArea.Control)?.Log(this,
                    "DaisyGlass: BlurMode=SkiaSharp needs the Flowery.NET.Skia package, registered with FlowerySkia.Register() "
                    + "or UseFlowerySkia(); the glass uses BitmapCapture instead.");
            }
        }

        /// <summary>
        /// Defines the <see cref="MeasureBackdropColor"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> MeasureBackdropColorProperty =
            AvaloniaProperty.Register<DaisyGlass, bool>(nameof(MeasureBackdropColor));

        /// <summary>
        /// Gets or sets whether the glass measures the average color of its backdrop into <see cref="BackdropColor"/>.
        /// The glass controls turn it on for their text contrast. Off by default: in <c>SkiaSharp</c> mode each
        /// measurement reads pixels back from the GPU, at most a few times per second.
        /// </summary>
        public bool MeasureBackdropColor
        {
            get => GetValue(MeasureBackdropColorProperty);
            set => SetValue(MeasureBackdropColorProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="BackdropColor"/> property.
        /// </summary>
        public static readonly DirectProperty<DaisyGlass, Color?> BackdropColorProperty =
            AvaloniaProperty.RegisterDirect<DaisyGlass, Color?>(nameof(BackdropColor), o => o.BackdropColor);

        private Color? _backdropColor;

        /// <summary>
        /// Gets the average color of what lies behind the glass, before its blur and tint, while
        /// <see cref="MeasureBackdropColor"/> is on and a blur mode can see the backdrop (<c>BitmapCapture</c> or
        /// <c>SkiaSharp</c>); otherwise <c>null</c>. It follows changes behind the glass with a short delay.
        /// </summary>
        public Color? BackdropColor
        {
            get => _backdropColor;
            private set => SetAndRaise(BackdropColorProperty, ref _backdropColor, value);
        }

        private bool CanMeasureBackdrop => MeasureBackdropColor && EnableBackdropBlur && !DaisyAccessibility.IsHighContrast
            && EffectiveBlurMode is GlassBlurMode.BitmapCapture or GlassBlurMode.SkiaSharp;

        /// <summary>
        /// Takes a measured backdrop color on the UI thread. Changes too small to see do not update the property, so
        /// the controls that follow it do not redraw for nothing.
        /// </summary>
        internal void ReportBackdropColor(Color color)
        {
            if (!CanMeasureBackdrop)
                return;
            if (BackdropColor is { } current && Math.Abs(current.R - color.R) <= 2 && Math.Abs(current.G - color.G) <= 2
                && Math.Abs(current.B - color.B) <= 2)
                return;
            BackdropColor = Color.FromRgb(color.R, color.G, color.B);
        }

        private void UpdateBackdropColorState()
        {
            if (!CanMeasureBackdrop)
                BackdropColor = null;
            else if (_capturedBitmap is BackdropBitmap { AverageColor: { } average })
                ReportBackdropColor(average);
        }

        /// <summary>
        /// Internal property for the blurred background bitmap.
        /// </summary>
        public static readonly StyledProperty<IImage?> BlurredBackgroundProperty =
            AvaloniaProperty.Register<DaisyGlass, IImage?>(nameof(BlurredBackground));

        public IImage? BlurredBackground
        {
            get => GetValue(BlurredBackgroundProperty);
            private set => SetValue(BlurredBackgroundProperty, value);
        }

        /// <summary>
        /// The height of the white shine that every glass draws at its top, in DIPs.
        /// </summary>
        public const double TopShineHeight = 60;

        /// <summary>
        /// The white shine that every glass draws at its top, <see cref="TopShineHeight"/> high. The glass template and
        /// the text contrast of the glass controls both use it, so the contrast accounts for the shine behind the text.
        /// </summary>
        public static IBrush TopShineBrush { get; } = new ImmutableLinearGradientBrush(
            [
                new ImmutableGradientStop(0, Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF)),
                new ImmutableGradientStop(0.5, Color.FromArgb(0x15, 0xFF, 0xFF, 0xFF)),
                new ImmutableGradientStop(1, Color.FromArgb(0x00, 0xFF, 0xFF, 0xFF))
            ],
            1, null, null, GradientSpreadMethod.Pad,
            new RelativePoint(0, 0, RelativeUnit.Relative), new RelativePoint(0, 1, RelativeUnit.Relative));

        /// <summary>
        /// Returns the opacity of the white top shine at <paramref name="y"/> DIPs from the top of the glass.
        /// </summary>
        internal static double TopShineOpacityAt(double y)
        {
            if (y < 0 || y >= TopShineHeight || TopShineBrush is not IGradientBrush { GradientStops: { Count: > 0 } stops })
                return 0;
            var offset = y / TopShineHeight;
            for (var i = 1; i < stops.Count; i++)
            {
                if (offset > stops[i].Offset)
                    continue;
                var span = stops[i].Offset - stops[i - 1].Offset;
                var t = span > 0 ? (offset - stops[i - 1].Offset) / span : 1;
                return (stops[i - 1].Color.A + (stops[i].Color.A - stops[i - 1].Color.A) * t) / 255.0;
            }
            return stops[^1].Color.A / 255.0;
        }

        static DaisyGlass()
        {
            AffectsRender<DaisyGlass>(
                EnableBackdropBlurProperty,
                BlurModeProperty,
                GlassBlurProperty,
                GlassOpacityProperty,
                GlassTintProperty,
                GlassTintOpacityProperty,
                GlassReflectDegreeProperty,
                GlassReflectOpacityProperty,
                GlassSaturationProperty,
                GlassDepthProperty,
                GlassCurvatureProperty,
                GlassBendProperty,
                GlassDispersionProperty,
                GlassRefractionProperty,
                GlassBezelProperty,
                GlassZoomProperty,
                GlassRimSaturationProperty);
        }

        public DaisyGlass()
        {
            UpdateEffectiveBlurMode();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _blurImage = e.NameScope.Find<Image>("PART_BlurImage");
            _patternLayer = e.NameScope.Find<Canvas>("PART_PatternLayer");
            _backgroundHost = e.NameScope.Find<Panel>("PART_BackgroundHost");
            UpdateBackdropImageBounds();
            RebuildPatternLayer();
            RebuildBackgroundLayer();
        }

        private void RebuildBackgroundLayer()
        {
            if (_backgroundHost == null) return;
            _backgroundHost.Children.Clear();
            if (GlassBackground?.CreateLayer() is { } layer)
                _backgroundHost.Children.Add(layer);
        }

        private void UpdateEffectiveGlassFill()
        {
            EffectiveGlassFill = GlassFill ?? DaisyGlassGradients.CreateBrush(GlassGradient);
        }

        private void RebuildPatternLayer()
        {
            if (_patternLayer == null) return;
            _patternLayer.Children.Clear();

            var size = Bounds.Size;
            if (GlassPattern == DaisyCardPattern.None || size.Width <= 0 || size.Height <= 0) return;

            var pattern = FloweryPatternTileGenerator.CreateTiledPatternCanvas(
                GlassPattern, new SolidColorBrush(GlassPatternColor), size.Width, size.Height);
            if (pattern == null) return;

            // The tiles carry the faint opacities of card patterns, mostly 0.15. Scale them so that
            // GlassPatternOpacity is the opacity of a typical stroke, keeping each pattern's own shading.
            ScalePatternOpacity(pattern, GlassPatternOpacity / CardPatternOpacity);
            _patternLayer.Children.Add(pattern);
        }

        private static void ScalePatternOpacity(Panel panel, double scale)
        {
            foreach (var child in panel.Children)
            {
                if (child is Panel nested)
                    ScalePatternOpacity(nested, scale);
                else
                    child.Opacity = Math.Clamp(child.Opacity * scale, 0, 1);
            }
        }

        private void UpdateBackdropImageBounds()
        {
            if (_blurImage == null) return;
            var displayBounds = _capturedBitmap is BackdropBitmap bitmap
                ? bitmap.DisplayBounds : new Rect(Bounds.Size);
            _blurImage.Width = displayBounds.Width;
            _blurImage.Height = displayBounds.Height;
            _blurImage.Margin = new Thickness(displayBounds.X, displayBounds.Y, 0, 0);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            DaisyAccessibility.PreferencesChanged += OnAccessibilityChanged;
            PseudoClasses.Set(":highcontrast", DaisyAccessibility.IsHighContrast);
            UpdateEffectiveBlurMode();
            UpdateBackdropColorState();
            if (ShouldCaptureBackdrop)
            {
                RequestBackdropCapture();
            }
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            DaisyAccessibility.PreferencesChanged -= OnAccessibilityChanged;
            base.OnDetachedFromVisualTree(e);
            ClearCapturedBackdrop();
        }

        private void OnAccessibilityChanged(object? sender, EventArgs e)
        {
            PseudoClasses.Set(":highcontrast", DaisyAccessibility.IsHighContrast);
            UpdateBackdropColorState();
            if (DaisyAccessibility.IsHighContrast) ClearCapturedBackdrop();
            else if (ShouldCaptureBackdrop) RequestBackdropCapture();
            InvalidateVisual();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == GlassBackgroundProperty)
            {
                RebuildBackgroundLayer();
                return;
            }

            if (change.Property == GlassFillProperty || change.Property == GlassGradientProperty)
            {
                UpdateEffectiveGlassFill();
                return;
            }

            if (change.Property == GlassPatternProperty || change.Property == GlassPatternColorProperty
                || change.Property == GlassPatternOpacityProperty
                || (change.Property == BoundsProperty && change.GetOldValue<Rect>().Size != change.GetNewValue<Rect>().Size))
            {
                RebuildPatternLayer();
            }

            if (change.Property == EnableBackdropBlurProperty)
            {
                if (ShouldCaptureBackdrop && TopLevel.GetTopLevel(this) != null)
                {
                    RequestBackdropCapture();
                }
                else
                {
                    ClearCapturedBackdrop();
                }
            }
            else if (change.Property == BlurModeProperty)
            {
                UpdateEffectiveBlurMode();
                if (ShouldCaptureBackdrop && TopLevel.GetTopLevel(this) != null)
                {
                    RequestBackdropCapture();
                }
                else
                {
                    ClearCapturedBackdrop();
                }
            }
            else if (change.Property == GlassBlurProperty && ShouldCaptureBackdrop)
            {
                RequestBackdropCapture();
            }
            else if (change.Property == BoundsProperty && ShouldCaptureBackdrop)
            {
                RequestBackdropCapture();
            }

            if (change.Property == MeasureBackdropColorProperty)
            {
                UpdateBackdropColorState();
                if (MeasureBackdropColor)
                {
                    // A capture taken without the measurement has no average; the live renderer measures on its next render.
                    if (ShouldCaptureBackdrop && TopLevel.GetTopLevel(this) != null)
                        RequestBackdropCapture();
                    InvalidateVisual();
                }
            }
            else if (change.Property == EnableBackdropBlurProperty || change.Property == BlurModeProperty)
            {
                UpdateBackdropColorState();
            }
        }

        private void RequestBackdropCapture()
        {
            _captureGeneration++;
            _needsUpdate = true;
            ScheduleBackdropCapture();
        }

        private void ScheduleBackdropCapture()
        {
            if (_captureScheduled || _isCapturing || !_needsUpdate || !ShouldCaptureBackdrop
                || TopLevel.GetTopLevel(this) == null)
                return;

            _captureScheduled = true;
            Dispatcher.UIThread.Post(() =>
            {
                _captureScheduled = false;
                CaptureAndBlurBackdrop();
            }, DispatcherPriority.Background);
        }

        private async void CaptureAndBlurBackdrop()
        {
            var host = TopLevel.GetTopLevel(this);
            if (_isCapturing || !_needsUpdate || !ShouldCaptureBackdrop || host == null)
                return;

            _isCapturing = true;
            _needsUpdate = false;
            var generation = _captureGeneration;
            var request = new CancellationTokenSource();
            _captureCancellation = request;

            try
            {
                var blurredBitmap = await CaptureBackdropAsync(request.Token);
                if (blurredBitmap != null)
                {
                    if (request.IsCancellationRequested || generation != _captureGeneration || !ShouldCaptureBackdrop
                        || !ReferenceEquals(host, TopLevel.GetTopLevel(this)))
                    {
                        (blurredBitmap as IDisposable)?.Dispose();
                        return;
                    }
                    var oldBitmap = _capturedBitmap;
                    _capturedBitmap = blurredBitmap;
                    UpdateBackdropImageBounds();
                    BlurredBackground = blurredBitmap;
                    (oldBitmap as IDisposable)?.Dispose();
                    UpdateBackdropColorState();
                }
            }
            catch
            {
                // Silently handle capture failures
            }
            finally
            {
                _isCapturing = false;
                if (ReferenceEquals(_captureCancellation, request)) _captureCancellation = null;
                request.Dispose();
                ScheduleBackdropCapture();
            }
        }

        /// <summary>Captures a new owned background image. Rejected and replaced images are disposed.</summary>
        protected virtual async Task<IImage?> CaptureBackdropAsync(CancellationToken cancellationToken)
        {
            // Called on the UI thread, so the capture stays there. A detour through the thread pool read
            // Dispatcher.UIThread on a pool thread, which can make that thread the owner of a new UI dispatcher (such as
            // between isolated headless test sessions).
            try
            {
                return await Dispatcher.UIThread.InvokeAsync<IImage?>(() =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    return CaptureBackdrop();
                });
            }
            catch
            {
                return null;
            }
        }

        private RenderTargetBitmap? CaptureBackdrop()
        {
            // Find the background container (parent with background)
            var backgroundSource = FindBackgroundSource();
            if (backgroundSource == null)
                return null;

            var bounds = Bounds;
            if (bounds.Width <= 0 || bounds.Height <= 0)
                return null;

            // Get our position relative to the background source
            var transform = this.TransformToVisual(backgroundSource);
            if (transform == null)
                return null;

            var topLeft = transform.Value.Transform(new Point(0, 0));

            // Keep the available blur padding outside the glass, including at the source edges.
            var blurPadding = Math.Clamp(GlassBlur, 0, 20);
            var capture = new Rect(topLeft, bounds.Size).Inflate(blurPadding)
                .Intersect(new Rect(backgroundSource.Bounds.Size));

            if (capture.Width <= 0 || capture.Height <= 0)
                return null;

            // Render at reduced resolution for performance (0.75 = good quality/performance balance)
            var scale = 0.75;
            var pixelWidth = (int)Math.Ceiling(capture.Width * scale);
            var pixelHeight = (int)Math.Ceiling(capture.Height * scale);

            if (pixelWidth <= 0 || pixelHeight <= 0)
                return null;

            RenderTargetBitmap? bitmap = null;

            try
            {
                var displayBounds = new Rect(capture.X - topLeft.X, capture.Y - topLeft.Y, capture.Width, capture.Height);
                var dpi = new Vector(96 * pixelWidth / capture.Width, 96 * pixelHeight / capture.Height);
                var backdrop = new BackdropBitmap(new PixelSize(pixelWidth, pixelHeight), dpi, displayBounds);
                bitmap = backdrop;

                using (var ctx = bitmap.CreateDrawingContext())
                {
                    BackdropRenderer.Render(ctx, backgroundSource, this, capture);
                }

                if (MeasureBackdropColor)
                {
                    // The glass itself, without the blur padding around it, in bitmap pixels.
                    var scaleX = pixelWidth / capture.Width;
                    var scaleY = pixelHeight / capture.Height;
                    var area = new PixelRect(
                        (int)Math.Floor((topLeft.X - capture.X) * scaleX), (int)Math.Floor((topLeft.Y - capture.Y) * scaleY),
                        (int)Math.Ceiling(bounds.Width * scaleX), (int)Math.Ceiling(bounds.Height * scaleY));
                    // A backend that cannot copy the pixels loses only the measurement, not the captured backdrop.
                    try
                    {
                        backdrop.AverageColor = AverageColor(backdrop, area);
                    }
                    catch
                    {
                        backdrop.AverageColor = null;
                    }
                }

                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                return null;
            }
        }

        /// <summary>
        /// Returns the average color of <paramref name="area"/> in <paramref name="bitmap"/>, weighted by alpha, or
        /// <c>null</c> when the area holds nothing opaque.
        /// </summary>
        private static Color? AverageColor(Bitmap bitmap, PixelRect area)
        {
            area = area.Intersect(new PixelRect(bitmap.PixelSize));
            if (area.Width <= 0 || area.Height <= 0)
                return null;

            // Copied into a bitmap of a known format; the copy converts from whatever format the platform renders in.
            using var copy = new WriteableBitmap(bitmap.PixelSize, bitmap.Dpi, PixelFormat.Bgra8888, AlphaFormat.Premul);
            using var frame = copy.Lock();
            bitmap.CopyPixels(frame);
            var row = new byte[area.Width * 4];
            long blue = 0, green = 0, red = 0, alpha = 0;
            for (var y = area.Y; y < area.Bottom; y++)
            {
                Marshal.Copy(frame.Address + y * frame.RowBytes + area.X * 4, row, 0, row.Length);
                for (var i = 0; i < row.Length; i += 4)
                {
                    // Premultiplied channels summed and divided by the summed alpha give the alpha-weighted average.
                    blue += row[i];
                    green += row[i + 1];
                    red += row[i + 2];
                    alpha += row[i + 3];
                }
            }
            if (alpha == 0)
                return null;
            return Color.FromRgb(
                (byte)Math.Min(255, red * 255 / alpha), (byte)Math.Min(255, green * 255 / alpha), (byte)Math.Min(255, blue * 255 / alpha));
        }

        // The display rectangle travels with the owned bitmap and is applied only after capture validation.
        private sealed class BackdropBitmap(PixelSize pixelSize, Vector dpi, Rect displayBounds)
            : RenderTargetBitmap(pixelSize, dpi), IImage
        {
            public Rect DisplayBounds { get; } = displayBounds;

            /// <summary>The average color behind the glass, measured when the glass asked for it.</summary>
            public Color? AverageColor { get; set; }

            // Bitmap.Draw uses pixel source coordinates. Image must see the same units,
            // while the render target retains its DPI for drawing shadows and effects.
            Size IImage.Size => new(PixelSize.Width, PixelSize.Height);
        }

        private Visual? FindBackgroundSource()
        {
            // Walk up the tree to find the FIRST parent with a background
            Visual? current = this.GetVisualParent();

            while (current != null)
            {
                // Stop at window/top level - don't use these
                if (current is TopLevel)
                    break;

                // Return the FIRST parent with a background (closest to us)
                if (current is Border border && border.Background != null)
                    return border;
                if (current is Panel panel && panel.Background != null)
                    return panel;

                current = current.GetVisualParent();
            }

            // Fallback: just use immediate parent
            return this.GetVisualParent();
        }

        /// <summary>
        /// Call this to manually refresh the backdrop blur (e.g., after content changes).
        /// </summary>
        public void RefreshBackdrop()
        {
            if (ShouldCaptureBackdrop)
            {
                RequestBackdropCapture();
            }
        }

        private void ClearCapturedBackdrop()
        {
            _captureGeneration++;
            _needsUpdate = false;
            _captureCancellation?.Cancel();
            var oldBitmap = _capturedBitmap;
            _capturedBitmap = null;
            BlurredBackground = null;
            (oldBitmap as IDisposable)?.Dispose();
        }

        /// <summary>
        /// Draws the live backdrop through the registered <see cref="DaisyGlassLiveRenderer"/> in SkiaSharp mode.
        /// </summary>
        public override void Render(DrawingContext context)
        {
            if (EnableBackdropBlur && EffectiveBlurMode == GlassBlurMode.SkiaSharp && !DaisyAccessibility.IsHighContrast)
                DaisyGlassLiveRenderer.Current?.Render(this, context);

            base.Render(context);
        }
    }
}
