using Avalonia;
using Avalonia.Controls;

namespace Flowery.Capture;

/// <summary>
/// Configuration options for screen capture operations.
/// </summary>
public sealed record ScreenCaptureOptions
{
    /// <summary>
    /// Optional ScrollViewer to clip capture region to visible viewport.
    /// </summary>
    public ScrollViewer? ScrollViewer { get; init; }

    /// <summary>
    /// Maximum height for each chunk when smart chunking is enabled.
    /// Defaults to 800 pixels, or viewport height if ScrollViewer is provided.
    /// </summary>
    public double MaxChunkHeight { get; init; } = 800;

    /// <summary>
    /// When true, attempts to split tall content at child element boundaries.
    /// When false, uses fixed-height chunks.
    /// </summary>
    public bool EnableSmartChunking { get; init; } = true;

    /// <summary>
    /// Margin adjustment for the capture region.
    /// Negative left value can exclude padding; positive values expand the region.
    /// </summary>
    public Thickness CaptureMargin { get; init; } = new(-8, 0, 0, 0);

    /// <summary>
    /// Delay in milliseconds after scrolling before capturing.
    /// Allows layout and rendering to settle.
    /// </summary>
    public int ScrollSettleDelayMs { get; init; } = 500;

    /// <summary>
    /// When provided, used to find child elements for smart chunking.
    /// Should be the content panel containing the items to chunk.
    /// </summary>
    public Control? ContentPanel { get; init; }
}

