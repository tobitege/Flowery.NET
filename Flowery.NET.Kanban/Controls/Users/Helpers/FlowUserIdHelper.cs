using System;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Static helper methods for working with composite user IDs.
/// </summary>
public static class FlowUserIdHelper
{
    public const char Delimiter = ':';

    /// <summary>
    /// Composes a composite ID from provider key and raw ID.
    /// </summary>
    public static string Compose(string providerKey, string rawId)
    {
        if (string.IsNullOrEmpty(providerKey))
            return rawId;

        return $"{providerKey}{Delimiter}{rawId}";
    }

    /// <summary>
    /// Parses a composite ID into provider key and raw ID.
    /// Returns (null, originalId) if no prefix found.
    /// </summary>
    public static (string? ProviderKey, string RawId) Parse(string compositeId)
    {
        if (string.IsNullOrEmpty(compositeId))
            return (null, compositeId);

        var delimiterIndex = compositeId.IndexOf(Delimiter);

        if (delimiterIndex <= 0)
            return (null, compositeId);

        var providerKey = compositeId[..delimiterIndex];
        var rawId = compositeId[(delimiterIndex + 1)..];

        return (providerKey, rawId);
    }

    /// <summary>
    /// Extracts just the provider key from a composite ID.
    /// </summary>
    public static string? GetProviderKey(string compositeId)
    {
        var (providerKey, _) = Parse(compositeId);
        return providerKey;
    }

    /// <summary>
    /// Extracts just the raw ID from a composite ID.
    /// </summary>
    public static string GetRawId(string compositeId)
    {
        var (_, rawId) = Parse(compositeId);
        return rawId;
    }

    /// <summary>
    /// Checks if an ID is in composite format (has provider prefix).
    /// </summary>
    public static bool IsCompositeId(string id)
    {
        if (string.IsNullOrEmpty(id))
            return false;

        var delimiterIndex = id.IndexOf(Delimiter);
        return delimiterIndex > 0 && delimiterIndex < id.Length - 1;
    }
}
