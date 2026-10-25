using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Flowery.Skia
{
    /// <summary>
    /// Keeps a SkiaSharp glass redrawing in full for a moment after its backdrop changed, and paces the measurement of
    /// its backdrop color. A frame redraws only the changed areas, but the blur spreads each change a few pixels
    /// further, so a partial redraw would leave the old blur at the edges of the changed area. While the backdrop keeps
    /// changing, the whole glass is redrawn every frame; afterwards it returns to partial redraws.
    /// </summary>
    /// <remarks>
    /// Every full redraw goes through the <see cref="LiveGroup"/> of the top level and is counted in frames, not in
    /// wall-clock time. A glass never redraws on its own: Avalonia repaints a glass in part whenever an overlapping
    /// visual redraws, so a lone redraw of one glass would look like a backdrop change to the glasses it overlaps. The
    /// group therefore takes the glasses that overlap a joining one along.
    /// </remarks>
    internal sealed class SkiaGlassRedraw(Visual owner)
    {
        /// <summary>Frames a glass keeps redrawing in full after the last change behind it, about 0.3 s at 60 Hz.</summary>
        internal const int LiveFrames = 18;

        /// <summary>
        /// Frames of full redraws between two measurements after a change. A measurement reads pixels back from the
        /// GPU, which stalls the frame: a few per second follow a change closely enough.
        /// </summary>
        internal const int MeasureFrames = 18;

        /// <summary>
        /// Milliseconds between two measurements without a known change, and after a repaint of all of the glass that
        /// was no redraw of its own, such as by a layout pass, a hover color or an animated background: once a second
        /// costs a third of measuring every <see cref="MeasureFrames"/> frames, for a glass that is repainted all the time.
        /// </summary>
        internal const long IdleMeasureInterval = 1000;

        // Why a measurement is due: a change behind the glass is measured after MeasureFrames full redraws, a repaint of
        // all of the glass once IdleMeasureInterval has passed.
        private const int NotDue = 0;
        private const int ChangeDue = 1;
        private const int RepaintDue = 2;
        // A measured color is on its way to the glass, or arrived as a new backdrop color and still has to be drawn.
        private const int NoReport = 0;
        private const int ReportPosted = 1;
        private const int ReportApplied = 2;
        private static readonly ConditionalWeakTable<TopLevel, LiveGroup> LiveGroups = new();

        /// <summary>The clock of the measurement pacing in milliseconds; tests replace it.</summary>
        internal static Func<long> Clock { get; set; } = static () => Environment.TickCount64;

        /// <summary>Runs an action on the UI thread after a delay; tests replace it.</summary>
        internal static Action<TimeSpan, Action> Wake { get; set; } =
            static (delay, action) => DispatcherTimer.RunOnce(action, delay, DispatcherPriority.Render);

        private int _changeQueued;
        private int _holdQueued;
        private int _waitQueued;
        private long _lastMeasure = long.MinValue / 2;
        private int _framesSinceMeasure = MeasureFrames;
        private int _measureDue = ChangeDue;
        // The group redrew the glass for a measurement after a repaint; if the glass was not rendered then (hidden or
        // clipped away), it does not hold the group any longer.
        private int _repaintRedrawn;
        private int _report;
        private int _renderedVersion = -1;
        private bool _measureBackdrop;
        private LiveGroup? _knownBy;

        /// <summary>Incremented on the UI thread for each full redraw; tests count the full redraws with it.</summary>
        public int Version { get; private set; }

        /// <summary>
        /// Set on the UI thread for each render: whether the glass wants its backdrop color measured. Turning it on
        /// measures on the next render.
        /// </summary>
        public bool MeasureBackdrop
        {
            get => Volatile.Read(ref _measureBackdrop);
            set
            {
                if (value && !Volatile.Read(ref _measureBackdrop))
                    Volatile.Write(ref _measureDue, ChangeDue);
                Volatile.Write(ref _measureBackdrop, value);
            }
        }

        // IdleMeasureInterval has passed since the last measurement.
        private bool IntervalPassed => Clock() - Interlocked.Read(ref _lastMeasure) >= IdleMeasureInterval;

        private bool RepaintPending => MeasureBackdrop && Volatile.Read(ref _measureDue) == RepaintDue;

        /// <summary>
        /// Whether the glass keeps its live group redrawing: a measurement after a change waits for its full redraws, a
        /// measurement after a repaint may run and has not had its frame yet, or a measured color has not been drawn yet.
        /// A measurement after a repaint that may not run yet waits for the group's wake-up instead, without redrawing.
        /// </summary>
        internal bool HoldsGroup
        {
            get
            {
                if (Volatile.Read(ref _report) != NoReport)
                    return true;
                if (!MeasureBackdrop)
                    return false;
                return Volatile.Read(ref _measureDue) switch
                {
                    ChangeDue => Volatile.Read(ref _framesSinceMeasure) < MeasureFrames,
                    RepaintDue => IntervalPassed && Volatile.Read(ref _repaintRedrawn) == 0,
                    _ => false
                };
            }
        }

        /// <summary>
        /// Called on the UI thread when the glass renders: the live group of its top level knows it from then on, so a
        /// glass that joins the group takes this one along when they overlap.
        /// </summary>
        public void Register()
        {
            if (Group() is { } group && !ReferenceEquals(group, _knownBy))
            {
                _knownBy = group;
                group.Know(this);
            }
        }

        /// <summary>
        /// Called on the render thread for each first render of a draw operation: returns whether the operation comes from
        /// a redraw of the group, which raised <see cref="Version"/>, and not from another invalidation of the glass,
        /// such as a new size.
        /// </summary>
        public bool IsNewRedraw(int version) => Interlocked.Exchange(ref _renderedVersion, version) != version;

        /// <summary>
        /// Called on the render thread when a frame redrew all of the glass without a redraw of its own: a layout pass of
        /// an ancestor (Avalonia then marks the whole subtree dirty), a new size, a layer of the glass such as its
        /// animated background, or something over all of it, such as a hover color. No part of the glass shows an old
        /// blur, but the backdrop may have changed, so a measurement is due, paced like one without a known change:
        /// right away when the interval has passed, otherwise in one frame of the group after it. A known change behind
        /// the glass stays due.
        /// </summary>
        public void BackdropRepainted()
        {
            if (Interlocked.CompareExchange(ref _measureDue, RepaintDue, NotDue) == NotDue)
                Volatile.Write(ref _repaintRedrawn, 0);
        }

        /// <summary>Called on the render thread when a frame redrew only part of the glass.</summary>
        public void BackdropChanged()
        {
            Volatile.Write(ref _measureDue, ChangeDue);
            if (Interlocked.Exchange(ref _changeQueued, 1) == 1)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                _changeQueued = 0;
                Group()?.Join(this, extend: true);
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Called on the render thread with the whole backdrop at hand: returns whether to measure it now. After the
        /// backdrop changed it measures once <see cref="MeasureFrames"/> full redraws have passed since the last
        /// measurement; a render too soon keeps the glass in its live group until then, so the last state behind the
        /// glass is measured in a frame that redraws the whole group. After a repaint of all of the glass it measures once
        /// <see cref="IdleMeasureInterval"/> has passed; a render too soon makes the group wake up then and redraw the
        /// glass once. Without either it measures at most every <see cref="IdleMeasureInterval"/> and asks for nothing.
        /// </summary>
        public bool TryStartMeasure()
        {
            if (!MeasureBackdrop)
                return false;
            switch (Volatile.Read(ref _measureDue))
            {
                case ChangeDue when Volatile.Read(ref _framesSinceMeasure) < MeasureFrames:
                    HoldForMeasure();
                    return false;
                case RepaintDue when !IntervalPassed:
                    WaitForMeasure();
                    return false;
                case NotDue when !IntervalPassed:
                    return false;
            }
            Interlocked.Exchange(ref _lastMeasure, Clock());
            Volatile.Write(ref _framesSinceMeasure, 0);
            Volatile.Write(ref _measureDue, NotDue);
            return true;
        }

        /// <summary>
        /// Called on the render thread with a measured backdrop color. The glass holds its live group until the color is
        /// applied and, when it is a new one, drawn in a frame that redraws the whole group: a new color can change the
        /// text over the glass, and a text redrawn alone repaints part of the glass, which would look like a change behind
        /// it and start the group again.
        /// </summary>
        public void ReportBackdropColor(Color color)
        {
            Volatile.Write(ref _report, ReportPosted);
            // At the priority of the group's frames, so the color mostly arrives before the next one; the group holds
            // either way until it did.
            Dispatcher.UIThread.Post(static state =>
            {
                var (redraw, color) = ((SkiaGlassRedraw, Color))state!;
                redraw.ApplyReport(color);
            }, (this, color), DispatcherPriority.Render);
        }

        private void ApplyReport(Color color)
        {
            if (!SkiaGlassRenderer.ReportMeasuredColor(owner, color))
            {
                Interlocked.CompareExchange(ref _report, NoReport, ReportPosted);
                return;
            }
            Interlocked.CompareExchange(ref _report, ReportApplied, ReportPosted);
            // A color measured outside the group, such as in the first render, is drawn in a frame of the group too.
            Group()?.Join(this, extend: false);
        }

        private LiveGroup? Group() =>
            TopLevel.GetTopLevel(owner) is { } topLevel ? LiveGroups.GetValue(topLevel, static top => new LiveGroup(top)) : null;

        // A measurement after a repaint that came too soon: the group wakes up once the interval passed.
        private void WaitForMeasure()
        {
            if (Interlocked.Exchange(ref _waitQueued, 1) == 1)
                return;
            Dispatcher.UIThread.Post(static state =>
            {
                var redraw = (SkiaGlassRedraw)state!;
                redraw._waitQueued = 0;
                redraw.Group()?.Wait(redraw);
            }, this, DispatcherPriority.Render);
        }

        private void HoldForMeasure()
        {
            if (Interlocked.Exchange(ref _holdQueued, 1) == 1)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                _holdQueued = 0;
                Group()?.Join(this, extend: false);
            }, DispatcherPriority.Render);
        }

        private void Redraw()
        {
            Version++;
            Interlocked.Increment(ref _framesSinceMeasure);
            if (Volatile.Read(ref _measureDue) == RepaintDue && IntervalPassed)
                Volatile.Write(ref _repaintRedrawn, 1);
            // This frame draws a new backdrop color, and the text it changed, with the whole group.
            Interlocked.CompareExchange(ref _report, NoReport, ReportApplied);
            owner.InvalidateVisual();
        }

        // The area of the glass in its top level, or null where it is not shown there.
        private Rect? AreaIn(TopLevel topLevel) =>
            owner.IsEffectivelyVisible && owner.TransformToVisual(topLevel) is { } transform
                ? new Rect(owner.Bounds.Size).TransformToAABB(transform)
                : null;

        /// <summary>
        /// The glasses of one top level that redraw in full. They share one frame request and one end, so they redraw in
        /// the same frames and stop in the same frame. A glass that stopped a frame before another would see that one's
        /// redraw as a change behind it and start again, and overlapping glasses would keep each other redrawing.
        /// </summary>
        private sealed class LiveGroup(TopLevel topLevel)
        {
            private readonly HashSet<SkiaGlassRedraw> _members = [];
            private readonly HashSet<SkiaGlassRedraw> _waiting = [];
            private readonly List<WeakReference<SkiaGlassRedraw>> _known = [];
            private int _framesLeft;
            private bool _frameRequested;
            private long _wakeAt = long.MaxValue;

            public void Know(SkiaGlassRedraw redraw) => _known.Add(new WeakReference<SkiaGlassRedraw>(redraw));

            // A glass whose backdrop changed keeps the whole group redrawing for LiveFrames more frames; a glass that
            // waits for a measurement or a new color only joins, and the group runs on until they had their frames. The
            // glasses that overlap a joining one join with it, so none of them sees the other's redraw as a change.
            public void Join(SkiaGlassRedraw redraw, bool extend)
            {
                AddWithOverlaps(redraw);
                if (extend)
                    _framesLeft = LiveFrames;
                RequestFrame();
            }

            // A measurement after a repaint waits for the interval since the last measurement of that glass. One wake-up
            // serves all waiting glasses of the top level, so they redraw together.
            public void Wait(SkiaGlassRedraw redraw)
            {
                if (!redraw.RepaintPending)
                    return;
                _waiting.Add(redraw);
                ScheduleWake(Interlocked.Read(ref redraw._lastMeasure) + IdleMeasureInterval);
            }

            private void ScheduleWake(long at)
            {
                if (at >= _wakeAt)
                    return;
                _wakeAt = at;
                Wake(TimeSpan.FromMilliseconds(Math.Max(1, at - Clock())), OnWake);
            }

            // A glass that was measured since it began to wait waits for the interval from that measurement.
            private void OnWake()
            {
                _wakeAt = long.MaxValue;
                var next = long.MaxValue;
                var joined = false;
                foreach (var redraw in _waiting.ToArray())
                {
                    if (!redraw.RepaintPending)
                    {
                        _waiting.Remove(redraw);
                    }
                    else if (redraw.IntervalPassed)
                    {
                        _waiting.Remove(redraw);
                        AddWithOverlaps(redraw);
                        joined = true;
                    }
                    else
                    {
                        next = Math.Min(next, Interlocked.Read(ref redraw._lastMeasure) + IdleMeasureInterval);
                    }
                }
                if (next != long.MaxValue)
                    ScheduleWake(next);
                if (joined)
                    RequestFrame();
            }

            private void AddWithOverlaps(SkiaGlassRedraw redraw)
            {
                if (!_members.Add(redraw))
                    return;
                var areas = new List<Rect>();
                if (redraw.AreaIn(topLevel) is { } area)
                    areas.Add(area);
                for (var i = 0; i < areas.Count; i++)
                {
                    for (var k = _known.Count - 1; k >= 0; k--)
                    {
                        if (!_known[k].TryGetTarget(out var other))
                        {
                            _known.RemoveAt(k);
                            continue;
                        }
                        if (_members.Contains(other) || other.AreaIn(topLevel) is not { } otherArea || !otherArea.Intersects(areas[i]))
                            continue;
                        _members.Add(other);
                        areas.Add(otherArea);
                    }
                }
            }

            private void RequestFrame()
            {
                if (_frameRequested)
                    return;
                _frameRequested = true;
                topLevel.RequestAnimationFrame(OnFrame);
            }

            private void OnFrame(TimeSpan time)
            {
                _frameRequested = false;
                // A pending measurement is allowed in the render of the frame that reaches MeasureFrames, or of the
                // frame after the wake-up; a glass that was not rendered then (hidden or clipped away) no longer holds
                // the group.
                if (_framesLeft == 0 && !_members.Any(static member => member.HoldsGroup))
                {
                    _members.Clear();
                    return;
                }
                if (_framesLeft > 0)
                    _framesLeft--;
                foreach (var member in _members)
                    member.Redraw();
                RequestFrame();
            }
        }
    }
}
