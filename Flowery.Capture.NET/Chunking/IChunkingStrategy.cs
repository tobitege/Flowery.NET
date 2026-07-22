using System.Collections.Generic;
using Avalonia.Controls;

namespace Flowery.Capture.Chunking;

/// <summary>
/// Strategy for splitting tall content into multiple capture chunks.
/// </summary>
public interface IChunkingStrategy
{
    /// <summary>
    /// Calculates scroll offsets for each chunk to capture.
    /// </summary>
    /// <param name="container">The container control being captured.</param>
    /// <param name="viewportHeight">Maximum height per chunk.</param>
    /// <param name="contentPanel">Optional content panel for smart child-boundary chunking.</param>
    /// <returns>List of Y-offsets (relative to container) where each chunk starts.</returns>
    IReadOnlyList<double> CalculateChunks(Control container, double viewportHeight, Panel? contentPanel = null);
}

