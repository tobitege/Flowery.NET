using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Orchestrates multiple user providers with composite ID support.
/// Interface version: 1.0
/// </summary>
internal interface ICompositeUserProvider : IUserProvider
{
    /// <summary>
    /// Registers a provider with its key.
    /// </summary>
    void RegisterProvider(IUserProvider provider);

    /// <summary>
    /// Unregisters a provider by key.
    /// </summary>
    bool UnregisterProvider(string providerKey);

    /// <summary>
    /// Gets a registered provider by key.
    /// </summary>
    IUserProvider? GetProviderByKey(string providerKey);

    /// <summary>
    /// Lists all registered provider keys.
    /// </summary>
    IReadOnlyList<string> RegisteredProviderKeys { get; }

    /// <summary>
    /// The default provider key for new users.
    /// </summary>
    string DefaultProviderKey { get; set; }

    /// <summary>
    /// True if only one provider is registered. Canonical IDs remain provider-qualified.
    /// </summary>
    bool IsSingleProviderMode { get; }

    /// <summary>
    /// Gets a user by canonical ID (e.g., "aad:guid").
    /// </summary>
    Task<IFlowUser?> GetUserByCompositeIdAsync(
        string compositeId,
        CancellationToken cancellation = default);

    /// <summary>
    /// Composes a composite ID from provider key and raw ID.
    /// </summary>
    string ComposeId(string providerKey, string rawId);

    /// <summary>
    /// Parses a composite ID into provider key and raw ID.
    /// Raw IDs without a provider prefix are rejected.
    /// </summary>
    (string ProviderKey, string RawId) ParseId(string compositeId);

    /// <summary>
    /// Searches across all providers, grouped by provider.
    /// </summary>
    Task<IReadOnlyDictionary<string, IEnumerable<IFlowUser>>> SearchAllProvidersAsync(
        string query,
        int maxResultsPerProvider = 10,
        CancellationToken cancellation = default);
}
