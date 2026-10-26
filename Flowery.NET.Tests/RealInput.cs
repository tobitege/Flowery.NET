using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.NET.Tests;

/// <summary>
/// Input that enters through the platform of the control's top level, like a real pointer or keyboard:
/// hit testing picks the receiver of a click, and a key goes to the focused element.
/// Popups are top levels of their own, so their controls work too.
/// </summary>
internal static class RealInput
{
    /// <summary>Clicks the centre of <paramref name="control"/>.</summary>
    public static void PointerClick(this Control control, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var topLevel = TopLevel.GetTopLevel(control)
            ?? throw new InvalidOperationException($"{control} is not shown.");
        // Lay out and render first: the point is taken from the layout, and hit testing uses the rendered scene.
        for (var frame = 0; frame < 2; frame++)
        {
            Dispatcher.UIThread.RunJobs();
            AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        }
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), topLevel)
            ?? throw new InvalidOperationException($"{control} is not in its top level.");
        // A click that something else would receive misses without a trace.
        if (topLevel.InputHitTest(center) is not Visual hit || (hit != control && !control.IsVisualAncestorOf(hit)))
            throw new InvalidOperationException($"{control} cannot be clicked at {center}: {topLevel.InputHitTest(center)} is there.");
        topLevel.MouseDown(center, MouseButton.Left, modifiers);
        topLevel.MouseUp(center, MouseButton.Left, modifiers);
    }

    /// <summary>Presses and releases <paramref name="key"/> on whatever has the focus in the control's top level.</summary>
    public static void Press(this Control control, PhysicalKey key, RawInputModifiers modifiers = RawInputModifiers.None)
    {
        var topLevel = TopLevel.GetTopLevel(control)
            ?? throw new InvalidOperationException($"{control} is not shown.");
        topLevel.KeyPressQwerty(key, modifiers);
        topLevel.KeyReleaseQwerty(key, modifiers);
    }
}
