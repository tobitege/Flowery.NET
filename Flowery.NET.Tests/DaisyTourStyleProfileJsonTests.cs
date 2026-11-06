using System.Text;
using System.Text.Json;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests;

/// <summary>Malformed style profile JSON is rejected as a whole, and the size limit holds in both directions.</summary>
public class DaisyTourStyleProfileJsonTests
{
    private const int MaximumJsonLength = 1024 * 1024;
    private const string Valid = "{\"schemaVersion\":1,\"tourId\":\"tour\"}";

    [Theory]
    [InlineData("")]
    [InlineData("{")]
    [InlineData("{\"schemaVersion\":1,")]
    [InlineData("{\"schemaVersion\":1 \"tourId\":\"tour\"}")]
    [InlineData("{'schemaVersion':1,'tourId':'tour'}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",}")]
    [InlineData("not json")]
    public void When_TheJsonHasASyntaxError_ItIsRejected(string json) =>
        Assert.ThrowsAny<JsonException>(() => DaisyTourStyleProfileSerializer.Deserialize(json));

    [Theory]
    [InlineData("[]")]
    [InlineData("[" + Valid + "]")]
    [InlineData("null")]
    [InlineData("1")]
    [InlineData("true")]
    [InlineData("\"tour\"")]
    public void When_TheRootIsNoObject_ItIsRejected(string json) =>
        Assert.ThrowsAny<JsonException>(() => DaisyTourStyleProfileSerializer.Deserialize(json));

    [Fact]
    public void When_TheJsonIsNestedDeeperThan32Levels_ItIsRejectedForItsDepth()
    {
        // The root object is the first level, the steps array the second.
        var json = "{\"schemaVersion\":1,\"tourId\":\"tour\",\"steps\":" + new string('[', 32) + new string(']', 32) + "}";
        var error = Assert.ThrowsAny<JsonException>(() => DaisyTourStyleProfileSerializer.Deserialize(json));
        Assert.Contains("depth", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void When_TheJsonReachesTheSizeLimit_ItIsReadAndOneByteMoreIsRejected()
    {
        // Whitespace counts like any other byte.
        var atLimit = Valid + new string(' ', MaximumJsonLength - Encoding.UTF8.GetByteCount(Valid));
        Assert.Equal(MaximumJsonLength, Encoding.UTF8.GetByteCount(atLimit));
        Assert.Equal("tour", DaisyTourStyleProfileSerializer.Deserialize(atLimit).TourId);

        var error = Assert.Throws<JsonException>(() => DaisyTourStyleProfileSerializer.Deserialize(atLimit + " "));
        Assert.Contains("1 MiB", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void When_TheSizeLimitCountsBytes_MultibyteTextCountsWithItsUtf8Length()
    {
        // "é" is two bytes in UTF-8: half as many characters reach the limit.
        var padding = MaximumJsonLength - Encoding.UTF8.GetByteCount(Valid);
        var json = Valid + new string(' ', padding % 2) + "\n" + new string('é', padding / 2);
        Assert.True(json.Length < MaximumJsonLength);
        Assert.True(Encoding.UTF8.GetByteCount(json) > MaximumJsonLength);
        var error = Assert.Throws<JsonException>(() => DaisyTourStyleProfileSerializer.Deserialize(json));
        Assert.Contains("1 MiB", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void When_AProfileWouldBeWrittenLargerThanTheLimit_SerializeRejectsIt()
    {
        var profile = TooLargeProfile();
        // The profile itself is valid; only its JSON is too large.
        DaisyTourStyleProfileSerializer.Validate(profile);
        var error = Assert.Throws<ArgumentException>(() => DaisyTourStyleProfileSerializer.Serialize(profile));
        Assert.Contains("1 MiB", error.Message, StringComparison.Ordinal);
    }

    // A valid profile whose JSON exceeds 1 MiB.
    internal static DaisyTourStyleProfile TooLargeProfile()
    {
        var profile = new DaisyTourStyleProfile { TourId = "tour" };
        for (var step = 0; step < 10; step++)
        {
            var stepProfile = new DaisyTourStepStyleProfile { Id = "step" + step };
            for (var annotation = 0; annotation < 1000; annotation++)
                stepProfile.Annotations.Add(new DaisyTourAnnotationStyleProfile { Id = annotation.ToString("D128") });
            profile.Steps.Add(stepProfile);
        }
        return profile;
    }

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"strokeThickness\":1e400}}}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"annotationDefaults\":{\"values\":{\"curvature\":-1e400}}}")]
    [InlineData("{\"schemaVersion\":1e400,\"tourId\":\"tour\"}")]
    [InlineData("{\"schemaVersion\":4294967297,\"tourId\":\"tour\"}")]
    public void When_ANumberOverflows_TheProfileIsRejected(string json) =>
        Assert.ThrowsAny<JsonException>(() => DaisyTourStyleProfileSerializer.Deserialize(json));

    [Theory]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"to\\u0001ur\"}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\\u007F\"}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"steps\":[{\"id\":\"st\\u0000ep\"}]}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"steps\":[{\"id\":\"step\",\"annotations\":[{\"id\":\"ar\\u0085row\"}]}]}")]
    [InlineData("{\"schemaVersion\":1,\"tourId\":\"tour\",\"steps\":[{\"id\":\"step\",\"annotations\":[{\"id\":\"ar\\trow\"}]}]}")]
    public void When_AnIdContainsAControlCharacter_TheProfileIsRejected(string json)
    {
        var error = Assert.ThrowsAny<JsonException>(() => DaisyTourStyleProfileSerializer.Deserialize(json));
        Assert.Contains("control characters", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void When_AnIdContainsAControlCharacter_ValidateRejectsTheProfile()
    {
        var profile = new DaisyTourStyleProfile { TourId = "tour" };
        profile.Steps.Add(new DaisyTourStepStyleProfile { Id = "step\u0001" });
        var error = Assert.Throws<ArgumentException>(() => DaisyTourStyleProfileSerializer.Validate(profile));
        Assert.Contains("$.steps", error.Message, StringComparison.Ordinal);
    }
}
