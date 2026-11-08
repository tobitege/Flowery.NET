#if WINDOWS
using Flowery.NET.Photos.Editing;
using Flowery.NET.Photos.Services;
using Windows.Graphics.Imaging;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [Theory]
    [InlineData(".tif")]
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
}
#endif
