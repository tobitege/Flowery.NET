using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// Partial class containing overlay effect logic for DaisyStatusIndicator.
    /// Overlays include glow effects, shadows, and other visual enhancements.
    /// </summary>
    public partial class DaisyStatusIndicator
    {
        private Border? _glowOverlay;

        private void InitializeOverlayElements(TemplateAppliedEventArgs e)
        {
            _glowOverlay = e.NameScope.Find<Border>("PART_GlowOverlay");
        }

        private void UpdateOverlayEffects()
        {
            var needsGlowOverlay = Variant == DaisyStatusIndicatorVariant.Glow ||
                                   Variant == DaisyStatusIndicatorVariant.Twinkle ||
                                   Variant == DaisyStatusIndicatorVariant.Flash;

            if (_glowOverlay != null)
            {
                _glowOverlay.IsVisible = needsGlowOverlay;
            }
        }
    }
}
