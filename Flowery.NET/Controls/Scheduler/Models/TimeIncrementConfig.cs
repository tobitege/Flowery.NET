// Adapted from Xtreme.Components (MIT), commit a72f9fc92d41dc7c6c1785d2747b1f9677f31706.
// Copyright (c) 2026 Data Access Inc. and Xtreme.Components contributors.
// See ThirdPartyNotices/Xtreme.Components.txt for the license.
using System;
namespace Flowery.Controls.Scheduler.Models;

/// <summary>
/// Controls the vertical time scale of the Day / Week / Planner time grids: how tall an
/// hour is and where the major (labeled) and minor (snap) gridlines fall.
/// </summary>
public sealed class TimeIncrementConfig
{
    /// <summary>
    /// Spacing between labeled, heavier gridlines (e.g. one hour). Drives the hour labels.
    /// </summary>
    public TimeSpan MajorTick { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Spacing between minor gridlines and the snap granularity for drag/create
    /// (e.g. 30 minutes). Must divide <see cref="MajorTick"/> evenly for clean rows.
    /// </summary>
    public TimeSpan MinorTick { get; set; } = TimeSpan.FromMinutes(30);

    /// <summary>
    /// Vertical pixels per minute. At the default 0.8 an hour is 48px tall and a full day
    /// is 1152px. Increase for a roomier grid, decrease to fit more on screen.
    /// </summary>
    public double PixelsPerMinute { get; set; } = 0.8;

    /// <summary>Pixel height of one <see cref="MinorTick"/> slot.</summary>
    public double MinorSlotHeight => MinorTick.TotalMinutes * PixelsPerMinute;

    /// <summary>Pixel height of one <see cref="MajorTick"/> block.</summary>
    public double MajorSlotHeight => MajorTick.TotalMinutes * PixelsPerMinute;

    /// <summary>Pixel height of a full 24-hour day at this scale.</summary>
    public double DayHeight => 24 * 60 * PixelsPerMinute;

    /// <summary>A fresh config with the default 1h major / 30m minor / 0.8 px-per-min scale.</summary>
    public static TimeIncrementConfig Default => new();
}
