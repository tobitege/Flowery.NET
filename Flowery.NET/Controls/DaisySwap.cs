using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum SwapEffect
    {
        None,
        Rotate,
        Flip
    }

    /// <summary>
    /// A swap toggle control styled after DaisyUI's Swap component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisySwap : ToggleButton, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisySwap);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<object?> OnContentProperty =
            AvaloniaProperty.Register<DaisySwap, object?>(nameof(OnContent));

        public static readonly StyledProperty<object?> OffContentProperty =
            AvaloniaProperty.Register<DaisySwap, object?>(nameof(OffContent));

        public static readonly StyledProperty<object?> IndeterminateContentProperty =
            AvaloniaProperty.Register<DaisySwap, object?>(nameof(IndeterminateContent));

        public static readonly StyledProperty<SwapEffect> TransitionEffectProperty =
            AvaloniaProperty.Register<DaisySwap, SwapEffect>(nameof(TransitionEffect), SwapEffect.None);

        public object? OnContent
        {
            get => GetValue(OnContentProperty);
            set => SetValue(OnContentProperty, value);
        }

        public object? OffContent
        {
            get => GetValue(OffContentProperty);
            set => SetValue(OffContentProperty, value);
        }

        public object? IndeterminateContent
        {
            get => GetValue(IndeterminateContentProperty);
            set => SetValue(IndeterminateContentProperty, value);
        }

        public SwapEffect TransitionEffect
        {
            get => GetValue(TransitionEffectProperty);
            set => SetValue(TransitionEffectProperty, value);
        }
    }
}
