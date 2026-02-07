using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Styling;
using Flowery.Services;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// A hero section control styled after DaisyUI's Hero component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyHero : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyHero);

        private const double BaseTextFontSize = 16.0;
        private readonly DaisyControlLifecycle _lifecycle;
        private Border? _backgroundBorder;
        private ContentPresenter? _contentPresenter;
        private Style? _textBlockStyle;
        private Setter? _textBlockForegroundSetter;
        private string? _detectedPaletteName;

        public DaisyHero()
        {
            _lifecycle = new DaisyControlLifecycle(
                this,
                ApplyAll,
                () => DaisySize.Medium,
                _ => { },
                subscribeSizeChanges: false);

            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch;
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch;
            HorizontalContentAlignment = Avalonia.Layout.HorizontalAlignment.Center;
            VerticalContentAlignment = Avalonia.Layout.VerticalAlignment.Center;
        }

        /// <summary>
        /// Defines the overlay opacity (0..1) for dimming hero backgrounds.
        /// </summary>
        public static readonly StyledProperty<double> OverlayOpacityProperty =
            AvaloniaProperty.Register<DaisyHero, double>(nameof(OverlayOpacity), 0.0);

        /// <summary>
        /// Gets or sets the opacity of the overlay. Set to > 0 for a dark overlay effect.
        /// </summary>
        public double OverlayOpacity
        {
            get => GetValue(OverlayOpacityProperty);
            set => SetValue(OverlayOpacityProperty, value < 0 ? 0 : value > 1 ? 1 : value);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 12.0, scaleFactor);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_contentPresenter != null && _textBlockStyle != null)
            {
                _contentPresenter.Styles.Remove(_textBlockStyle);
            }

            _backgroundBorder = e.NameScope.Find<Border>("PART_BackgroundBorder");
            _contentPresenter = e.NameScope.Find<ContentPresenter>("PART_ContentPresenter");
            _textBlockStyle = null;
            _textBlockForegroundSetter = null;

            ApplyAll();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == BackgroundProperty)
            {
                _detectedPaletteName = null;
                ApplyColors();
            }
        }

        private void ApplyAll()
        {
            ApplyColors();
        }

        private void ApplyColors()
        {
            if (_backgroundBorder == null)
            {
                return;
            }

            var baseBackground = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
            var baseContent = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush");
            var bgColor = (Background as ISolidColorBrush)?.Color;

            if (bgColor == null)
            {
                _backgroundBorder.Background = baseBackground;
                ApplyContentForeground(baseContent);
                return;
            }

            _detectedPaletteName ??= DaisyResourceLookup.GetPaletteNameForColor(bgColor.Value);
            var (freshBackground, freshContentBrush) = DaisyResourceLookup.GetPaletteBrushes(_detectedPaletteName);

            _backgroundBorder.Background = freshBackground ?? baseBackground;
            ApplyContentForeground(freshContentBrush ?? baseContent);
        }

        private void ApplyContentForeground(IBrush? contentBrush)
        {
            if (_contentPresenter == null || contentBrush == null)
            {
                return;
            }

            if (_textBlockStyle == null)
            {
                _textBlockForegroundSetter = new Setter(TextBlock.ForegroundProperty, contentBrush);
                _textBlockStyle = new Style(x => x.OfType<TextBlock>())
                {
                    Setters = { _textBlockForegroundSetter }
                };
                _contentPresenter.Styles.Add(_textBlockStyle);
            }
            else if (_textBlockForegroundSetter != null)
            {
                _textBlockForegroundSetter.Value = contentBrush;
            }
        }
    }
}
