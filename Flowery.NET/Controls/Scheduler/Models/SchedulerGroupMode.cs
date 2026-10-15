// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
namespace Flowery.Controls.Scheduler.Models;

/// <summary>
/// Header hierarchy for the Planner view: which axis is the outer grouping.
/// </summary>
public enum SchedulerGroupMode
{
    /// <summary>Outer = resource, inner = day. One lane per resource, days nested within.</summary>
    ByResource,

    /// <summary>Outer = day, inner = resource. One column per day, resources nested within.</summary>
    ByDay,
}
