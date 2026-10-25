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

        // A change behind the whole glass redraws all of it, which looks like any other full redraw, so without a known
        // change a check every second still catches it, at a third of the cost for an animated glass. This check only
        // decides whether a render that happens anyway measures; it never asks for a render.
        private const long IdleMeasureInterval = 1000;
        private static readonly ConditionalWeakTable<TopLevel, LiveGroup> LiveGroups = new();

        private int _changeQueued;
        private int _holdQueued;
        private long _lastMeasure = long.MinValue / 2;
        private int _framesSinceMeasure = MeasureFrames;
        private int _measureDue = 1;
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
                    Volatile.Write(ref _measureDue, 1);
                Volatile.Write(ref _measureBackdrop, value);
            }
        }

        // A measurement is due and has not run yet; the group keeps redrawing until it can.
        private bool MeasurePending => MeasureBackdrop && Volatile.Read(ref _measureDue) == 1;

        /// <summary>Called on the render thread when a frame redrew only part of the glass.</summary>
        public void BackdropChanged()
        {
            Volatile.Write(ref _measureDue, 1);
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
        /// glass is measured in a frame that redraws the whole group. Without a change it measures at most every
        /// <see cref="IdleMeasureInterval"/> milliseconds and asks for nothing.
        /// </summary>
        public bool TryStartMeasure()
        {
            if (!MeasureBackdrop)
                return false;
            var now = Environment.TickCount64;
            if (Volatile.Read(ref _measureDue) == 1)
            {
                if (Volatile.Read(ref _framesSinceMeasure) < MeasureFrames)
                {
                    HoldForMeasure();
                    return false;
                }
            }
            else if (now - Interlocked.Read(ref _lastMeasure) < IdleMeasureInterval)
                return false;
            Interlocked.Exchange(ref _lastMeasure, now);
            Volatile.Write(ref _framesSinceMeasure, 0);
            Volatile.Write(ref _measureDue, 0);
            return true;
        }

        /// <summary>Called on the render thread with a measured backdrop color.</summary>
        public void ReportBackdropColor(Color color) =>
            Dispatcher.UIThread.Post(() => SkiaGlassRenderer.ReportMeasuredColor(owner, color), DispatcherPriority.Background);

        private LiveGroup? Group() =>
            TopLevel.GetTopLevel(owner) is { } topLevel ? LiveGroups.GetValue(topLevel, static top => new LiveGroup(top)) : null;

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
                // A pending measurement is allowed in the render of the frame that reaches MeasureFrames; a glass
                // that was not rendered by then (hidden or clipped away) no longer holds the group.
                if (_framesLeft == 0 && !_members.Any(static member =>
                        member.MeasurePending && Volatile.Read(ref member._framesSinceMeasure) < MeasureFrames))
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
