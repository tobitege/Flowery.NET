using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyMaskVariant
    {
        Squircle,
        Heart,
        Hexagon,
        Circle,
        Square,
        Diamond,
        Triangle
    }

    /// <summary>
    /// A mask shape container that clips content to various shapes.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyMask : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyMask);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<DaisyMaskVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyMask, DaisyMaskVariant>(nameof(Variant), DaisyMaskVariant.Squircle);

        public DaisyMaskVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        static DaisyMask()
        {
            AffectsRender<DaisyMask>(VariantProperty);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BoundsProperty || change.Property == VariantProperty)
            {
                UpdateClip();
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var result = base.ArrangeOverride(finalSize);
            UpdateClip();
            return result;
        }

        private void UpdateClip()
        {
            var w = Bounds.Width;
            var h = Bounds.Height;
            if (w <= 0 || h <= 0)
            {
                Clip = null;
                return;
            }

            Clip = Variant switch
            {
                DaisyMaskVariant.Circle => new EllipseGeometry { Rect = new Rect(0, 0, w, h) },
                DaisyMaskVariant.Square => new RectangleGeometry { Rect = new Rect(0, 0, w, h) },
                DaisyMaskVariant.Squircle => CreateScaledGeometry("M 50,0 C 10,0 0,10 0,50 0,90 10,100 50,100 90,100 100,90 100,50 100,10 90,0 50,0 Z", w, h),
                DaisyMaskVariant.Heart => CreateScaledGeometry("M50,90 C50,90 10,50 10,30 A20,20 0 0 1 50,15 A20,20 0 0 1 90,30 C90,50 50,90 50,90 Z", w, h),
                DaisyMaskVariant.Hexagon => CreateScaledGeometry("M50,0 L100,25 L100,75 L50,100 L0,75 L0,25 Z", w, h),
                DaisyMaskVariant.Triangle => CreateScaledGeometry("M50,0 L100,100 L0,100 Z", w, h),
                DaisyMaskVariant.Diamond => CreateScaledGeometry("M50,0 L100,50 L50,100 L0,50 Z", w, h),
                _ => new EllipseGeometry { Rect = new Rect(0, 0, w, h) }
            };
        }

        private static Geometry CreateScaledGeometry(string pathData, double width, double height)
        {
            var geometry = Geometry.Parse(pathData);
            var clone = geometry.Clone();
            clone.Transform = new ScaleTransform(width / 100.0, height / 100.0);
            return clone;
        }
    }
}
