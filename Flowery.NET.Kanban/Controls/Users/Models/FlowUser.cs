using System;
using System.Collections.Generic;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Default implementation of IFlowUser.
/// </summary>
internal sealed class FlowUser : IFlowUser
{
    public string Id { get; }
    public string ProviderKey { get; }
    public string RawId { get; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public byte[]? AvatarBytes { get; set; }
    public FlowUserStatus Status { get; set; } = FlowUserStatus.Unknown;
    public string? Department { get; set; }
    public string? Title { get; set; }
    public IReadOnlyDictionary<string, object>? CustomData { get; set; }

    public FlowUser(string id, string displayName, string providerKey = "local")
    {
        var normalizedDisplayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDisplayName))
            throw new ArgumentException("Display name must be provided.", nameof(displayName));

        Id = FlowUserIdHelper.Compose(providerKey, id);
        ProviderKey = FlowUserIdHelper.GetProviderKey(Id);
        RawId = FlowUserIdHelper.GetRawId(Id);
        DisplayName = normalizedDisplayName;
    }
}
