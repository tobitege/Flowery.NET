// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
namespace Flowery.Controls.Scheduler.Models;

/// <summary>The calendar view currently displayed by the <c>DaisyScheduler</c>.</summary>
public enum SchedulerView
{
    /// <summary>Single day, time grid.</summary>
    Day,

    /// <summary>Seven days, time grid.</summary>
    Week,

    /// <summary>Calendar month, day cells.</summary>
    Month,

    /// <summary>Flat, date-grouped list of upcoming appointments.</summary>
    Agenda,

    /// <summary>Multi-resource lanes with a nested time grid.</summary>
    Planner,

    /// <summary>One horizontal day, with named, collapsible resource groups.</summary>
    Timeline,

    /// <summary>Month navigator with week numbers and a scrollable daily appointment list.</summary>
    Calendar,
}
