using Flowery.NET.Photos.Editing;

namespace Flowery.NET.Photos.Views;

public sealed partial class PhotoCanvas
{
    private enum CropHandle { None, Move, TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left }

    private static CropHandle HitTestCrop(Rect rect, Point point)
    {
        const double radius = 16;
        var left = Math.Abs(point.X - rect.X) <= radius;
        var right = Math.Abs(point.X - rect.Right) <= radius;
        var top = Math.Abs(point.Y - rect.Y) <= radius;
        var bottom = Math.Abs(point.Y - rect.Bottom) <= radius;
        if (left && top) return CropHandle.TopLeft;
        if (right && top) return CropHandle.TopRight;
        if (left && bottom) return CropHandle.BottomLeft;
        if (right && bottom) return CropHandle.BottomRight;
        var withinX = point.X >= rect.X - radius && point.X <= rect.Right + radius;
        var withinY = point.Y >= rect.Y - radius && point.Y <= rect.Bottom + radius;
        if (top && withinX) return CropHandle.Top;
        if (bottom && withinX) return CropHandle.Bottom;
        if (left && withinY) return CropHandle.Left;
        if (right && withinY) return CropHandle.Right;
        return rect.Contains(point) ? CropHandle.Move : CropHandle.None;
    }

    private Rect ResizeCrop(Rect start, CropHandle handle, double dx, double dy)
    {
        const double minimum = .04;
        if (handle == CropHandle.Move)
            return new Rect(Math.Clamp(start.X + dx, 0, 1 - start.Width),
                Math.Clamp(start.Y + dy, 0, 1 - start.Height), start.Width, start.Height);
        var left = start.X; var top = start.Y; var right = start.Right; var bottom = start.Bottom;
        if (handle is CropHandle.Left or CropHandle.TopLeft or CropHandle.BottomLeft)
            left = Math.Clamp(left + dx, 0, right - minimum);
        if (handle is CropHandle.Right or CropHandle.TopRight or CropHandle.BottomRight)
            right = Math.Clamp(right + dx, left + minimum, 1);
        if (handle is CropHandle.Top or CropHandle.TopLeft or CropHandle.TopRight)
            top = Math.Clamp(top + dy, 0, bottom - minimum);
        if (handle is CropHandle.Bottom or CropHandle.BottomLeft or CropHandle.BottomRight)
            bottom = Math.Clamp(bottom + dy, top + minimum, 1);
        var rect = new Rect(left, top, right - left, bottom - top);
        if (AspectRatio <= 0) return rect;
        var size = EditRenderer.GetTransformedSize(_originalSize, State);
        return EnforceAspect(rect, handle, AspectRatio * size.Height / size.Width);
    }

    private static Rect EnforceAspect(Rect rect, CropHandle handle, double ratio)
    {
        var width = rect.Width; var height = rect.Height;
        if (handle is CropHandle.Top or CropHandle.Bottom) width = height * ratio;
        else height = width / ratio;
        if (width > 1) { width = 1; height = width / ratio; }
        if (height > 1) { height = 1; width = height * ratio; }
        var x = handle switch
        {
            CropHandle.TopLeft or CropHandle.BottomLeft or CropHandle.Left => rect.Right - width,
            CropHandle.Top or CropHandle.Bottom => rect.X + (rect.Width - width) / 2,
            _ => rect.X
        };
        var y = handle switch
        {
            CropHandle.TopLeft or CropHandle.TopRight or CropHandle.Top => rect.Bottom - height,
            CropHandle.Left or CropHandle.Right => rect.Y + (rect.Height - height) / 2,
            _ => rect.Y
        };
        if (x < 0) { width += x; x = 0; height = width / ratio; }
        if (y < 0) { height += y; y = 0; width = height * ratio; }
        if (x + width > 1) { width = 1 - x; height = width / ratio; }
        if (y + height > 1) { height = 1 - y; width = height * ratio; }
        return new Rect(x, y, Math.Max(.02, width), Math.Max(.02, height));
    }

    public Rect CreateAspectCrop(double aspect)
    {
        var crop = State.Crop;
        if (aspect <= 0 || _bitmap == null) return crop;
        var size = EditRenderer.GetTransformedSize(_originalSize, State);
        var ratio = aspect * size.Height / size.Width;
        var width = crop.Width;
        var height = width / ratio;
        if (height > crop.Height) { height = crop.Height; width = height * ratio; }
        return new Rect(crop.X + (crop.Width - width) / 2, crop.Y + (crop.Height - height) / 2, width, height);
    }
}
