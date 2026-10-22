using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.RuntimeTests.Controls;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.RuntimeTests.Rendering;

/// <summary>
/// Neumorphic shadows must reach the screen: every registered surface draws them in the right places, composite
/// controls keep their rows, cells and segments flat, configurations without a surface draw nothing, and high
/// contrast removes them. Each check renders the same control in the same mode twice, at intensity 0 and at the
/// default intensity. Everything else the effect does (removing a drop shadow Effect, replacing a template
/// BoxShadow, lifting clips) is the same in both images, so only the shadow pixels differ.
/// </summary>
public class Given_NeumorphicRendering
{
    // Room around the control for its outer shadows.
    private const double FrameMargin = 48;
    // Controls stretch to this column width, like the 280-pixel columns of the Gallery's neumorphic showcase.
    private const double ColumnWidth = 280;
    // Space between a checked control and the reference button, wider than both their shadows.
    private const double ReferenceSpacing = 48;
    // A drawn shadow changes at least this many pixels by more than the channel tolerance.
    private const int MinimumChangedPixels = 8;
    private const int ChannelTolerance = 2;

    // Mid-gray, so that the white light shadow and the dark shadow both differ from the background.
    private static readonly IBrush FrameBrush = new SolidColorBrush(Color.FromRgb(160, 160, 160));

    // Controls whose surfaces do not cover the whole control, or whose mode is fixed (see DaisyNeumorphic.md).
    // All other controls have one surface of their full size that follows the global mode.
    private static readonly Dictionary<Type, SurfaceLayout> Layouts = new()
    {
        [typeof(DaisyTabs)] = SurfaceLayout.TopLeftHeader,
        [typeof(DaisyTimelineItem)] = SurfaceLayout.InnerBoxes,
        [typeof(DaisyCheckBox)] = SurfaceLayout.InsetWell,
        [typeof(DaisyRadio)] = SurfaceLayout.InsetWell,
        [typeof(DaisyToggle)] = SurfaceLayout.InsetWellWithRaisedKnob,
        [typeof(DaisySlideToConfirm)] = SurfaceLayout.InsetWellWithRaisedKnob
    };

    // Off by default (see DaisyNeumorphic.md); they draw only when enabled on themselves.
    private static readonly HashSet<Type> DisabledByDefault = [typeof(DaisyChatBubble)];

    [Theory]
    [InlineData(DaisyNeumorphicMode.Raised)]
    [InlineData(DaisyNeumorphicMode.Inset)]
    [InlineData(DaisyNeumorphicMode.Flat)]
    public async Task When_The_Global_Mode_Is_Set_Every_Registered_Surface_Draws_Its_Shadow(DaisyNeumorphicMode mode)
    {
        UseStaticMotion();
        var failures = new StringBuilder();
        var checkedControls = 0;
        foreach (var type in GetRegisteredControls())
        {
            try
            {
                var control = CreateVisible(type);
                var (frame, bounds, _) = await AttachInFrameAsync(control, null);
                DaisyNeumorphic.GlobalMode = mode;
                await RuntimeTestHelpers.WaitForIdleAsync();
                if (DisabledByDefault.Contains(type))
                    DaisyNeumorphic.SetIsEnabled(control, true);
                else if (!DaisyNeumorphic.IsEffectivelyEnabled(control))
                    failures.AppendLine($"{type.Name}: the global mode {mode} does not enable the effect.");

                var pixels = await MeasureShadowAsync(frame, bounds);
                checkedControls++;
                if (CheckShadow(type, mode, pixels) is { } problem)
                {
                    failures.AppendLine($"{type.Name}: {mode} {problem}");
                    using var _ = RuntimeTestHelpers.Render(frame, $"neumorphic-{mode}-{type.Name}");
                }
            }
            catch (Exception exception)
            {
                var cause = RuntimeTestHelpers.Unwrap(exception);
                failures.AppendLine($"{type.Name}: {cause.GetType().Name} - {cause.Message}");
            }
        }

        Assert.True(failures.Length == 0, failures.ToString());
        Assert.True(checkedControls >= 40, $"Only {checkedControls} registered controls were checked.");
    }

    [Theory]
    [InlineData("table", true)]
    [InlineData("button-group", true)]
    [InlineData("join", false)]
    [InlineData("pagination", false)]
    public async Task When_Raised_A_Composite_Control_Keeps_Its_Inside_Flat(string composite, bool hasSurface)
    {
        UseStaticMotion();
        var (frame, bounds, reference) = await AttachInFrameAsync(CreateComposite(composite), new DaisyButton { Content = "Reference" });
        DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Raised;
        var pixels = await MeasureShadowAsync(frame, bounds, reference);

        // The reference button proves that the effect is drawn in this render.
        Assert.True(pixels.ReferenceDarkerBottomRight >= MinimumChangedPixels, "The reference button drew no shadow.");
        // A shadow of a row, cell or segment would change pixels inside the control.
        Assert.Equal(0, pixels.InsideAwayFromEdges);
        if (hasSurface)
            Assert.True(pixels.DarkerBottomRight >= MinimumChangedPixels, $"The {composite} surface drew no outer shadow.");
    }

    [Theory]
    [InlineData("button-ghost")]
    [InlineData("button-link")]
    [InlineData("countdown-without-background")]
    [InlineData("number-flow-without-controls")]
    [InlineData("tabs-bordered")]
    [InlineData("timeline-item-not-boxed")]
    [InlineData("chat-bubble-default")]
    public async Task When_A_Configuration_Has_No_Surface_It_Draws_No_Shadow(string configuration)
    {
        UseStaticMotion();
        var (frame, bounds, reference) = await AttachInFrameAsync(CreateWithoutSurface(configuration), new DaisyButton { Content = "Reference" });
        DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Raised;
        var pixels = await MeasureShadowAsync(frame, bounds, reference);

        Assert.True(pixels.ReferenceDarkerBottomRight >= MinimumChangedPixels, "The reference button drew no shadow.");
        Assert.Equal(0, pixels.Changed);
    }

    [Fact]
    public async Task When_High_Contrast_Turns_On_Every_Shadow_Disappears()
    {
        UseStaticMotion();
        var failures = new StringBuilder();
        foreach (var type in GetRegisteredControls())
        {
            try
            {
                DaisyAccessibility.HighContrastOverride = false;
                var control = CreateVisible(type);
                // High contrast wins even over the control's own settings.
                DaisyNeumorphic.SetIsEnabled(control, true);
                DaisyNeumorphic.SetMode(control, DaisyNeumorphicMode.Raised);
                var (frame, bounds, _) = await AttachInFrameAsync(control, null);
                DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Raised;
                var drawn = await MeasureShadowAsync(frame, bounds);
                if (drawn.Changed < MinimumChangedPixels)
                {
                    failures.AppendLine($"{type.Name}: no shadow was drawn before high contrast turned on.");
                    continue;
                }

                // Turn high contrast on while the shadow is drawn, then compare with the same state without any effect.
                DaisyAccessibility.HighContrastOverride = true;
                await RuntimeTestHelpers.WaitForIdleAsync();
                using var highContrast = RuntimeTestHelpers.Render(frame);
                DaisyNeumorphic.SetIsEnabled(control, null);
                DaisyNeumorphic.SetMode(control, null);
                DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.None;
                await RuntimeTestHelpers.WaitForIdleAsync();
                using var withoutEffect = RuntimeTestHelpers.Render(frame);

                var changed = CountChanged(withoutEffect, highContrast, null, countInside: true);
                if (changed > 0)
                    failures.AppendLine($"{type.Name}: high contrast left {changed} changed pixels.");
            }
            catch (Exception exception)
            {
                var cause = RuntimeTestHelpers.Unwrap(exception);
                failures.AppendLine($"{type.Name}: {cause.GetType().Name} - {cause.Message}");
            }
        }

        Assert.True(failures.Length == 0, failures.ToString());
    }

    /// <summary>The shadow checks compare still images; animations such as the skeleton's pulse would change pixels.</summary>
    private static void UseStaticMotion() => DaisyAccessibility.ReducedMotionOverride = true;

    /// <summary>
    /// Returns a problem if the measured pixels do not match where the control's surfaces draw their shadows in
    /// <paramref name="mode"/>: Raised draws a light shadow top-left and a dark one bottom-right, Flat a single dark
    /// one bottom-right, Inset only inside the surface.
    /// </summary>
    private static string? CheckShadow(Type type, DaisyNeumorphicMode mode, ShadowPixels pixels)
    {
        var layout = Layouts.GetValueOrDefault(type, SurfaceLayout.FullSize);
        var inset = mode == DaisyNeumorphicMode.Inset || layout is SurfaceLayout.InsetWell or SurfaceLayout.InsetWellWithRaisedKnob;
        if (inset && pixels.Inside < MinimumChangedPixels)
            return $"changed {pixels.Inside} pixels inside the control.";
        // Inset shadows stay within their surface; a raised knob or handle may cast its shadow past the control.
        if (inset && layout != SurfaceLayout.InsetWellWithRaisedKnob && pixels.Outside > 0)
            return $"changed {pixels.Outside} pixels outside the control.";
        if (inset)
            return null;

        return (layout, mode) switch
        {
            // Inner boxes cast their outer shadows inside the control.
            (SurfaceLayout.InnerBoxes, _) when pixels.Changed < MinimumChangedPixels => $"changed {pixels.Changed} pixels.",
            (SurfaceLayout.InnerBoxes, _) => null,
            // A full-size surface and a top-left header both cast the light shadow past the top and left edges.
            (_, DaisyNeumorphicMode.Raised) when pixels.LighterTopLeft < MinimumChangedPixels =>
                $"made {pixels.LighterTopLeft} pixels lighter above or left of the control.",
            // A header's dark shadow falls on the control's own content below it.
            (SurfaceLayout.TopLeftHeader, _) when pixels.Changed < MinimumChangedPixels => $"changed {pixels.Changed} pixels.",
            (SurfaceLayout.TopLeftHeader, _) => null,
            _ when pixels.DarkerBottomRight < MinimumChangedPixels =>
                $"made {pixels.DarkerBottomRight} pixels darker below or right of the control.",
            _ => null
        };
    }

    /// <summary>The public controls that the neumorphic registry draws a surface for.</summary>
    private static List<Type> GetRegisteredControls()
    {
        var registered = new List<Type>();
        foreach (var type in ControlCatalog.GetDaisyControls()
                     .Concat(ControlCatalog.GetCustomControls())
                     .Concat(ControlCatalog.GetKanbanControls()))
        {
            if (DaisyNeumorphic.IsRegistered(type) && type.GetConstructor(Type.EmptyTypes) is not null
                && !typeof(Window).IsAssignableFrom(type))
            {
                registered.Add(type);
            }
        }

        return registered;
    }

    /// <summary>
    /// Attaches <paramref name="control"/>, and an optional reference control below it, in a mid-gray frame with
    /// room for their shadows. Returns the frame and both bounds in the frame's pixels.
    /// </summary>
    private static async Task<(Border Frame, PixelRect Bounds, PixelRect? Reference)> AttachInFrameAsync(Control control, Control? reference)
    {
        // Shadows are designed for light themes, where they contrast most.
        Assert.True(DaisyThemeManager.ApplyTheme("Light"));
        // Dialogs that build their own chrome in the constructor are attached through that chrome.
        var root = control;
        while (root.GetVisualParent() is Control parent)
            root = parent;
        Control content = root;
        if (reference is not null)
        {
            reference.HorizontalAlignment = HorizontalAlignment.Left;
            content = new StackPanel { Spacing = ReferenceSpacing, Children = { root, reference } };
        }

        var frame = new Border
        {
            Padding = new Thickness(FrameMargin),
            Width = ColumnWidth + 2 * FrameMargin,
            Background = FrameBrush,
            // A resting mouse pointer must not start hover transitions between the two renders.
            IsHitTestVisible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = content
        };
        await RuntimeTestHelpers.AttachAsync(frame);
        return (frame, BoundsIn(frame, control), reference is null ? null : BoundsIn(frame, reference));
    }

    private static PixelRect BoundsIn(Visual frame, Control control)
    {
        var origin = control.TranslatePoint(default, frame) ?? throw new InvalidOperationException("The control is not in the frame.");
        return new PixelRect(
            (int)Math.Floor(origin.X), (int)Math.Floor(origin.Y),
            (int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height));
    }

    /// <summary>
    /// Renders the frame at intensity 0 and at the default intensity, in the current mode, and classifies the
    /// pixels that differ.
    /// </summary>
    private static async Task<ShadowPixels> MeasureShadowAsync(Border frame, PixelRect bounds, PixelRect? reference = null)
    {
        DaisyNeumorphic.GlobalIntensity = 0;
        await RuntimeTestHelpers.WaitForIdleAsync();
        using var baseline = RuntimeTestHelpers.Render(frame);
        DaisyNeumorphic.GlobalIntensity = RuntimeTestRunner.DefaultNeumorphicIntensity;
        await RuntimeTestHelpers.WaitForIdleAsync();
        using var shadow = RuntimeTestHelpers.Render(frame);
        return ShadowPixels.Measure(baseline, shadow, bounds, reference);
    }

    private static int CountChanged(SKBitmap first, SKBitmap second, PixelRect? region, bool countInside)
    {
        var changed = 0;
        for (var y = 0; y < Math.Min(first.Height, second.Height); y++)
        for (var x = 0; x < Math.Min(first.Width, second.Width); x++)
        {
            if (region is { } area && area.Contains(new PixelPoint(x, y)) != countInside)
                continue;
            if (ShadowPixels.Difference(first.GetPixel(x, y), second.GetPixel(x, y)) != 0)
                changed++;
        }

        return changed;
    }

    /// <summary>
    /// Creates <paramref name="type"/> in a configuration that shows its surface. Most controls show it as
    /// constructed; the others get content, a size or the variant that has a surface.
    /// </summary>
    private static Control CreateVisible(Type type)
    {
        Control control = type switch
        {
            _ when type == typeof(DaisyButtonGroup) => CreateComposite("button-group"),
            _ when type == typeof(DaisyTable) => CreateComposite("table"),
            _ when type == typeof(DaisyList) => CreateList(),
            _ when type == typeof(DaisyStats) => new DaisyStats
            {
                Items = { new DaisyStat { Title = "Downloads", Value = "31K" }, new DaisyStat { Title = "Users", Value = "4,200" } }
            },
            // Countdown and NumberFlow have a surface only with a visible background.
            _ when type == typeof(DaisyCountdown) => new DaisyCountdown { Background = Brushes.White },
            _ when type == typeof(DaisyNumberFlow) => new DaisyNumberFlow { ShowControls = true, Value = 42 },
            _ when type == typeof(DaisyProgress) => new DaisyProgress { Value = 60, Maximum = 100 },
            _ when type == typeof(DaisyRange) => new DaisyRange { Value = 40, Maximum = 100 },
            _ when type == typeof(DaisySkeleton) => new DaisySkeleton { Height = 32 },
            // Only boxed tabs and timeline items have a surface.
            _ when type == typeof(DaisyTabs) => new DaisyTabs
            {
                Variant = DaisyTabVariant.Boxed,
                Items = { new TabItem { Header = "One", Content = "First" }, new TabItem { Header = "Two", Content = "Second" } }
            },
            _ when type == typeof(DaisyTimelineItem) => new DaisyTimelineItem { StartContent = "2024", EndContent = "Planned", IsBoxed = true },
            _ => (Control)Activator.CreateInstance(type)!
        };

        // A dialog's surface exists only while it is open.
        if (control is DaisyModal modal)
        {
            modal.Content ??= "Dialog";
            modal.IsOpen = true;
        }

        return control;
    }

    /// <summary>Configurations that DaisyNeumorphic.md documents as drawing no effect.</summary>
    private static Control CreateWithoutSurface(string configuration) => configuration switch
    {
        "button-ghost" => new DaisyButton { Content = "Ghost", Variant = DaisyButtonVariant.Ghost },
        "button-link" => new DaisyButton { Content = "Link", Variant = DaisyButtonVariant.Link },
        "countdown-without-background" => new DaisyCountdown(),
        "number-flow-without-controls" => new DaisyNumberFlow { Value = 42 },
        "tabs-bordered" => new DaisyTabs
        {
            Variant = DaisyTabVariant.Bordered,
            Items = { new TabItem { Header = "One", Content = "First" }, new TabItem { Header = "Two", Content = "Second" } }
        },
        "timeline-item-not-boxed" => new DaisyTimelineItem { StartContent = "2024", EndContent = "Planned" },
        "chat-bubble-default" => new DaisyChatBubble { Content = "Hello" },
        _ => throw new ArgumentOutOfRangeException(nameof(configuration), configuration, null)
    };

    private static DaisyList CreateList()
    {
        var list = new DaisyList();
        for (var index = 1; index <= 3; index++)
            list.Items.Add(new DaisyListRow { Items = { new DaisyListColumn { Content = $"List row {index}" } } });
        return list;
    }

    private static Control CreateComposite(string composite)
    {
        switch (composite)
        {
            case "table":
                var headerRow = new DaisyTableRow();
                headerRow.Items.Add(new DaisyTableHeaderCell { Content = "Name" });
                headerRow.Items.Add(new DaisyTableHeaderCell { Content = "Value" });
                var head = new DaisyTableHead();
                head.Items.Add(headerRow);
                var body = new DaisyTableBody();
                for (var index = 1; index <= 3; index++)
                {
                    var row = new DaisyTableRow();
                    row.Items.Add(new DaisyTableCell { Content = $"Row {index}" });
                    row.Items.Add(new DaisyTableCell { Content = $"{index * 10}" });
                    body.Items.Add(row);
                }

                var table = new DaisyTable();
                table.Items.Add(head);
                table.Items.Add(body);
                return table;
            case "button-group":
                var group = new DaisyButtonGroup { HorizontalAlignment = HorizontalAlignment.Left };
                group.Items.Add(new DaisyButton { Content = "One" });
                group.Items.Add(new DaisyButton { Content = "Two" });
                group.Items.Add(new DaisyButton { Content = "Three" });
                return group;
            case "join":
                return new DaisyJoin
                {
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Children = { new DaisyButton { Content = "One" }, new DaisyButton { Content = "Two" }, new DaisyButton { Content = "Three" } }
                };
            case "pagination":
                return new DaisyPagination { TotalPages = 5, CurrentPage = 2, HorizontalAlignment = HorizontalAlignment.Left };
            default:
                throw new ArgumentOutOfRangeException(nameof(composite), composite, null);
        }
    }

    /// <summary>Where a control's neumorphic surfaces lie, and whether they follow the global mode.</summary>
    private enum SurfaceLayout
    {
        /// <summary>One surface of the control's full size that follows the mode.</summary>
        FullSize,
        /// <summary>A surface in the control's top-left corner above its content (boxed tabs header).</summary>
        TopLeftHeader,
        /// <summary>Surfaces inside the control, away from its edges (boxed timeline start and end).</summary>
        InnerBoxes,
        /// <summary>A well that is always inset (checkbox box, radio circle).</summary>
        InsetWell,
        /// <summary>An always inset well with an always raised knob or handle (toggle, slide to confirm).</summary>
        InsetWellWithRaisedKnob
    }

    /// <summary>
    /// The pixels that a shadow changed, by place and direction. The control's pixels are counted only within the
    /// reach of its shadow; the reference button's only within the reach of its own.
    /// </summary>
    private readonly record struct ShadowPixels(
        int Changed,
        int Inside,
        int InsideAwayFromEdges,
        int Outside,
        int LighterTopLeft,
        int DarkerBottomRight,
        int ReferenceDarkerBottomRight)
    {
        // Pixels within this distance of the control's edge belong to its outer shadow.
        private const int EdgeWidth = 4;
        // How far a shadow can reach beyond its surface; less than half the reference spacing.
        private const int ShadowReach = 20;

        public static ShadowPixels Measure(SKBitmap baseline, SKBitmap shadow, PixelRect bounds, PixelRect? reference)
        {
            var inner = Inflate(bounds, -EdgeWidth);
            var zone = Inflate(bounds, ShadowReach);
            var referenceZone = reference is { } area ? Inflate(area, ShadowReach) : default;
            int changed = 0, inside = 0, insideAwayFromEdges = 0, outside = 0, lighterTopLeft = 0, darkerBottomRight = 0, referenceDarker = 0;
            for (var y = 0; y < Math.Min(baseline.Height, shadow.Height); y++)
            for (var x = 0; x < Math.Min(baseline.Width, shadow.Width); x++)
            {
                var difference = Difference(baseline.GetPixel(x, y), shadow.GetPixel(x, y));
                if (difference == 0)
                    continue;

                var point = new PixelPoint(x, y);
                if (zone.Contains(point))
                {
                    changed++;
                    if (bounds.Contains(point))
                    {
                        inside++;
                        if (inner.Contains(point))
                            insideAwayFromEdges++;
                        continue;
                    }

                    outside++;
                    if (difference == 1 && (y < bounds.Y || x < bounds.X))
                        lighterTopLeft++;
                    if (difference == -1 && (y >= bounds.Bottom || x >= bounds.Right))
                        darkerBottomRight++;
                }
                else if (reference is { } r && referenceZone.Contains(point) && !r.Contains(point)
                         && difference == -1 && (y >= r.Bottom || x >= r.Right))
                {
                    referenceDarker++;
                }
            }

            return new ShadowPixels(changed, inside, insideAwayFromEdges, outside, lighterTopLeft, darkerBottomRight, referenceDarker);
        }

        private static PixelRect Inflate(PixelRect rect, int amount) =>
            new(rect.X - amount, rect.Y - amount, Math.Max(0, rect.Width + 2 * amount), Math.Max(0, rect.Height + 2 * amount));

        /// <summary>
        /// +1 if <paramref name="after"/> is lighter in every channel beyond the tolerance, -1 if darker,
        /// 2 for any other change beyond the tolerance, and 0 for no change.
        /// </summary>
        public static int Difference(SKColor before, SKColor after)
        {
            var red = after.Red - before.Red;
            var green = after.Green - before.Green;
            var blue = after.Blue - before.Blue;
            if (red > ChannelTolerance && green > ChannelTolerance && blue > ChannelTolerance)
                return 1;
            if (red < -ChannelTolerance && green < -ChannelTolerance && blue < -ChannelTolerance)
                return -1;
            return Math.Abs(red) > ChannelTolerance || Math.Abs(green) > ChannelTolerance || Math.Abs(blue) > ChannelTolerance ? 2 : 0;
        }
    }
}
