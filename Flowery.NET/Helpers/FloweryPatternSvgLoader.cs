#if FLOWERY_SVG_PATTERNS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls.Shapes;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Flowery.Enums;
using SkiaSharp;
using Svg.Skia;

namespace Flowery.Helpers
{
    /// <summary>
    /// Draws the SVG pattern assets of <see cref="Controls.DaisyPatternedCard"/>. The SVGs are white with the opacity
    /// of the pattern, so Svg.Skia draws each one once per display scaling into a mask; the brush of the card fills
    /// the mask, as it colors the generated tiles. The loader runs on the UI thread.
    /// </summary>
    internal static class FloweryPatternSvgLoader
    {
        internal const double TileSize = 120;

        private static readonly Dictionary<(DaisyCardPattern Pattern, int Scale), Bitmap?> Masks = [];

        public static string? GetAssetPath(DaisyCardPattern pattern)
        {
            var fileName = GetPatternFileName(pattern);
            return fileName == null ? null : $"avares://Flowery.NET/Assets/Patterns/{fileName}.svg";
        }

        public static bool HasSvgAsset(DaisyCardPattern pattern) => GetPatternFileName(pattern) != null;

        /// <summary>
        /// Creates a rectangle of the given size that shows the pattern in <paramref name="brush"/>, or null when the
        /// asset does not load.
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        /// <param name="width">The width of the rectangle.</param>
        /// <param name="height">The height of the rectangle.</param>
        /// <param name="brush">The brush of the pattern.</param>
        /// <param name="renderScaling">The scaling of the display, which sets the resolution of the mask.</param>
        public static Rectangle? CreatePatternLayer(DaisyCardPattern pattern, double width, double height, IBrush brush,
            double renderScaling)
        {
            var mask = GetMask(pattern, renderScaling);
            if (mask == null) return null;

            return new Rectangle
            {
                Width = width,
                Height = height,
                Fill = brush,
                IsHitTestVisible = false,
                OpacityMask = new ImageBrush(mask)
                {
                    TileMode = TileMode.Tile,
                    Stretch = Stretch.Fill,
                    AlignmentX = AlignmentX.Left,
                    AlignmentY = AlignmentY.Top,
                    DestinationRect = new RelativeRect(0, 0, TileSize, TileSize, RelativeUnit.Absolute)
                }
            };
        }

        /// <summary>The whole-number scale the mask of a display scaling is drawn at; 1.5 takes the mask of 2.</summary>
        internal static int GetMaskScale(double renderScaling) => Math.Clamp((int)Math.Ceiling(renderScaling), 1, 4);

        internal static Bitmap? GetMask(DaisyCardPattern pattern, double renderScaling)
        {
            var scale = GetMaskScale(renderScaling);
            if (!Masks.TryGetValue((pattern, scale), out var mask))
            {
                mask = CreateMask(pattern, scale);
                Masks[(pattern, scale)] = mask;
            }
            return mask;
        }

        private static Bitmap? CreateMask(DaisyCardPattern pattern, int scale)
        {
            var assetPath = GetAssetPath(pattern);
            if (assetPath == null) return null;
            try
            {
                using var tile = RenderTile(assetPath, scale);
                if (tile == null)
                    return null;

                var pixels = tile.Width;
                var mask = new WriteableBitmap(new PixelSize(pixels, pixels), new Vector(96 * scale, 96 * scale),
                    PixelFormat.Bgra8888, AlphaFormat.Premul);
                using (var target = mask.Lock())
                {
                    var source = tile.Bytes;
                    var rowBytes = tile.RowBytes;
                    for (var row = 0; row < pixels; row++)
                        Marshal.Copy(source, row * rowBytes, target.Address + row * target.RowBytes, rowBytes);
                }
                return mask;
            }
            catch (Exception exception)
            {
                // Without the native SkiaSharp library, for example, the card draws the generated tiles.
                Avalonia.Logging.Logger.TryGet(Avalonia.Logging.LogEventLevel.Warning, "Flowery")?.Log(null,
                    "The pattern asset {Path} did not load: {Error}", assetPath, exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Draws one tile of an SVG asset with Svg.Skia into a premultiplied BGRA bitmap of
        /// <see cref="TileSize"/> times <paramref name="scale"/> pixels, or returns null when the SVG has no picture.
        /// </summary>
        /// <param name="assetPath">The avares URI of the SVG asset.</param>
        /// <param name="scale">The pixels per DIP.</param>
        internal static SKBitmap? RenderTile(string assetPath, int scale)
        {
            using var stream = AssetLoader.Open(new Uri(assetPath));
            using var svg = new SKSvg();
            if (svg.Load(stream) is not { } picture)
                return null;

            var pixels = (int)(TileSize * scale);
            var tile = new SKBitmap(new SKImageInfo(pixels, pixels, SKColorType.Bgra8888, SKAlphaType.Premul));
            using (var canvas = new SKCanvas(tile))
            {
                canvas.Clear(SKColors.Transparent);
                var bounds = picture.CullRect;
                if (bounds.Width > 0 && bounds.Height > 0)
                    canvas.Scale(pixels / bounds.Width, pixels / bounds.Height);
                canvas.DrawPicture(picture);
            }
            return tile;
        }

        private static string? GetPatternFileName(DaisyCardPattern pattern)
        {
            return pattern switch
            {
                DaisyCardPattern.CarbonFiber => "carbon_fiber",
                DaisyCardPattern.Dots => "dots",
                DaisyCardPattern.Grid => "grid",
                DaisyCardPattern.Stripes => "stripes",
                DaisyCardPattern.Noise => "noise",
                DaisyCardPattern.Honeycomb => "honeycomb",
                DaisyCardPattern.Circuit => "circuit",
                DaisyCardPattern.Twill => "twill",
                DaisyCardPattern.DiamondPlate => "diamond_plate",
                DaisyCardPattern.Mesh => "mesh",
                DaisyCardPattern.Perforated => "perforated",
                DaisyCardPattern.Bumps => "bumps",
                DaisyCardPattern.Scales => "scales",
                _ => null
            };
        }
    }
}
#endif
