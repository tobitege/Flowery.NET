using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Abstraction for user data sources.
/// </summary>
internal interface IUserProvider
{
    /// <summary>
    /// Unique key identifying this provider (e.g., "aad", "auth0", "local").
    /// Used as prefix in composite IDs.
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// Human-readable provider name for UI display.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Provider implementation version string for diagnostics.
    /// </summary>
    string ImplementationVersion { get; }

    /// <summary>
    /// Whether this provider supports avatar images.
    /// </summary>
    bool SupportsAvatars { get; }

    /// <summary>
    /// Whether this provider supports presence/status information.
    /// </summary>
    bool SupportsPresence { get; }

    /// <summary>
    /// Whether this provider supports real-time updates.
    /// </summary>
    bool SupportsRealtime { get; }

    /// <summary>
    /// Gets all available users from this provider.
    /// </summary>
    Task<IEnumerable<IFlowUser>> GetAllUsersAsync(CancellationToken cancellation = default);

    /// <summary>
    /// Gets a user by their raw provider ID.
    /// </summary>
    Task<IFlowUser?> GetUserByIdAsync(string rawId, CancellationToken cancellation = default);

    /// <summary>
    /// Searches users by query string (name, email, etc.).
    /// </summary>
    Task<IEnumerable<IFlowUser>> SearchUsersAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellation = default);

    /// <summary>
    /// Gets the currently authenticated user, if applicable.
    /// </summary>
    Task<IFlowUser?> GetCurrentUserAsync(CancellationToken cancellation = default);

    /// <summary>
    /// Refreshes the provider's internal cache.
    /// </summary>
    Task RefreshAsync(CancellationToken cancellation = default);

    /// <summary>
    /// Event raised when the user list changes.
    /// Providers may raise this event from any thread. UI consumers must marshal
    /// resulting UI work to their dispatcher and ignore notifications after detach.
    /// </summary>
    event Action? UsersChanged;
}
