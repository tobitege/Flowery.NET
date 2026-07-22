using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Enums;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A divider control styled after DaisyUI's Divider component.
    /// Supports multiple visual styles, orientations, and color variants.
    /// Automatically scales when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyDivider : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyDivider);

        private const double BaseTextFontSize = 12.0;

        private Border? _startLine;
        private Border? _endLine;
        private Border? _startLineH;
        private Border? _endLineH;
        private Border? _startGradientLine;
        private Border? _endGradientLine;
        private DispatcherTimer? _glowAnimationTimer;
        private DateTime _glowAnimationStartedAt;
        private Border? _glowStartTarget;
        private Border? _glowEndTarget;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        #region Styled Properties

        public static readonly StyledProperty<bool> HorizontalProperty =
            AvaloniaProperty.Register<DaisyDivider, bool>(nameof(Horizontal), false);

        public static readonly StyledProperty<DaisyDividerColor> ColorProperty =
            AvaloniaProperty.Register<DaisyDivider, DaisyDividerColor>(nameof(Color), DaisyDividerColor.Default);

        public static readonly StyledProperty<DaisyDividerPlacement> PlacementProperty =
            AvaloniaProperty.Register<DaisyDivider, DaisyDividerPlacement>(nameof(Placement), DaisyDividerPlacement.Default);

        public static readonly StyledProperty<Thickness> DividerMarginProperty =
            AvaloniaProperty.Register<DaisyDivider, Thickness>(nameof(DividerMargin), new Thickness(0, 4));

        public static readonly StyledProperty<DaisyDividerStyle> DividerStyleProperty =
            AvaloniaProperty.Register<DaisyDivider, DaisyDividerStyle>(nameof(DividerStyle), DaisyDividerStyle.Solid);

        public static readonly StyledProperty<DaisyDividerOrnament> OrnamentProperty =
            AvaloniaProperty.Register<DaisyDivider, DaisyDividerOrnament>(nameof(Ornament), DaisyDividerOrnament.Diamond);

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyDivider, DaisySize>(nameof(Size), DaisySize.Small);

        public static readonly StyledProperty<double> LineThicknessProperty =
            AvaloniaProperty.Register<DaisyDivider, double>(nameof(LineThickness), 2.0);

        public static readonly StyledProperty<IBrush?> TextBackgroundProperty =
            AvaloniaProperty.Register<DaisyDivider, IBrush?>(nameof(TextBackground));

        /// <summary>
        /// Gets or sets whether the divider is horizontal (vertical line) or vertical (horizontal line).
        /// When true, renders a vertical line; when false (default), renders a horizontal line.
        /// </summary>
        public bool Horizontal
        {
            get => GetValue(HorizontalProperty);
            set => SetValue(HorizontalProperty, value);
        }

        /// <summary>
        /// Gets or sets the color variant of the divider.
        /// </summary>
        public DaisyDividerColor Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        /// <summary>
        /// Gets or sets the placement of content within the divider.
        /// </summary>
        public DaisyDividerPlacement Placement
        {
            get => GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }

        /// <summary>
        /// Gets or sets the margin of the divider.
        /// </summary>
        public Thickness DividerMargin
        {
            get => GetValue(DividerMarginProperty);
            set => SetValue(DividerMarginProperty, value);
        }

        /// <summary>
        /// Gets or sets the visual style of the divider.
        /// </summary>
        public DaisyDividerStyle DividerStyle
        {
            get => GetValue(DividerStyleProperty);
            set => SetValue(DividerStyleProperty, value);
        }

        /// <summary>
        /// Gets or sets the ornament shape when DividerStyle is Ornament.
        /// </summary>
        public DaisyDividerOrnament Ornament
        {
            get => GetValue(OrnamentProperty);
            set => SetValue(OrnamentProperty, value);
        }

        /// <summary>
        /// Gets or sets the size variant of the divider.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the thickness of the divider line(s) in pixels. Default is 2.
        /// </summary>
        public double LineThickness
        {
            get => GetValue(LineThicknessProperty);
            set => SetValue(LineThicknessProperty, value);
        }

        /// <summary>
        /// Gets or sets the background brush for the divider text container.
        /// When null (default), uses the base background. Set this to match
        /// the parent container's background when using dividers inside cards.
        /// </summary>
        public IBrush? TextBackground
        {
            get => GetValue(TextBackgroundProperty);
            set => SetValue(TextBackgroundProperty, value);
        }

        #endregion

        static DaisyDivider()
        {
            AffectsRender<DaisyDivider>(
                DividerStyleProperty,
                OrnamentProperty,
                LineThicknessProperty,
                HorizontalProperty,
                ColorProperty,
                PlacementProperty,
                SizeProperty
            );
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            StopGlowAnimation();

            _startLine = e.NameScope.Find<Border>("PART_StartLine");
            _endLine = e.NameScope.Find<Border>("PART_EndLine");
            _startLineH = e.NameScope.Find<Border>("PART_StartLineH");
            _endLineH = e.NameScope.Find<Border>("PART_EndLineH");
            _startGradientLine = e.NameScope.Find<Border>("PART_StartGradientLine");
            _endGradientLine = e.NameScope.Find<Border>("PART_EndGradientLine");
            UpdateVisuals();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            StopGlowAnimation();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == DividerStyleProperty ||
                change.Property == ColorProperty ||
                change.Property == HorizontalProperty)
            {
                UpdateVisuals();
            }
        }

        private void UpdateVisuals()
        {
            UpdateGradientBrushes();
            UpdateGlowAnimation();
        }

        private IBrush GetColorBrush()
        {
            var resourceKey = Color switch
            {
                DaisyDividerColor.Neutral => "DaisyNeutralBrush",
                DaisyDividerColor.Primary => "DaisyPrimaryBrush",
                DaisyDividerColor.Secondary => "DaisySecondaryBrush",
                DaisyDividerColor.Accent => "DaisyAccentBrush",
                DaisyDividerColor.Success => "DaisySuccessBrush",
                DaisyDividerColor.Warning => "DaisyWarningBrush",
                DaisyDividerColor.Info => "DaisyInfoBrush",
                DaisyDividerColor.Error => "DaisyErrorBrush",
                _ => "DaisyBaseContentBrush"
            };

            if (this.TryFindResource(resourceKey, out var resource) && resource is IBrush brush)
            {
                return brush;
            }
            return Brushes.Gray;
        }

        private Color GetColorValue()
        {
            var brush = GetColorBrush();
            if (brush is SolidColorBrush scb)
            {
                return scb.Color;
            }
            return Colors.Gray;
        }

        private void UpdateGradientBrushes()
        {
            if (DividerStyle != DaisyDividerStyle.Gradient) return;

            var color = GetColorValue();

            if (_startGradientLine != null)
            {
                _startGradientLine.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(Colors.Transparent, 0),
                        new GradientStop(color, 1)
                    }
                };
            }

            if (_endGradientLine != null)
            {
                _endGradientLine.Background = new LinearGradientBrush
                {
                    StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
                    EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
                    GradientStops =
                    {
                        new GradientStop(color, 0),
                        new GradientStop(Colors.Transparent, 1)
                    }
                };
            }
        }

        private void UpdateGlowAnimation()
        {
            if (DividerStyle != DaisyDividerStyle.Glow)
            {
                StopGlowAnimation();
                return;
            }

            var targetLine = Horizontal ? _startLineH : _startLine;
            var targetLineEnd = Horizontal ? _endLineH : _endLine;

            if (targetLine == null)
            {
                StopGlowAnimation();
                return;
            }

            StopGlowAnimation();

            _glowStartTarget = targetLine;
            _glowEndTarget = targetLineEnd;
            _glowAnimationStartedAt = DateTime.UtcNow;
            _glowAnimationTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(33)
            };
            _glowAnimationTimer.Tick += OnGlowAnimationTick;
            UpdateGlowOpacity();
            _glowAnimationTimer.Start();
        }

        private void OnGlowAnimationTick(object? sender, EventArgs e)
        {
            UpdateGlowOpacity();
        }

        private void UpdateGlowOpacity()
        {
            const double MinOpacity = 0.4;
            const double MaxOpacity = 1.0;
            const double HalfPeriodSeconds = 1.5;

            var elapsedSeconds = (DateTime.UtcNow - _glowAnimationStartedAt).TotalSeconds;
            var easedProgress = (1 - Math.Cos(elapsedSeconds / HalfPeriodSeconds * Math.PI)) / 2;
            var opacity = MinOpacity + (MaxOpacity - MinOpacity) * easedProgress;

            if (_glowStartTarget != null)
            {
                _glowStartTarget.Opacity = opacity;
            }

            if (_glowEndTarget != null)
            {
                _glowEndTarget.Opacity = opacity;
            }
        }

        private void StopGlowAnimation()
        {
            if (_glowAnimationTimer != null)
            {
                _glowAnimationTimer.Stop();
                _glowAnimationTimer.Tick -= OnGlowAnimationTick;
                _glowAnimationTimer = null;
            }

            _glowStartTarget?.ClearValue(OpacityProperty);
            _glowEndTarget?.ClearValue(OpacityProperty);
            _glowStartTarget = null;
            _glowEndTarget = null;
        }
    }
}
