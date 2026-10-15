namespace Flowery.Controls.Scheduler.Models;

/// <summary>The category of a schedulable resource.</summary>
public enum ResourceKind
{
    /// <summary>No category has been assigned.</summary>
    Unspecified,
    /// <summary>A participant or member of staff.</summary>
    Person,
    /// <summary>A room or other bookable space.</summary>
    Room,
    /// <summary>A car, van, or other vehicle.</summary>
    Vehicle,
    /// <summary>A machine, tool, or other piece of equipment.</summary>
    Equipment,
    /// <summary>A resource outside the predefined categories.</summary>
    Other
}
