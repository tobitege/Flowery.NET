using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// Partial class containing animation variant logic for DaisyStatusIndicator.
    /// Animation variants are primarily handled via AXAML styles, but this partial
    /// provides hooks for any code-behind animation requirements.
    /// </summary>
    public partial class DaisyStatusIndicator
    {
        private Ellipse? _mainEllipse;
        private Ellipse? _animationEllipse;
        private Ellipse? _animationEllipse2;
        private Ellipse? _animationEllipse3;

        /// <summary>
        /// Gets the main ellipse element from the template.
        /// </summary>
        protected Ellipse? MainEllipse => _mainEllipse;

        /// <summary>
        /// Gets the primary animation ellipse element from the template.
        /// </summary>
        protected Ellipse? AnimationEllipse => _animationEllipse;

        private void InitializeAnimationElements(TemplateAppliedEventArgs e)
        {
            _mainEllipse = e.NameScope.Find<Ellipse>("PART_MainEllipse");
            _animationEllipse = e.NameScope.Find<Ellipse>("PART_AnimationEllipse");
            _animationEllipse2 = e.NameScope.Find<Ellipse>("PART_AnimationEllipse2");
            _animationEllipse3 = e.NameScope.Find<Ellipse>("PART_AnimationEllipse3");
        }

        private void OnVariantChanged()
        {
            UpdateAnimationVisibility();
        }

        private void UpdateAnimationVisibility()
        {
            var needsAnimationLayers = Variant switch
            {
                DaisyStatusIndicatorVariant.Ping => true,
                DaisyStatusIndicatorVariant.Ripple => true,
                DaisyStatusIndicatorVariant.Heartbeat => true,
                DaisyStatusIndicatorVariant.Spin => true,
                DaisyStatusIndicatorVariant.Glow => true,
                DaisyStatusIndicatorVariant.Orbit => true,
                DaisyStatusIndicatorVariant.Radar => true,
                DaisyStatusIndicatorVariant.Sonar => true,
                DaisyStatusIndicatorVariant.Beacon => true,
                DaisyStatusIndicatorVariant.Ring => true,
                DaisyStatusIndicatorVariant.Splash => true,
                _ => false
            };

            if (_animationEllipse != null)
            {
                _animationEllipse.IsVisible = needsAnimationLayers;
            }

            if (_animationEllipse2 != null)
            {
                var needsSecondLayer = Variant == DaisyStatusIndicatorVariant.Ripple ||
                                       Variant == DaisyStatusIndicatorVariant.Spin ||
                                       Variant == DaisyStatusIndicatorVariant.Glow ||
                                       Variant == DaisyStatusIndicatorVariant.Radar ||
                                       Variant == DaisyStatusIndicatorVariant.Sonar ||
                                       Variant == DaisyStatusIndicatorVariant.Splash;
                _animationEllipse2.IsVisible = needsSecondLayer;
            }

            if (_animationEllipse3 != null)
            {
                var needsThirdLayer = Variant == DaisyStatusIndicatorVariant.Ripple ||
                                      Variant == DaisyStatusIndicatorVariant.Radar ||
                                      Variant == DaisyStatusIndicatorVariant.Splash;
                _animationEllipse3.IsVisible = needsThirdLayer;
            }
        }
    }
}
