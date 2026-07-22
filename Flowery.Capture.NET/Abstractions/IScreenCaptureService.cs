using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;

namespace Flowery.Capture;

/// <summary>
/// Service for capturing screenshots of Avalonia controls.
/// </summary>
public interface IScreenCaptureService
{
    /// <summary>
    /// Captures a screenshot of the specified control.
    /// For tall controls, may return multiple chunks if smart chunking is enabled.
    /// </summary>
    /// <param name="control">The control to capture.</param>
    /// <param name="options">Optional capture configuration.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Capture result containing PNG-encoded image data.</returns>
    Task<ScreenCaptureResult> CaptureControlAsync(
        Control control,
        ScreenCaptureOptions? options = null,
        CancellationToken ct = default);

    /// <summary>
    /// Captures a specific screen region.
    /// </summary>
    /// <param name="region">Screen region in physical pixels.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>PNG-encoded image data, or empty array on failure.</returns>
    Task<byte[]> CaptureRegionAsync(PixelRect region, CancellationToken ct = default);

    /// <summary>
    /// Indicates whether this implementation provides high-quality capture.
    /// Windows GDI+ capture returns true; RenderTargetBitmap fallback returns false.
    /// </summary>
    bool IsHighQualityAvailable { get; }
}

