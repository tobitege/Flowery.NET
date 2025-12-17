using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A join container control styled after DaisyUI's Join component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyJoin : StackPanel, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyJoin);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            SetValue(TextBlock.FontSizeProperty, FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor));
        }

        public DaisyJoin()
        {
            Orientation = Orientation.Horizontal;
        }
    }
}
