#if WINDOWS
using Flowery.NET.Photos.Editing;
using Flowery.NET.Photos.Services;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [Theory]
    [InlineData(".bmp")]
    [InlineData(".gif")]
    [InlineData(".tif")]
    [InlineData(".tiff")]
    [InlineData(".jxr")]
    [InlineData(".wdp")]
    public async Task WindowsExportKeepsOriginalFormatAndCanBeDecodedAndIndexed(string extension)
    {
        var source = CreateImage();
        var target = Path.ChangeExtension(source, extension);
        Assert.True(EditRenderer.CanEncode(extension));
        await EditRenderer.SaveAsync(source, target, EditState.Original.Rotate(true), 90, overwrite: false);
        using var decoded = PhotoDecoder.Load(target);
        Assert.Equal(160, decoded.Width);
        Assert.Equal(240, decoded.Height);
        var probe = await ImageMetadataReader.ProbeAsync(target);
        Assert.NotNull(probe);
        Assert.Equal(160, probe.Value.Width);
        Assert.Equal(240, probe.Value.Height);
        using var file = File.OpenRead(target);
        using var stream = file.AsRandomAccessStream();
        var decoder = await BitmapDecoder.CreateAsync(stream);
        var expected = extension switch
        {
            ".bmp" => BitmapDecoder.BmpDecoderId,
            ".gif" => BitmapDecoder.GifDecoderId,
            ".tif" or ".tiff" => BitmapDecoder.TiffDecoderId,
            _ => BitmapDecoder.JpegXRDecoderId
        };
        Assert.Equal(expected, decoder.DecoderInformation.CodecId);
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".tiff")]
    public async Task MetadataIncludesIsoAndFocalLengthForJpegAndTiff(string extension)
    {
        var source = CreateImage();
        var target = Path.ChangeExtension(source, extension);
        var exif = extension == ".jpg" ? "/app1/ifd/exif/" : "/ifd/exif/";
        using (var file = new FileStream(target, FileMode.CreateNew, FileAccess.ReadWrite))
        using (var stream = file.AsRandomAccessStream())
        {
            var encoder = await BitmapEncoder.CreateAsync(extension == ".jpg" ? BitmapEncoder.JpegEncoderId : BitmapEncoder.TiffEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Ignore, 2, 2, 96, 96,
                [0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255, 0, 0, 255, 255]);
            await encoder.BitmapProperties.SetPropertiesAsync(new BitmapPropertySet
            {
                [exif + "{ushort=34855}"] = new BitmapTypedValue((ushort)400, PropertyType.UInt16),
                [exif + "{ushort=37386}"] = new BitmapTypedValue((1UL << 32) | 50, PropertyType.UInt64),
                [exif + "{ushort=33437}"] = new BitmapTypedValue((10UL << 32) | 28, PropertyType.UInt64),
                [exif + "{ushort=33434}"] = new BitmapTypedValue((125UL << 32) | 1, PropertyType.UInt64)
            });
            await encoder.FlushAsync();
        }
        using (var file = File.OpenRead(target))
        using (var stream = file.AsRandomAccessStream())
        {
            var decoder = await BitmapDecoder.CreateAsync(stream);
            var stored = await decoder.BitmapProperties.GetPropertiesAsync([exif + "{ushort=34855}"]);
            Assert.True(stored.TryGetValue(exif + "{ushort=34855}", out var iso) && Equals(iso.Value, (ushort)400),
                "Fixture ISO readback: " + string.Join(", ", stored.Select(pair => pair.Key + "=" + pair.Value.Value)));
        }
        var metadata = await ImageMetadataReader.ReadDetailsAsync(target);
        Assert.Contains("ISO 400", metadata!.Value.Details!.Exposure);
        Assert.Contains("50 mm", metadata.Value.Details.Exposure);
        Assert.Contains("1/125 s", metadata.Value.Details.Exposure);
        if (extension == ".jpg")
        {
            var fast = ImageMetadataReader.ProbeFast(target);
            Assert.Contains("ISO 400", fast!.Value.Details!.Exposure);
            Assert.Contains("50 mm", fast.Value.Details.Exposure);
        }
    }
}
#endif
