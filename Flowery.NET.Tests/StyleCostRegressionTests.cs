using Avalonia;
using Avalonia.Animation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Xunit;
using DaisySize = Flowery.Controls.DaisySize;

namespace Flowery.NET.Tests;

/// <summary>
/// Styles that cost every control of an app something moved to where they cost only the controls they are for. These
/// tests keep the looks and the behavior they had.
/// </summary>
public class StyleCostRegressionTests
{
    [AvaloniaFact]
    public void DateTimelineBuildsOnlyTheDaysInViewAndShowsTheSelectedDay()
    {
        var date = new DateTime(2026, 10, 15);
        var timeline = new DaisyDateTimeline
        {
            Width = 400, FirstDate = date.AddMonths(-3), LastDate = date.AddMonths(3), SelectedDate = date.AddMonths(2)
        };
        var window = Show(timeline);
        try
        {
            var days = timeline.GetVisualDescendants().OfType<ItemsControl>().Single().ItemsSource!.Cast<DaisyDateTimelineItem>().Count();
            var built = timeline.GetVisualDescendants().OfType<DaisyDateTimelineItem>().ToList();
            Assert.True(built.Count < days / 4, $"{built.Count} of {days} days are built.");
            var selected = Assert.Single(built, static item => item.IsSelected);
            Assert.Equal(date.AddMonths(2), selected.Date);
            var scrollViewer = timeline.GetVisualDescendants().OfType<ScrollViewer>().Single();
            var left = selected.TranslatePoint(default, scrollViewer)!.Value.X;
            Assert.InRange(left, 0, scrollViewer.Viewport.Width - selected.Bounds.Width);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ReducedMotionAndHighContrastReachEveryControlAndClearTheirEffects()
    {
        var previousMotion = DaisyAccessibility.ReducedMotionOverride;
        var previousContrast = DaisyAccessibility.HighContrastOverride;
        DaisyAccessibility.ReducedMotionOverride = false;
        DaisyAccessibility.HighContrastOverride = false;
        // Values from a control theme, which the accessibility styles override, as they do for the Daisy themes.
        var transitions = new Transitions { new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromSeconds(1) } };
        var shadow = BoxShadows.Parse("0 4 8 0 #40000000");
        var effect = new DropShadowEffect();
        var inner = new TextBlock { Text = "Inner" };
        var border = new Border
        {
            Theme = new ControlTheme(typeof(Border))
            {
                Setters =
                {
                    new Setter(Animatable.TransitionsProperty, transitions),
                    new Setter(Border.BoxShadowProperty, shadow),
                    new Setter(Visual.EffectProperty, effect),
                }
            },
            Child = new StackPanel { Children = { inner } }
        };
        var window = Show(border);
        try
        {
            Assert.Same(transitions, border.Transitions);
            Assert.True(DaisyAccessibility.GetMotionEnabled(inner));

            DaisyAccessibility.ReducedMotionOverride = true;
            Flush(window);
            Assert.False(DaisyAccessibility.GetMotionEnabled(inner));
            Assert.Null(border.Transitions);

            DaisyAccessibility.ReducedMotionOverride = false;
            Flush(window);
            Assert.True(DaisyAccessibility.GetMotionEnabled(inner));
            Assert.Same(transitions, border.Transitions);

            DaisyAccessibility.HighContrastOverride = true;
            Flush(window);
            Assert.True(DaisyAccessibility.GetHighContrastEnabled(inner));
            Assert.Null(border.Effect);
            Assert.Equal(1, border.BoxShadow.Count);
            Assert.Equal(Colors.Transparent, border.BoxShadow[0].Color);

            DaisyAccessibility.HighContrastOverride = false;
            Flush(window);
            Assert.False(DaisyAccessibility.GetHighContrastEnabled(inner));
            Assert.Same(effect, border.Effect);
            Assert.Equal(shadow, border.BoxShadow);
        }
        finally
        {
            window.Close();
            DaisyAccessibility.ReducedMotionOverride = previousMotion;
            DaisyAccessibility.HighContrastOverride = previousContrast;
        }
    }

    [AvaloniaFact]
    public void ContributionCellsKeepTheirLookAndAppStylesStillOverrideIt()
    {
        var graph = new DaisyContributionGraph
        {
            Year = 2026,
            Contributions = [new DaisyContributionDay { Date = new DateTime(2026, 3, 2), Count = 50, Level = 4 }]
        };
        var window = Show(graph);
        try
        {
            var cells = graph.GetVisualDescendants().OfType<Border>().Where(static border => border.Classes.Contains("daisy-contribution-cell")).ToList();
            var primary = window.FindResource("DaisyPrimaryBrush");
            var busiest = Assert.Single(cells, static cell => Equals(cell.Tag, 4));
            Assert.Same(primary, busiest.Background);
            Assert.Equal(1, busiest.Opacity);
            var legend = cells.Single(static cell => Equals(cell.Tag, "2"));
            Assert.Same(primary, legend.Background);
            Assert.Equal(0.45, legend.Opacity);

            // The override of the documentation, from the styles of the app.
            var red = new SolidColorBrush(Colors.Red);
            var overrideStyle = new Style(static x => x.OfType<Border>().Class("daisy-contribution-cell").PropertyEquals(Border.TagProperty, 4))
            {
                Setters = { new Setter(Border.BackgroundProperty, red) }
            };
            Application.Current!.Styles.Add(overrideStyle);
            try
            {
                Flush(window);
                Assert.Same(red, busiest.Background);
            }
            finally { Application.Current.Styles.Remove(overrideStyle); }
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void ALocalAccessibilityValueReachesTheControlsInside()
    {
        var previousMotion = DaisyAccessibility.ReducedMotionOverride;
        DaisyAccessibility.ReducedMotionOverride = false;
        var transitions = new Transitions { new DoubleTransition { Property = Visual.OpacityProperty, Duration = TimeSpan.FromSeconds(1) } };
        var inner = new Border { Theme = new ControlTheme(typeof(Border)) { Setters = { new Setter(Animatable.TransitionsProperty, transitions) } } };
        var container = new StackPanel { Children = { inner } };
        var window = Show(container);
        try
        {
            Assert.True(DaisyAccessibility.GetMotionEnabled(inner));
            DaisyAccessibility.SetMotionEnabled(container, false);
            Flush(window);
            Assert.False(DaisyAccessibility.GetMotionEnabled(inner));
            Assert.Null(inner.Transitions);
        }
        finally
        {
            window.Close();
            DaisyAccessibility.ReducedMotionOverride = previousMotion;
        }
    }

    [AvaloniaFact]
    public async Task EveryLoadingVariantAnimatesItsPartsAndStandsStillUnderReducedMotion()
    {
        var previousMotion = DaisyAccessibility.ReducedMotionOverride;
        DaisyAccessibility.ReducedMotionOverride = false;
        var loaders = Enum.GetValues<DaisyLoadingVariant>().Select(static variant => new DaisyLoading { Variant = variant, Size = DaisySize.Large }).ToList();
        var panel = new WrapPanel();
        foreach (var loader in loaders)
            panel.Children.Add(loader);
        var window = Show(panel);
        try
        {
            // An animation can rest for most of its cycle (DocumentStamp holds for 1.2 of 2 s), so the frames span the
            // longest cycle, 3.6 s, and a variant passes when any of them differs from the first.
            var first = await Frames(window, loaders, TimeSpan.Zero);
            var changed = new bool[loaders.Count];
            for (var frame = 0; frame < 10 && changed.Contains(false); frame++)
            {
                var next = await Frames(window, loaders, TimeSpan.FromMilliseconds(400));
                for (var i = 0; i < loaders.Count; i++)
                    changed[i] |= next[i] != first[i];
            }
            var still = loaders.Where((_, i) => !changed[i]).Select(static loader => loader.Variant).ToList();
            Assert.True(still.Count == 0, $"Not animated: {string.Join(", ", still)}");

            DaisyAccessibility.ReducedMotionOverride = true;
            var stopped = await Frames(window, loaders, TimeSpan.FromMilliseconds(50));
            var later = await Frames(window, loaders, TimeSpan.FromMilliseconds(350));
            var moving = loaders.Where((_, i) => stopped[i] != later[i]).Select(static loader => loader.Variant).ToList();
            Assert.True(moving.Count == 0, $"Animated under reduced motion: {string.Join(", ", moving)}");
        }
        finally
        {
            window.Close();
            DaisyAccessibility.ReducedMotionOverride = previousMotion;
        }
    }

    [AvaloniaFact]
    public void Win95StartupVariantsShareTheirTemplateAndKeepTheirTileColors()
    {
        var loader = new DaisyLoading { Variant = DaisyLoadingVariant.Win95Startup, Foreground = Brushes.Teal };
        var window = Show(loader);
        try
        {
            var tiles = loader.GetVisualDescendants().OfType<Border>().Where(static border => border.Classes.Any(static c => c.StartsWith("startup", StringComparison.Ordinal))).ToList();
            Assert.Equal(4, tiles.Count);
            Assert.All(tiles, static tile => Assert.Same(Brushes.Teal, tile.Background));

            loader.Variant = DaisyLoadingVariant.Win95StartupColor;
            Flush(window);
            var colors = loader.GetVisualDescendants().OfType<Border>().Where(static border => border.Classes.Any(static c => c.StartsWith("startup", StringComparison.Ordinal)))
                .Select(static tile => Assert.IsAssignableFrom<ISolidColorBrush>(tile.Background).Color).ToList();
            Assert.Equal(new[] { Color.Parse("#F25022"), Color.Parse("#7FBA00"), Color.Parse("#00A4EF"), Color.Parse("#FFB900") }, colors);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public void StatusIndicatorGlyphPartsFollowSizeAndSignalStrength()
    {
        var light = new DaisyStatusIndicator { Variant = DaisyStatusIndicatorVariant.TrafficLightVertical, Size = DaisySize.Large };
        var wifi = new DaisyStatusIndicator { Variant = DaisyStatusIndicatorVariant.WifiSignal, SignalStrength = 2 };
        var window = Show(new StackPanel { Children = { light, wifi } });
        try
        {
            var lights = light.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Ellipse>().Where(static e => e.Classes.Contains("traffic-light-light")).ToList();
            Assert.NotEmpty(lights);
            Assert.All(lights, static e => Assert.Equal(12, e.Width));

            var arc3 = wifi.GetVisualDescendants().OfType<Avalonia.Controls.Shapes.Path>().Single(static p => p.Name == "PART_Arc3");
            Assert.NotEqual(1, arc3.Opacity);
            wifi.SignalStrength = 3;
            Flush(window);
            Assert.Equal(1, arc3.Opacity);
        }
        finally { window.Close(); }
    }

    private static async Task<List<string>> Frames(Window window, List<DaisyLoading> loaders, TimeSpan delay)
    {
        if (delay > TimeSpan.Zero)
            await Task.Delay(delay);
        AvaloniaHeadlessPlatform.ForceRenderTimerTick(1);
        Flush(window);
        return loaders.Select(static loader => string.Join(";", loader.GetVisualDescendants().Select(static v =>
            $"{v.Opacity:F3}|{v.RenderTransform?.Value}|{v.Bounds}|{(v as Avalonia.Controls.Shapes.Shape)?.StrokeDashOffset:F2}"))).ToList();
    }

    private static Window Show(Control control)
    {
        var window = new Window { Content = control, Width = 1200, Height = 900 };
        window.Show();
        Flush(window);
        return window;
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }
}
