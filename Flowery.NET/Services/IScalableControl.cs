namespace Flowery.Services
{
    /// <summary>
    /// Interface for controls that support automatic font scaling via FloweryScaleManager.
    /// </summary>
    public interface IScalableControl
    {
        /// <summary>
        /// Applies the given scale factor to the control's text elements.
        /// Called automatically when the window resizes and EnableScaling is true.
        /// </summary>
        /// <param name="scaleFactor">Scale factor between 0.5 and 1.0</param>
        void ApplyScaleFactor(double scaleFactor);
    }
}
