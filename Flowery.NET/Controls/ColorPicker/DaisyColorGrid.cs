using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;

namespace Flowery.Controls.ColorPicker
{
    /// <summary>
    /// A grid control for displaying and selecting colors from a palette.
    /// </summary>
    public class DaisyColorGrid : Control
    {
        protected override Type StyleKeyOverride => typeof(DaisyColorGrid);

        private readonly Dictionary<int, Rect> _colorRegions = new();
        private int _hotIndex = -1;
        private int _previousHotIndex = -1;

        #region Styled Properties

        /// <summary>
        /// Gets or sets the currently selected color.
        /// </summary>
        public static readonly StyledProperty<Color> ColorProperty =
            AvaloniaProperty.Register<DaisyColorGrid, Color>(nameof(Color), Colors.Black);

        public Color Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the index of the selected color in the palette.
        /// </summary>
        public static readonly StyledProperty<int> ColorIndexProperty =
            AvaloniaProperty.Register<DaisyColorGrid, int>(nameof(ColorIndex), -1);

        public int ColorIndex
        {
            get => GetValue(ColorIndexProperty);
            set => SetValue(ColorIndexProperty, value);
        }

        /// <summary>
        /// Gets or sets the color collection to display.
        /// </summary>
        public static readonly StyledProperty<ColorCollection?> PaletteProperty =
            AvaloniaProperty.Register<DaisyColorGrid, ColorCollection?>(nameof(Palette));

        public ColorCollection? Palette
        {
            get => GetValue(PaletteProperty);
            set => SetValue(PaletteProperty, value);
        }

        /// <summary>
        /// Gets or sets the custom colors collection.
        /// </summary>
        public static readonly StyledProperty<ColorCollection?> CustomColorsProperty =
            AvaloniaProperty.Register<DaisyColorGrid, ColorCollection?>(nameof(CustomColors));

        public ColorCollection? CustomColors
        {
            get => GetValue(CustomColorsProperty);
            set => SetValue(CustomColorsProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the custom colors section.
        /// </summary>
        public static readonly StyledProperty<bool> ShowCustomColorsProperty =
            AvaloniaProperty.Register<DaisyColorGrid, bool>(nameof(ShowCustomColors), true);

        public bool ShowCustomColors
        {
            get => GetValue(ShowCustomColorsProperty);
            set => SetValue(ShowCustomColorsProperty, value);
        }

        /// <summary>
        /// Gets or sets the size of each color cell.
        /// </summary>
        public static readonly StyledProperty<Size> CellSizeProperty =
            AvaloniaProperty.Register<DaisyColorGrid, Size>(nameof(CellSize), new Size(16, 16));

        public Size CellSize
        {
            get => GetValue(CellSizeProperty);
            set => SetValue(CellSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the spacing between cells.
        /// </summary>
        public static readonly StyledProperty<Size> SpacingProperty =
            AvaloniaProperty.Register<DaisyColorGrid, Size>(nameof(Spacing), new Size(3, 3));

        public Size Spacing
        {
            get => GetValue(SpacingProperty);
            set => SetValue(SpacingProperty, value);
        }

        /// <summary>
        /// Gets or sets the number of columns in the grid.
        /// </summary>
        public static readonly StyledProperty<int> ColumnsProperty =
            AvaloniaProperty.Register<DaisyColorGrid, int>(nameof(Columns), 16);

        public int Columns
        {
            get => GetValue(ColumnsProperty);
            set => SetValue(ColumnsProperty, Math.Max(1, value));
        }

        /// <summary>
        /// Gets or sets the cell border color.
        /// </summary>
        public static readonly StyledProperty<Color> CellBorderColorProperty =
            AvaloniaProperty.Register<DaisyColorGrid, Color>(nameof(CellBorderColor), Color.FromRgb(160, 160, 160));

        public Color CellBorderColor
        {
            get => GetValue(CellBorderColorProperty);
            set => SetValue(CellBorderColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the selection border color.
        /// </summary>
        public static readonly StyledProperty<Color> SelectionBorderColorProperty =
            AvaloniaProperty.Register<DaisyColorGrid, Color>(nameof(SelectionBorderColor), Color.FromRgb(0, 120, 215));

        public Color SelectionBorderColor
        {
            get => GetValue(SelectionBorderColorProperty);
            set => SetValue(SelectionBorderColorProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to automatically add selected colors to custom colors.
        /// </summary>
        public static readonly StyledProperty<bool> AutoAddColorsProperty =
            AvaloniaProperty.Register<DaisyColorGrid, bool>(nameof(AutoAddColors), true);

        public bool AutoAddColors
        {
            get => GetValue(AutoAddColorsProperty);
            set => SetValue(AutoAddColorsProperty, value);
        }

        /// <summary>
        /// Gets or sets an optional callback invoked when the color changes.
        /// This provides a simpler alternative to the ColorChanged event.
        /// </summary>
        public static readonly StyledProperty<Action<Color>?> OnColorChangedProperty =
            AvaloniaProperty.Register<DaisyColorGrid, Action<Color>?>(nameof(OnColorChanged));

        public Action<Color>? OnColorChanged
        {
            get => GetValue(OnColorChangedProperty);
            set => SetValue(OnColorChangedProperty, value);
        }

        #endregion

        /// <summary>
        /// Occurs when the selected color changes.
        /// </summary>
        public event EventHandler<ColorChangedEventArgs>? ColorChanged;

        static DaisyColorGrid()
        {
            AffectsRender<DaisyColorGrid>(
                ColorProperty,
                ColorIndexProperty,
                PaletteProperty,
                CustomColorsProperty,
                ShowCustomColorsProperty,
                CellSizeProperty,
                SpacingProperty,
                ColumnsProperty,
                CellBorderColorProperty,
                SelectionBorderColorProperty);

            AffectsMeasure<DaisyColorGrid>(
                PaletteProperty,
                CustomColorsProperty,
                ShowCustomColorsProperty,
                CellSizeProperty,
                SpacingProperty,
                ColumnsProperty);

            ColorProperty.Changed.AddClassHandler<DaisyColorGrid>((x, e) => x.OnColorPropertyChanged(e));
            ColorIndexProperty.Changed.AddClassHandler<DaisyColorGrid>((x, e) => x.OnColorIndexPropertyChanged(e));
        }

        public DaisyColorGrid()
        {
            Palette = ColorPalettes.Paint;
            CustomColors = ColorPalettes.CreateCustom(16);
            Cursor = new Cursor(StandardCursorType.Hand);
        }

        private void OnColorPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var color = (Color)e.NewValue!;
            var index = FindColorIndex(color);
            if (index != ColorIndex)
            {
                ColorIndex = index;
            }
            OnColorChangedRaised(new ColorChangedEventArgs(color));
        }

        private void OnColorIndexPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            var index = (int)e.NewValue!;
            if (index >= 0)
            {
                var color = GetColorAtIndex(index);
                if (color != null && color != Color)
                {
                    Color = color.Value;
                }
            }
            InvalidateVisual();
        }

        private int FindColorIndex(Color color)
        {
            int index = 0;

            if (Palette != null)
            {
                var found = Palette.Find(color, 0);
                if (found >= 0) return found;
                index = Palette.Count;
            }

            if (ShowCustomColors && CustomColors != null)
            {
                var found = CustomColors.Find(color, 0);
                if (found >= 0) return index + found;
            }

            return -1;
        }

        private Color? GetColorAtIndex(int index)
        {
            if (Palette != null)
            {
                if (index < Palette.Count)
                    return Palette[index];
                index -= Palette.Count;
            }

            if (ShowCustomColors && CustomColors != null && index < CustomColors.Count)
            {
                return CustomColors[index];
            }

            return null;
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            var totalColors = GetTotalColorCount();
            if (totalColors == 0) return new Size(0, 0);

            var rows = (int)Math.Ceiling((double)totalColors / Columns);
            var width = Columns * (CellSize.Width + Spacing.Width) - Spacing.Width;
            var height = rows * (CellSize.Height + Spacing.Height) - Spacing.Height;

            return new Size(width, height);
        }

        private int GetTotalColorCount()
        {
            var count = Palette?.Count ?? 0;
            if (ShowCustomColors && CustomColors != null)
                count += CustomColors.Count;
            return count;
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            _colorRegions.Clear();

            var cellPen = new Pen(new SolidColorBrush(CellBorderColor), 1);
            var selectionPen = new Pen(new SolidColorBrush(SelectionBorderColor), 2);
            var hotPen = new Pen(new SolidColorBrush(Color.FromRgb(0, 0, 0)), 1);

            int index = 0;
            double x = 0;
            double y = 0;

            // Draw main colors
            if (Palette != null)
            {
                foreach (var color in Palette)
                {
                    DrawColorCell(context, index, color, x, y, cellPen, selectionPen, hotPen);
                    index++;
                    x += CellSize.Width + Spacing.Width;
                    if (index % Columns == 0)
                    {
                        x = 0;
                        y += CellSize.Height + Spacing.Height;
                    }
                }
            }

            // Draw custom colors
            if (ShowCustomColors && CustomColors != null)
            {
                foreach (var color in CustomColors)
                {
                    DrawColorCell(context, index, color, x, y, cellPen, selectionPen, hotPen);
                    index++;
                    x += CellSize.Width + Spacing.Width;
                    if (index % Columns == 0)
                    {
                        x = 0;
                        y += CellSize.Height + Spacing.Height;
                    }
                }
            }
        }

        private void DrawColorCell(DrawingContext context, int index, Color color, double x, double y,
            Pen cellPen, Pen selectionPen, Pen hotPen)
        {
            var rect = new Rect(x, y, CellSize.Width, CellSize.Height);
            _colorRegions[index] = rect;

            // Draw checkerboard pattern for transparent colors
            if (color.A < 255)
            {
                DrawCheckerboard(context, rect);
            }

            // Fill with color
            context.FillRectangle(new SolidColorBrush(color), rect);

            // Draw border
            if (index == ColorIndex)
            {
                context.DrawRectangle(selectionPen, rect.Inflate(-1));
            }
            else if (index == _hotIndex)
            {
                context.DrawRectangle(hotPen, rect);
            }
            else
            {
                context.DrawRectangle(cellPen, rect);
            }
        }

        private void DrawCheckerboard(DrawingContext context, Rect rect)
        {
            var checkSize = 4;
            var light = new SolidColorBrush(Colors.White);
            var dark = new SolidColorBrush(Color.FromRgb(204, 204, 204));

            using (context.PushClip(rect))
            {
                for (int cy = 0; cy < rect.Height; cy += checkSize)
                {
                    for (int cx = 0; cx < rect.Width; cx += checkSize)
                    {
                        var brush = ((cx / checkSize + cy / checkSize) % 2 == 0) ? light : dark;
                        context.FillRectangle(brush, new Rect(rect.X + cx, rect.Y + cy, checkSize, checkSize));
                    }
                }
            }
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);

            var point = e.GetPosition(this);
            var newHotIndex = GetColorIndexAtPoint(point);

            if (newHotIndex != _hotIndex)
            {
                _previousHotIndex = _hotIndex;
                _hotIndex = newHotIndex;
                InvalidateVisual();
            }
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);

            if (_hotIndex != -1)
            {
                _previousHotIndex = _hotIndex;
                _hotIndex = -1;
                InvalidateVisual();
            }
        }

        protected override void OnPointerPressed(PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                var point = e.GetPosition(this);
                var index = GetColorIndexAtPoint(point);

                if (index >= 0)
                {
                    ColorIndex = index;
                    var color = GetColorAtIndex(index);
                    if (color.HasValue)
                    {
                        Color = color.Value;
                    }
                }
            }
        }

        private int GetColorIndexAtPoint(Point point)
        {
            foreach (var kvp in _colorRegions)
            {
                if (kvp.Value.Contains(point))
                    return kvp.Key;
            }
            return -1;
        }

        /// <summary>
        /// Adds a color to the custom colors collection.
        /// </summary>
        public void AddCustomColor(Color color)
        {
            if (CustomColors == null)
                CustomColors = ColorPalettes.CreateCustom(16);

            // Shift colors and add new one at the beginning
            for (int i = CustomColors.Count - 1; i > 0; i--)
            {
                CustomColors[i] = CustomColors[i - 1];
            }
            if (CustomColors.Count > 0)
            {
                CustomColors[0] = color;
            }
            else
            {
                CustomColors.Add(color);
            }

            InvalidateVisual();
        }

        protected virtual void OnColorChangedRaised(ColorChangedEventArgs e)
        {
            ColorChanged?.Invoke(this, e);
            OnColorChanged?.Invoke(e.Color);
        }
    }
}

