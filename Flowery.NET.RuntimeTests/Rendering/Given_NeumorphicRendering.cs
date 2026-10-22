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
/// Neumorphic shadows must reach the screen: every registered surface draws them, composite controls keep their
/// rows, cells and segments flat, and high contrast removes them. Each check renders the same control with the
/// effect off and on and compares the pixels.
/// </summary>
public class Given_NeumorphicRendering
{
    // Room around the control for its outer shadows.
    private const double FrameMargin = 48;
    // The column width of the Gallery's neumorphic showcase; controls stretch to it as they do there.
    private const double ColumnWidth = 280;
    // A drawn shadow changes at least this many pixels by more than the channel tolerance.
    private const int MinimumChangedPixels = 8;
    private const int ChannelTolerance = 2;

    // Input wells that are always inset (see DaisyNeumorphic.md); their shadows lie inside the control in every mode.
    private static readonly HashSet<Type> AlwaysInset = [typeof(DaisyCheckBox), typeof(DaisyRadio), typeof(DaisyToggle)];

    // Surfaces that cover only part of the control, such as the boxed tabs header above the content;
    // their outer shadows can fall inside the control.
    private static readonly HashSet<Type> PartialSurfaces = [typeof(DaisyTabs)];

    [Theory]
    [InlineData(DaisyNeumorphicMode.Raised)]
    [InlineData(DaisyNeumorphicMode.Inset)]
    [InlineData(DaisyNeumorphicMode.Flat)]
    public async Task When_The_Global_Mode_Is_Set_Every_Registered_Surface_Draws_Its_Shadow(DaisyNeumorphicMode mode)
    {
        var failures = new StringBuilder();
        var checkedControls = 0;
        foreach (var type in GetRegisteredControls())
        {
            try
            {
                var control = CreateVisible(type);
                var (frame, bounds) = await AttachInFrameAsync(control);
                var (changed, outside) = await CompareAsync(frame, bounds, control, mode);
                checkedControls++;
                if (changed < MinimumChangedPixels)
                {
                    failures.AppendLine($"{type.Name}: {mode} changed {changed} pixels.");
                    using var _ = RuntimeTestHelpers.Render(frame, $"neumorphic-{mode}-{type.Name}");
                }
                else if (mode != DaisyNeumorphicMode.Inset && !AlwaysInset.Contains(type) && !PartialSurfaces.Contains(type)
                         && outside < MinimumChangedPixels)
                {
                    // Raised and Flat shadows lie outside the surface; a clip around it would cut them off.
                    failures.AppendLine($"{type.Name}: {mode} changed only {outside} pixels outside the control.");
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
    [InlineData("table")]
    [InlineData("button-group")]
    [InlineData("join")]
    [InlineData("pagination")]
    public async Task When_Raised_A_Composite_Control_Keeps_Its_Inside_Flat(string composite)
    {
        var control = CreateComposite(composite);
        var (frame, bounds) = await AttachInFrameAsync(control);

        DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.None;
        await RuntimeTestHelpers.WaitForIdleAsync();
        using var flat = RuntimeTestHelpers.Render(frame);
        DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.Raised;
        await RuntimeTestHelpers.WaitForIdleAsync();
        using var raised = RuntimeTestHelpers.Render(frame, $"neumorphic-composite-{composite}");

        // A shadow of a row, cell or segment would change pixels inside the control; the edge belongs to the outer shadow.
        var inside = new PixelRect(bounds.X + 4, bounds.Y + 4, bounds.Width - 8, bounds.Height - 8);
        Assert.Equal(0, CountChanged(flat, raised, inside, countInside: true));
    }

    [Fact]
    public async Task When_High_Contrast_Is_On_No_Surface_Draws_A_Shadow()
    {
        DaisyAccessibility.HighContrastOverride = true;
        var failures = new StringBuilder();
        foreach (var type in GetRegisteredControls())
        {
            try
            {
                var control = CreateVisible(type);
                var (frame, bounds) = await AttachInFrameAsync(control);
                var (changed, _) = await CompareAsync(frame, bounds, control, DaisyNeumorphicMode.Raised);
                if (changed > 0)
                    failures.AppendLine($"{type.Name}: high contrast still changed {changed} pixels.");
            }
            catch (Exception exception)
            {
                var cause = RuntimeTestHelpers.Unwrap(exception);
                failures.AppendLine($"{type.Name}: {cause.GetType().Name} - {cause.Message}");
            }
        }

        Assert.True(failures.Length == 0, failures.ToString());
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
    /// Attaches <paramref name="control"/> in a frame with room for its shadows and returns the frame and the
    /// control's bounds in the frame's pixels.
    /// </summary>
    private static async Task<(Border Frame, PixelRect Bounds)> AttachInFrameAsync(Control control)
    {
        // Shadows are designed for light surfaces, where they contrast most.
        Assert.True(DaisyThemeManager.ApplyTheme("Light"));
        // Dialogs that build their own chrome in the constructor are attached through that chrome.
        var root = control;
        while (root.GetVisualParent() is Control parent)
            root = parent;
        var frame = new Border
        {
            Padding = new Thickness(FrameMargin),
            Width = ColumnWidth + 2 * FrameMargin,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            Child = root
        };
        // The frame paints the surface color, as a neumorphic background does.
        frame[!Border.BackgroundProperty] = frame.GetResourceObservable("DaisyBase200Brush").ToBinding();
        await RuntimeTestHelpers.AttachAsync(frame);

        var origin = control.TranslatePoint(default, frame) ?? throw new InvalidOperationException("The control is not in the frame.");
        var bounds = new PixelRect(
            (int)Math.Floor(origin.X), (int)Math.Floor(origin.Y),
            (int)Math.Ceiling(control.Bounds.Width), (int)Math.Ceiling(control.Bounds.Height));
        return (frame, bounds);
    }

    /// <summary>
    /// Renders the frame with the effect off and in <paramref name="mode"/>. Returns the changed pixels and how
    /// many of them lie outside the control. A control that is off by default is enabled on itself.
    /// </summary>
    private static async Task<(int Changed, int Outside)> CompareAsync(Border frame, PixelRect bounds, Control control, DaisyNeumorphicMode mode)
    {
        DaisyNeumorphic.GlobalMode = DaisyNeumorphicMode.None;
        await RuntimeTestHelpers.WaitForIdleAsync();
        using var off = RuntimeTestHelpers.Render(frame);

        DaisyNeumorphic.GlobalMode = mode;
        await RuntimeTestHelpers.WaitForIdleAsync();
        if (!DaisyNeumorphic.IsEffectivelyEnabled(control) && !DaisyAccessibility.IsHighContrast)
        {
            DaisyNeumorphic.SetIsEnabled(control, true);
            await RuntimeTestHelpers.WaitForIdleAsync();
        }

        using var on = RuntimeTestHelpers.Render(frame);
        return (CountChanged(off, on, null, countInside: true), CountChanged(off, on, bounds, countInside: false));
    }

    /// <summary>
    /// Counts the pixels that differ between the two images, inside <paramref name="region"/> or outside it.
    /// Without a region every pixel counts.
    /// </summary>
    private static int CountChanged(SKBitmap first, SKBitmap second, PixelRect? region, bool countInside)
    {
        var changed = 0;
        for (var y = 0; y < Math.Min(first.Height, second.Height); y++)
        for (var x = 0; x < Math.Min(first.Width, second.Width); x++)
        {
            if (region is { } area && area.Contains(new PixelPoint(x, y)) != countInside)
                continue;

            var a = first.GetPixel(x, y);
            var b = second.GetPixel(x, y);
            if (Math.Abs(a.Red - b.Red) > ChannelTolerance || Math.Abs(a.Green - b.Green) > ChannelTolerance
                || Math.Abs(a.Blue - b.Blue) > ChannelTolerance)
            {
                changed++;
            }
        }

        return changed;
    }

    /// <summary>
    /// Creates <paramref name="type"/> in a configuration that shows its surface, as the Gallery's neumorphic
    /// showcase does. Most controls show it as constructed.
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
            // Countdown draws the effect only on a visible background.
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

                var table = new DaisyTable { Width = 300 };
                table.Items.Add(head);
                table.Items.Add(body);
                return table;
            case "button-group":
                var group = new DaisyButtonGroup();
                group.Items.Add(new DaisyButton { Content = "One" });
                group.Items.Add(new DaisyButton { Content = "Two" });
                group.Items.Add(new DaisyButton { Content = "Three" });
                return group;
            case "join":
                return new DaisyJoin
                {
                    Children = { new DaisyButton { Content = "One" }, new DaisyButton { Content = "Two" }, new DaisyButton { Content = "Three" } }
                };
            case "pagination":
                return new DaisyPagination { TotalPages = 5, CurrentPage = 2 };
            default:
                throw new ArgumentOutOfRangeException(nameof(composite), composite, null);
        }
    }
}
