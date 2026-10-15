using System;

namespace Flowery.Controls.Scheduler.Models;

/// <summary>A host-named resource group. Kind and the optional GroupType/GroupValue criterion are combined.</summary>
public sealed class ResourceGroupModel
{
    public object Id { get; set; } = 0;
    public string Name { get; set; } = string.Empty;
    public ResourceKind? Kind { get; set; }

    /// <summary>Host-defined key in ResourceModel.GroupValues, for example "Department" or "Location".
    /// Null/empty matches every resource of the requested Kind.</summary>
    public string? GroupType { get; set; }
    public object? GroupValue { get; set; }

    public bool Matches(ResourceModel resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        return (Kind is null || resource.Kind == Kind) &&
            (string.IsNullOrEmpty(GroupType) || resource.GroupValues.TryGetValue(GroupType, out var value) && Equals(value, GroupValue));
    }
}
