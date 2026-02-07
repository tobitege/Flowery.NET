using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Flowery.Enums;
using Flowery.Helpers;

namespace Flowery.Controls
{
    /// <summary>
    /// A Card control with decorative background patterns and corner ornaments.
    /// </summary>
    public class DaisyPatternedCard : DaisyCard
    {
        protected override Type StyleKeyOverride => typeof(DaisyPatternedCard);

        private Canvas? _patternLayer;
        private Canvas? _ornamentLayer;

        /// <summary>
        /// Defines the <see cref="Pattern"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyCardPattern> PatternProperty =
            AvaloniaProperty.Register<DaisyPatternedCard, DaisyCardPattern>(nameof(Pattern), DaisyCardPattern.None);

        /// <summary>
        /// Gets or sets the decorative background pattern.
        /// </summary>
        public DaisyCardPattern Pattern
        {
            get => GetValue(PatternProperty);
            set => SetValue(PatternProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="PatternMode"/> property.
        /// </summary>
        public static readonly StyledProperty<FloweryPatternMode> PatternModeProperty =
            AvaloniaProperty.Register<DaisyPatternedCard, FloweryPatternMode>(nameof(PatternMode), FloweryPatternMode.Tiled);

        /// <summary>
        /// Gets or sets the pattern rendering mode (Tiled or SvgAsset).
        /// </summary>
        public FloweryPatternMode PatternMode
        {
            get => GetValue(PatternModeProperty);
            set => SetValue(PatternModeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Ornament"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyCardOrnament> OrnamentProperty =
            AvaloniaProperty.Register<DaisyPatternedCard, DaisyCardOrnament>(nameof(Ornament), DaisyCardOrnament.None);

        /// <summary>
        /// Gets or sets the corner decorative ornaments.
        /// </summary>
        public DaisyCardOrnament Ornament
        {
            get => GetValue(OrnamentProperty);
            set => SetValue(OrnamentProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _patternLayer = e.NameScope.Find<Canvas>("PART_PatternLayer");
            _ornamentLayer = e.NameScope.Find<Canvas>("PART_OrnamentLayer");
            RebuildLayers();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BoundsProperty)
            {
                if (change.OldValue is Rect oldBounds &&
                    change.NewValue is Rect newBounds &&
                    Math.Abs(newBounds.Width - oldBounds.Width) < 0.5 &&
                    Math.Abs(newBounds.Height - oldBounds.Height) < 0.5)
                {
                    return;
                }

                RebuildLayers();
                return;
            }

            if (change.Property == PatternProperty ||
                change.Property == PatternModeProperty ||
                change.Property == OrnamentProperty ||
                change.Property == ForegroundProperty)
            {
                RebuildLayers();
            }
        }

        private void RebuildLayers()
        {
            RebuildPatternLayer();
            RebuildOrnamentLayer();
        }

        private void RebuildPatternLayer()
        {
            if (_patternLayer == null) return;
            _patternLayer.Children.Clear();

            if (Pattern == DaisyCardPattern.None) return;

            double width = Bounds.Width;
            double height = Bounds.Height;
            if (width <= 0 || height <= 0) return;

            var patternBrush = Foreground ?? Brushes.White;

            // Asset mode (PNG files)
            if (PatternMode == FloweryPatternMode.SvgAsset && FloweryPatternSvgLoader.HasSvgAsset(Pattern))
            {
                var tiledCanvas = FloweryPatternSvgLoader.CreateTiledCanvas(Pattern, width, height);
                if (tiledCanvas != null)
                {
                    _patternLayer.Children.Add(tiledCanvas);
                    return;
                }
            }

            // Tile generator mode (Generated paths)
            if (FloweryPatternTileGenerator.SupportsTiling(Pattern))
            {
                var tiledCanvas = FloweryPatternTileGenerator.CreateTiledPatternCanvas(Pattern, patternBrush, width, height);
                if (tiledCanvas != null)
                {
                    _patternLayer.Children.Add(tiledCanvas);
                }
            }
        }

        private void RebuildOrnamentLayer()
        {
            if (_ornamentLayer == null) return;
            _ornamentLayer.Children.Clear();

            if (Ornament == DaisyCardOrnament.None) return;

            double width = Bounds.Width;
            double height = Bounds.Height;
            if (width <= 0 || height <= 0) return;

            var brush = Foreground ?? Brushes.White;
            double size = 30;
            double margin = 2;

            switch (Ornament)
            {
                case DaisyCardOrnament.Corners:
                    AddCornerTriangle(_ornamentLayer, 0, 0, size, brush, 0);
                    AddCornerTriangle(_ornamentLayer, width, 0, size, brush, 90);
                    AddCornerTriangle(_ornamentLayer, width, height, size, brush, 180);
                    AddCornerTriangle(_ornamentLayer, 0, height, size, brush, 270);
                    break;

                case DaisyCardOrnament.Brackets:
                    AddBracket(_ornamentLayer, margin, margin, size, brush, 0);
                    AddBracket(_ornamentLayer, width - margin, margin, size, brush, 90);
                    AddBracket(_ornamentLayer, width - margin, height - margin, size, brush, 180);
                    AddBracket(_ornamentLayer, margin, height - margin, size, brush, 270);
                    break;

                case DaisyCardOrnament.Industrial:
                    AddIndustrial(_ornamentLayer, margin + 4, margin + 4, brush);
                    AddIndustrial(_ornamentLayer, width - margin - 4, margin + 4, brush);
                    AddIndustrial(_ornamentLayer, width - margin - 4, height - margin - 4, brush);
                    AddIndustrial(_ornamentLayer, margin + 4, height - margin - 4, brush);
                    break;
            }
        }

        private void AddCornerTriangle(Canvas canvas, double x, double y, double size, IBrush brush, double rotation)
        {
            var path = new Avalonia.Controls.Shapes.Path
            {
                Data = StreamGeometry.Parse($"M 0,0 L {size},0 L 0,{size} Z"),
                Fill = brush,
                Opacity = 0.4,
                RenderTransform = new RotateTransform(rotation),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(path, x);
            Canvas.SetTop(path, y);
            canvas.Children.Add(path);
        }

        private void AddBracket(Canvas canvas, double x, double y, double size, IBrush brush, double rotation)
        {
            var thickness = 2.0;
            var path = new Avalonia.Controls.Shapes.Path
            {
                Data = StreamGeometry.Parse($"M 0,{size} L 0,0 L {size},0"),
                Stroke = brush,
                StrokeThickness = thickness,
                Opacity = 0.6,
                RenderTransform = new RotateTransform(rotation),
                IsHitTestVisible = false
            };
            Canvas.SetLeft(path, x);
            Canvas.SetTop(path, y);
            canvas.Children.Add(path);
        }

        private void AddIndustrial(Canvas canvas, double x, double y, IBrush brush)
        {
            // A techy bolt/cross ornament
            var group = new GeometryGroup();
            group.Children.Add(new EllipseGeometry { Center = new Point(0, 0), RadiusX = 3, RadiusY = 3 });
            group.Children.Add(new RectangleGeometry { Rect = new Rect(-4, -0.5, 8, 1) });
            group.Children.Add(new RectangleGeometry { Rect = new Rect(-0.5, -4, 1, 8) });

            var path = new Avalonia.Controls.Shapes.Path
            {
                Data = group,
                Fill = brush,
                Opacity = 0.5,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(path, x);
            Canvas.SetTop(path, y);
            canvas.Children.Add(path);
        }
    }
}
