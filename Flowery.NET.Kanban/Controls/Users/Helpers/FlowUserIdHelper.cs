using System;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Static helper methods for working with composite user IDs.
/// </summary>
internal static class FlowUserIdHelper
{
    public const char Delimiter = ':';

    /// <summary>
    /// Composes the canonical ID from a provider key and raw provider ID.
    /// </summary>
    public static string Compose(string providerKey, string rawId)
    {
        var normalizedProviderKey = providerKey?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedProviderKey))
            throw new ArgumentException("Provider key must be provided.", nameof(providerKey));

        if (normalizedProviderKey.Contains(Delimiter, StringComparison.Ordinal))
            throw new ArgumentException($"Provider key cannot contain '{Delimiter}'.", nameof(providerKey));

        var normalizedRawId = rawId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedRawId))
            throw new ArgumentException("Raw user ID must be provided.", nameof(rawId));

        return $"{normalizedProviderKey}{Delimiter}{normalizedRawId}";
    }

    /// <summary>
    /// Parses a canonical ID into its provider key and raw provider ID.
    /// </summary>
    public static (string ProviderKey, string RawId) Parse(string compositeId)
    {
        if (string.IsNullOrWhiteSpace(compositeId))
            throw new ArgumentException("Canonical user ID must be provided.", nameof(compositeId));

        var delimiterIndex = compositeId.IndexOf(Delimiter);
        if (delimiterIndex <= 0 || delimiterIndex == compositeId.Length - 1)
            throw new FormatException($"User ID '{compositeId}' is not in canonical provider:rawId format.");

        var providerKey = compositeId[..delimiterIndex];
        var rawId = compositeId[(delimiterIndex + 1)..];
        return (providerKey, rawId.Trim());
    }

    /// <summary>
    /// Resolves an identity from its provider-owned fields instead of trusting a second mutable ID representation.
    /// </summary>
    public static string Resolve(IFlowUser user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Compose(user.ProviderKey, user.RawId);
    }

    /// <summary>
    /// Tries to resolve a canonical identity from provider-owned fields.
    /// </summary>
    public static bool TryResolve(IFlowUser? user, out string canonicalId)
    {
        canonicalId = string.Empty;
        if (user == null)
            return false;

        try
        {
            canonicalId = Resolve(user);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    /// <summary>
    /// Extracts just the provider key from a composite ID.
    /// </summary>
    public static string GetProviderKey(string compositeId)
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
        if (string.IsNullOrWhiteSpace(id))
            return false;

        var delimiterIndex = id.IndexOf(Delimiter);
        return delimiterIndex > 0 && delimiterIndex < id.Length - 1;
    }
}
