using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyBadgeVariant
    {
        Default,
        Neutral,
        Primary,
        Secondary,
        Accent,
        Ghost,
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// A Badge control styled after DaisyUI's Badge component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyBadge : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyBadge);

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            var tokenSize = Size.ToTokenSizeKey();
            var baseFontSize = this.GetResourceOrDefault($"DaisyBadge{tokenSize}FontSize", FontSize);
            var baseHeight = this.GetResourceOrDefault($"DaisyBadge{tokenSize}Height", Height);
            var basePadding = this.GetResourceOrDefault($"DaisyBadge{tokenSize}Padding", Padding);

            FontSize = FloweryScaleManager.ApplyScale(baseFontSize, scaleFactor);
            Height = FloweryScaleManager.ApplyScale(baseHeight, scaleFactor);

            Padding = new Thickness(
                FloweryScaleManager.ApplyScale(basePadding.Left, scaleFactor),
                FloweryScaleManager.ApplyScale(basePadding.Top, scaleFactor),
                FloweryScaleManager.ApplyScale(basePadding.Right, scaleFactor),
                FloweryScaleManager.ApplyScale(basePadding.Bottom, scaleFactor));
        }

        public static readonly StyledProperty<DaisyBadgeVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyBadge, DaisyBadgeVariant>(nameof(Variant), DaisyBadgeVariant.Default);

        public DaisyBadgeVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyBadge, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<bool> IsOutlineProperty =
            AvaloniaProperty.Register<DaisyBadge, bool>(nameof(IsOutline));

        public bool IsOutline
        {
            get => GetValue(IsOutlineProperty);
            set => SetValue(IsOutlineProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SizeProperty && FloweryScaleManager.GetEnableScaling(this))
            {
                ApplyScaleFactor(FloweryScaleManager.GetScaleFactor(this));
            }
        }
    }
}
