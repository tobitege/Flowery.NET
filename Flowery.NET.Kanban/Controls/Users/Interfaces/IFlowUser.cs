using System.Collections.Generic;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Represents a user identity in the Kanban system.
/// </summary>
internal interface IFlowUser
{
    /// <summary>
    /// Canonical unique identifier in provider:rawId format.
    /// It must equal FlowUserIdHelper.Compose(ProviderKey, RawId).
    /// </summary>
    string Id { get; }

    /// <summary>
    /// The provider key this user belongs to (e.g., "aad", "auth0", "local").
    /// </summary>
    string ProviderKey { get; }

    /// <summary>
    /// The raw ID as provided by the identity source (without provider prefix).
    /// </summary>
    string RawId { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Contact email address.
    /// </summary>
    string? Email { get; }

    /// <summary>
    /// Provider-owned URL to the user's avatar/profile picture. The Kanban UI does
    /// not fetch this URL directly; providers can resolve it through
    /// <see cref="IUserAvatarStreamProvider"/>.
    /// </summary>
    string? AvatarUrl { get; }

    /// <summary>
    /// Embedded avatar image bytes (for offline support).
    /// </summary>
    byte[]? AvatarBytes { get; }

    /// <summary>
    /// User's current presence status.
    /// </summary>
    FlowUserStatus Status { get; }

    /// <summary>
    /// Organizational department or team.
    /// </summary>
    string? Department { get; }

    /// <summary>
    /// Job title or role.
    /// </summary>
    string? Title { get; }

    /// <summary>
    /// Extensible custom properties.
    /// </summary>
    IReadOnlyDictionary<string, object>? CustomData { get; }
}
