using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Services;

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

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 12.0, scaleFactor);
        }

        // Background Image property?
        // Or just allow user to put Image in Background.
        // DaisyUI Hero usually has `hero-overlay` and `hero-content`.
    }
}
