using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Flowery.Enums;

namespace Flowery.Helpers
{
    /// <summary>
    /// Generates seamless pattern tiles for DaisyCard.
    /// These tiles are designed to match the Generated patterns exactly.
    /// </summary>
    internal static class FloweryPatternTileGenerator
    {
        // Tile size must be a multiple of each pattern's step size for seamless tiling
        // CarbonFiber: step=12, need multiple of 24 (tileSize in original)
        // Dots: step=20, need multiple of 20
        // Stripes: step=40, need multiple of 40
        // Honeycomb: s=10, h=17.32, need careful alignment
        // Circuit: step=30
        // DiamondPlate: step=30
        // Using 120 as LCM-friendly size that works for most patterns
        private const double TileSize = 120;

        /// <summary>
        /// Creates a tiled pattern canvas that matches the Generated pattern exactly.
        /// </summary>
        public static Canvas? CreateTiledPatternCanvas(DaisyCardPattern pattern, IBrush brush, double width, double height)
        {
            if (!SupportsTiling(pattern))
                return null;

            int tilesX = (int)Math.Ceiling(width / TileSize) + 1;
            int tilesY = (int)Math.Ceiling(height / TileSize) + 1;

            var canvas = new Canvas
            {
                Width = width,
                Height = height,
                IsHitTestVisible = false
            };

            // Repeat the tile across the canvas
            for (int tx = 0; tx < tilesX; tx++)
            {
                for (int ty = 0; ty < tilesY; ty++)
                {
                    // Create a container for this tile with clipping
                    var tileCanvas = new Canvas
                    {
                        Width = TileSize,
                        Height = TileSize,
                        IsHitTestVisible = false,
                        Clip = new RectangleGeometry { Rect = new Rect(0, 0, TileSize, TileSize) }
                    };

                    // Add the pattern elements to the tile
                    AddPatternToTile(pattern, brush, tileCanvas);

                    Canvas.SetLeft(tileCanvas, tx * TileSize);
                    Canvas.SetTop(tileCanvas, ty * TileSize);
                    canvas.Children.Add(tileCanvas);
                }
            }

            return canvas;
        }

        private static void AddPatternToTile(DaisyCardPattern pattern, IBrush brush, Canvas tile)
        {
            switch (pattern)
            {
                case DaisyCardPattern.CarbonFiber:
                    AddCarbonFiberTile(brush, tile);
                    break;
                case DaisyCardPattern.Dots:
                    AddDotsTile(brush, tile);
                    break;
                case DaisyCardPattern.Grid:
                    AddGridTile(brush, tile);
                    break;
                case DaisyCardPattern.Stripes:
                    AddStripesTile(brush, tile);
                    break;
                case DaisyCardPattern.Noise:
                    AddNoiseTile(brush, tile);
                    break;
                case DaisyCardPattern.Honeycomb:
                    AddHoneycombTile(brush, tile);
                    break;
                case DaisyCardPattern.Circuit:
                    AddCircuitTile(brush, tile);
                    break;
                case DaisyCardPattern.Twill:
                    AddTwillTile(brush, tile);
                    break;
                case DaisyCardPattern.DiamondPlate:
                    AddDiamondPlateTile(brush, tile);
                    break;
                case DaisyCardPattern.Mesh:
                    AddMeshTile(brush, tile);
                    break;
                case DaisyCardPattern.Perforated:
                    AddPerforatedTile(brush, tile);
                    break;
                case DaisyCardPattern.Bumps:
                    AddBumpsTile(brush, tile);
                    break;
                case DaisyCardPattern.Scales:
                    AddScalesTile(brush, tile);
                    break;
            }
        }

        public static bool SupportsTiling(DaisyCardPattern pattern)
        {
            return pattern switch
            {
                DaisyCardPattern.CarbonFiber => true,
                DaisyCardPattern.Dots => true,
                DaisyCardPattern.Grid => true,
                DaisyCardPattern.Stripes => true,
                DaisyCardPattern.Noise => true,
                DaisyCardPattern.Honeycomb => true,
                DaisyCardPattern.Circuit => true,
                DaisyCardPattern.Twill => true,
                DaisyCardPattern.DiamondPlate => true,
                DaisyCardPattern.Mesh => true,
                DaisyCardPattern.Perforated => true,
                DaisyCardPattern.Bumps => true,
                DaisyCardPattern.Scales => true,
                _ => false
            };
        }

        /// <summary>
        /// Generates an SVG string for the pattern tile.
        /// Uses the same pattern logic as the Canvas version.
        /// </summary>
        /// <param name="pattern">The pattern to generate.</param>
        /// <param name="fillColor">SVG fill color (e.g., "white", "black", "#FF0000").</param>
        public static string? GeneratePatternSvg(DaisyCardPattern pattern, string fillColor = "white")
        {
            if (!SupportsTiling(pattern))
                return null;

            var svg = new System.Text.StringBuilder();
            svg.AppendLine($@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""{TileSize}"" height=""{TileSize}"" viewBox=""0 0 {TileSize} {TileSize}"">");

            switch (pattern)
            {
                case DaisyCardPattern.CarbonFiber:
                    GenerateCarbonFiberSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Dots:
                    GenerateDotsSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Grid:
                    GenerateGridSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Stripes:
                    GenerateStripesSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Noise:
                    GenerateNoiseSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Honeycomb:
                    GenerateHoneycombSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Circuit:
                    GenerateCircuitSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Twill:
                    GenerateTwillSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.DiamondPlate:
                    GenerateDiamondPlateSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Mesh:
                    GenerateMeshSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Perforated:
                    GeneratePerforatedSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Bumps:
                    GenerateBumpsSvg(svg, fillColor);
                    break;
                case DaisyCardPattern.Scales:
                    GenerateScalesSvg(svg, fillColor);
                    break;
            }

            svg.AppendLine("</svg>");
            return svg.ToString();
        }

        // ============================================================
        // SVG Generation Methods - Use same parameters as Canvas methods
        // ============================================================

        private static void GenerateCarbonFiberSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double tileSize = 24;
            double step = tileSize / 2;

            // Layer 1 - opacity 0.12
            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.12"">");
            for (double x = -tileSize; x < TileSize + tileSize; x += step)
            {
                for (double y = -tileSize; y < TileSize + tileSize; y += step)
                {
                    bool altX = ((int)(x / step)) % 2 == 0;
                    bool altY = ((int)(y / step)) % 2 == 0;
                    if (altX ^ altY)
                        svg.AppendLine($@"    <rect x=""{x + 2:F1}"" y=""{y:F1}"" width=""2"" height=""{step:F1}""/>");
                    else
                        svg.AppendLine($@"    <rect x=""{x:F1}"" y=""{y + 2:F1}"" width=""{step:F1}"" height=""2""/>");
                }
            }
            svg.AppendLine("  </g>");

            // Layer 2 - opacity 0.08
            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.08"">");
            for (double x = -tileSize; x < TileSize + tileSize; x += step)
            {
                for (double y = -tileSize; y < TileSize + tileSize; y += step)
                {
                    bool altX = ((int)(x / step)) % 2 == 0;
                    bool altY = ((int)(y / step)) % 2 == 0;
                    if (altX ^ altY)
                        svg.AppendLine($@"    <rect x=""{x + 5:F1}"" y=""{y:F1}"" width=""2"" height=""{step:F1}""/>");
                    else
                        svg.AppendLine($@"    <rect x=""{x:F1}"" y=""{y + 5:F1}"" width=""{step:F1}"" height=""2""/>");
                }
            }
            svg.AppendLine("  </g>");

            // Layer 3 - opacity 0.04
            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.04"">");
            for (double x = -tileSize; x < TileSize + tileSize; x += step)
            {
                for (double y = -tileSize; y < TileSize + tileSize; y += step)
                {
                    bool altX = ((int)(x / step)) % 2 == 0;
                    bool altY = ((int)(y / step)) % 2 == 0;
                    if (altX ^ altY)
                        svg.AppendLine($@"    <rect x=""{x + 8:F1}"" y=""{y:F1}"" width=""2"" height=""{step:F1}""/>");
                    else
                        svg.AppendLine($@"    <rect x=""{x:F1}"" y=""{y + 8:F1}"" width=""{step:F1}"" height=""2""/>");
                }
            }
            svg.AppendLine("  </g>");
        }

        private static void GenerateDotsSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double step = 20;
            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.15"">");
            for (double x = 10; x < TileSize; x += step)
            {
                for (double y = 10; y < TileSize; y += step)
                {
                    svg.AppendLine($@"    <circle cx=""{x:F1}"" cy=""{y:F1}"" r=""1.5""/>");
                }
            }
            svg.AppendLine("  </g>");
        }

        private static void GenerateStripesSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double step = 30; // Divides evenly into 120

            // Use stroke lines instead of rotated rects for seamless diagonal tiling
            svg.AppendLine($@"  <g fill=""none"" stroke=""{fillColor}"" stroke-width=""3"" opacity=""0.1"">");

            // Diagonal lines from bottom-left to top-right
            for (double offset = -TileSize; offset <= TileSize * 2; offset += step)
            {
                svg.AppendLine($@"    <line x1=""{offset:F0}"" y1=""{TileSize}"" x2=""{offset + TileSize:F0}"" y2=""0""/>");
            }

            svg.AppendLine("  </g>");
        }

        private static void GenerateHoneycombSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double s = 10;
            double h = s * Math.Sqrt(3);
            double w = s * 2;

            svg.AppendLine($@"  <g fill=""none"" stroke=""{fillColor}"" stroke-width=""1.2"" opacity=""0.15"">");
            for (double y = -h; y < TileSize + h; y += h)
            {
                for (double x = -w; x < TileSize + w; x += s * 3)
                {
                    AppendHexSvg(svg, x, y, s);
                    AppendHexSvg(svg, x + s * 1.5, y + h * 0.5, s);
                }
            }
            svg.AppendLine("  </g>");
        }

        private static void AppendHexSvg(System.Text.StringBuilder svg, double x, double y, double s)
        {
            double h = s * Math.Sqrt(3);
            var points = $"{x:F1},{y + h * 0.5:F1} {x + s * 0.5:F1},{y:F1} {x + s * 1.5:F1},{y:F1} {x + s * 2:F1},{y + h * 0.5:F1} {x + s * 1.5:F1},{y + h:F1} {x + s * 0.5:F1},{y + h:F1}";
            svg.AppendLine($@"    <polygon points=""{points}""/>");
        }

        private static void GenerateCircuitSvg(System.Text.StringBuilder svg, string fillColor)
        {
            var rnd = new Random(42);
            double step = 30;

            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.15"">");
            for (double x = -step; x < TileSize + step; x += step)
            {
                for (double y = -step; y < TileSize + step; y += step)
                {
                    if (rnd.NextDouble() > 0.4)
                    {
                        svg.AppendLine($@"    <circle cx=""{x:F1}"" cy=""{y:F1}"" r=""1.2""/>");
                        double len = step * rnd.Next(1, 4);
                        if (rnd.NextDouble() > 0.5)
                            svg.AppendLine($@"    <rect x=""{x:F1}"" y=""{y:F1}"" width=""0.8"" height=""{len:F1}""/>");
                        else
                            svg.AppendLine($@"    <rect x=""{x:F1}"" y=""{y:F1}"" width=""{len:F1}"" height=""0.8""/>");
                    }
                }
            }
            svg.AppendLine("  </g>");
        }

        private static void GenerateDiamondPlateSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double step = 30;

            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.25"">");
            for (double x = -step; x < TileSize + step; x += step)
            {
                for (double y = -step; y < TileSize + step; y += step)
                {
                    int ix = (int)Math.Round(x / step);
                    int iy = (int)Math.Round(y / step);
                    bool alt = (ix + iy) % 2 == 0;

                    if (alt)
                    {
                        AppendDiamondSvg(svg, x + step * 0.2, y + step * 0.3, 15, 45);
                        AppendDiamondSvg(svg, x + step * 0.7, y + step * 0.8, 15, 135);
                    }
                }
            }
            svg.AppendLine("  </g>");
        }

        private static void AppendDiamondSvg(System.Text.StringBuilder svg, double cx, double cy, double length, double angle)
        {
            double rad = angle * Math.PI / 180;
            double w = length / 3;
            double h = length / 2;

            var p0 = RotateSvgPoint(0, -h, cx, cy, rad);
            var p1 = RotateSvgPoint(w, 0, cx, cy, rad);
            var p2 = RotateSvgPoint(0, h, cx, cy, rad);
            var p3 = RotateSvgPoint(-w, 0, cx, cy, rad);

            var points = $"{p0.x:F1},{p0.y:F1} {p1.x:F1},{p1.y:F1} {p2.x:F1},{p2.y:F1} {p3.x:F1},{p3.y:F1}";
            svg.AppendLine($@"    <polygon points=""{points}""/>");
        }

        private static (double x, double y) RotateSvgPoint(double px, double py, double cx, double cy, double rad)
        {
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            return (cx + (px * cos - py * sin), cy + (px * sin + py * cos));
        }

        // ============================================================
        // Grid: step=30, width 1.2, opacity 0.1
        // ============================================================
        private static void GenerateGridSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double step = 30;
            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.1"">");
            for (double x = step; x < TileSize; x += step)
            {
                svg.AppendLine($@"    <rect x=""{x:F1}"" y=""0"" width=""1.2"" height=""{TileSize:F1}""/>");
            }
            for (double y = step; y < TileSize; y += step)
            {
                svg.AppendLine($@"    <rect x=""0"" y=""{y:F1}"" width=""{TileSize:F1}"" height=""1.2""/>");
            }
            svg.AppendLine("  </g>");
        }

        // ============================================================
        // Noise: seeded random dots, radius 0.8, opacity 0.12
        // ============================================================
        private static void GenerateNoiseSvg(System.Text.StringBuilder svg, string fillColor)
        {
            var rnd = new Random(42); // Seeded for consistency
            int count = (int)(300 * (TileSize * TileSize) / (200 * 120)); // Scale count to tile size

            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.12"">");
            for (int i = 0; i < count; i++)
            {
                double x = rnd.NextDouble() * TileSize;
                double y = rnd.NextDouble() * TileSize;
                svg.AppendLine($@"    <circle cx=""{x:F1}"" cy=""{y:F1}"" r=""0.8""/>");
            }
            svg.AppendLine("  </g>");
        }

        // ============================================================
        // Twill: step=20, rotated -30°, opacity 0.12
        // ============================================================
        private static void GenerateTwillSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double step = 20;
            double margin = TileSize;

            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.12"" transform=""rotate(-30)"">");
            for (double x = -margin; x < TileSize + margin; x += step)
            {
                for (double y = -margin; y < TileSize + margin; y += step)
                {
                    bool alt = ((int)((x + y) / step)) % 3 == 0;
                    if (alt)
                    {
                        svg.AppendLine($@"    <rect x=""{x:F1}"" y=""{y:F1}"" width=""{step * 1.5:F1}"" height=""{step * 0.4:F1}""/>");
                    }
                }
            }
            svg.AppendLine("  </g>");
        }

        // ============================================================
        // Mesh: step=8, width 0.8, opacity 0.18
        // ============================================================
        private static void GenerateMeshSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double step = 8;
            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.18"">");
            for (double x = 0; x < TileSize; x += step)
            {
                svg.AppendLine($@"    <rect x=""{x:F1}"" y=""0"" width=""0.8"" height=""{TileSize:F1}""/>");
            }
            for (double y = 0; y < TileSize; y += step)
            {
                svg.AppendLine($@"    <rect x=""0"" y=""{y:F1}"" width=""{TileSize:F1}"" height=""0.8""/>");
            }
            svg.AppendLine("  </g>");
        }

        // ============================================================
        // Perforated: 24px grid = 5 columns/rows, centered circles
        // ============================================================
        private static void GeneratePerforatedSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double step = 24;
            double offset = 12; // Center circles in each cell

            // Main holes - opacity 0.2
            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.2"">");
            for (double x = offset; x < TileSize; x += step)
            {
                for (double y = offset; y < TileSize; y += step)
                {
                    svg.AppendLine($@"    <circle cx=""{x:F1}"" cy=""{y:F1}"" r=""8""/>");
                }
            }
            svg.AppendLine("  </g>");

            // Inner shadow - opacity 0.1, offset by 1px
            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.1"">");
            for (double x = offset; x < TileSize; x += step)
            {
                for (double y = offset; y < TileSize; y += step)
                {
                    svg.AppendLine($@"    <circle cx=""{x + 1:F1}"" cy=""{y + 1:F1}"" r=""6""/>");
                }
            }
            svg.AppendLine("  </g>");
        }

        // ============================================================
        // Bumps: step=24, radius 6, opacity 0.15
        // ============================================================
        private static void GenerateBumpsSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double step = 24;

            svg.AppendLine($@"  <g fill=""{fillColor}"" opacity=""0.15"">");
            for (double x = 0; x < TileSize + step; x += step)
            {
                for (double y = 0; y < TileSize + step; y += step)
                {
                    svg.AppendLine($@"    <circle cx=""{x:F1}"" cy=""{y:F1}"" r=""6""/>");
                }
            }
            svg.AppendLine("  </g>");
        }

        // ============================================================
        // Scales: half-circle arcs, stroke 1.2, opacity 0.15
        // Arc height matches row spacing so legs stop at next row's top
        // ============================================================
        private static void GenerateScalesSvg(System.Text.StringBuilder svg, string fillColor)
        {
            double sw = 32;
            double rowStep = 15; // Row spacing
            double arcHeight = rowStep; // Arc height = row spacing so legs meet next row's top

            svg.AppendLine($@"  <g fill=""none"" stroke=""{fillColor}"" stroke-width=""1.2"" opacity=""0.15"">");
            for (double y = 0; y < TileSize + rowStep; y += rowStep)
            {
                bool shift = ((int)Math.Round(y / rowStep)) % 2 == 0;
                double dx = shift ? sw / 2 : 0;

                for (double x = -sw; x < TileSize + sw; x += sw)
                {
                    double startX = x + dx - sw / 2;
                    double endX = x + dx + sw / 2;
                    svg.AppendLine($@"    <path d=""M {startX:F1},{y:F1} A {sw / 2:F1},{arcHeight:F1} 0 0 1 {endX:F1},{y:F1}""/>");
                }
            }
            svg.AppendLine("  </g>");
        }

        // ============================================================
        // CarbonFiber: Exact match to BuildCarbonFiberPattern
        // Original: tileSize=24, step=12, 3 layers with opacities 0.12, 0.08, 0.04
        // ============================================================
        private static void AddCarbonFiberTile(IBrush brush, Canvas tile)
        {
            var group1 = new GeometryGroup();
            var group2 = new GeometryGroup();
            var group3 = new GeometryGroup();

            double tileSize = 24;
            double step = tileSize / 2; // 12

            for (double x = -tileSize; x < TileSize + tileSize; x += step)
            {
                for (double y = -tileSize; y < TileSize + tileSize; y += step)
                {
                    bool altX = ((int)(x / step)) % 2 == 0;
                    bool altY = ((int)(y / step)) % 2 == 0;

                    if (altX ^ altY)
                    {
                        // Vertical weave segment
                        group1.Children!.Add(new RectangleGeometry { Rect = new Rect(x + 2, y, 2, step) });
                        group2.Children!.Add(new RectangleGeometry { Rect = new Rect(x + 5, y, 2, step) });
                        group3.Children!.Add(new RectangleGeometry { Rect = new Rect(x + 8, y, 2, step) });
                    }
                    else
                    {
                        // Horizontal weave segment
                        group1.Children!.Add(new RectangleGeometry { Rect = new Rect(x, y + 2, step, 2) });
                        group2.Children!.Add(new RectangleGeometry { Rect = new Rect(x, y + 5, step, 2) });
                        group3.Children!.Add(new RectangleGeometry { Rect = new Rect(x, y + 8, step, 2) });
                    }
                }
            }

            tile.Children!.Add(CreatePath(group1, brush, 0.12));
            tile.Children.Add(CreatePath(group2, brush, 0.08));
            tile.Children.Add(CreatePath(group3, brush, 0.04));
        }

        // ============================================================
        // Dots: Exact match to BuildDotsPattern
        // Original: step=20, start at 10, radius 1.5, opacity 0.15
        // ============================================================
        private static void AddDotsTile(IBrush brush, Canvas tile)
        {
            var group = new GeometryGroup();
            double step = 20;

            for (double x = 10; x < TileSize; x += step)
            {
                for (double y = 10; y < TileSize; y += step)
                {
                    group.Children.Add(new EllipseGeometry { Center = new Point(x, y), RadiusX = 1.5, RadiusY = 1.5 });
                }
            }

            tile.Children.Add(CreatePath(group, brush, 0.15));
        }

        // ============================================================
        // Stripes: Diagonal lines, step=30, stroke 3, opacity 0.1
        // ============================================================
        private static void AddStripesTile(IBrush brush, Canvas tile)
        {
            double step = 30; // Divides evenly into 120

            // Create diagonal lines from bottom-left to top-right
            for (double offset = -TileSize; offset <= TileSize * 2; offset += step)
            {
                var line = new Line
                {
                    StartPoint = new Point(offset, TileSize),
                    EndPoint = new Point(offset + TileSize, 0),
                    Stroke = brush,
                    StrokeThickness = 3,
                    Opacity = 0.1
                };
                tile.Children.Add(line);
            }
        }

        // ============================================================
        // Honeycomb: Exact match to BuildHoneycombPattern
        // Original: s=10, stroke 1.2, opacity 0.15
        // ============================================================
        private static void AddHoneycombTile(IBrush brush, Canvas tile)
        {
            var pg = new PathGeometry();
            double s = 10;
            double h = s * Math.Sqrt(3);
            double w = s * 2;

            for (double y = -h; y < TileSize + h; y += h)
            {
                for (double x = -w; x < TileSize + w; x += s * 3)
                {
                    AddHex(pg, x, y, s);
                    AddHex(pg, x + s * 1.5, y + h * 0.5, s);
                }
            }

            var path = new Path
            {
                Data = pg,
                Stroke = brush,
                StrokeThickness = 1.2,
                Opacity = 0.15,
                IsHitTestVisible = false
            };
            tile.Children!.Add(path);
        }

        private static void AddHex(PathGeometry pg, double x, double y, double s)
        {
            double h = s * Math.Sqrt(3);
            var fig = new PathFigure { StartPoint = new Point(x, y + h * 0.5), IsClosed = true };
            fig.Segments!.Add(new LineSegment { Point = new Point(x + s * 0.5, y) });
            fig.Segments.Add(new LineSegment { Point = new Point(x + s * 1.5, y) });
            fig.Segments.Add(new LineSegment { Point = new Point(x + s * 2, y + h * 0.5) });
            fig.Segments.Add(new LineSegment { Point = new Point(x + s * 1.5, y + h) });
            fig.Segments.Add(new LineSegment { Point = new Point(x + s * 0.5, y + h) });
            pg.Figures!.Add(fig);
        }

        // ============================================================
        // Circuit: Exact match to BuildCircuitPattern
        // Original: step=30, radius 1.2, line width 0.8, opacity 0.15
        // ============================================================
        private static void AddCircuitTile(IBrush brush, Canvas tile)
        {
            var group = new GeometryGroup();
            var rnd = new Random(42); // Seeded for consistency
            double step = 30;

            for (double x = -step; x < TileSize + step; x += step)
            {
                for (double y = -step; y < TileSize + step; y += step)
                {
                    if (rnd.NextDouble() > 0.4)
                    {
                        group.Children!.Add(new EllipseGeometry { Center = new Point(x, y), RadiusX = 1.2, RadiusY = 1.2 });

                        double len = step * rnd.Next(1, 4);
                        if (rnd.NextDouble() > 0.5)
                            group.Children.Add(new RectangleGeometry { Rect = new Rect(x, y, 0.8, len) });
                        else
                            group.Children.Add(new RectangleGeometry { Rect = new Rect(x, y, len, 0.8) });
                    }
                }
            }

            tile.Children!.Add(CreatePath(group, brush, 0.15));
        }

        // ============================================================
        // DiamondPlate: Exact match to BuildDiamondPlatePattern
        // Original: step=30, diamond length 15, opacity 0.25
        // ============================================================
        private static void AddDiamondPlateTile(IBrush brush, Canvas tile)
        {
            var pg = new PathGeometry();
            double step = 30;

            for (double x = -step; x < TileSize + step; x += step)
            {
                for (double y = -step; y < TileSize + step; y += step)
                {
                    int ix = (int)Math.Round(x / step);
                    int iy = (int)Math.Round(y / step);
                    bool alt = (ix + iy) % 2 == 0;

                    if (alt)
                    {
                        AddDiamond(pg, x + step * 0.2, y + step * 0.3, 15, 45);
                        AddDiamond(pg, x + step * 0.7, y + step * 0.8, 15, 135);
                    }
                }
            }

            tile.Children!.Add(CreatePath(pg, brush, 0.25));
        }

        private static void AddDiamond(PathGeometry pg, double x, double y, double length, double angle)
        {
            var fig = new PathFigure { IsClosed = true, IsFilled = true };
            double rad = angle * Math.PI / 180;
            double w = length / 3;
            double h = length / 2;

            Point[] pts = [
                RotatePoint(new Point(0, -h), x, y, rad),
                RotatePoint(new Point(w, 0), x, y, rad),
                RotatePoint(new Point(0, h), x, y, rad),
                RotatePoint(new Point(-w, 0), x, y, rad)
            ];

            fig.StartPoint = pts[0];
            fig.Segments!.Add(new LineSegment { Point = pts[1] });
            fig.Segments.Add(new LineSegment { Point = pts[2] });
            fig.Segments.Add(new LineSegment { Point = pts[3] });
            pg.Figures!.Add(fig);
        }

        private static Point RotatePoint(Point p, double cx, double cy, double rad)
        {
            double cos = Math.Cos(rad);
            double sin = Math.Sin(rad);
            return new Point(
                cx + (p.X * cos - p.Y * sin),
                cy + (p.X * sin + p.Y * cos)
            );
        }

        private static Path CreatePath(Geometry geometry, IBrush brush, double opacity)
        {
            return new Path
            {
                Data = geometry,
                Fill = brush,
                Opacity = opacity,
                IsHitTestVisible = false
            };
        }

        // ============================================================
        // Additional Canvas Tile Methods for all patterns
        // ============================================================

        private static void AddGridTile(IBrush brush, Canvas tile)
        {
            var group = new GeometryGroup();
            double step = 30;

            for (double x = step; x < TileSize; x += step)
                group.Children!.Add(new RectangleGeometry { Rect = new Rect(x, 0, 1.2, TileSize) });
            for (double y = step; y < TileSize; y += step)
                group.Children.Add(new RectangleGeometry { Rect = new Rect(0, y, TileSize, 1.2) });

            tile.Children!.Add(CreatePath(group, brush, 0.1));
        }

        private static void AddNoiseTile(IBrush brush, Canvas tile)
        {
            var group = new GeometryGroup();
            var rnd = new Random(42); // Seeded for consistency
            int count = (int)(300 * (TileSize * TileSize) / (200 * 120));

            for (int i = 0; i < count; i++)
            {
                double x = rnd.NextDouble() * TileSize;
                double y = rnd.NextDouble() * TileSize;
                group.Children!.Add(new EllipseGeometry { Center = new Point(x, y), RadiusX = 0.8, RadiusY = 0.8 });
            }

            tile.Children!.Add(CreatePath(group, brush, 0.12));
        }

        private static void AddTwillTile(IBrush brush, Canvas tile)
        {
            var group = new GeometryGroup();
            double step = 20;
            double margin = TileSize;

            for (double x = -margin; x < TileSize + margin; x += step)
            {
                for (double y = -margin; y < TileSize + margin; y += step)
                {
                    bool alt = ((int)((x + y) / step)) % 3 == 0;
                    if (alt)
                    {
                        group.Children!.Add(new RectangleGeometry { Rect = new Rect(x, y, step * 1.5, step * 0.4) });
                    }
                }
            }

            var path = CreatePath(group, brush, 0.12);
            path.RenderTransform = new RotateTransform { Angle = -30 };
            tile.Children!.Add(path);
        }

        private static void AddMeshTile(IBrush brush, Canvas tile)
        {
            var group = new GeometryGroup();
            double step = 8;

            for (double x = 0; x < TileSize; x += step)
                group.Children!.Add(new RectangleGeometry { Rect = new Rect(x, 0, 0.8, TileSize) });
            for (double y = 0; y < TileSize; y += step)
                group.Children.Add(new RectangleGeometry { Rect = new Rect(0, y, TileSize, 0.8) });

            tile.Children!.Add(CreatePath(group, brush, 0.18));
        }

        private static void AddPerforatedTile(IBrush brush, Canvas tile)
        {
            var group1 = new GeometryGroup();
            var group2 = new GeometryGroup();
            double step = 24;
            double offset = 12; // Center circles in each cell

            for (double x = offset; x < TileSize; x += step)
            {
                for (double y = offset; y < TileSize; y += step)
                {
                    // Main hole - r=8
                    group1.Children!.Add(new EllipseGeometry { Center = new Point(x, y), RadiusX = 8, RadiusY = 8 });
                    // Inner shadow - r=6, offset by 1px
                    group2.Children!.Add(new EllipseGeometry { Center = new Point(x + 1, y + 1), RadiusX = 6, RadiusY = 6 });
                }
            }

            tile.Children!.Add(CreatePath(group1, brush, 0.2));
            tile.Children.Add(CreatePath(group2, brush, 0.1));
        }

        private static void AddBumpsTile(IBrush brush, Canvas tile)
        {
            var group = new GeometryGroup();
            double step = 24;

            for (double x = 0; x < TileSize + step; x += step)
            {
                for (double y = 0; y < TileSize + step; y += step)
                {
                    group.Children!.Add(new EllipseGeometry { Center = new Point(x, y), RadiusX = 6, RadiusY = 6 });
                }
            }

            tile.Children!.Add(CreatePath(group, brush, 0.15));
        }

        private static void AddScalesTile(IBrush brush, Canvas tile)
        {
            var pg = new PathGeometry();
            double sw = 32;
            double rowStep = 15; // Row spacing
            double arcHeight = rowStep; // Arc height = row spacing so legs meet next row's top

            for (double y = 0; y < TileSize + rowStep; y += rowStep)
            {
                bool shift = ((int)Math.Round(y / rowStep)) % 2 == 0;
                double dx = shift ? sw / 2 : 0;

                for (double x = -sw; x < TileSize + sw; x += sw)
                {
                    AddScale(pg, x + dx, y, sw, arcHeight);
                }
            }

            var path = new Path
            {
                Data = pg,
                Stroke = brush,
                StrokeThickness = 1.2,
                Opacity = 0.15,
                IsHitTestVisible = false
            };
            tile.Children!.Add(path);
        }

        private static void AddScale(PathGeometry pg, double x, double y, double w, double h)
        {
            var fig = new PathFigure { StartPoint = new Point(x - w / 2, y), IsClosed = false };
            fig.Segments!.Add(new ArcSegment
            {
                Point = new Point(x + w / 2, y),
                Size = new Size(w / 2, h),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = false
            });
            pg.Figures!.Add(fig);
        }
    }
}
