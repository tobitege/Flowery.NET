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
    internal sealed class SkiaGlassRedraw(Visual owner)
    {
        // A measurement reads pixels back from the GPU, which stalls the frame: a few per second follow a change
        // closely enough. A change behind the whole glass redraws all of it, which looks like any other full redraw, so
        // without a known change a check every second still catches it, at a third of the cost for an animated glass.
        private const long MeasureInterval = 300;
        private const long IdleMeasureInterval = 1000;
        private static readonly TimeSpan LiveTime = TimeSpan.FromMilliseconds(300);

        private int _changeQueued;
        private DateTime _liveUntil;
        private bool _frameRequested;
        private long _lastMeasure = long.MinValue / 2;
        private int _measureLaterQueued;
        private int _measureDue = 1;
        private bool _measureBackdrop;

        /// <summary>Gets how many glasses redraw in full now, counted on the UI thread; for tests.</summary>
        internal static int LiveCount { get; private set; }

        /// <summary>Incremented on the UI thread for each full redraw, so the new draw operation differs from the last.</summary>
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

        /// <summary>Called on the render thread when a frame redrew only part of the glass.</summary>
        public void BackdropChanged()
        {
            Volatile.Write(ref _measureDue, 1);
            if (Interlocked.Exchange(ref _changeQueued, 1) == 1)
                return;
            Dispatcher.UIThread.Post(() =>
            {
                _changeQueued = 0;
                _liveUntil = DateTime.UtcNow + LiveTime;
                RequestFrame();
            }, DispatcherPriority.Render);
        }

        /// <summary>
        /// Called on the render thread with the whole backdrop at hand: returns whether to measure it now. After the
        /// backdrop changed it measures at most every <see cref="MeasureInterval"/>, and a render too soon schedules a
        /// render for later, so the last state behind the glass is measured too. Without a change it measures at most
        /// every <see cref="IdleMeasureInterval"/> and schedules nothing, since a scheduled render redraws the glass,
        /// which could make a glass around it redraw this one again.
        /// </summary>
        public bool TryStartMeasure()
        {
            if (!MeasureBackdrop)
                return false;
            var due = Volatile.Read(ref _measureDue) == 1;
            var now = Environment.TickCount64;
            var wait = Interlocked.Read(ref _lastMeasure) + (due ? MeasureInterval : IdleMeasureInterval) - now;
            if (wait > 0)
            {
                if (due)
                    MeasureLater(wait);
                return false;
            }
            Interlocked.Exchange(ref _lastMeasure, now);
            Volatile.Write(ref _measureDue, 0);
            return true;
        }

        /// <summary>Called on the render thread with a measured backdrop color.</summary>
        public void ReportBackdropColor(Color color) =>
            Dispatcher.UIThread.Post(() => SkiaGlassRenderer.ReportMeasuredColor(owner, color), DispatcherPriority.Background);

        private void RequestFrame()
        {
            if (_frameRequested || TopLevel.GetTopLevel(owner) is not { } topLevel)
                return;
            _frameRequested = true;
            LiveCount++;
            topLevel.RequestAnimationFrame(OnFrame);
        }

        private void OnFrame(TimeSpan time)
        {
            _frameRequested = false;
            LiveCount--;
            if (DateTime.UtcNow > _liveUntil)
                return;
            Version++;
            owner.InvalidateVisual();
            RequestFrame();
        }

        private void MeasureLater(long delay)
        {
            if (Interlocked.Exchange(ref _measureLaterQueued, 1) == 1)
                return;
            Dispatcher.UIThread.Post(() => DispatcherTimer.RunOnce(() =>
            {
                _measureLaterQueued = 0;
                owner.InvalidateVisual();
            }, TimeSpan.FromMilliseconds(delay)));
        }
    }
}
