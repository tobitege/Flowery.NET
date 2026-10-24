using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Rendering.SceneGraph;
using Avalonia.Skia;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.Services;
using SkiaSharp;

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
        /// Uses SkiaSharp for GPU-accelerated blur (experimental).
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
        private SkiaGlassRedraw? _skiaRedraw;
        private bool _needsUpdate = true;
        private bool _captureScheduled;
        private int _captureGeneration;
        private CancellationTokenSource? _captureCancellation;

        private bool ShouldCaptureBackdrop => EnableBackdropBlur && BlurMode == GlassBlurMode.BitmapCapture && !DaisyAccessibility.IsHighContrast;

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
        /// Gets or sets how far the liquid-glass refraction reaches inward from the edge.
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
        /// Internal property for the blurred background bitmap.
        /// </summary>
        public static readonly StyledProperty<IImage?> BlurredBackgroundProperty =
            AvaloniaProperty.Register<DaisyGlass, IImage?>(nameof(BlurredBackground));

        public IImage? BlurredBackground
        {
            get => GetValue(BlurredBackgroundProperty);
            private set => SetValue(BlurredBackgroundProperty, value);
        }

        // The glass properties that glass controls such as DaisyGlassButton own and pass to their glass surface.
        private static readonly AvaloniaProperty[] SurfaceProperties =
        [
            EnableBackdropBlurProperty, BlurModeProperty, GlassBlurProperty, GlassSaturationProperty, GlassOpacityProperty,
            GlassTintProperty, GlassTintOpacityProperty, GlassReflectDegreeProperty, GlassReflectOpacityProperty,
            GlassDepthProperty, GlassCurvatureProperty, GlassBendProperty, GlassDispersionProperty,
            GlassFillProperty, GlassGradientProperty, GlassFillOpacityProperty,
            GlassPatternProperty, GlassPatternColorProperty, GlassPatternOpacityProperty
        ];

        /// <summary>
        /// Binds the glass properties of <paramref name="surface"/> to the same properties of <paramref name="owner"/>,
        /// which owns them through <c>AddOwner</c>. Dispose the result before binding a new surface.
        /// </summary>
        internal static IDisposable BindSurface(DaisyGlass surface, AvaloniaObject owner)
        {
            var bindings = new IDisposable[SurfaceProperties.Length];
            for (var i = 0; i < SurfaceProperties.Length; i++)
                bindings[i] = surface.Bind(SurfaceProperties[i], owner.GetObservable(SurfaceProperties[i]));
            return new SurfaceBindings(bindings);
        }

        private sealed class SurfaceBindings(IDisposable[] bindings) : IDisposable
        {
            public void Dispose()
            {
                foreach (var binding in bindings)
                    binding.Dispose();
            }
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
                GlassDispersionProperty);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _blurImage = e.NameScope.Find<Image>("PART_BlurImage");
            _patternLayer = e.NameScope.Find<Canvas>("PART_PatternLayer");
            UpdateBackdropImageBounds();
            RebuildPatternLayer();
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

        private const double CardPatternOpacity = 0.15;

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
            if (DaisyAccessibility.IsHighContrast) ClearCapturedBackdrop();
            else if (ShouldCaptureBackdrop) RequestBackdropCapture();
            InvalidateVisual();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

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
            return await Task.Run<IImage?>(() =>
            {
                try
                {
                    return Dispatcher.UIThread.Invoke(() =>
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        return CaptureBackdrop();
                    });
                }
                catch
                {
                    return null;
                }
            }, cancellationToken);
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
                bitmap = new BackdropBitmap(new PixelSize(pixelWidth, pixelHeight), dpi, displayBounds);

                using (var ctx = bitmap.CreateDrawingContext())
                {
                    BackdropRenderer.Render(ctx, backgroundSource, this, capture);
                }

                return bitmap;
            }
            catch
            {
                bitmap?.Dispose();
                return null;
            }
        }

        // The display rectangle travels with the owned bitmap and is applied only after capture validation.
        private sealed class BackdropBitmap(PixelSize pixelSize, Vector dpi, Rect displayBounds)
            : RenderTargetBitmap(pixelSize, dpi), IImage
        {
            public Rect DisplayBounds { get; } = displayBounds;

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
        /// Override render to support SkiaSharp blur mode.
        /// </summary>
        public override void Render(DrawingContext context)
        {
            if (EnableBackdropBlur && BlurMode == GlassBlurMode.SkiaSharp && !DaisyAccessibility.IsHighContrast)
            {
                // Use custom SkiaSharp draw operation for real-time blur
                _skiaRedraw ??= new SkiaGlassRedraw(this);
                var operation = new SkiaGlassDrawOperation(
                    _skiaRedraw,
                    new Rect(0, 0, Bounds.Width, Bounds.Height),
                    GlassBlur,
                    GlassTint,
                    GlassTintOpacity,
                    CornerRadius.TopLeft,
                    GlassSaturation,
                    GlassReflectDegree,
                    GlassReflectOpacity,
                    GlassDepth,
                    GlassCurvature,
                    GlassBend,
                    GlassDispersion);

                context.Custom(operation);
            }

            base.Render(context);
        }
    }

    /// <summary>
    /// Keeps a SkiaSharp glass redrawing in full for a moment after its backdrop changed.
    /// A frame redraws only the changed areas, but the blur spreads each change a few pixels further, so a partial
    /// redraw would leave the old blur at the edges of the changed area. While the backdrop keeps changing,
    /// the whole glass is redrawn every frame; afterwards it returns to partial redraws.
    /// </summary>
    internal sealed class SkiaGlassRedraw(Visual owner)
    {
        private static readonly TimeSpan LiveTime = TimeSpan.FromMilliseconds(300);

        private int _changeQueued;
        private DateTime _liveUntil;
        private bool _frameRequested;

        /// <summary>Incremented on the UI thread for each full redraw, so the new draw operation differs from the last.</summary>
        public int Version { get; private set; }

        /// <summary>Called on the render thread when a frame redrew only part of the glass.</summary>
        public void BackdropChanged()
        {
            if (Interlocked.Exchange(ref _changeQueued, 1) == 1)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                _changeQueued = 0;
                _liveUntil = DateTime.UtcNow + LiveTime;
                RequestFrame();
            }, DispatcherPriority.Render);
        }

        private void RequestFrame()
        {
            if (_frameRequested || TopLevel.GetTopLevel(owner) is not { } topLevel)
                return;
            _frameRequested = true;
            topLevel.RequestAnimationFrame(_ =>
            {
                _frameRequested = false;
                if (DateTime.UtcNow > _liveUntil)
                    return;
                Version++;
                owner.InvalidateVisual();
                RequestFrame();
            });
        }
    }

    /// <summary>
    /// Custom SkiaSharp draw operation for real-time glass blur effect.
    /// </summary>
    internal class SkiaGlassDrawOperation : ICustomDrawOperation
    {
        private readonly Rect _bounds;
        private readonly float _blurSigma;
        private readonly SKColor _tintColor;
        private readonly float _cornerRadius;
        private readonly float _saturation;
        private readonly float _sheenAngle;
        private readonly float _reflectOpacity;
        private readonly float _depth;
        private readonly float _curvature;
        private readonly float _bend;
        private readonly float _dispersion;
        private readonly SkiaGlassRedraw _redraw;
        private readonly int _redrawVersion;
        private SKRectI? _seen;

        public SkiaGlassDrawOperation(
            SkiaGlassRedraw redraw,
            Rect bounds,
            double blurRadius,
            Color tintColor,
            double tintOpacity,
            double cornerRadius,
            double saturation,
            double sheenAngle,
            double reflectOpacity,
            double depth,
            double curvature,
            double bend,
            double dispersion)
        {
            _redraw = redraw;
            _redrawVersion = redraw.Version;
            _bounds = bounds;
            _blurSigma = (float)(blurRadius / 10.0);
            _tintColor = new SKColor(
                tintColor.R,
                tintColor.G,
                tintColor.B,
                ToAlpha(Clamp01((float)tintOpacity)));
            _cornerRadius = (float)cornerRadius;
            _saturation = Math.Max(0, (float)saturation);
            _sheenAngle = (float)sheenAngle;
            _reflectOpacity = Clamp01((float)reflectOpacity);
            _depth = Clamp01((float)depth);
            _curvature = Clamp01((float)curvature);
            _bend = Clamp01((float)bend);
            _dispersion = Clamp01((float)dispersion);
        }

        public Rect Bounds => _bounds;

        public bool HitTest(Point p) => _bounds.Contains(p);

        public bool Equals(ICustomDrawOperation? other)
        {
            // A full redraw must differ from the last operation, or the renderer would keep the old one.
            return other is SkiaGlassDrawOperation op &&
                   op._redrawVersion == _redrawVersion &&
                   op._bounds == _bounds &&
                   op._tintColor == _tintColor &&
                   op._cornerRadius == _cornerRadius &&
                   op._sheenAngle == _sheenAngle &&
                   Math.Abs(op._blurSigma - _blurSigma) < 0.1f &&
                   Math.Abs(op._saturation - _saturation) < 0.01f &&
                   Math.Abs(op._reflectOpacity - _reflectOpacity) < 0.01f &&
                   Math.Abs(op._depth - _depth) < 0.01f &&
                   Math.Abs(op._curvature - _curvature) < 0.01f &&
                   Math.Abs(op._bend - _bend) < 0.01f &&
                   Math.Abs(op._dispersion - _dispersion) < 0.01f;
        }

        public void Dispose() { }

        public void Render(ImmediateDrawingContext context)
        {
            var leaseFeature = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
            if (leaseFeature == null)
                return;

            using var lease = leaseFeature.Lease();
            var canvas = lease.SkCanvas;

            if (canvas == null)
                return;

            var rect = new SKRect(
                (float)_bounds.X,
                (float)_bounds.Y,
                (float)_bounds.Right,
                (float)_bounds.Bottom);

            var roundedRect = new SKRoundRect(rect, _cornerRadius);

            // Taken before the rounded clip below: the clip on entry bounds the area this frame redraws.
            var backdrop = lease.SkSurface is SKSurface surface ? GetBackdrop(surface, canvas, rect) : null;

            // Save canvas state
            int saveCount = canvas.Save();

            // Clip to our bounds
            canvas.ClipRoundRect(roundedRect, SKClipOperation.Intersect, true);

            // Create blur filter
            using var blurFilter = SKImageFilter.CreateBlur(_blurSigma, _blurSigma, SKShaderTileMode.Clamp);

            SKImageFilter? backdropFilter = blurFilter;
            SKColorFilter? colorFilter = null;

            // Apply saturation if needed
            if (Math.Abs(_saturation - 1.0f) > 0.01f)
            {
                // Standard luminance weights
                float r = 0.2126f;
                float g = 0.7152f;
                float b = 0.0722f;

                float sr = (1 - _saturation) * r;
                float sg = (1 - _saturation) * g;
                float sb = (1 - _saturation) * b;

                float[] matrix = new float[]
                {
                    sr + _saturation, sr,              sr,              0, 0,
                    sg,               sg + _saturation, sg,              0, 0,
                    sb,               sb,              sb + _saturation, 0, 0,
                    0,                0,               0,                1, 0
                };

                colorFilter = SKColorFilter.CreateColorMatrix(matrix);
                backdropFilter = SKImageFilter.CreateColorFilter(colorFilter, blurFilter);
            }

            // Try to use SKSaveLayerRec if available (Standard way)
            // Note: If SKSaveLayerRec is missing in this binding, we fallback to Snapshot
            bool useSnapshot = true;

            if (!useSnapshot)
            {
                // Original attempt - failed compilation for user
                /*
                var saveLayerRec = new SKSaveLayerRec
                {
                    Bounds = rect,
                    Backdrop = backdropFilter
                };
                canvas.SaveLayer(saveLayerRec);
                canvas.Restore();
                */
            }

            // Fallback: Snapshot approach (Manual Backdrop Blur)
            // This is robust and works without SKSaveLayerRec
            if (backdrop is { } snapshot)
            {
                using var paint = new SKPaint
                {
                    ImageFilter = backdropFilter,
                    IsAntialias = true
                };

                // The backdrop is in device pixels; draw it with the identity matrix so it aligns with the surface.
                // The rounded clip is kept in device space, so it masks correctly.
                canvas.Save();
                canvas.ResetMatrix();
                canvas.DrawImage(snapshot.Image, snapshot.Bounds.Left, snapshot.Bounds.Top, paint);
                canvas.Restore();
                snapshot.Image.Dispose();
            }

            if (backdropFilter != blurFilter)
            {
                backdropFilter?.Dispose();
            }
            colorFilter?.Dispose();

            // Draw tint overlay
            using var tintPaint = new SKPaint
            {
                Color = _tintColor,
                Style = SKPaintStyle.Fill,
                IsAntialias = true
            };
            canvas.DrawRoundRect(roundedRect, tintPaint);

            DrawLiquidOptics(canvas, rect);

            // Draw highlight border (gradient from top to bottom to simulate light source)
            using var highlightPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 1,
                IsAntialias = true
            };

            // Gradient: White (Top) -> Transparent (Bottom)
            var colors = new SKColor[]
            {
                new SKColor(255, 255, 255, 80),
                new SKColor(255, 255, 255, 0)
            };

            using var shader = SKShader.CreateLinearGradient(
                new SKPoint((float)_bounds.Left, (float)_bounds.Top),
                new SKPoint((float)_bounds.Left, (float)_bounds.Bottom),
                colors,
                null,
                SKShaderTileMode.Clamp);

            highlightPaint.Shader = shader;

            canvas.DrawRoundRect(roundedRect, highlightPaint);

            // Restore canvas
            canvas.RestoreToCount(saveCount);
        }

        private void DrawLiquidOptics(SKCanvas canvas, SKRect rect)
        {
            var minSide = Math.Min(rect.Width, rect.Height);
            if (minSide <= 0)
                return;

            var rimWidth = Math.Clamp(minSide * (0.035f + _depth * 0.055f + _bend * 0.04f), 2f, minSide * 0.2f);

            // The rim is stroked on the glass edge at twice its width and blurred; the rounded clip keeps the inner
            // half. It fades inward and follows the corner radius, with no inner edge that would read as a second,
            // squarer rectangle on small controls.
            var rimRect = new SKRoundRect(rect, _cornerRadius);
            using var rimBlur = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, rimWidth * 0.5f);
            var angle = _sheenAngle * MathF.PI / 180f;
            var light = new SKPoint(MathF.Cos(angle), MathF.Sin(angle));
            var specularAlpha = ToAlpha(Math.Clamp(_reflectOpacity + _curvature * 0.12f, 0f, 0.75f));

            if (_dispersion > 0.001f)
            {
                var offset = Math.Clamp(_dispersion * 4f, 0.5f, 4f);
                DrawChromaticRim(canvas, rimRect, offset, rimWidth, rimBlur);
            }

            using (var rimPaint = new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeWidth = rimWidth * 2,
                MaskFilter = rimBlur,
                IsAntialias = true
            })
            {
                var colors = new[]
                {
                    new SKColor(255, 255, 255, specularAlpha),
                    new SKColor(255, 255, 255, ToAlpha(_reflectOpacity * 0.28f)),
                    new SKColor(0, 0, 0, ToAlpha(_bend * 0.18f)),
                    new SKColor(255, 255, 255, ToAlpha(_reflectOpacity * 0.36f))
                };
                var points = new[] { 0f, 0.38f, 0.72f, 1f };
                rimPaint.Shader = SKShader.CreateLinearGradient(
                    new SKPoint(rect.MidX - light.X * rect.Width * 0.5f, rect.MidY - light.Y * rect.Height * 0.5f),
                    new SKPoint(rect.MidX + light.X * rect.Width * 0.5f, rect.MidY + light.Y * rect.Height * 0.5f),
                    colors,
                    points,
                    SKShaderTileMode.Clamp);

                canvas.DrawRoundRect(rimRect, rimPaint);
            }

            if (_curvature > 0.001f || _bend > 0.001f)
            {
                var glowCenter = new SKPoint(
                    rect.MidX - light.X * rect.Width * 0.22f,
                    rect.MidY - light.Y * rect.Height * 0.22f);
                var glowRadius = Math.Max(8f, minSide * (0.28f + _depth * 0.2f));
                using var glowPaint = new SKPaint
                {
                    Style = SKPaintStyle.Fill,
                    IsAntialias = true,
                    Shader = SKShader.CreateRadialGradient(
                        glowCenter,
                        glowRadius,
                        new[]
                        {
                            new SKColor(255, 255, 255, ToAlpha(_reflectOpacity * (0.25f + _curvature * 0.25f))),
                            new SKColor(255, 255, 255, 0)
                        },
                        new[] { 0f, 1f },
                        SKShaderTileMode.Clamp)
                };

                canvas.DrawRoundRect(new SKRoundRect(rect, _cornerRadius), glowPaint);
            }
        }

        private static void DrawChromaticRim(SKCanvas canvas, SKRoundRect rimRect, float offset, float rimWidth, SKMaskFilter blur)
        {
            canvas.Save();
            canvas.Translate(-offset, 0);
            using (var redPaint = new SKPaint
            {
                Color = new SKColor(255, 80, 80, 34),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(2f, rimWidth * 0.7f),
                MaskFilter = blur,
                IsAntialias = true
            })
            {
                canvas.DrawRoundRect(rimRect, redPaint);
            }
            canvas.Restore();

            canvas.Save();
            canvas.Translate(offset, 0);
            using (var bluePaint = new SKPaint
            {
                Color = new SKColor(80, 150, 255, 34),
                Style = SKPaintStyle.Stroke,
                StrokeWidth = Math.Max(2f, rimWidth * 0.7f),
                MaskFilter = blur,
                IsAntialias = true
            })
            {
                canvas.DrawRoundRect(rimRect, bluePaint);
            }
            canvas.Restore();
        }

        /// <summary>
        /// Returns the backdrop of the glass and its blur margin from the surface, in device pixels.
        /// The caller disposes the image after drawing it; no surface image outlives the frame.
        /// </summary>
        private (SKImage Image, SKRectI Bounds)? GetBackdrop(SKSurface surface, SKCanvas canvas, SKRect rect)
        {
            var glass = SKRectI.Ceiling(canvas.TotalMatrix.MapRect(rect));
            var clipBounds = canvas.DeviceClipBounds;

            // The first render of an operation follows an invalidation of the glass, which redraws all of it that can
            // be seen; a scroll viewer or another clipping ancestor may hide the rest, which is never redrawn.
            // A later render that misses part of that visible area redraws only part of the glass.
            var first = _seen == null;
            if (first)
            {
                var seen = glass;
                seen.Intersect(clipBounds);
                _seen = seen;
            }
            clipBounds.Inflate(1, 1);
            var partial = !clipBounds.Contains(_seen!.Value);

            // A partial render follows a change behind the glass. The blur spreads that change beyond the redrawn
            // area, where this frame cannot draw, so the glass redraws in full for a moment.
            if (!first && partial)
                _redraw.BackdropChanged();

            var margin = (int)Math.Ceiling(_blurSigma * 3) + 1;
            var sample = glass;
            sample.Inflate(margin, margin);
            sample = new SKRectI(Math.Max(0, sample.Left), Math.Max(0, sample.Top), sample.Right, sample.Bottom);
            if (sample.Width <= 0 || sample.Height <= 0 || surface.Snapshot(sample) is not { } fresh)
                return null;
            // The surface clips the snapshot at its right and bottom edges.
            sample = new SKRectI(sample.Left, sample.Top, sample.Left + fresh.Width, sample.Top + fresh.Height);

            return (fresh, sample);
        }

        private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

        private static byte ToAlpha(float opacity) => (byte)Math.Clamp(opacity * 255f, 0f, 255f);
    }
}
