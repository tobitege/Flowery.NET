using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

namespace Flowery.Helpers;

/// <summary>Draws the portion of a visual tree painted before a target, into a bounded capture.</summary>
internal static class BackdropRenderer
{
    public static void Render(DrawingContext context, Visual source, Visual target, Rect capture)
    {
        var targetBranch = new HashSet<Visual>(target.GetVisualAncestors()) { target };
        using (context.PushTransform(Matrix.CreateTranslation(-capture.X, -capture.Y)))
        using (context.PushClip(capture))
            RenderBeforeTarget(context, source, target, targetBranch, Matrix.Identity, capture);
    }

    private static bool RenderBeforeTarget(DrawingContext context, Visual visual, Visual target,
        HashSet<Visual> targetBranch, Matrix toSource, Rect capture)
    {
        if (ReferenceEquals(visual, target)) return true;
        if (!visual.IsVisible || visual.Opacity <= 0) return targetBranch.Contains(visual);

        var bounds = new Rect(visual.Bounds.Size);
        var renderBounds = visual switch
        {
            Border border => border.BoxShadow.TransformBounds(bounds),
            ContentPresenter presenter => presenter.BoxShadow.TransformBounds(bounds),
            _ => bounds
        };
        var sourceBounds = bounds.TransformToAABB(toSource);
        if (visual.ClipToBounds)
        {
            capture = capture.Intersect(sourceBounds);
            if (capture.Width <= 0 || capture.Height <= 0) return targetBranch.Contains(visual);
        }

        var renderOptions = new RenderOptions
        {
            BitmapInterpolationMode = RenderOptions.GetBitmapInterpolationMode(visual),
            BitmapBlendingMode = RenderOptions.GetBitmapBlendingMode(visual),
            EdgeMode = RenderOptions.GetEdgeMode(visual),
            RequiresFullOpacityHandling = RenderOptions.GetRequiresFullOpacityHandling(visual)
        };
        using (context.PushOpacity(visual.Opacity))
        using (context.PushRenderOptions(renderOptions))
        using (context.PushTextOptions(TextOptions.GetTextOptions(visual)))
        // Border has a rounded clip contract; CornerRadius on template inputs does not imply clipping.
        using (visual.ClipToBounds ? context.PushClip(new RoundedRect(bounds,
            visual is Border border ? border.ClipToBoundsRadius : default)) : default)
        using (visual.Clip is { } clip ? context.PushGeometryClip(clip) : default)
        using (visual.OpacityMask is { } mask ? context.PushOpacityMask(mask, bounds) : default)
        using (visual.Effect is { } effect ? context.PushEffect(effect, bounds) : default)
        {
            // Box shadows and effects may extend beyond the visual's layout bounds.
            if (visual.Effect is not null || renderBounds.TransformToAABB(toSource).Intersects(capture))
                visual.Render(context);

            // OrderBy is stable: equal ZIndex values retain the visual child order.
            foreach (var child in visual.GetVisualChildren().OrderBy(child => child.ZIndex))
            {
                if (child.TransformToVisual(visual) is not { } transform) continue;
                using (context.PushTransform(transform))
                {
                    if (RenderBeforeTarget(context, child, target, targetBranch, transform * toSource, capture))
                        return true;
                }
            }
        }

        return false;
    }
}
