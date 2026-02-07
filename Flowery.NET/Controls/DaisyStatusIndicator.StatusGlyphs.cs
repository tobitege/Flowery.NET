using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Flowery.Enums;

namespace Flowery.Controls
{
    /// <summary>
    /// Partial class containing status glyph variant logic (Battery, TrafficLight, WiFi, Cellular).
    /// </summary>
    public partial class DaisyStatusIndicator
    {
        private Border? _batteryBar1;
        private Border? _batteryBar2;
        private Border? _batteryBar3;
        private Border? _batteryBar4;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // Initialize battery bar elements
            _batteryBar1 = e.NameScope.Find<Border>("PART_BatteryBar1");
            _batteryBar2 = e.NameScope.Find<Border>("PART_BatteryBar2");
            _batteryBar3 = e.NameScope.Find<Border>("PART_BatteryBar3");
            _batteryBar4 = e.NameScope.Find<Border>("PART_BatteryBar4");

            // Initialize animation elements (from Animations partial)
            InitializeAnimationElements(e);

            // Initialize overlay elements (from Overlays partial)
            InitializeOverlayElements(e);

            // Update all visuals
            UpdateGlyphVisuals();
            UpdateAnimationVisibility();
            UpdateOverlayEffects();
        }

        private void UpdateGlyphVisuals()
        {
            if (Variant == DaisyStatusIndicatorVariant.Battery)
            {
                UpdateBatteryVisual();
            }
        }

        private void UpdateBatteryVisual()
        {
            // Update battery bar opacities based on charge percentage
            // Bar 1: >= 25%
            // Bar 2: >= 50%
            // Bar 3: >= 75%
            // Bar 4: >= 98% (near-full)

            var charge = BatteryChargePercent;
            const double dimOpacity = 0.2;
            const double litOpacity = 1.0;

            if (_batteryBar1 != null)
            {
                _batteryBar1.Opacity = charge >= 25 ? litOpacity : dimOpacity;
            }

            if (_batteryBar2 != null)
            {
                _batteryBar2.Opacity = charge >= 50 ? litOpacity : dimOpacity;
            }

            if (_batteryBar3 != null)
            {
                _batteryBar3.Opacity = charge >= 75 ? litOpacity : dimOpacity;
            }

            if (_batteryBar4 != null)
            {
                _batteryBar4.Opacity = charge >= 98 ? litOpacity : dimOpacity;
            }
        }

        private void OnBatteryChargePercentChanged()
        {
            if (Variant == DaisyStatusIndicatorVariant.Battery)
            {
                UpdateBatteryVisual();
            }
        }
    }
}
