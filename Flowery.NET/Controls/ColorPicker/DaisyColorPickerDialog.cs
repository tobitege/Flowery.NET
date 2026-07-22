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
        #region Layout Constants (shared with DaisyColorPickerDialogContent)

        internal const double DialogWidth = 680;
        internal const double DialogHeight = 560;
        internal const double DialogMinWidth = 600;
        internal const double DialogMinHeight = 480;

        internal const double ColorWheelSize = 180;
        internal const double SliderHeight = 18;
        internal const double ColorPreviewHeight = 22;
        internal const double ButtonWidth = 80;

        internal const double PanelSpacing = 8;
        internal const double SmallSpacing = 5;
        internal const double TinySpacing = 4;
        internal const double LabelSpacing = 2;

        internal const double DialogMargin = 10;
        internal const double LeftPanelRightMargin = 12;

        internal const int LabelFontSize = 11;
        internal const int SmallFontSize = 10;

        internal const int ColorGridColumns = 16;
        internal const double ColorGridCellSize = 12;
        internal const double ColorGridSpacing = 2;

        internal static readonly Color BorderColor = Color.FromRgb(160, 160, 160);

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
        private DaisyButton? _okButton;
        private DaisyButton? _cancelButton;

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

            _okButton = new DaisyButton
            {
                Content = "OK",
                Variant = DaisyButtonVariant.Primary,
                Size = DaisySize.Small,
                MinWidth = ButtonWidth
            };
            _okButton.Click += OnOkButtonClick;
            buttonPanel.Children.Add(_okButton);

            _cancelButton = new DaisyButton
            {
                Content = "Cancel",
                Variant = DaisyButtonVariant.Primary,
                Size = DaisySize.Small,
                MinWidth = ButtonWidth
            };
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

        protected override void OnKeyDown(Avalonia.Input.KeyEventArgs e)
        {
            base.OnKeyDown(e);

            if (e.Key == Avalonia.Input.Key.Escape)
            {
                e.Handled = true;
                Close(null);
            }
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

        /// <summary>
        /// Shows the color picker dialog using an overlay (works in Browser/WASM).
        /// </summary>
        /// <param name="target">Any visual element to get the overlay layer from.</param>
        /// <param name="initialColor">The initial color to display.</param>
        /// <returns>The selected color, or null if cancelled.</returns>
        public static async System.Threading.Tasks.Task<Color?> ShowOverlayAsync(Visual target, Color initialColor)
        {
            var overlayLayer = OverlayLayer.GetOverlayLayer(target);
            if (overlayLayer == null)
                throw new InvalidOperationException("No overlay layer found for the target visual");

            var tcs = new System.Threading.Tasks.TaskCompletionSource<Color?>();

            // Create the dialog content (reuse the same UI building logic)
            var picker = new DaisyColorPickerDialogContent
            {
                Color = initialColor,
                OriginalColor = initialColor
            };

            // Create modal overlay - use same dimensions as Windows dialog
            // OverlayLayer is a Canvas, so we need to size backdrop to fill it
            var dialogBox = new Border
            {
                Background = Application.Current?.FindResource("DaisyBase100Brush") as IBrush
                             ?? new SolidColorBrush(Colors.White),
                CornerRadius = new CornerRadius(8),
                Width = DialogWidth,
                MinWidth = DialogMinWidth,
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                BoxShadow = new BoxShadows(new BoxShadow { Blur = 25, Color = Color.FromArgb(128, 0, 0, 0) }),
                Child = picker
            };

            var innerGrid = new Grid { Children = { dialogBox } };

            var backdrop = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(160, 0, 0, 0)),
                Width = overlayLayer.Bounds.Width,
                Height = overlayLayer.Bounds.Height,
                Child = innerGrid
            };

            // Update backdrop size when overlay layer resizes
            void UpdateBackdropSize(object? sender, EventArgs e)
            {
                backdrop.Width = overlayLayer.Bounds.Width;
                backdrop.Height = overlayLayer.Bounds.Height;
            }
            overlayLayer.LayoutUpdated += UpdateBackdropSize;

            void CloseDialog(Color? result)
            {
                overlayLayer.LayoutUpdated -= UpdateBackdropSize;
                overlayLayer.Children.Remove(backdrop);
                tcs.TrySetResult(result);
            }

            picker.OkClicked += (s, e) => CloseDialog(picker.Color);
            picker.CancelClicked += (s, e) => CloseDialog(null);

            // Handle backdrop click to cancel (click on dark area outside dialog)
            backdrop.PointerPressed += (s, e) =>
            {
                // Only close if clicking the backdrop itself, not the dialog
                if (e.Source == backdrop || e.Source == innerGrid)
                {
                    CloseDialog(null);
                }
            };

            overlayLayer.Children.Add(backdrop);

            return await tcs.Task;
        }
    }

    /// <summary>
    /// The content panel for the color picker dialog, usable in both Window and overlay contexts.
    /// </summary>
    internal class DaisyColorPickerDialogContent : UserControl
    {
        // Use constants from DaisyColorPickerDialog
        private static double ColorWheelSize => DaisyColorPickerDialog.ColorWheelSize;
        private static double SliderHeight => DaisyColorPickerDialog.SliderHeight;
        private static double ColorPreviewHeight => DaisyColorPickerDialog.ColorPreviewHeight;
        private static double ButtonWidth => DaisyColorPickerDialog.ButtonWidth;
        private static double PanelSpacing => DaisyColorPickerDialog.PanelSpacing;
        private static double SmallSpacing => DaisyColorPickerDialog.SmallSpacing;
        private static double TinySpacing => DaisyColorPickerDialog.TinySpacing;
        private static double LabelSpacing => DaisyColorPickerDialog.LabelSpacing;
        private static double DialogMargin => DaisyColorPickerDialog.DialogMargin;
        private static double LeftPanelRightMargin => DaisyColorPickerDialog.LeftPanelRightMargin;
        private static int LabelFontSize => DaisyColorPickerDialog.LabelFontSize;
        private static int SmallFontSize => DaisyColorPickerDialog.SmallFontSize;
        private static int ColorGridColumns => DaisyColorPickerDialog.ColorGridColumns;
        private static double ColorGridCellSize => DaisyColorPickerDialog.ColorGridCellSize;
        private static double ColorGridSpacing => DaisyColorPickerDialog.ColorGridSpacing;
        private static Color BorderColor => DaisyColorPickerDialog.BorderColor;

        private bool _lockUpdates;
        private DaisyColorWheel? _colorWheel;
        private DaisyColorGrid? _colorGrid;
        private DaisyColorEditor? _colorEditor;
        private DaisyScreenColorPicker? _screenColorPicker;
        private DaisyColorSlider? _lightnessSlider;
        private Border? _colorPreview;
        private Border? _originalColorPreview;
        private TextBlock? _originalColorHexText;

        public event EventHandler? OkClicked;
        public event EventHandler? CancelClicked;

        public static readonly StyledProperty<Color> ColorProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialogContent, Color>(nameof(Color), Colors.Red);

        public Color Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public static readonly StyledProperty<Color> OriginalColorProperty =
            AvaloniaProperty.Register<DaisyColorPickerDialogContent, Color>(nameof(OriginalColor), Colors.Red);

        public Color OriginalColor
        {
            get => GetValue(OriginalColorProperty);
            set => SetValue(OriginalColorProperty, value);
        }

        static DaisyColorPickerDialogContent()
        {
            ColorProperty.Changed.AddClassHandler<DaisyColorPickerDialogContent>((x, e) => x.OnColorPropertyChanged(e));
        }

        public DaisyColorPickerDialogContent()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            var borderBrush = new SolidColorBrush(BorderColor);

            var mainGrid = new Grid
            {
                RowDefinitions = new RowDefinitions("Auto,Auto"),
                ColumnDefinitions = new ColumnDefinitions("Auto,*"),
                Margin = new Thickness(DialogMargin),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            // Left panel
            var leftPanel = new StackPanel
            {
                Spacing = PanelSpacing,
                Margin = new Thickness(0, 0, LeftPanelRightMargin, 0),
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top
            };

            _colorWheel = new DaisyColorWheel { Width = ColorWheelSize, Height = ColorWheelSize };
            _colorWheel.ColorChanged += (s, e) => { if (!_lockUpdates) Color = e.Color; };
            leftPanel.Children.Add(_colorWheel);

            var lightnessPanel = new StackPanel { Spacing = TinySpacing };
            lightnessPanel.Children.Add(new TextBlock { Text = "Lightness", FontSize = LabelFontSize });
            _lightnessSlider = new DaisyColorSlider { Channel = ColorSliderChannel.Lightness, Width = ColorWheelSize, Height = SliderHeight };
            _lightnessSlider.ColorChanged += (s, e) =>
            {
                if (_lockUpdates || _colorWheel == null) return;
                var hsl = new HslColor(e.Color);
                _colorWheel.Lightness = hsl.L;
                Color = e.Color;
            };
            lightnessPanel.Children.Add(_lightnessSlider);
            leftPanel.Children.Add(lightnessPanel);

            _screenColorPicker = new DaisyScreenColorPicker { Margin = new Thickness(0, PanelSpacing, 0, 0) };
            _screenColorPicker.ColorChanged += (s, e) => { if (!_lockUpdates) Color = e.Color; };
            leftPanel.Children.Add(_screenColorPicker);

            Grid.SetRow(leftPanel, 0);
            Grid.SetColumn(leftPanel, 0);
            mainGrid.Children.Add(leftPanel);

            // Right panel
            var rightPanel = new StackPanel { Spacing = PanelSpacing, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top };

            var previewPanel = new Grid
            {
                ColumnDefinitions = new ColumnDefinitions("*,*"),
                Height = ColorPreviewHeight + LabelFontSize + TinySpacing,
                Margin = new Thickness(0, 0, 0, TinySpacing)
            };

            var originalStack = new StackPanel();
            originalStack.Children.Add(new TextBlock { Text = "Original", FontSize = SmallFontSize, Margin = new Thickness(0, 0, 0, LabelSpacing) });
            _originalColorHexText = new TextBlock { FontSize = SmallFontSize, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            _originalColorPreview = new Border { Height = ColorPreviewHeight, BorderBrush = borderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2), Child = _originalColorHexText };
            originalStack.Children.Add(_originalColorPreview);

            var newStack = new StackPanel();
            newStack.Children.Add(new TextBlock { Text = "New", FontSize = SmallFontSize, Margin = new Thickness(0, 0, 0, LabelSpacing) });
            _colorPreview = new Border { Height = ColorPreviewHeight, BorderBrush = borderBrush, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(2) };
            newStack.Children.Add(_colorPreview);

            Grid.SetColumn(originalStack, 0);
            Grid.SetColumn(newStack, 1);
            previewPanel.Children.Add(originalStack);
            previewPanel.Children.Add(newStack);
            rightPanel.Children.Add(previewPanel);

            rightPanel.Children.Add(new TextBlock { Text = "Color Palette", FontWeight = FontWeight.SemiBold, FontSize = LabelFontSize, Margin = new Thickness(0, 0, 0, LabelSpacing) });

            _colorGrid = new DaisyColorGrid
            {
                Palette = ColorPalettes.Paint,
                CustomColors = ColorPalettes.CreateCustom(ColorGridColumns),
                ShowCustomColors = true,
                CellSize = new Size(ColorGridCellSize, ColorGridCellSize),
                Columns = ColorGridColumns,
                Spacing = new Size(ColorGridSpacing, ColorGridSpacing)
            };
            _colorGrid.ColorChanged += (s, e) => { if (!_lockUpdates) Color = e.Color; };
            rightPanel.Children.Add(_colorGrid);

            rightPanel.Children.Add(new TextBlock { Text = "Color Values", FontWeight = FontWeight.SemiBold, FontSize = LabelFontSize, Margin = new Thickness(0, TinySpacing, 0, LabelSpacing) });

            _colorEditor = new DaisyColorEditor { ShowAlphaChannel = true, ShowHexInput = true, ShowRgbSliders = true, ShowHslSliders = true, Margin = new Thickness(0, 0, 0, SmallSpacing) };
            _colorEditor.ColorChanged += (s, e) => { if (!_lockUpdates) Color = e.Color; };
            rightPanel.Children.Add(_colorEditor);

            Grid.SetRow(rightPanel, 0);
            Grid.SetColumn(rightPanel, 1);
            mainGrid.Children.Add(rightPanel);

            // Buttons
            var buttonPanel = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right, Spacing = PanelSpacing, Margin = new Thickness(0, SmallSpacing, 0, 0) };

            var okButton = new DaisyButton { Content = "OK", Variant = DaisyButtonVariant.Primary, Size = DaisySize.Small, MinWidth = ButtonWidth };
            okButton.Click += (s, e) => OkClicked?.Invoke(this, EventArgs.Empty);
            buttonPanel.Children.Add(okButton);

            var cancelButton = new DaisyButton { Content = "Cancel", Variant = DaisyButtonVariant.Primary, Size = DaisySize.Small, MinWidth = ButtonWidth };
            cancelButton.Click += (s, e) => CancelClicked?.Invoke(this, EventArgs.Empty);
            buttonPanel.Children.Add(cancelButton);

            Grid.SetRow(buttonPanel, 1);
            Grid.SetColumnSpan(buttonPanel, 2);
            mainGrid.Children.Add(buttonPanel);

            Content = mainGrid;
            UpdateAllControls();
        }

        private void OnColorPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_lockUpdates) return;
            UpdateAllControls();
        }

        private void UpdateAllControls()
        {
            _lockUpdates = true;
            try
            {
                var color = Color;
                var hsl = new HslColor(color);

                if (_colorWheel != null) { _colorWheel.Color = color; _colorWheel.Lightness = hsl.L; }
                if (_lightnessSlider != null) _lightnessSlider.Color = color;
                if (_colorGrid != null) _colorGrid.Color = color;
                if (_colorEditor != null) _colorEditor.Color = color;
                if (_colorPreview != null) _colorPreview.Background = new SolidColorBrush(color);
                if (_originalColorPreview != null)
                {
                    _originalColorPreview.Background = new SolidColorBrush(OriginalColor);
                    if (_originalColorHexText != null)
                    {
                        _originalColorHexText.Text = $"#{OriginalColor.R:X2}{OriginalColor.G:X2}{OriginalColor.B:X2}";
                        var luminance = 0.299 * OriginalColor.R + 0.587 * OriginalColor.G + 0.114 * OriginalColor.B;
                        _originalColorHexText.Foreground = new SolidColorBrush(luminance > 128 ? Colors.Black : Colors.White);
                    }
                }
            }
            finally { _lockUpdates = false; }
        }
    }
}
