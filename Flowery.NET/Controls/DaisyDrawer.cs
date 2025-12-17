using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A drawer/sidebar control styled after DaisyUI's Drawer component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyDrawer : SplitView, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyDrawer);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        // Defaults for DaisyUI Drawer
        public DaisyDrawer()
        {
            DisplayMode = SplitViewDisplayMode.Inline;
            PanePlacement = SplitViewPanePlacement.Left;
            OpenPaneLength = 300; // Typical drawer width
            CompactPaneLength = 0;
            // Don't set IsPaneOpen here - let XAML control it
        }
    }
}
