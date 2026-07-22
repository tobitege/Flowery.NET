using System.Collections.Generic;

namespace Flowery.Capture;

/// <summary>
/// Result of a screen capture operation, potentially containing multiple chunks for tall content.
/// </summary>
public sealed record ScreenCaptureResult
{
    /// <summary>
    /// Whether the capture operation succeeded.
    /// </summary>
    public bool Success { get; init; }

    /// <summary>
    /// PNG-encoded image data for each chunk.
    /// Single-chunk captures will have exactly one element.
    /// </summary>
    public IReadOnlyList<byte[]> Chunks { get; init; } = [];

    /// <summary>
    /// Error message if capture failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Creates a successful result with the given chunks.
    /// </summary>
    public static ScreenCaptureResult Ok(IReadOnlyList<byte[]> chunks) =>
        new() { Success = true, Chunks = chunks };

    /// <summary>
    /// Creates a successful result with a single chunk.
    /// </summary>
    public static ScreenCaptureResult Ok(byte[] singleChunk) =>
        new() { Success = true, Chunks = [singleChunk] };

    /// <summary>
    /// Creates a failed result with the given error message.
    /// </summary>
    public static ScreenCaptureResult Fail(string errorMessage) =>
        new() { Success = false, ErrorMessage = errorMessage };
}

