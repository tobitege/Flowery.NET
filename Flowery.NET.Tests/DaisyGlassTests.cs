using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyGlassTests
    {
        [AvaloniaFact]
        public void A_Gradient_Preset_Fills_The_Glass_Until_A_Fill_Brush_Replaces_It()
        {
            var glass = new DaisyGlass { Width = 160, Height = 60 };
            var window = new Window { Content = glass };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                var fill = glass.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_Fill");
                Assert.Null(glass.EffectiveGlassFill);

                glass.GlassGradient = DaisyGlassGradient.Aurora;
                var aurora = Assert.IsType<LinearGradientBrush>(glass.EffectiveGlassFill);
                Assert.Equal(3, aurora.GradientStops.Count);
                Assert.Same(aurora, fill.Background);
                Assert.Equal(0.6, fill.Opacity);

                var custom = new SolidColorBrush(Colors.Orange);
                glass.GlassFill = custom;
                Assert.Same(custom, glass.EffectiveGlassFill);

                glass.GlassFill = null;
                Assert.IsType<LinearGradientBrush>(glass.EffectiveGlassFill);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void A_Resize_Rebuilds_The_Pattern_Only_When_It_Needs_Other_Tiles()
        {
            // Regression: every size change, such as each arrange pass of a resize, rebuilt all pattern tiles.
            var glass = new DaisyGlass { Width = 160, Height = 60, GlassPattern = DaisyCardPattern.Dots };
            var window = new Window { Content = glass, Width = 800, Height = 600 };
            try
            {
                window.Show();
                window.UpdateLayout();
                var layer = glass.GetVisualDescendants().OfType<Canvas>().Single(canvas => canvas.Name == "PART_PatternLayer");
                var tiles = Assert.Single(layer.Children);

                // 160 and 200 both need two tiles of 120 and one more across.
                glass.Width = 200;
                window.UpdateLayout();
                Assert.Same(tiles, Assert.Single(layer.Children));

                glass.Width = 260;
                window.UpdateLayout();
                var wider = Assert.Single(layer.Children);
                Assert.NotSame(tiles, wider);
                Assert.Equal(4 * 2, ((Canvas)wider).Children.Count);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void Without_A_Live_Renderer_A_SkiaSharp_Glass_Uses_BitmapCapture_And_Shows_The_Captured_Backdrop()
        {
            var previous = DaisyGlassLiveRenderer.Current;
            DaisyGlassLiveRenderer.Register(null);
            var glass = new DaisyGlass { Width = 160, Height = 60, EnableBackdropBlur = true, BlurMode = GlassBlurMode.SkiaSharp };
            var window = new Window { Content = glass };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(GlassBlurMode.BitmapCapture, glass.EffectiveBlurMode);
                var panel = glass.GetVisualDescendants().OfType<Canvas>().Single(canvas => canvas.Name == "PART_BackdropBlurPanel");
                Assert.True(panel.IsVisible);
            }
            finally
            {
                window.Close();
                DaisyGlassLiveRenderer.Register(previous);
            }
        }

        [AvaloniaFact]
        public void A_Renderer_Registered_After_The_Glass_Was_Created_Draws_It_Once_It_Is_Shown()
        {
            var previous = DaisyGlassLiveRenderer.Current;
            DaisyGlassLiveRenderer.Register(null);
            var glass = new DaisyGlass { Width = 160, Height = 60, EnableBackdropBlur = true, BlurMode = GlassBlurMode.SkiaSharp };
            var renderer = new RecordingRenderer();
            var window = new Window { Content = glass };
            try
            {
                Assert.Equal(GlassBlurMode.BitmapCapture, glass.EffectiveBlurMode);
                DaisyGlassLiveRenderer.Register(renderer);
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(GlassBlurMode.SkiaSharp, glass.EffectiveBlurMode);

                using (var context = new DrawingGroup().Open())
                    glass.Render(context);
                Assert.Same(glass, renderer.Rendered);
            }
            finally
            {
                window.Close();
                DaisyGlassLiveRenderer.Register(previous);
            }
        }

        [AvaloniaFact]
        public void A_Shown_Glass_Follows_A_Renderer_That_Is_Removed_Or_Registered()
        {
            // Regression: after Register(null) a shown SkiaSharp glass drew no backdrop instead of capturing it.
            var previous = DaisyGlassLiveRenderer.Current;
            var renderer = new RecordingRenderer();
            DaisyGlassLiveRenderer.Register(renderer);
            var glass = new DaisyGlass { Width = 160, Height = 60, EnableBackdropBlur = true, BlurMode = GlassBlurMode.SkiaSharp };
            var window = new Window { Content = new Border { Background = Brushes.Blue, Child = glass } };
            try
            {
                window.Show();
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(GlassBlurMode.SkiaSharp, glass.EffectiveBlurMode);
                Assert.Null(glass.BlurredBackground);

                DaisyGlassLiveRenderer.Register(null);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(GlassBlurMode.BitmapCapture, glass.EffectiveBlurMode);
                Assert.NotNull(glass.BlurredBackground);

                DaisyGlassLiveRenderer.Register(renderer);
                Assert.Equal(GlassBlurMode.SkiaSharp, glass.EffectiveBlurMode);
                Assert.Null(glass.BlurredBackground);
            }
            finally
            {
                window.Close();
                DaisyGlassLiveRenderer.Register(previous);
            }
        }

        private sealed class RecordingRenderer : DaisyGlassLiveRenderer
        {
            public DaisyGlass? Rendered { get; private set; }

            public override void Render(DaisyGlass glass, DrawingContext context) => Rendered = glass;
        }
    }
}
