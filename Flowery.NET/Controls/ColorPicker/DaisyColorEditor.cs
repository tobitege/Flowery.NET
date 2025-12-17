using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls.ColorPicker
{
    /// <summary>
    /// Specifies the editing mode for the color editor.
    /// </summary>
    public enum ColorEditingMode
    {
        /// <summary>RGB color editing (Red, Green, Blue).</summary>
        Rgb,
        /// <summary>HSL color editing (Hue, Saturation, Lightness).</summary>
        Hsl
    }

    /// <summary>
    /// A comprehensive color editor control with RGB/HSL sliders and numeric inputs.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyColorEditor : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyColorEditor);

        private const double BaseTextFontSize = 12.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        private bool _lockUpdates;

        // Template parts
        private DaisyColorSlider? _redSlider;
        private DaisyColorSlider? _greenSlider;
        private DaisyColorSlider? _blueSlider;
        private DaisyColorSlider? _alphaSlider;
        private DaisyColorSlider? _hueSlider;
        private DaisyColorSlider? _saturationSlider;
        private DaisyColorSlider? _lightnessSlider;
        private NumericUpDown? _redInput;
        private NumericUpDown? _greenInput;
        private NumericUpDown? _blueInput;
        private NumericUpDown? _alphaInput;
        private NumericUpDown? _hueInput;
        private NumericUpDown? _saturationInput;
        private NumericUpDown? _lightnessInput;
        private TextBox? _hexInput;

        #region Styled Properties

        /// <summary>
        /// Gets or sets the selected color.
        /// </summary>
        public static readonly StyledProperty<Color> ColorProperty =
            AvaloniaProperty.Register<DaisyColorEditor, Color>(nameof(Color), Colors.Red);

        public Color Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the HSL representation of the color.
        /// </summary>
        public static readonly StyledProperty<HslColor> HslColorProperty =
            AvaloniaProperty.Register<DaisyColorEditor, HslColor>(nameof(HslColor), new HslColor(0, 1, 0.5));

        public HslColor HslColor
        {
            get => GetValue(HslColorProperty);
            set => SetValue(HslColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the editing mode (RGB or HSL).
        /// </summary>
        public static readonly StyledProperty<ColorEditingMode> EditingModeProperty =
            AvaloniaProperty.Register<DaisyColorEditor, ColorEditingMode>(nameof(EditingMode), ColorEditingMode.Rgb);

        public ColorEditingMode EditingMode
        {
            get => GetValue(EditingModeProperty);
            set => SetValue(EditingModeProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the alpha channel controls.
        /// </summary>
        public static readonly StyledProperty<bool> ShowAlphaChannelProperty =
            AvaloniaProperty.Register<DaisyColorEditor, bool>(nameof(ShowAlphaChannel), true);

        public bool ShowAlphaChannel
        {
            get => GetValue(ShowAlphaChannelProperty);
            set => SetValue(ShowAlphaChannelProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the hex input field.
        /// </summary>
        public static readonly StyledProperty<bool> ShowHexInputProperty =
            AvaloniaProperty.Register<DaisyColorEditor, bool>(nameof(ShowHexInput), true);

        public bool ShowHexInput
        {
            get => GetValue(ShowHexInputProperty);
            set => SetValue(ShowHexInputProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the RGB sliders.
        /// </summary>
        public static readonly StyledProperty<bool> ShowRgbSlidersProperty =
            AvaloniaProperty.Register<DaisyColorEditor, bool>(nameof(ShowRgbSliders), true);

        public bool ShowRgbSliders
        {
            get => GetValue(ShowRgbSlidersProperty);
            set => SetValue(ShowRgbSlidersProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the HSL sliders.
        /// </summary>
        public static readonly StyledProperty<bool> ShowHslSlidersProperty =
            AvaloniaProperty.Register<DaisyColorEditor, bool>(nameof(ShowHslSliders), true);

        public bool ShowHslSliders
        {
            get => GetValue(ShowHslSlidersProperty);
            set => SetValue(ShowHslSlidersProperty, value);
        }

        // Read-only properties for binding
        public static readonly DirectProperty<DaisyColorEditor, byte> RedProperty =
            AvaloniaProperty.RegisterDirect<DaisyColorEditor, byte>(nameof(Red), o => o.Red, (o, v) => o.Red = v);

        private byte _red;
        public byte Red
        {
            get => _red;
            set
            {
                if (SetAndRaise(RedProperty, ref _red, value) && !_lockUpdates)
                {
                    UpdateColorFromRgb();
                }
            }
        }

        public static readonly DirectProperty<DaisyColorEditor, byte> GreenProperty =
            AvaloniaProperty.RegisterDirect<DaisyColorEditor, byte>(nameof(Green), o => o.Green, (o, v) => o.Green = v);

        private byte _green;
        public byte Green
        {
            get => _green;
            set
            {
                if (SetAndRaise(GreenProperty, ref _green, value) && !_lockUpdates)
                {
                    UpdateColorFromRgb();
                }
            }
        }

        public static readonly DirectProperty<DaisyColorEditor, byte> BlueProperty =
            AvaloniaProperty.RegisterDirect<DaisyColorEditor, byte>(nameof(Blue), o => o.Blue, (o, v) => o.Blue = v);

        private byte _blue;
        public byte Blue
        {
            get => _blue;
            set
            {
                if (SetAndRaise(BlueProperty, ref _blue, value) && !_lockUpdates)
                {
                    UpdateColorFromRgb();
                }
            }
        }

        public static readonly DirectProperty<DaisyColorEditor, byte> AlphaProperty =
            AvaloniaProperty.RegisterDirect<DaisyColorEditor, byte>(nameof(Alpha), o => o.Alpha, (o, v) => o.Alpha = v);

        private byte _alpha = 255;
        public byte Alpha
        {
            get => _alpha;
            set
            {
                if (SetAndRaise(AlphaProperty, ref _alpha, value) && !_lockUpdates)
                {
                    UpdateColorFromRgb();
                }
            }
        }

        public static readonly DirectProperty<DaisyColorEditor, double> HueProperty =
            AvaloniaProperty.RegisterDirect<DaisyColorEditor, double>(nameof(Hue), o => o.Hue, (o, v) => o.Hue = v);

        private double _hue;
        public double Hue
        {
            get => _hue;
            set
            {
                if (SetAndRaise(HueProperty, ref _hue, value) && !_lockUpdates)
                {
                    UpdateColorFromHsl();
                }
            }
        }

        public static readonly DirectProperty<DaisyColorEditor, double> SaturationProperty =
            AvaloniaProperty.RegisterDirect<DaisyColorEditor, double>(nameof(Saturation), o => o.Saturation, (o, v) => o.Saturation = v);

        private double _saturation = 100;
        public double Saturation
        {
            get => _saturation;
            set
            {
                if (SetAndRaise(SaturationProperty, ref _saturation, value) && !_lockUpdates)
                {
                    UpdateColorFromHsl();
                }
            }
        }

        public static readonly DirectProperty<DaisyColorEditor, double> LightnessProperty =
            AvaloniaProperty.RegisterDirect<DaisyColorEditor, double>(nameof(Lightness), o => o.Lightness, (o, v) => o.Lightness = v);

        private double _lightness = 50;
        public double Lightness
        {
            get => _lightness;
            set
            {
                if (SetAndRaise(LightnessProperty, ref _lightness, value) && !_lockUpdates)
                {
                    UpdateColorFromHsl();
                }
            }
        }

        public static readonly DirectProperty<DaisyColorEditor, string?> HexValueProperty =
            AvaloniaProperty.RegisterDirect<DaisyColorEditor, string?>(nameof(HexValue), o => o.HexValue, (o, v) => o.HexValue = v ?? string.Empty);

        private string? _hexValue = "#FF0000";
        public string? HexValue
        {
            get => _hexValue;
            set
            {
                if (SetAndRaise(HexValueProperty, ref _hexValue, value) && !_lockUpdates)
                {
                    UpdateColorFromHex();
                }
            }
        }

        /// <summary>
        /// Gets or sets an optional callback invoked when the color changes.
        /// This provides a simpler alternative to the ColorChanged event.
        /// </summary>
        public static readonly StyledProperty<Action<Color>?> OnColorChangedCallbackProperty =
            AvaloniaProperty.Register<DaisyColorEditor, Action<Color>?>(nameof(OnColorChangedCallback));

        public Action<Color>? OnColorChangedCallback
        {
            get => GetValue(OnColorChangedCallbackProperty);
            set => SetValue(OnColorChangedCallbackProperty, value);
        }

        #endregion

        /// <summary>
        /// Occurs when the color changes.
        /// </summary>
        public event EventHandler<ColorChangedEventArgs>? ColorChanged;

        static DaisyColorEditor()
        {
            ColorProperty.Changed.AddClassHandler<DaisyColorEditor>((x, e) => x.OnColorPropertyChanged(e));
            HslColorProperty.Changed.AddClassHandler<DaisyColorEditor>((x, e) => x.OnHslColorPropertyChanged(e));
        }

        private void OnColorPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_lockUpdates) return;

            _lockUpdates = true;
            try
            {
                var color = (Color)e.NewValue!;
                UpdateComponentsFromColor(color);
                OnColorChanged(new ColorChangedEventArgs(color));
            }
            finally
            {
                _lockUpdates = false;
            }
        }

        private void OnHslColorPropertyChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_lockUpdates) return;

            _lockUpdates = true;
            try
            {
                var hsl = (HslColor)e.NewValue!;
                Color = hsl.ToRgbColor();
            }
            finally
            {
                _lockUpdates = false;
            }
        }

        private void UpdateComponentsFromColor(Color color)
        {
            _red = color.R;
            _green = color.G;
            _blue = color.B;
            _alpha = color.A;

            var hsl = new HslColor(color);
            _hue = hsl.H;
            _saturation = hsl.S * 100;
            _lightness = hsl.L * 100;

            _hexValue = ShowAlphaChannel
                ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
                : $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            RaisePropertyChanged(RedProperty, default, _red);
            RaisePropertyChanged(GreenProperty, default, _green);
            RaisePropertyChanged(BlueProperty, default, _blue);
            RaisePropertyChanged(AlphaProperty, default, _alpha);
            RaisePropertyChanged(HueProperty, default, _hue);
            RaisePropertyChanged(SaturationProperty, default, _saturation);
            RaisePropertyChanged(LightnessProperty, default, _lightness);
            RaisePropertyChanged(HexValueProperty, default, _hexValue);

            HslColor = hsl;

            // Update template part controls
            UpdateTemplateControls(color);
        }

        private void UpdateTemplateControls(Color color)
        {
            // Update sliders
            if (_redSlider != null) _redSlider.Color = color;
            if (_greenSlider != null) _greenSlider.Color = color;
            if (_blueSlider != null) _blueSlider.Color = color;
            if (_alphaSlider != null) _alphaSlider.Color = color;
            if (_hueSlider != null) _hueSlider.Color = color;
            if (_saturationSlider != null) _saturationSlider.Color = color;
            if (_lightnessSlider != null) _lightnessSlider.Color = color;

            // Update numeric inputs
            if (_redInput != null) _redInput.Value = _red;
            if (_greenInput != null) _greenInput.Value = _green;
            if (_blueInput != null) _blueInput.Value = _blue;
            if (_alphaInput != null) _alphaInput.Value = _alpha;
            if (_hueInput != null) _hueInput.Value = (decimal)_hue;
            if (_saturationInput != null) _saturationInput.Value = (decimal)_saturation;
            if (_lightnessInput != null) _lightnessInput.Value = (decimal)_lightness;

            // Update hex input
            if (_hexInput != null) _hexInput.Text = _hexValue;
        }

        private void UpdateColorFromRgb()
        {
            _lockUpdates = true;
            try
            {
                var color = Color.FromArgb(_alpha, _red, _green, _blue);
                Color = color;
                UpdateHslFromRgb(color);
                UpdateHexFromRgb(color);
                UpdateTemplateControls(color);
                OnColorChanged(new ColorChangedEventArgs(color));
            }
            finally
            {
                _lockUpdates = false;
            }
        }

        private void UpdateColorFromHsl()
        {
            _lockUpdates = true;
            try
            {
                var hsl = new HslColor(_alpha, _hue, _saturation / 100, _lightness / 100);
                var color = hsl.ToRgbColor();
                Color = color;
                UpdateRgbFromColor(color);
                UpdateHexFromRgb(color);
                HslColor = hsl;
                UpdateTemplateControls(color);
                OnColorChanged(new ColorChangedEventArgs(color));
            }
            finally
            {
                _lockUpdates = false;
            }
        }

        private void UpdateColorFromHex()
        {
            if (string.IsNullOrWhiteSpace(_hexValue)) return;

            try
            {
                var hex = _hexValue!.TrimStart('#');
                Color color;

                if (hex.Length == 6)
                {
                    var r = Convert.ToByte(hex.Substring(0, 2), 16);
                    var g = Convert.ToByte(hex.Substring(2, 2), 16);
                    var b = Convert.ToByte(hex.Substring(4, 2), 16);
                    color = Color.FromRgb(r, g, b);
                }
                else if (hex.Length == 8)
                {
                    var a = Convert.ToByte(hex.Substring(0, 2), 16);
                    var r = Convert.ToByte(hex.Substring(2, 2), 16);
                    var g = Convert.ToByte(hex.Substring(4, 2), 16);
                    var b = Convert.ToByte(hex.Substring(6, 2), 16);
                    color = Color.FromArgb(a, r, g, b);
                }
                else
                {
                    return;
                }

                _lockUpdates = true;
                try
                {
                    Color = color;
                    UpdateRgbFromColor(color);
                    UpdateHslFromRgb(color);
                }
                finally
                {
                    _lockUpdates = false;
                }
            }
            catch
            {
                // Invalid hex format
            }
        }

        private void UpdateHslFromRgb(Color color)
        {
            var hsl = new HslColor(color);
            _hue = hsl.H;
            _saturation = hsl.S * 100;
            _lightness = hsl.L * 100;

            RaisePropertyChanged(HueProperty, default, _hue);
            RaisePropertyChanged(SaturationProperty, default, _saturation);
            RaisePropertyChanged(LightnessProperty, default, _lightness);
            HslColor = hsl;
        }

        private void UpdateRgbFromColor(Color color)
        {
            _red = color.R;
            _green = color.G;
            _blue = color.B;
            _alpha = color.A;

            RaisePropertyChanged(RedProperty, default, _red);
            RaisePropertyChanged(GreenProperty, default, _green);
            RaisePropertyChanged(BlueProperty, default, _blue);
            RaisePropertyChanged(AlphaProperty, default, _alpha);
        }

        private void UpdateHexFromRgb(Color color)
        {
            _hexValue = ShowAlphaChannel
                ? $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}"
                : $"#{color.R:X2}{color.G:X2}{color.B:X2}";

            RaisePropertyChanged(HexValueProperty, default, _hexValue);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Unsubscribe from old controls
            UnsubscribeFromControls();

            // Find template parts
            _redSlider = e.NameScope.Find<DaisyColorSlider>("PART_RedSlider");
            _greenSlider = e.NameScope.Find<DaisyColorSlider>("PART_GreenSlider");
            _blueSlider = e.NameScope.Find<DaisyColorSlider>("PART_BlueSlider");
            _alphaSlider = e.NameScope.Find<DaisyColorSlider>("PART_AlphaSlider");
            _hueSlider = e.NameScope.Find<DaisyColorSlider>("PART_HueSlider");
            _saturationSlider = e.NameScope.Find<DaisyColorSlider>("PART_SaturationSlider");
            _lightnessSlider = e.NameScope.Find<DaisyColorSlider>("PART_LightnessSlider");
            _redInput = e.NameScope.Find<NumericUpDown>("PART_RedInput");
            _greenInput = e.NameScope.Find<NumericUpDown>("PART_GreenInput");
            _blueInput = e.NameScope.Find<NumericUpDown>("PART_BlueInput");
            _alphaInput = e.NameScope.Find<NumericUpDown>("PART_AlphaInput");
            _hueInput = e.NameScope.Find<NumericUpDown>("PART_HueInput");
            _saturationInput = e.NameScope.Find<NumericUpDown>("PART_SaturationInput");
            _lightnessInput = e.NameScope.Find<NumericUpDown>("PART_LightnessInput");
            _hexInput = e.NameScope.Find<TextBox>("PART_HexInput");

            // Subscribe to controls
            SubscribeToControls();

            // Initialize with current color
            UpdateComponentsFromColor(Color);
        }

        private void SubscribeToControls()
        {
            if (_redSlider != null) _redSlider.ColorChanged += OnSliderColorChanged;
            if (_greenSlider != null) _greenSlider.ColorChanged += OnSliderColorChanged;
            if (_blueSlider != null) _blueSlider.ColorChanged += OnSliderColorChanged;
            if (_alphaSlider != null) _alphaSlider.ColorChanged += OnSliderColorChanged;
            if (_hueSlider != null) _hueSlider.ColorChanged += OnSliderColorChanged;
            if (_saturationSlider != null) _saturationSlider.ColorChanged += OnSliderColorChanged;
            if (_lightnessSlider != null) _lightnessSlider.ColorChanged += OnSliderColorChanged;

            ConfigureNumericInput(_redInput);
            ConfigureNumericInput(_greenInput);
            ConfigureNumericInput(_blueInput);
            ConfigureNumericInput(_alphaInput);
            ConfigureNumericInput(_hueInput);
            ConfigureNumericInput(_saturationInput);
            ConfigureNumericInput(_lightnessInput);

            if (_hexInput != null) _hexInput.LostFocus += OnHexInputLostFocus;
        }

        private void ConfigureNumericInput(NumericUpDown? input)
        {
            if (input == null) return;

            input.ValueChanged += OnNumericValueChanged;
            input.AddHandler(TextInputEvent, OnNumericTextInput, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        }

        private void OnNumericTextInput(object? sender, TextInputEventArgs e)
        {
            // Only allow digits
            if (!string.IsNullOrEmpty(e.Text) && !e.Text.All(char.IsDigit))
            {
                e.Handled = true;
            }
        }

        private void UnsubscribeFromControls()
        {
            if (_redSlider != null) _redSlider.ColorChanged -= OnSliderColorChanged;
            if (_greenSlider != null) _greenSlider.ColorChanged -= OnSliderColorChanged;
            if (_blueSlider != null) _blueSlider.ColorChanged -= OnSliderColorChanged;
            if (_alphaSlider != null) _alphaSlider.ColorChanged -= OnSliderColorChanged;
            if (_hueSlider != null) _hueSlider.ColorChanged -= OnSliderColorChanged;
            if (_saturationSlider != null) _saturationSlider.ColorChanged -= OnSliderColorChanged;
            if (_lightnessSlider != null) _lightnessSlider.ColorChanged -= OnSliderColorChanged;

            UnconfigureNumericInput(_redInput);
            UnconfigureNumericInput(_greenInput);
            UnconfigureNumericInput(_blueInput);
            UnconfigureNumericInput(_alphaInput);
            UnconfigureNumericInput(_hueInput);
            UnconfigureNumericInput(_saturationInput);
            UnconfigureNumericInput(_lightnessInput);

            if (_hexInput != null) _hexInput.LostFocus -= OnHexInputLostFocus;
        }

        private void UnconfigureNumericInput(NumericUpDown? input)
        {
            if (input == null) return;

            input.ValueChanged -= OnNumericValueChanged;
            input.RemoveHandler(TextInputEvent, OnNumericTextInput);
        }

        private void OnSliderColorChanged(object? sender, ColorChangedEventArgs e)
        {
            if (_lockUpdates) return;

            _lockUpdates = true;
            try
            {
                Color = e.Color;
                UpdateComponentsFromColor(e.Color);
            }
            finally
            {
                _lockUpdates = false;
            }
        }

        private void OnNumericValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (_lockUpdates || e.NewValue == null) return;

            if (sender == _redInput) Red = (byte)e.NewValue.Value;
            else if (sender == _greenInput) Green = (byte)e.NewValue.Value;
            else if (sender == _blueInput) Blue = (byte)e.NewValue.Value;
            else if (sender == _alphaInput) Alpha = (byte)e.NewValue.Value;
            else if (sender == _hueInput) Hue = (double)e.NewValue.Value;
            else if (sender == _saturationInput) Saturation = (double)e.NewValue.Value;
            else if (sender == _lightnessInput) Lightness = (double)e.NewValue.Value;
        }

        private void OnHexInputLostFocus(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            if (_hexInput != null)
            {
                HexValue = _hexInput.Text ?? string.Empty;
            }
        }

        protected virtual void OnColorChanged(ColorChangedEventArgs e)
        {
            ColorChanged?.Invoke(this, e);
            OnColorChangedCallback?.Invoke(e.Color);
        }
    }
}
