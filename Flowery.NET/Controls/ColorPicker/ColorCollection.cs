using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Avalonia.Media;

namespace Flowery.Controls.ColorPicker
{
    /// <summary>
    /// Represents a collection of colors with support for standard color palettes.
    /// </summary>
    public class ColorCollection : IList<Color>, IReadOnlyList<Color>
    {
        private readonly List<Color> _colors;

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorCollection"/> class.
        /// </summary>
        public ColorCollection()
        {
            _colors = new List<Color>();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ColorCollection"/> class with the specified colors.
        /// </summary>
        public ColorCollection(IEnumerable<Color> colors)
        {
            _colors = new List<Color>(colors);
        }

        /// <summary>
        /// Occurs when the collection changes.
        /// </summary>
        public event EventHandler? CollectionChanged;

        /// <inheritdoc/>
        public int Count => _colors.Count;

        /// <inheritdoc/>
        public bool IsReadOnly => false;

        /// <inheritdoc/>
        public Color this[int index]
        {
            get => _colors[index];
            set
            {
                _colors[index] = value;
                OnCollectionChanged();
            }
        }

        /// <inheritdoc/>
        public void Add(Color item)
        {
            _colors.Add(item);
            OnCollectionChanged();
        }

        /// <summary>
        /// Adds a range of colors to the collection.
        /// </summary>
        public void AddRange(IEnumerable<Color> colors)
        {
            _colors.AddRange(colors);
            OnCollectionChanged();
        }

        /// <inheritdoc/>
        public void Clear()
        {
            _colors.Clear();
            OnCollectionChanged();
        }

        /// <inheritdoc/>
        public bool Contains(Color item)
        {
            return _colors.Contains(item);
        }

        /// <inheritdoc/>
        public void CopyTo(Color[] array, int arrayIndex)
        {
            _colors.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Finds the index of a color within a tolerance.
        /// </summary>
        public int Find(Color color, int tolerance = 0)
        {
            for (int i = 0; i < _colors.Count; i++)
            {
                var c = _colors[i];
                if (tolerance == 0)
                {
                    if (c.R == color.R && c.G == color.G && c.B == color.B && c.A == color.A)
                        return i;
                }
                else
                {
                    if (Math.Abs(c.R - color.R) <= tolerance &&
                        Math.Abs(c.G - color.G) <= tolerance &&
                        Math.Abs(c.B - color.B) <= tolerance &&
                        Math.Abs(c.A - color.A) <= tolerance)
                        return i;
                }
            }
            return -1;
        }

        /// <inheritdoc/>
        public IEnumerator<Color> GetEnumerator()
        {
            return _colors.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <inheritdoc/>
        public int IndexOf(Color item)
        {
            return _colors.IndexOf(item);
        }

        /// <inheritdoc/>
        public void Insert(int index, Color item)
        {
            _colors.Insert(index, item);
            OnCollectionChanged();
        }

        /// <inheritdoc/>
        public bool Remove(Color item)
        {
            var result = _colors.Remove(item);
            if (result) OnCollectionChanged();
            return result;
        }

        /// <inheritdoc/>
        public void RemoveAt(int index)
        {
            _colors.RemoveAt(index);
            OnCollectionChanged();
        }

        /// <summary>
        /// Sorts the collection using the specified comparer.
        /// </summary>
        public void Sort(IComparer<Color>? comparer = null)
        {
            _colors.Sort(comparer);
            OnCollectionChanged();
        }

        /// <summary>
        /// Converts the collection to an array.
        /// </summary>
        public Color[] ToArray()
        {
            return _colors.ToArray();
        }

        protected virtual void OnCollectionChanged()
        {
            CollectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Provides predefined color palettes.
    /// </summary>
    public static class ColorPalettes
    {
        private static ColorCollection? _namedColors;
        private static ColorCollection? _office2010;
        private static ColorCollection? _paint;
        private static ColorCollection? _webSafe;

        /// <summary>
        /// Gets a collection of standard named colors.
        /// </summary>
        public static ColorCollection NamedColors
        {
            get
            {
                if (_namedColors == null)
                {
                    _namedColors = new ColorCollection(new[]
                    {
                        Colors.AliceBlue, Colors.AntiqueWhite, Colors.Aqua, Colors.Aquamarine, Colors.Azure,
                        Colors.Beige, Colors.Bisque, Colors.Black, Colors.BlanchedAlmond, Colors.Blue,
                        Colors.BlueViolet, Colors.Brown, Colors.BurlyWood, Colors.CadetBlue, Colors.Chartreuse,
                        Colors.Chocolate, Colors.Coral, Colors.CornflowerBlue, Colors.Cornsilk, Colors.Crimson,
                        Colors.Cyan, Colors.DarkBlue, Colors.DarkCyan, Colors.DarkGoldenrod, Colors.DarkGray,
                        Colors.DarkGreen, Colors.DarkKhaki, Colors.DarkMagenta, Colors.DarkOliveGreen, Colors.DarkOrange,
                        Colors.DarkOrchid, Colors.DarkRed, Colors.DarkSalmon, Colors.DarkSeaGreen, Colors.DarkSlateBlue,
                        Colors.DarkSlateGray, Colors.DarkTurquoise, Colors.DarkViolet, Colors.DeepPink, Colors.DeepSkyBlue,
                        Colors.DimGray, Colors.DodgerBlue, Colors.Firebrick, Colors.FloralWhite, Colors.ForestGreen,
                        Colors.Fuchsia, Colors.Gainsboro, Colors.GhostWhite, Colors.Gold, Colors.Goldenrod,
                        Colors.Gray, Colors.Green, Colors.GreenYellow, Colors.Honeydew, Colors.HotPink,
                        Colors.IndianRed, Colors.Indigo, Colors.Ivory, Colors.Khaki, Colors.Lavender,
                        Colors.LavenderBlush, Colors.LawnGreen, Colors.LemonChiffon, Colors.LightBlue, Colors.LightCoral,
                        Colors.LightCyan, Colors.LightGoldenrodYellow, Colors.LightGray, Colors.LightGreen, Colors.LightPink,
                        Colors.LightSalmon, Colors.LightSeaGreen, Colors.LightSkyBlue, Colors.LightSlateGray, Colors.LightSteelBlue,
                        Colors.LightYellow, Colors.Lime, Colors.LimeGreen, Colors.Linen, Colors.Magenta,
                        Colors.Maroon, Colors.MediumAquamarine, Colors.MediumBlue, Colors.MediumOrchid, Colors.MediumPurple,
                        Colors.MediumSeaGreen, Colors.MediumSlateBlue, Colors.MediumSpringGreen, Colors.MediumTurquoise, Colors.MediumVioletRed,
                        Colors.MidnightBlue, Colors.MintCream, Colors.MistyRose, Colors.Moccasin, Colors.NavajoWhite,
                        Colors.Navy, Colors.OldLace, Colors.Olive, Colors.OliveDrab, Colors.Orange,
                        Colors.OrangeRed, Colors.Orchid, Colors.PaleGoldenrod, Colors.PaleGreen, Colors.PaleTurquoise,
                        Colors.PaleVioletRed, Colors.PapayaWhip, Colors.PeachPuff, Colors.Peru, Colors.Pink,
                        Colors.Plum, Colors.PowderBlue, Colors.Purple, Colors.Red, Colors.RosyBrown,
                        Colors.RoyalBlue, Colors.SaddleBrown, Colors.Salmon, Colors.SandyBrown, Colors.SeaGreen,
                        Colors.SeaShell, Colors.Sienna, Colors.Silver, Colors.SkyBlue, Colors.SlateBlue,
                        Colors.SlateGray, Colors.Snow, Colors.SpringGreen, Colors.SteelBlue, Colors.Tan,
                        Colors.Teal, Colors.Thistle, Colors.Tomato, Colors.Transparent, Colors.Turquoise,
                        Colors.Violet, Colors.Wheat, Colors.White, Colors.WhiteSmoke, Colors.Yellow,
                        Colors.YellowGreen
                    });
                }
                return _namedColors;
            }
        }

        /// <summary>
        /// Gets the Office 2010 color palette.
        /// </summary>
        public static ColorCollection Office2010
        {
            get
            {
                if (_office2010 == null)
                {
                    _office2010 = new ColorCollection(new[]
                    {
                        Color.FromRgb(255, 255, 255), Color.FromRgb(0, 0, 0), Color.FromRgb(238, 236, 225),
                        Color.FromRgb(31, 73, 125), Color.FromRgb(79, 129, 189), Color.FromRgb(192, 80, 77),
                        Color.FromRgb(155, 187, 89), Color.FromRgb(128, 100, 162), Color.FromRgb(75, 172, 198),
                        Color.FromRgb(247, 150, 70),
                        Color.FromRgb(242, 242, 242), Color.FromRgb(127, 127, 127), Color.FromRgb(221, 217, 195),
                        Color.FromRgb(198, 217, 240), Color.FromRgb(219, 229, 241), Color.FromRgb(242, 220, 219),
                        Color.FromRgb(235, 241, 221), Color.FromRgb(229, 224, 236), Color.FromRgb(219, 238, 243),
                        Color.FromRgb(253, 234, 218),
                        Color.FromRgb(216, 216, 216), Color.FromRgb(89, 89, 89), Color.FromRgb(196, 189, 151),
                        Color.FromRgb(141, 179, 226), Color.FromRgb(184, 204, 228), Color.FromRgb(229, 185, 183),
                        Color.FromRgb(215, 227, 188), Color.FromRgb(204, 193, 217), Color.FromRgb(183, 221, 232),
                        Color.FromRgb(251, 213, 181),
                        Color.FromRgb(191, 191, 191), Color.FromRgb(63, 63, 63), Color.FromRgb(147, 137, 83),
                        Color.FromRgb(84, 141, 212), Color.FromRgb(149, 179, 215), Color.FromRgb(217, 150, 148),
                        Color.FromRgb(195, 214, 155), Color.FromRgb(178, 162, 199), Color.FromRgb(146, 205, 220),
                        Color.FromRgb(250, 192, 143),
                        Color.FromRgb(165, 165, 165), Color.FromRgb(38, 38, 38), Color.FromRgb(73, 68, 41),
                        Color.FromRgb(23, 54, 93), Color.FromRgb(54, 96, 146), Color.FromRgb(149, 55, 52),
                        Color.FromRgb(118, 146, 60), Color.FromRgb(95, 73, 122), Color.FromRgb(49, 133, 156),
                        Color.FromRgb(227, 108, 9),
                        Color.FromRgb(127, 127, 127), Color.FromRgb(12, 12, 12), Color.FromRgb(29, 27, 16),
                        Color.FromRgb(15, 36, 62), Color.FromRgb(36, 64, 98), Color.FromRgb(99, 36, 35),
                        Color.FromRgb(79, 97, 40), Color.FromRgb(63, 49, 81), Color.FromRgb(32, 88, 103),
                        Color.FromRgb(151, 72, 6)
                    });
                }
                return _office2010;
            }
        }

        /// <summary>
        /// Gets the Paint.NET color palette.
        /// </summary>
        public static ColorCollection Paint
        {
            get
            {
                if (_paint == null)
                {
                    _paint = new ColorCollection(new[]
                    {
                        Color.FromRgb(0, 0, 0), Color.FromRgb(64, 64, 64), Color.FromRgb(128, 128, 128),
                        Color.FromRgb(192, 192, 192), Color.FromRgb(255, 255, 255), Color.FromRgb(128, 0, 0),
                        Color.FromRgb(255, 0, 0), Color.FromRgb(255, 128, 0), Color.FromRgb(255, 255, 0),
                        Color.FromRgb(128, 255, 0), Color.FromRgb(0, 255, 0), Color.FromRgb(0, 255, 128),
                        Color.FromRgb(0, 255, 255), Color.FromRgb(0, 128, 255), Color.FromRgb(0, 0, 255),
                        Color.FromRgb(128, 0, 255), Color.FromRgb(255, 0, 255), Color.FromRgb(255, 0, 128),
                        Color.FromRgb(128, 64, 64), Color.FromRgb(255, 128, 128), Color.FromRgb(255, 192, 128),
                        Color.FromRgb(255, 255, 128), Color.FromRgb(192, 255, 128), Color.FromRgb(128, 255, 128),
                        Color.FromRgb(128, 255, 192), Color.FromRgb(128, 255, 255), Color.FromRgb(128, 192, 255),
                        Color.FromRgb(128, 128, 255), Color.FromRgb(192, 128, 255), Color.FromRgb(255, 128, 255),
                        Color.FromRgb(255, 128, 192)
                    });
                }
                return _paint;
            }
        }

        /// <summary>
        /// Gets a web-safe color palette (216 colors).
        /// </summary>
        public static ColorCollection WebSafe
        {
            get
            {
                if (_webSafe == null)
                {
                    var colors = new List<Color>();
                    byte[] values = { 0, 51, 102, 153, 204, 255 };

                    foreach (byte r in values)
                    {
                        foreach (byte g in values)
                        {
                            foreach (byte b in values)
                            {
                                colors.Add(Color.FromRgb(r, g, b));
                            }
                        }
                    }

                    _webSafe = new ColorCollection(colors);
                }
                return _webSafe;
            }
        }

        /// <summary>
        /// Creates a grayscale palette with the specified number of shades.
        /// </summary>
        public static ColorCollection CreateGrayscale(int shades = 16)
        {
            var colors = new List<Color>();
            for (int i = 0; i < shades; i++)
            {
                byte value = (byte)(i * 255 / (shades - 1));
                colors.Add(Color.FromRgb(value, value, value));
            }
            return new ColorCollection(colors);
        }

        /// <summary>
        /// Creates an empty custom colors palette with the specified size.
        /// </summary>
        public static ColorCollection CreateCustom(int size = 16)
        {
            return new ColorCollection(Enumerable.Repeat(Colors.White, size));
        }
    }
}

