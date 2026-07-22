using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Animation.Easings;
using Avalonia.Threading;

namespace Flowery.Effects
{
    /// <summary>
    /// Provides WASM-compatible animation utilities using manual interpolation.
    /// Uses Task.Delay + Dispatcher pattern for cross-platform reliability.
    /// </summary>
    public static class AnimationHelper
    {
        /// <summary>
        /// Minimum delay (in ms) per interpolation step.
        /// On some platforms (notably browser/WASM), sub-frame delays are effectively clamped,
        /// which can stretch short animations and cause overlap/cancellation artifacts.
        /// </summary>
        private const double MinimumStepDurationMilliseconds = 16.0;

        /// <summary>
        /// Default number of steps for smooth animation.
        /// </summary>
        public const int DefaultSteps = 30;

        private static int NormalizeSteps(TimeSpan duration, int steps)
        {
            if (steps < 1)
                return 1;

            var totalMs = duration.TotalMilliseconds;
            if (totalMs <= 0)
                return 1;

            // Cap the number of steps so each step delay stays >= MinimumStepDurationMilliseconds.
            // This keeps requested duration closer to reality on platforms where small delays are clamped.
            var maxSteps = (int)Math.Floor(totalMs / MinimumStepDurationMilliseconds);
            if (maxSteps < 1)
                maxSteps = 1;

            return steps > maxSteps ? maxSteps : steps;
        }

        /// <summary>
        /// Animate a single value from start to end using WASM-compatible interpolation.
        /// </summary>
        /// <param name="applyValue">Action to apply the interpolated value (called on UI thread)</param>
        /// <param name="from">Starting value</param>
        /// <param name="to">Target value</param>
        /// <param name="duration">Animation duration</param>
        /// <param name="easing">Easing function (defaults to LinearEasing)</param>
        /// <param name="steps">Number of interpolation steps</param>
        /// <param name="ct">Cancellation token</param>
        public static async Task AnimateAsync(
            Action<double> applyValue,
            double from,
            double to,
            TimeSpan duration,
            Easing? easing = null,
            int steps = DefaultSteps,
            CancellationToken ct = default)
        {
            easing ??= new LinearEasing();
            steps = NormalizeSteps(duration, steps);
            var stepDuration = duration.TotalMilliseconds / steps;

            for (int i = 0; i <= steps; i++)
            {
                if (ct.IsCancellationRequested) break;

                var t = (double)i / steps;
                var easedT = easing.Ease(t);
                var value = from + (to - from) * easedT;

                await Dispatcher.UIThread.InvokeAsync(() => applyValue(value));

                if (i < steps)
                    await Task.Delay((int)stepDuration, ct);
            }
        }

        /// <summary>
        /// Animate multiple values simultaneously (e.g., opacity + translateY).
        /// </summary>
        /// <param name="applyValues">Action receiving interpolation progress t (0-1, eased)</param>
        /// <param name="duration">Animation duration</param>
        /// <param name="easing">Easing function</param>
        /// <param name="steps">Number of interpolation steps</param>
        /// <param name="ct">Cancellation token</param>
        public static async Task AnimateAsync(
            Action<double> applyValues,
            TimeSpan duration,
            Easing? easing = null,
            int steps = DefaultSteps,
            CancellationToken ct = default)
        {
            easing ??= new LinearEasing();
            steps = NormalizeSteps(duration, steps);
            var stepDuration = duration.TotalMilliseconds / steps;

            for (int i = 0; i <= steps; i++)
            {
                if (ct.IsCancellationRequested) break;

                var t = (double)i / steps;
                var easedT = easing.Ease(t);

                await Dispatcher.UIThread.InvokeAsync(() => applyValues(easedT));

                if (i < steps)
                    await Task.Delay((int)stepDuration, ct);
            }
        }

        /// <summary>
        /// Linear interpolation between two values.
        /// </summary>
        public static double Lerp(double from, double to, double t)
            => from + (to - from) * t;
    }
}
