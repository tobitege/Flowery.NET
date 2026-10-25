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
    /// visual redraws, so a lone redraw of one glass would look like a backdrop change to the glasses it overlaps.
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
        /// Milliseconds between two measurements without a known change, and after a repaint of all of the glass from
        /// outside, such as by a layout pass, a hover color or an animated background: once a second costs a third of
        /// measuring every <see cref="MeasureFrames"/> frames, for a glass that is repainted all the time.
        /// </summary>
        internal const long IdleMeasureInterval = 1000;

        // Why a measurement is due: a change behind the glass is measured after MeasureFrames full redraws, a repaint of
        // all of the glass after IdleMeasureInterval.
        private const int ChangeDue = 1;
        private const int RepaintDue = 2;
        // A measured color is on its way to the glass, or arrived as a new backdrop color and still has to be drawn.
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
        private int _wakeQueued;
        private long _lastMeasure = long.MinValue / 2;
        private int _framesSinceMeasure = MeasureFrames;
        private int _measureDue = ChangeDue;
        // The group redrew the glass for a measurement after a repaint; if the glass was not rendered then (hidden or
        // clipped away), it does not hold the group any longer.
        private int _repaintRedrawn;
        private int _report;
        private bool _measureBackdrop;

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

        // A measurement after a repaint may run: the interval since the last one has passed.
        private bool RepaintMayMeasure => Clock() - Interlocked.Read(ref _lastMeasure) >= IdleMeasureInterval;

        /// <summary>
        /// Whether the glass keeps its live group redrawing: a measurement after a change waits for its full redraws, a
        /// measurement after a repaint may run and has not had its frame yet, or a measured color has not been drawn yet.
        /// A measurement after a repaint that may not run yet waits for a wake-up instead, without redrawing.
        /// </summary>
        internal bool HoldsGroup
        {
            get
            {
                if (Volatile.Read(ref _report) != 0)
                    return true;
                if (!MeasureBackdrop)
                    return false;
                return Volatile.Read(ref _measureDue) switch
                {
                    ChangeDue => Volatile.Read(ref _framesSinceMeasure) < MeasureFrames,
                    RepaintDue => RepaintMayMeasure && Volatile.Read(ref _repaintRedrawn) == 0,
                    _ => false
                };
            }
        }

        /// <summary>
        /// Called on the render thread when a frame redrew all of the glass without a redraw of its own: a layout pass of
        /// an ancestor (Avalonia then marks the whole subtree dirty), a layer of the glass such as its animated background,
        /// or something over all of it, such as a hover color. No part of the glass shows an old blur, but the backdrop
        /// may have changed, so a measurement is due, paced like one without a known change: right away when the interval
        /// has passed, otherwise in one frame of the live group after it. A known change behind the glass stays due.
        /// </summary>
        public void BackdropRepainted()
        {
            if (Interlocked.CompareExchange(ref _measureDue, RepaintDue, 0) == 0)
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
        /// <see cref="IdleMeasureInterval"/> has passed; a render too soon asks for one frame of the group after it.
        /// Without either it measures at most every <see cref="IdleMeasureInterval"/> and asks for nothing.
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
                case RepaintDue when !RepaintMayMeasure:
                    WakeForMeasure();
                    return false;
                case 0 when !RepaintMayMeasure:
                    return false;
            }
            Interlocked.Exchange(ref _lastMeasure, Clock());
            Volatile.Write(ref _framesSinceMeasure, 0);
            Volatile.Write(ref _measureDue, 0);
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
            // At the priority of the group's frames, so the color is applied before the group goes on or stops.
            Dispatcher.UIThread.Post(() =>
            {
                if (!SkiaGlassRenderer.ReportMeasuredColor(owner, color))
                {
                    Interlocked.CompareExchange(ref _report, 0, ReportPosted);
                    return;
                }
                Interlocked.CompareExchange(ref _report, ReportApplied, ReportPosted);
                // A color measured outside the group, such as in the first render, is drawn in a frame of the group too.
                Group()?.Join(this, extend: false);
            }, DispatcherPriority.Render);
        }

        private LiveGroup? Group() =>
            TopLevel.GetTopLevel(owner) is { } topLevel ? LiveGroups.GetValue(topLevel, static top => new LiveGroup(top)) : null;

        // A measurement after a repaint that came too soon: one frame of the group lets it run once the interval passed.
        private void WakeForMeasure()
        {
            if (Interlocked.Exchange(ref _wakeQueued, 1) == 1)
                return;
            var remaining = IdleMeasureInterval - (Clock() - Interlocked.Read(ref _lastMeasure));
            Dispatcher.UIThread.Post(() => Wake(TimeSpan.FromMilliseconds(Math.Max(1, remaining)), () =>
            {
                _wakeQueued = 0;
                Group()?.Join(this, extend: false);
            }), DispatcherPriority.Render);
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
            if (Volatile.Read(ref _measureDue) == RepaintDue && RepaintMayMeasure)
                Volatile.Write(ref _repaintRedrawn, 1);
            // This frame draws a new backdrop color, and the text it changed, with the whole group.
            Interlocked.CompareExchange(ref _report, 0, ReportApplied);
            owner.InvalidateVisual();
        }

        /// <summary>
        /// The glasses of one top level that redraw in full. They share one frame request and one end, so they redraw in
        /// the same frames and stop in the same frame. A glass that stopped a frame before another would see that one's
        /// redraw as a change behind it and start again, and overlapping glasses would keep each other redrawing.
        /// </summary>
        private sealed class LiveGroup(TopLevel topLevel)
        {
            private readonly HashSet<SkiaGlassRedraw> _members = [];
            private int _framesLeft;
            private bool _frameRequested;

            // A glass whose backdrop changed keeps the whole group redrawing for LiveFrames more frames; a glass that
            // waits for a measurement only joins, and the group runs on until that measurement had its chance.
            public void Join(SkiaGlassRedraw redraw, bool extend)
            {
                _members.Add(redraw);
                if (extend)
                    _framesLeft = LiveFrames;
                RequestFrame();
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
