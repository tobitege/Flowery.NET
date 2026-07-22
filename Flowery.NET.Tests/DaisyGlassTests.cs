using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyGlassTests
    {
        [AvaloniaFact]
        public void Should_Have_Liquid_Glass_Defaults()
        {
            var glass = new DaisyGlass();

            Assert.Equal(GlassBlurMode.BitmapCapture, glass.BlurMode);
            Assert.Equal(0.65, glass.GlassDepth);
            Assert.Equal(0.6, glass.GlassCurvature);
            Assert.Equal(0.35, glass.GlassBend);
            Assert.Equal(0.18, glass.GlassDispersion);
        }

        [AvaloniaFact]
        public void Should_Initialize_SkiaSharp_Liquid_Glass_Mode()
        {
            var glass = new DaisyGlass
            {
                EnableBackdropBlur = true,
                BlurMode = GlassBlurMode.SkiaSharp,
                GlassTint = Colors.White,
                GlassTintOpacity = 0.2,
                GlassDepth = 0.8,
                GlassCurvature = 0.75,
                GlassBend = 0.5,
                GlassDispersion = 0.25,
                Width = 160,
                Height = 96
            };

            var window = new Window { Content = glass };
            window.Show();

            Assert.Equal(GlassBlurMode.SkiaSharp, glass.BlurMode);
            Assert.True(glass.EnableBackdropBlur);
            Assert.Equal(0.8, glass.GlassDepth);
            Assert.Equal(0.75, glass.GlassCurvature);
            Assert.Equal(0.5, glass.GlassBend);
            Assert.Equal(0.25, glass.GlassDispersion);
        }
    }
}
