using System;
using System.Collections.Generic;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Default implementation of IFlowUser.
/// </summary>
public class FlowUser : IFlowUser
{
    public string Id { get; set; } = string.Empty;
    public string ProviderKey { get; set; } = "local";
    public string RawId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? AvatarUrl { get; set; }
    public byte[]? AvatarBytes { get; set; }
    public FlowUserStatus Status { get; set; } = FlowUserStatus.Unknown;
    public string? Department { get; set; }
    public string? Title { get; set; }
    public IReadOnlyDictionary<string, object>? CustomData { get; set; }

    public FlowUser() { }

    public FlowUser(string id, string displayName, string providerKey = "local")
    {
        RawId = id;
        ProviderKey = providerKey;
        Id = FlowUserIdHelper.Compose(providerKey, id);
        DisplayName = displayName;
    }
}
