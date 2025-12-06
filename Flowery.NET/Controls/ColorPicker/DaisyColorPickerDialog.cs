using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Media;

namespace Flowery.Controls.ColorPicker
{
    /// <summary>
    /// A comprehensive color picker dialog similar to Cyotek ColorPickerDialog.
    /// Combines color wheel, color grid, sliders, and numeric editors.
    /// </summary>
    public class DaisyColorPickerDialog : Window
    {
        #region Layout Constants

        private const double DialogWidth = 680;
        private const double DialogHeight = 560;
        private const double DialogMinWidth = 600;
        private const double DialogMinHeight = 480;

        private const double ColorWheelSize = 180;
        private const double SliderHeight = 18;
        private const double ColorPreviewHeight = 22;
        private const double ButtonWidth = 80;

        private const double PanelSpacing = 8;
        private const double SmallSpacing = 5;
        private const double TinySpacing = 4;
        private const double LabelSpacing = 2;

        private const double DialogMargin = 10;
        private const double LeftPanelRightMargin = 12;

        private const int LabelFontSize = 11;
        private const int SmallFontSize = 10;

        private const int ColorGridColumns = 16;
        private const double ColorGridCellSize = 12;
        private const double ColorGridSpacing = 2;

        private static readonly Color BorderColor = Color.FromRgb(160, 160, 160);

        #endregion

        private bool _lockUpdates;

        // Template parts
        private DaisyColorWheel? _colorWheel;
        private DaisyColorGrid? _colorGrid;
        private DaisyColorEditor? _colorEditor;
        private DaisyScreenColorPicker? _screenColorPicker;
        private DaisyColorSlider? _lightnessSlider;
        private Border? _colorPreview;
        private Border? _originalColorPreview;
        private TextBlock? _originalColorHexText;
        private Button? _okButton;
        private Button? _cancelButton;

        #region Styled Properties

        /// <summary>
        /// Gets or sets the selected color.
        /// </summary>
        public static readonly StyledProperty<Color> ColorProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialog, Color>(nameof(Color), Colors.Red);

        public Color Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the original color (shown for comparison).
        /// </summary>
        public static readonly StyledProperty<Color> OriginalColorProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialog, Color>(nameof(OriginalColor), Colors.Red);

        public Color OriginalColor
        {
            get => GetValue(OriginalColorProperty);
            set => SetValue(OriginalColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the custom colors collection.
        /// </summary>
        public static readonly StyledProperty<ColorCollection?> CustomColorsProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialog, ColorCollection?>(nameof(CustomColors));

        public ColorCollection? CustomColors
        {
            get => GetValue(CustomColorsProperty);
            set => SetValue(CustomColorsProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the alpha channel controls.
        /// </summary>
        public static readonly StyledProperty<bool> ShowAlphaChannelProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialog, bool>(nameof(ShowAlphaChannel), true);

        public bool ShowAlphaChannel
        {
            get => GetValue(ShowAlphaChannelProperty);
            set => SetValue(ShowAlphaChannelProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the color wheel.
        /// </summary>
        public static readonly StyledProperty<bool> ShowColorWheelProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialog, bool>(nameof(ShowColorWheel), true);

        public bool ShowColorWheel
        {
            get => GetValue(ShowColorWheelProperty);
            set => SetValue(ShowColorWheelProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the color grid.
        /// </summary>
        public static readonly StyledProperty<bool> ShowColorGridProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialog, bool>(nameof(ShowColorGrid), true);

        public bool ShowColorGrid
        {
            get => GetValue(ShowColorGridProperty);
            set => SetValue(ShowColorGridProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the color editor.
        /// </summary>
        public static readonly StyledProperty<bool> ShowColorEditorProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialog, bool>(nameof(ShowColorEditor), true);

        public bool ShowColorEditor
        {
            get => GetValue(ShowColorEditorProperty);
            set => SetValue(ShowColorEditorProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the screen color picker.
        /// </summary>
        public static readonly StyledProperty<bool> ShowScreenColorPickerProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialog, bool>(nameof(ShowScreenColorPicker), true);

        public bool ShowScreenColorPicker
        {
            get => GetValue(ShowScreenColorPickerProperty);
            set => SetValue(ShowScreenColorPickerProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to preserve the alpha channel when selecting colors.
        /// </summary>
        public static readonly StyledProperty<bool> PreserveAlphaChannelProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialog, bool>(nameof(PreserveAlphaChannel), false);

        public bool PreserveAlphaChannel
        {
            get => GetValue(PreserveAlphaChannelProperty);
            set => SetValue(PreserveAlphaChannelProperty, value);
        }

        #endregion

        /// <summary>
        /// Occurs when the preview color changes.
        /// </summary>
        public event EventHandler<ColorChangedEventArgs>? PreviewColorChanged;

        static DaisyColorPickerDialog()
        {
            ColorProperty.Changed.AddClassHandler<DaisyColorPickerDialog>((x, e) => x.OnColorPropertyChanged(e));
        }

        public DaisyColorPickerDialog()
        {
            Title = "Color Picker";
            Width = DialogWidth;
            MinWidth = DialogMinWidth;
            MinHeight = DialogMinHeight;
            SizeToContent = SizeToContent.Height;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = true;

            CustomColors = ColorPalettes.CreateCustom(ColorGridColumns);

            InitializeComponent();
        }

        private void InitializeComponent()
        {
            var borderBrush = new SolidColorBrush(BorderColor);

            // Create the dialog content programmatically
            var mainGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Margin = new Thickness(DialogMargin),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            // Left panel - Color Wheel and Lightness slider
            var leftPanel = new StackPanel
            {
                Spacing = PanelSpacing,
                Margin = new Thickness(0, 0, LeftPanelRightMargin, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            _colorWheel = new DaisyColorWheel
            {
                Width = ColorWheelSize,
                Height = ColorWheelSize
            };
            _colorWheel.ColorChanged += OnColorWheelColorChanged;
            leftPanel.Children.Add(_colorWheel);

            // Lightness slider
            var lightnessPanel = new StackPanel { Spacing = TinySpacing };
            lightnessPanel.Children.Add(new TextBlock { Text = "Lightness", FontSize = LabelFontSize });
            _lightnessSlider = new DaisyColorSlider
            {
                Channel = ColorSliderChannel.Lightness,
                Width = ColorWheelSize,
                Height = SliderHeight
            };
            _lightnessSlider.ColorChanged += OnLightnessSliderChanged;
            lightnessPanel.Children.Add(_lightnessSlider);
            leftPanel.Children.Add(lightnessPanel);

            // Screen color picker
            _screenColorPicker = new DaisyScreenColorPicker
            {
                Margin = new Thickness(0, PanelSpacing, 0, 0)
            };
            _screenColorPicker.ColorChanged += OnScreenColorPickerColorChanged;
            leftPanel.Children.Add(_screenColorPicker);

            Grid.SetRow(leftPanel, 0);
            Grid.SetColumn(leftPanel, 0);
            mainGrid.Children.Add(leftPanel);

            // Right panel - Color Grid, Editor, and Preview
            var rightPanel = new StackPanel { Spacing = PanelSpacing, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };

            // Color preview
            var previewPanel = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                Height = ColorPreviewHeight + LabelFontSize + TinySpacing,
                Margin = new Thickness(0, 0, 0, TinySpacing)
            };

            var originalLabel = new TextBlock { Text = "Original", FontSize = SmallFontSize, Margin = new Thickness(0, 0, 0, LabelSpacing) };
            var newLabel = new TextBlock { Text = "New", FontSize = SmallFontSize, Margin = new Thickness(0, 0, 0, LabelSpacing) };

            var originalStack = new StackPanel();
            originalStack.Children.Add(originalLabel);
            _originalColorHexText = new TextBlock
            {
                FontSize = SmallFontSize,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
            };
            _originalColorPreview = new Border
            {
                Height = ColorPreviewHeight,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2),
                Child = _originalColorHexText
            };
            originalStack.Children.Add(_originalColorPreview);

            var newStack = new StackPanel();
            newStack.Children.Add(newLabel);
            _colorPreview = new Border
            {
                Height = ColorPreviewHeight,
                BorderBrush = borderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(2)
            };
            newStack.Children.Add(_colorPreview);

            Grid.SetColumn(originalStack, 0);
            Grid.SetColumn(newStack, 1);
            previewPanel.Children.Add(originalStack);
            previewPanel.Children.Add(newStack);
            rightPanel.Children.Add(previewPanel);

            // Color grid
            var gridLabel = new TextBlock { Text = "Color Palette", FontWeight = FontWeight.SemiBold, FontSize = LabelFontSize, Margin = new Thickness(0, 0, 0, LabelSpacing) };
            rightPanel.Children.Add(gridLabel);

            _colorGrid = new DaisyColorGrid
            {
                Palette = ColorPalettes.Paint,
                CustomColors = CustomColors,
                ShowCustomColors = true,
                CellSize = new Size(ColorGridCellSize, ColorGridCellSize),
                Columns = ColorGridColumns,
                Spacing = new Size(ColorGridSpacing, ColorGridSpacing)
            };
            _colorGrid.ColorChanged += OnColorGridColorChanged;
            rightPanel.Children.Add(_colorGrid);

            // Color editor
            var editorLabel = new TextBlock { Text = "Color Values", FontWeight = FontWeight.SemiBold, FontSize = LabelFontSize, Margin = new Thickness(0, TinySpacing, 0, LabelSpacing) };
            rightPanel.Children.Add(editorLabel);

            _colorEditor = new DaisyColorEditor
            {
                ShowAlphaChannel = ShowAlphaChannel,
                ShowHexInput = true,
                ShowRgbSliders = true,
                ShowHslSliders = true,
                Margin = new Thickness(0, 0, 0, SmallSpacing)
            };
            _colorEditor.ColorChanged += OnColorEditorColorChanged;
            rightPanel.Children.Add(_colorEditor);

            Grid.SetRow(rightPanel, 0);
            Grid.SetColumn(rightPanel, 1);
            mainGrid.Children.Add(rightPanel);

            // Button panel
            var buttonPanel = new StackPanel
            {
                Orientation = Avalonia.Layout.Orientation.Horizontal,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
                Spacing = PanelSpacing,
                Margin = new Thickness(0, SmallSpacing, 0, 0)
            };

            _okButton = new Button { Content = "OK", Width = ButtonWidth };
            _okButton.Click += OnOkButtonClick;
            buttonPanel.Children.Add(_okButton);

            _cancelButton = new Button { Content = "Cancel", Width = ButtonWidth };
            _cancelButton.Click += OnCancelButtonClick;
            buttonPanel.Children.Add(_cancelButton);

            Grid.SetRow(buttonPanel, 1);
            Grid.SetColumnSpan(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;

            // Initialize colors
            UpdateAllControls();
        }

        private void OnColorPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_lockUpdates) return;

            UpdateAllControls();
            OnPreviewColorChanged(new ColorChangedEventArgs((Color)e.NewValue!));
        }

        private void UpdateAllControls()
        {
            _lockUpdates = true;
            try
            {
                var color = Color;
                var hsl = new HslColor(color);

                if (_colorWheel != null)
                {
                    _colorWheel.Color = color;
                    _colorWheel.Lightness = hsl.L;
                }

                if (_lightnessSlider != null)
                {
                    _lightnessSlider.Color = color;
                }

                if (_colorGrid != null)
                {
                    _colorGrid.Color = color;
                }

                if (_colorEditor != null)
                {
                    _colorEditor.Color = color;
                }

                if (_colorPreview != null)
                {
                    _colorPreview.Background = new SolidColorBrush(color);
                }

                if (_originalColorPreview != null)
                {
                    _originalColorPreview.Background = new SolidColorBrush(OriginalColor);
                    if (_originalColorHexText != null)
                    {
                        _originalColorHexText.Text = $"#{OriginalColor.R:X2}{OriginalColor.G:X2}{OriginalColor.B:X2}";
                        // Set text color based on luminance for readability
                        var luminance = 0.299 * OriginalColor.R + 0.587 * OriginalColor.G + 0.114 * OriginalColor.B;
                        _originalColorHexText.Foreground = new SolidColorBrush(luminance > 128 ? Colors.Black : Colors.White);
                    }
                }
            }
            finally
            {
                _lockUpdates = false;
            }
        }

        private void OnColorWheelColorChanged(object? sender, ColorChangedEventArgs e)
        {
            if (_lockUpdates) return;
            Color = e.Color;
        }

        private void OnLightnessSliderChanged(object? sender, ColorChangedEventArgs e)
        {
            if (_lockUpdates || _lightnessSlider == null || _colorWheel == null) return;

            var hsl = new HslColor(e.Color);
            _colorWheel.Lightness = hsl.L;
            Color = e.Color;
        }

        private void OnColorGridColorChanged(object? sender, ColorChangedEventArgs e)
        {
            if (_lockUpdates) return;

            if (PreserveAlphaChannel)
            {
                Color = Color.FromArgb(Color.A, e.Color.R, e.Color.G, e.Color.B);
            }
            else
            {
                Color = e.Color;
            }
        }

        private void OnColorEditorColorChanged(object? sender, ColorChangedEventArgs e)
        {
            if (_lockUpdates) return;
            Color = e.Color;
        }

        private void OnScreenColorPickerColorChanged(object? sender, ColorChangedEventArgs e)
        {
            if (_lockUpdates) return;
            Color = e.Color;
        }

        private void OnOkButtonClick(object? sender, RoutedEventArgs e)
        {
            // Add current color to custom colors
            if (_colorGrid != null && CustomColors != null)
            {
                _colorGrid.AddCustomColor(Color);
            }

            Close(Color);
        }

        private void OnCancelButtonClick(object? sender, RoutedEventArgs e)
        {
            Close(null);
        }

        protected virtual void OnPreviewColorChanged(ColorChangedEventArgs e)
        {
            PreviewColorChanged?.Invoke(this, e);
        }

        /// <summary>
        /// Shows the color picker dialog and returns the selected color.
        /// </summary>
        /// <param name="owner">The owner window.</param>
        /// <param name="initialColor">The initial color to display.</param>
        /// <returns>The selected color, or null if cancelled.</returns>
        public static async System.Threading.Tasks.Task<Color?> ShowDialogAsync(Window owner, Color initialColor)
        {
            var dialog = new DaisyColorPickerDialog
            {
                Color = initialColor,
                OriginalColor = initialColor
            };

            var result = await dialog.ShowDialog<Color?>(owner);
            return result;
        }

        /// <summary>
        /// Shows the color picker dialog with custom options.
        /// </summary>
        public static async System.Threading.Tasks.Task<Color?> ShowDialogAsync(
            Window owner,
            Color initialColor,
            bool showAlphaChannel = true,
            ColorCollection? customColors = null)
        {
            var dialog = new DaisyColorPickerDialog
            {
                Color = initialColor,
                OriginalColor = initialColor,
                ShowAlphaChannel = showAlphaChannel
            };

            if (customColors != null)
            {
                dialog.CustomColors = customColors;
            }

            var result = await dialog.ShowDialog<Color?>(owner);
            return result;
        }
    }
}

