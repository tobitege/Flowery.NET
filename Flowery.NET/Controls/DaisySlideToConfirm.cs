using System;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Styling;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Variant colors for slide-to-confirm control.
    /// </summary>
    public enum DaisySlideToConfirmVariant
    {
        Default,
        Primary,
        Secondary,
        Accent,
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// 3D depth style for controls.
    /// </summary>
    public enum DaisyDepthStyle
    {
        /// <summary>No shadow/depth effect.</summary>
        Flat,
        /// <summary>Subtle 3D shadow.</summary>
        ThreeDimensional,
        /// <summary>Deep shadow for raised appearance.</summary>
        Raised
    }

    /// <summary>
    /// A slide-to-confirm control that requires the user to drag a handle to complete an action.
    /// Provides visual feedback with color transition and opacity changes.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisySlideToConfirm : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisySlideToConfirm);

        private const double BaseFontSize = 11.0;

        private Border? _track;
        private Border? _handle;
        private TextBlock? _label;
        private Path? _icon;
        private TranslateTransform? _handleTransform;

        private bool _isDragging;
        private double _dragStartX;
        private double _handleStartX;
        private bool _slideCompleted;
        private Color _originalTrackColor;
        private Color _originalLabelColor;
        private Color _targetLabelColor;
        private IPointer? _capturedPointer;

        #region Dependency Properties

        public static readonly StyledProperty<DaisySlideToConfirmVariant> VariantProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, DaisySlideToConfirmVariant>(nameof(Variant), DaisySlideToConfirmVariant.Primary);

        /// <summary>
        /// Gets or sets the color variant.
        /// </summary>
        public DaisySlideToConfirmVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisyDepthStyle> DepthStyleProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, DaisyDepthStyle>(nameof(DepthStyle), DaisyDepthStyle.Flat);

        /// <summary>
        /// Gets or sets the 3D depth style.
        /// </summary>
        public DaisyDepthStyle DepthStyle
        {
            get => GetValue(DepthStyleProperty);
            set => SetValue(DepthStyleProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<string> TextProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, string>(nameof(Text), "SLIDE TO CONFIRM");

        /// <summary>
        /// Gets or sets the text displayed in the track.
        /// </summary>
        public string Text
        {
            get => GetValue(TextProperty);
            set => SetValue(TextProperty, value);
        }

        public static readonly StyledProperty<string> ConfirmingTextProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, string>(nameof(ConfirmingText), "CONFIRMING...");

        /// <summary>
        /// Gets or sets the text displayed when slide is completed.
        /// </summary>
        public string ConfirmingText
        {
            get => GetValue(ConfirmingTextProperty);
            set => SetValue(ConfirmingTextProperty, value);
        }

        public static readonly StyledProperty<Color> SlideColorProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, Color>(nameof(SlideColor));

        /// <summary>
        /// Gets or sets the color the track transitions to when sliding.
        /// </summary>
        public Color SlideColor
        {
            get => GetValue(SlideColorProperty);
            set => SetValue(SlideColorProperty, value);
        }

        public static readonly StyledProperty<StreamGeometry?> IconDataProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, StreamGeometry?>(nameof(IconData));

        /// <summary>
        /// Gets or sets the icon path data for the handle.
        /// </summary>
        public StreamGeometry? IconData
        {
            get => GetValue(IconDataProperty);
            set => SetValue(IconDataProperty, value);
        }

        public static readonly StyledProperty<IBrush?> IconForegroundProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, IBrush?>(nameof(IconForeground));

        /// <summary>
        /// Gets or sets the icon foreground color.
        /// </summary>
        public IBrush? IconForeground
        {
            get => GetValue(IconForegroundProperty);
            set => SetValue(IconForegroundProperty, value);
        }

        public static readonly StyledProperty<IBrush?> HandleBackgroundProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, IBrush?>(nameof(HandleBackground));

        /// <summary>
        /// Gets or sets the handle background color.
        /// </summary>
        public IBrush? HandleBackground
        {
            get => GetValue(HandleBackgroundProperty);
            set => SetValue(HandleBackgroundProperty, value);
        }

        public static readonly StyledProperty<TimeSpan> ResetDelayProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, TimeSpan>(nameof(ResetDelay), TimeSpan.FromMilliseconds(900));

        /// <summary>
        /// Gets or sets the delay before auto-reset.
        /// </summary>
        public TimeSpan ResetDelay
        {
            get => GetValue(ResetDelayProperty);
            set => SetValue(ResetDelayProperty, value);
        }

        public static readonly StyledProperty<bool> AutoResetProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, bool>(nameof(AutoReset), false);

        /// <summary>
        /// Gets or sets whether to auto-reset after completion.
        /// </summary>
        public bool AutoReset
        {
            get => GetValue(AutoResetProperty);
            set => SetValue(AutoResetProperty, value);
        }

        public static readonly StyledProperty<double> TrackWidthProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, double>(nameof(TrackWidth), double.NaN);

        /// <summary>
        /// Gets or sets the track width override.
        /// </summary>
        public double TrackWidth
        {
            get => GetValue(TrackWidthProperty);
            set => SetValue(TrackWidthProperty, value);
        }

        public static readonly StyledProperty<double> TrackHeightProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, double>(nameof(TrackHeight), double.NaN);

        /// <summary>
        /// Gets or sets the track height override.
        /// </summary>
        public double TrackHeight
        {
            get => GetValue(TrackHeightProperty);
            set => SetValue(TrackHeightProperty, value);
        }

        #endregion

        #region Computed Properties

        public static readonly DirectProperty<DaisySlideToConfirm, double> EffectiveTrackHeightProperty =
            AvaloniaProperty.RegisterDirect<DaisySlideToConfirm, double>(
                nameof(EffectiveTrackHeight),
                o => o.EffectiveTrackHeight);

        private double _effectiveTrackHeight = 48;

        public double EffectiveTrackHeight
        {
            get => _effectiveTrackHeight;
            private set => SetAndRaise(EffectiveTrackHeightProperty, ref _effectiveTrackHeight, value);
        }

        public static readonly DirectProperty<DaisySlideToConfirm, double> EffectiveHandleSizeProperty =
            AvaloniaProperty.RegisterDirect<DaisySlideToConfirm, double>(
                nameof(EffectiveHandleSize),
                o => o.EffectiveHandleSize);

        private double _effectiveHandleSize = 40;

        public double EffectiveHandleSize
        {
            get => _effectiveHandleSize;
            private set => SetAndRaise(EffectiveHandleSizeProperty, ref _effectiveHandleSize, value);
        }

        public static readonly DirectProperty<DaisySlideToConfirm, double> EffectiveIconSizeProperty =
            AvaloniaProperty.RegisterDirect<DaisySlideToConfirm, double>(
                nameof(EffectiveIconSize),
                o => o.EffectiveIconSize);

        private double _effectiveIconSize = 20;

        public double EffectiveIconSize
        {
            get => _effectiveIconSize;
            private set => SetAndRaise(EffectiveIconSizeProperty, ref _effectiveIconSize, value);
        }

        public static readonly DirectProperty<DaisySlideToConfirm, double> EffectiveFontSizeProperty =
            AvaloniaProperty.RegisterDirect<DaisySlideToConfirm, double>(
                nameof(EffectiveFontSize),
                o => o.EffectiveFontSize);

        private double _effectiveFontSize = 11;

        public double EffectiveFontSize
        {
            get => _effectiveFontSize;
            private set => SetAndRaise(EffectiveFontSizeProperty, ref _effectiveFontSize, value);
        }

        #endregion

        #region Scaling Properties

        public static readonly StyledProperty<double> ScaledFontSizeProperty =
            AvaloniaProperty.Register<DaisySlideToConfirm, double>(nameof(ScaledFontSize), BaseFontSize);

        /// <summary>
        /// Gets the scaled font size for the label text. Automatically updated by FloweryScaleManager.
        /// </summary>
        public double ScaledFontSize
        {
            get => GetValue(ScaledFontSizeProperty);
            private set => SetValue(ScaledFontSizeProperty, value);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            ScaledFontSize = FloweryScaleManager.ApplyScale(BaseFontSize, 8.0, scaleFactor);
        }

        #endregion

        #region Events

        /// <summary>
        /// Occurs when the slide is completed.
        /// </summary>
        public event EventHandler? SlideCompleted;

        #endregion

        public DaisySlideToConfirm()
        {
            UpdateSizing();
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Unsubscribe old handlers
            if (_handle != null)
            {
                _handle.PointerPressed -= Handle_PointerPressed;
                _handle.PointerMoved -= Handle_PointerMoved;
                _handle.PointerReleased -= Handle_PointerReleased;
                _handle.PointerCaptureLost -= Handle_PointerCaptureLost;
            }

            _track = e.NameScope.Find<Border>("PART_Track");
            _handle = e.NameScope.Find<Border>("PART_Handle");
            _label = e.NameScope.Find<TextBlock>("PART_Label");
            _icon = e.NameScope.Find<Path>("PART_Icon");
            _handleTransform = _handle?.RenderTransform as TranslateTransform ?? new TranslateTransform();
            if (_handle != null && _handle.RenderTransform != _handleTransform)
            {
                _handle.RenderTransform = _handleTransform;
            }

            if (_handle != null)
            {
                _handle.PointerPressed += Handle_PointerPressed;
                _handle.PointerMoved += Handle_PointerMoved;
                _handle.PointerReleased += Handle_PointerReleased;
                _handle.PointerCaptureLost += Handle_PointerCaptureLost;
            }

            ApplyVariantColors();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SizeProperty ||
                change.Property == TrackWidthProperty ||
                change.Property == TrackHeightProperty)
            {
                UpdateSizing();
            }
            else if (change.Property == VariantProperty ||
                     change.Property == DepthStyleProperty)
            {
                ApplyVariantColors();
            }
            else if (change.Property == TextProperty && _label != null && !_slideCompleted)
            {
                _label.Text = Text;
            }
        }

        private void UpdateSizing()
        {
            EffectiveTrackHeight = !double.IsNaN(TrackHeight) ? TrackHeight : Size switch
            {
                DaisySize.ExtraSmall => 28,
                DaisySize.Small => 36,
                DaisySize.Medium => 48,
                DaisySize.Large => 56,
                DaisySize.ExtraLarge => 64,
                _ => 48
            };

            EffectiveHandleSize = Size switch
            {
                DaisySize.ExtraSmall => 20,
                DaisySize.Small => 28,
                DaisySize.Medium => 40,
                DaisySize.Large => 48,
                DaisySize.ExtraLarge => 56,
                _ => 40
            };

            EffectiveIconSize = Size switch
            {
                DaisySize.ExtraSmall => 12,
                DaisySize.Small => 16,
                DaisySize.Medium => 20,
                DaisySize.Large => 24,
                DaisySize.ExtraLarge => 28,
                _ => 20
            };

            EffectiveFontSize = Size switch
            {
                DaisySize.ExtraSmall => 8,
                DaisySize.Small => 9,
                DaisySize.Medium => 11,
                DaisySize.Large => 13,
                DaisySize.ExtraLarge => 15,
                _ => 11
            };
        }

        private void ApplyVariantColors()
        {
            if (_track == null) return;

            // Get variant color
            var variantBrushKey = Variant switch
            {
                DaisySlideToConfirmVariant.Primary => "DaisyPrimaryBrush",
                DaisySlideToConfirmVariant.Secondary => "DaisySecondaryBrush",
                DaisySlideToConfirmVariant.Accent => "DaisyAccentBrush",
                DaisySlideToConfirmVariant.Success => "DaisySuccessBrush",
                DaisySlideToConfirmVariant.Warning => "DaisyWarningBrush",
                DaisySlideToConfirmVariant.Info => "DaisyInfoBrush",
                DaisySlideToConfirmVariant.Error => "DaisyErrorBrush",
                _ => "DaisyPrimaryBrush"
            };

            if (this.TryFindResource(variantBrushKey, out var brush) && brush is SolidColorBrush scb)
            {
                var variantColor = scb.Color;

                // Set slide color if not explicitly set
                if (SlideColor == default)
                {
                    SlideColor = variantColor;
                }

                // Calculate contrasting text color
                _targetLabelColor = GetContrastingTextColor(variantColor);

                // Set icon foreground
                if (IconForeground == null && _icon != null)
                {
                    _icon.Fill = scb;
                }

                // Set handle background with tint
                if (HandleBackground == null && _handle != null)
                {
                    var tintedHandle = Color.FromArgb(51, variantColor.R, variantColor.G, variantColor.B);
                    _handle.Background = new SolidColorBrush(tintedHandle);
                }

                // Apply border based on depth style
                _track.BorderBrush = scb;
            }

            // Store original colors
            if (this.TryFindResource("DaisyBase300Brush", out var trackBrush) && trackBrush is SolidColorBrush trackScb)
            {
                _originalTrackColor = trackScb.Color;
                _track.Background = trackScb;
            }

            if (this.TryFindResource("DaisyBaseContentBrush", out var labelBrush) && labelBrush is SolidColorBrush labelScb)
            {
                _originalLabelColor = labelScb.Color;
                if (_label != null)
                {
                    _label.Foreground = labelScb;
                }
            }
        }

        #region Pointer Handlers

        private void Handle_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_slideCompleted || _handle == null || _handleTransform == null || _track == null)
                return;

            _isDragging = true;
            e.Pointer.Capture(_handle);
            _capturedPointer = e.Pointer;

            var position = e.GetPosition(_track);
            _dragStartX = position.X;
            _handleStartX = _handleTransform.X;
        }

        private void Handle_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDragging || _slideCompleted || _handle == null || _handleTransform == null || _track == null)
                return;

            var position = e.GetPosition(_track);
            var delta = position.X - _dragStartX;
            var newX = _handleStartX + delta;

            var maxX = GetMaxLeft();
            newX = Math.Max(0, Math.Min(newX, maxX));

            _handleTransform.X = newX;
            UpdateVisualFeedback(newX, maxX);

            if (newX >= maxX)
            {
                _ = CompleteSlideAsync(maxX);
            }
        }

        private void Handle_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            EndDrag(e, resetIfNeeded: true);
        }

        private void Handle_PointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            EndDrag(null, resetIfNeeded: true);
        }

        private void EndDrag(PointerEventArgs? args, bool resetIfNeeded)
        {
            _isDragging = false;
            if (args != null)
            {
                args.Pointer.Capture(null);
                _capturedPointer = null;
            }
            else
            {
                ReleaseCapture();
            }

            if (!_slideCompleted && resetIfNeeded)
            {
                AnimateHandleTo(0);
                ResetVisualFeedback();
            }
        }

        #endregion

        #region Slide Logic

        private async Task CompleteSlideAsync(double maxLeft)
        {
            if (_slideCompleted)
                return;

            _slideCompleted = true;
            _isDragging = false;
            ReleaseCapture();

            if (_label != null)
                _label.Text = ConfirmingText;

            AnimateHandleTo(maxLeft);

            SlideCompleted?.Invoke(this, EventArgs.Empty);

            if (AutoReset)
            {
                await Task.Delay(ResetDelay);
                Reset();
            }
        }

        /// <summary>
        /// Resets the control to its initial state.
        /// </summary>
        public void Reset()
        {
            if (_label != null)
                _label.Text = Text;

            AnimateHandleTo(0);
            ResetVisualFeedback();
            _slideCompleted = false;
        }

        private double GetMaxLeft()
        {
            if (_track == null || _handle == null)
                return 0;

            var trackWidth = _track.Bounds.Width;
            var handleWidth = EffectiveHandleSize;
            var padding = 4; // Account for padding

            var max = trackWidth - handleWidth - padding * 2;
            return max < 0 ? 0 : max;
        }

        private void AnimateHandleTo(double targetX)
        {
            if (_handleTransform == null)
                return;

            // Simple animation using Avalonia transitions
            _handleTransform.X = targetX;
        }

        #endregion

        #region Visual Feedback

        private void UpdateVisualFeedback(double currentLeft, double maxLeft)
        {
            if (_track == null || _label == null || maxLeft <= 0)
                return;

            var progress = currentLeft / maxLeft;
            progress = progress < 0 ? 0 : progress > 1 ? 1 : progress;

            // Interpolate track background color
            var trackR = (byte)(_originalTrackColor.R + ((SlideColor.R - _originalTrackColor.R) * progress));
            var trackG = (byte)(_originalTrackColor.G + ((SlideColor.G - _originalTrackColor.G) * progress));
            var trackB = (byte)(_originalTrackColor.B + ((SlideColor.B - _originalTrackColor.B) * progress));
            _track.Background = new SolidColorBrush(Color.FromRgb(trackR, trackG, trackB));

            // Interpolate label color
            var labelR = (byte)(_originalLabelColor.R + ((_targetLabelColor.R - _originalLabelColor.R) * progress));
            var labelG = (byte)(_originalLabelColor.G + ((_targetLabelColor.G - _originalLabelColor.G) * progress));
            var labelB = (byte)(_originalLabelColor.B + ((_targetLabelColor.B - _originalLabelColor.B) * progress));
            _label.Foreground = new SolidColorBrush(Color.FromRgb(labelR, labelG, labelB));

            // Animate label opacity
            _label.Opacity = 0.7 + (0.3 * progress);
        }

        private void ResetVisualFeedback()
        {
            if (_track != null)
            {
                _track.Background = new SolidColorBrush(_originalTrackColor);
            }

            if (_label != null)
            {
                _label.Opacity = 0.7;
                _label.Foreground = new SolidColorBrush(_originalLabelColor);
            }
        }

        #endregion

        #region Helpers

        private static Color GetContrastingTextColor(Color backgroundColor)
        {
            var r = backgroundColor.R / 255.0;
            var g = backgroundColor.G / 255.0;
            var b = backgroundColor.B / 255.0;
            var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;

            return luminance > 0.5 ? Colors.Black : Colors.White;
        }

        private void ReleaseCapture()
        {
            if (_capturedPointer != null)
            {
                _capturedPointer.Capture(null);
                _capturedPointer = null;
            }
        }

        #endregion
    }
}
