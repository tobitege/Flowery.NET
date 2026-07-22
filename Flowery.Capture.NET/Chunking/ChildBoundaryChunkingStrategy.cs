using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;

namespace Flowery.Capture.Chunking;

/// <summary>
/// Chunking strategy that splits content at child element boundaries.
/// Avoids cutting through controls by starting new chunks at the top of child elements.
/// </summary>
public sealed class ChildBoundaryChunkingStrategy : IChunkingStrategy
{
    /// <summary>
    /// Default instance for convenience.
    /// </summary>
    public static ChildBoundaryChunkingStrategy Default { get; } = new();

    /// <inheritdoc/>
    public IReadOnlyList<double> CalculateChunks(Control container, double viewportHeight, Panel? contentPanel = null)
    {
        var chunks = new List<double>();
        var bounds = container.Bounds;

        // If it fits in one chunk, return single offset at 0
        if (bounds.Height <= viewportHeight)
        {
            chunks.Add(0);
            return chunks;
        }

        // Try smart chunking if we have a content panel
        if (contentPanel != null && TryCalculateSmartChunks(contentPanel, viewportHeight, chunks))
        {
            return chunks;
        }

        // Try to find content panel within container if it's a Panel with children
        if (container is Panel panel && panel.Children.Count > 0)
        {
            // Look for the largest child panel (likely the content area)
            Panel? largestPanel = null;
            double largestHeight = 0;

            foreach (var child in panel.Children)
            {
                if (child is Panel childPanel && childPanel.Bounds.Height > largestHeight)
                {
                    largestHeight = childPanel.Bounds.Height;
                    largestPanel = childPanel;
                }
            }

            if (largestPanel != null && TryCalculateSmartChunks(largestPanel, viewportHeight, chunks))
            {
                return chunks;
            }
        }

        // Fallback: fixed-height chunks
        return CalculateFixedChunks(bounds.Height, viewportHeight);
    }

    private static bool TryCalculateSmartChunks(Panel contentPanel, double viewportHeight, List<double> chunks)
    {
        if (contentPanel.Children.Count == 0)
            return false;

        var contentStartY = contentPanel.Bounds.Y;

        // Get spacing from parent if it's a StackPanel
        double spacing = contentPanel is StackPanel sp ? sp.Spacing : 0;

        double pageStart = 0;
        double currentPageHeight = contentStartY; // Start after any header content

        foreach (var child in contentPanel.Children)
        {
            var childMargin = child is Control c ? c.Margin : default;
            var childHeight = child.Bounds.Height + childMargin.Top + childMargin.Bottom + spacing;

            // If adding this child would exceed viewport and we have some content already
            if (currentPageHeight + childHeight > viewportHeight && currentPageHeight > 100)
            {
                chunks.Add(pageStart);

                // New page starts at this child's position
                var childTop = contentStartY + child.Bounds.Y;
                pageStart = childTop;
                currentPageHeight = 0;
            }

            currentPageHeight += childHeight;
        }

        // Add final chunk
        if (chunks.Count == 0 || pageStart > chunks[^1])
        {
            chunks.Add(pageStart);
        }

        return chunks.Count > 0;
    }

    private static IReadOnlyList<double> CalculateFixedChunks(double totalHeight, double viewportHeight)
    {
        var chunks = new List<double>();
        var count = (int)Math.Ceiling(totalHeight / viewportHeight);

        for (int i = 0; i < count; i++)
        {
            chunks.Add(i * viewportHeight);
        }

        return chunks;
    }
}

