#if WINDOWS
using System.Runtime.InteropServices;
using SkiaSharp;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Flowery.NET.Photos.Services;

/// <summary>Uses the same Windows codecs and photo properties as the WinUI application.</summary>
internal static class WindowsPhotoCodec
{
    public static bool CanEncode(string extension) => extension.ToLowerInvariant() is
        ".bmp" or ".gif" or ".tif" or ".tiff" or ".jxr" or ".wdp";

    public static async Task<byte[]> EncodeAsync(SKBitmap bitmap, string extension)
    {
        var id = extension.ToLowerInvariant() switch
        {
            ".bmp" => BitmapEncoder.BmpEncoderId,
            ".gif" => BitmapEncoder.GifEncoderId,
            ".tif" or ".tiff" => BitmapEncoder.TiffEncoderId,
            ".jxr" or ".wdp" => BitmapEncoder.JpegXREncoderId,
            _ => throw new NotSupportedException(extension)
        };
        using var stream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(id, stream).AsTask().ConfigureAwait(false);
        using var pixels = bitmap.Copy(SKColorType.Bgra8888);
        encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            (uint)pixels.Width, (uint)pixels.Height, 96, 96, pixels.Bytes);
        await encoder.FlushAsync().AsTask().ConfigureAwait(false);
        using var input = stream.GetInputStreamAt(0).AsStreamForRead();
        using var output = new MemoryStream();
        await input.CopyToAsync(output).ConfigureAwait(false);
        return output.ToArray();
    }

    public static async Task<SKBitmap> DecodeAsync(string path, int maxEdge)
    {
        using var file = File.OpenRead(path);
        using var stream = file.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
        var scale = Math.Min(1, (double)maxEdge / Math.Max(decoder.PixelWidth, decoder.PixelHeight));
        var transform = new BitmapTransform
        {
            ScaledWidth = Math.Max(1, (uint)(decoder.PixelWidth * scale)),
            ScaledHeight = Math.Max(1, (uint)(decoder.PixelHeight * scale)),
            InterpolationMode = BitmapInterpolationMode.Linear
        };
        var data = await decoder.GetPixelDataAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied,
            transform, ExifOrientationMode.RespectExifOrientation, ColorManagementMode.ColorManageToSRgb)
            .AsTask().ConfigureAwait(false);
        var rotated = decoder.OrientedPixelWidth != decoder.PixelWidth;
        var bitmap = new SKBitmap((int)(rotated ? transform.ScaledHeight : transform.ScaledWidth),
            (int)(rotated ? transform.ScaledWidth : transform.ScaledHeight), SKColorType.Bgra8888, SKAlphaType.Premul);
        try
        {
            var bytes = data.DetachPixelData();
            if (bytes.Length != bitmap.ByteCount) throw new InvalidDataException("Unexpected decoded image size.");
            Marshal.Copy(bytes, 0, bitmap.GetPixels(), bytes.Length);
            return bitmap;
        }
        catch { bitmap.Dispose(); throw; }
    }

    public static async Task<ImageProbe> ProbeAsync(string path)
    {
        using var file = File.OpenRead(path);
        using var stream = file.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(stream).AsTask().ConfigureAwait(false);
        return new ImageProbe((int)decoder.OrientedPixelWidth, (int)decoder.OrientedPixelHeight, null, 1, null);
    }

    public static async Task<ImageProbe> ReadMetadataAsync(string path)
    {
        var file = await StorageFile.GetFileFromPathAsync(path).AsTask().ConfigureAwait(false);
        var properties = await file.Properties.GetImagePropertiesAsync().AsTask().ConfigureAwait(false);
        var extra = await file.Properties.RetrievePropertiesAsync([
            "System.Photo.FNumber", "System.Photo.ExposureTime", "System.Photo.ISOSpeed",
            "System.Photo.FocalLength", "System.Photo.LensModel"]).AsTask().ConfigureAwait(false);
        List<string> exposure = [];
        var culture = Localization.PhotosLocalization.CurrentCulture;
        if (extra.TryGetValue("System.Photo.FNumber", out var f) && f is double aperture && aperture > 0)
            exposure.Add(string.Format(culture, "ƒ/{0:0.#}", aperture));
        if (extra.TryGetValue("System.Photo.ExposureTime", out var et) && et is double time && time > 0)
            exposure.Add(time >= 1 ? string.Format(culture, "{0:0.#} s", time) : $"1/{Math.Round(1 / time)} s");
        if (extra.TryGetValue("System.Photo.ISOSpeed", out var iso) && iso != null) exposure.Add($"ISO {iso}");
        if (extra.TryGetValue("System.Photo.FocalLength", out var fl) && fl is double focal && focal > 0)
            exposure.Add(string.Format(culture, "{0:0} mm", focal));
        extra.TryGetValue("System.Photo.LensModel", out var lens);
        var camera = string.Join(" ", new[] { properties.CameraManufacturer, properties.CameraModel }
            .Where(value => !string.IsNullOrWhiteSpace(value)).Distinct());
        return new ImageProbe((int)properties.Width, (int)properties.Height,
            properties.DateTaken.Year > 1601 ? properties.DateTaken.LocalDateTime : null, 1, camera,
            new PhotoDetails(lens as string, string.Join("  ·  ", exposure), properties.Latitude, properties.Longitude));
    }
}
#endif
