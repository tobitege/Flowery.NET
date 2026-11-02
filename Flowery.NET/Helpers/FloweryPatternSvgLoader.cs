#if FLOWERY_SVG_PATTERNS
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Flowery.Controls;
using Flowery.Enums;
using SkiaSharp;
using Svg.Skia;

namespace Flowery.Helpers
{
    /// <summary>
    /// Loads the SVG pattern assets of <see cref="DaisyPatternedCard"/> and tiles them over a card. Each SVG is drawn
    /// once per pattern, color and display scaling into a bitmap with Svg.Skia; light themes use the black assets,
    /// dark themes the white ones.
    /// </summary>
    internal static class FloweryPatternSvgLoader
    {
        internal const double TileSize = 120;

        private static readonly object CacheLock = new();
        private static readonly Dictionary<(string Path, int Scale), Bitmap?> Cache = [];

        public static string? GetAssetPath(DaisyCardPattern pattern)
        {
            var fileName = GetPatternFileName(pattern);
            if (fileName == null) return null;

            // Select color based on theme - use white for dark themes, black for light themes
            var colorFolder = IsDarkTheme() ? "white" : "black";

            return $"avares://Flowery.NET/Assets/Patterns/{colorFolder}/{fileName}.svg";
        }

        public static bool HasSvgAsset(DaisyCardPattern pattern)
        {
            return pattern != DaisyCardPattern.None && GetPatternFileName(pattern) != null;
        }

        /// <summary>
        /// Creates a canvas of the given size covered with tiles of the pattern, or null when the asset does not load.
        /// </summary>
        /// <param name="pattern">The pattern.</param>
        /// <param name="width">The width of the canvas.</param>
        /// <param name="height">The height of the canvas.</param>
        /// <param name="renderScaling">The scaling of the display, which sets the resolution of the tiles.</param>
        public static Canvas? CreateTiledCanvas(DaisyCardPattern pattern, double width, double height, double renderScaling)
        {
            var assetPath = GetAssetPath(pattern);
            if (assetPath == null) return null;
            var bitmap = GetBitmap(assetPath, renderScaling);
            if (bitmap == null) return null;

            int tilesX = (int)Math.Ceiling(width / TileSize) + 1;
            int tilesY = (int)Math.Ceiling(height / TileSize) + 1;

            var canvas = new Canvas
            {
                Width = width,
                Height = height,
                IsHitTestVisible = false
            };

            for (int tx = 0; tx < tilesX; tx++)
            {
                for (int ty = 0; ty < tilesY; ty++)
                {
                    var image = new Image
                    {
                        Source = bitmap,
                        Width = TileSize,
                        Height = TileSize,
                        Stretch = Stretch.Fill,
                        IsHitTestVisible = false
                    };

                    Canvas.SetLeft(image, tx * TileSize);
                    Canvas.SetTop(image, ty * TileSize);
                    canvas.Children.Add(image);
                }
            }

            return canvas;
        }

        private static Bitmap? GetBitmap(string assetPath, double renderScaling)
        {
            // Whole scales keep the cache small; a tile drawn at 2 stays sharp at 1.5.
            var scale = Math.Clamp((int)Math.Ceiling(renderScaling), 1, 4);
            lock (CacheLock)
            {
                if (!Cache.TryGetValue((assetPath, scale), out var bitmap))
                {
                    bitmap = Render(assetPath, scale);
                    Cache[(assetPath, scale)] = bitmap;
                }
                return bitmap;
            }
        }

        private static Bitmap? Render(string assetPath, int scale)
        {
            try
            {
                using var tile = RenderTile(assetPath, scale);
                if (tile == null)
                    return null;

                var pixels = tile.Width;
                var bitmap = new WriteableBitmap(new PixelSize(pixels, pixels), new Vector(96 * scale, 96 * scale),
                    PixelFormat.Bgra8888, AlphaFormat.Premul);
                using (var target = bitmap.Lock())
                {
                    var source = tile.Bytes;
                    var rowBytes = tile.RowBytes;
                    for (var row = 0; row < pixels; row++)
                        Marshal.Copy(source, row * rowBytes, target.Address + row * target.RowBytes, rowBytes);
                }
                return bitmap;
            }
            catch (Exception exception)
            {
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

        private static bool IsDarkTheme()
        {
            // The current theme as drawn: a base override can make it the other kind.
            if (!string.IsNullOrEmpty(DaisyThemeManager.CurrentThemeName))
                return DaisyThemeManager.IsCurrentThemeDark;

            // Fallback: the base background of the application
            if (Application.Current?.TryGetResource("DaisyBase100Brush", null, out var brush) == true &&
                brush is ISolidColorBrush solid)
            {
                var luminance = (0.299 * solid.Color.R + 0.587 * solid.Color.G + 0.114 * solid.Color.B) / 255;
                return luminance < 0.5;
            }

            return false; // Default to light theme
        }
    }
}
#endif
