using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.RuntimeTests.Controls;
using SkiaSharp;
using Xunit;
using DaisyCardStyle = Flowery.Enums.DaisyCardStyle;

namespace Flowery.NET.RuntimeTests.Rendering;

/// <summary>
/// A shadow that a control sets must reach the screen. Avalonia, for example, draws no BoxShadow for a Border
/// with uneven BorderThickness, so a property check alone cannot tell whether the shadow is visible.
/// </summary>
public class Given_ShadowRendering
{
    // Room around the control for shadows that extend outside its bounds.
    private const double Margin = 48;
    // A drawn shadow changes at least this many pixels by more than the channel tolerance.
    private const int MinimumChangedPixels = 8;
    private const int ChannelTolerance = 2;
    // A smaller caster spreads too little coverage over its blur to show, such as an unopened dialog's 2x2 border.
    private const double MinimumCasterSize = 16;

    // Configurations that show a shadow; each must keep showing one.
    private static readonly Dictionary<string, Func<Control>> Scenarios = new()
    {
        ["button-shadow"] = () => new DaisyButton { Content = "Shadow", ShowShadow = true },
        ["button-group-shadow"] = () => new DaisyButtonGroup
        {
            ShowShadow = true,
            Items = { new DaisyButton { Content = "One" }, new DaisyButton { Content = "Two" } }
        },
        ["card-beveled"] = () => new DaisyCard { CardStyle = DaisyCardStyle.Beveled, Width = 160, Height = 96, Content = "Card" },
        ["card-inset"] = () => new DaisyCard { CardStyle = DaisyCardStyle.Inset, Width = 160, Height = 96, Content = "Card" },
        ["number-flow-controls"] = () => new DaisyNumberFlow { ShowControls = true, Value = 42 },
        ["slide-three-dimensional"] = () => new DaisySlideToConfirm { DepthStyle = DaisyDepthStyle.ThreeDimensional, Width = 240 },
        ["slide-raised"] = () => new DaisySlideToConfirm { DepthStyle = DaisyDepthStyle.Raised, Width = 240 }
    };

    [Fact]
    public async Task When_A_Control_Shows_A_Shadow_By_Default_It_Is_Drawn()
    {
        var controlTypes = ControlCatalog.GetDaisyControls()
            .Concat(ControlCatalog.GetColorPickerControls())
            .Concat(ControlCatalog.GetCustomControls())
            .Concat(ControlCatalog.GetKanbanControls())
            .Where(type => type.GetConstructor(Type.EmptyTypes) is not null && !typeof(Window).IsAssignableFrom(type));

        var failures = new StringBuilder();
        foreach (var type in controlTypes)
        {
            Control control;
            try
            {
                control = (Control)Activator.CreateInstance(type)!;
            }
            catch
            {
                // Given_AllControls reports construction failures.
                continue;
            }

            await CheckShadowsAsync(type.Name, control, failures);
        }

        Assert.True(failures.Length == 0, failures.ToString());
    }

    [Theory]
    [InlineData("button-shadow")]
    [InlineData("button-group-shadow")]
    [InlineData("card-beveled")]
    [InlineData("card-inset")]
    [InlineData("number-flow-controls")]
    [InlineData("slide-three-dimensional")]
    [InlineData("slide-raised")]
    public async Task When_A_Configuration_Shows_A_Shadow_It_Is_Drawn(string scenario)
    {
        var failures = new StringBuilder();
        var checkedShadows = await CheckShadowsAsync(scenario, Scenarios[scenario](), failures);

        Assert.True(checkedShadows > 0, $"The {scenario} configuration no longer shows a shadow.");
        Assert.True(failures.Length == 0, failures.ToString());
    }

    /// <summary>
    /// Renders <paramref name="control"/> in a frame with room for shadows and checks every shadow it shows.
    /// Returns the number of checked shadows.
    /// </summary>
    private static async Task<int> CheckShadowsAsync(string name, Control control, StringBuilder failures)
    {
        // Shadows are designed for light surfaces, where they contrast most.
        Assert.True(DaisyThemeManager.ApplyTheme("Light"));
        // Dialogs that build their own chrome in the constructor are attached through that chrome.
        var root = control;
        while (root.GetVisualParent() is Control parent)
            root = parent;
        var frame = new Border
        {
            Padding = new Thickness(Margin),
            Background = Brushes.White,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = root
        };
        await RuntimeTestHelpers.AttachAsync(frame);

        var checkedShadows = 0;
        foreach (var (caster, shadow) in FindShadowCasters(root))
        {
            checkedShadows++;
            var changed = CountShadowPixels(frame, caster, shadow);
            if (changed < MinimumChangedPixels)
            {
                failures.AppendLine($"{name}: the shadow of {Describe(caster)} changed {changed} pixels.");
                using var _ = RuntimeTestHelpers.Render(frame, $"shadow-{name}");
            }
        }

        return checkedShadows;
    }

    /// <summary>Visible borders and presenters large enough to cast their current, visible BoxShadow.</summary>
    private static IEnumerable<(Control Caster, BoxShadows Shadow)> FindShadowCasters(Control root)
    {
        foreach (var visual in root.GetSelfAndVisualDescendants().OfType<Control>())
        {
            var shadow = visual switch
            {
                Border border => border.BoxShadow,
                ContentPresenter presenter => presenter.BoxShadow,
                _ => default
            };
            if (visual.IsEffectivelyVisible
                && visual.Bounds.Width >= MinimumCasterSize && visual.Bounds.Height >= MinimumCasterSize
                && IsVisibleShadow(shadow))
                yield return (visual, shadow);
        }
    }

    private static bool IsVisibleShadow(BoxShadows shadows)
    {
        foreach (var shadow in shadows)
        {
            if (shadow.Color.A > 0 && (shadow.Blur > 0 || shadow.Spread > 0 || shadow.OffsetX != 0 || shadow.OffsetY != 0))
                return true;
        }

        return false;
    }

    /// <summary>Renders the frame with and without the shadow and counts the pixels that differ.</summary>
    private static int CountShadowPixels(Border frame, Control caster, BoxShadows shadow)
    {
        // A transition would animate the removal; both images must show settled values.
        var transitions = caster.Transitions;
        caster.Transitions = null;
        try
        {
            using var withShadow = RuntimeTestHelpers.Render(frame);
            SetShadow(caster, default);
            using var withoutShadow = RuntimeTestHelpers.Render(frame);
            return CountDifferences(withShadow, withoutShadow);
        }
        finally
        {
            SetShadow(caster, shadow);
            caster.Transitions = transitions;
        }
    }

    private static void SetShadow(Control caster, BoxShadows shadow)
    {
        if (caster is Border border)
            border.BoxShadow = shadow;
        else if (caster is ContentPresenter presenter)
            presenter.BoxShadow = shadow;
    }

    private static int CountDifferences(SKBitmap first, SKBitmap second)
    {
        var differences = 0;
        for (var y = 0; y < first.Height; y++)
        for (var x = 0; x < first.Width; x++)
        {
            var a = first.GetPixel(x, y);
            var b = second.GetPixel(x, y);
            if (Math.Abs(a.Red - b.Red) > ChannelTolerance || Math.Abs(a.Green - b.Green) > ChannelTolerance
                || Math.Abs(a.Blue - b.Blue) > ChannelTolerance)
            {
                differences++;
            }
        }

        return differences;
    }

    private static string Describe(Control caster) =>
        string.IsNullOrEmpty(caster.Name) ? caster.GetType().Name : $"{caster.GetType().Name} '{caster.Name}'";
}
