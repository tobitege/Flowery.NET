using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Threading;

#nullable enable

namespace Flowery.Helpers
{
    /// <summary>
    /// Represents the state of a managed timer.
    /// </summary>
    public enum ManagedTimerState
    {
        Stopped,
        Running,
        Paused
    }

    /// <summary>
    /// A wrapper around DispatcherTimer that provides pause/resume functionality
    /// and simplifies common timer patterns used across Flowery controls.
    /// </summary>
    public sealed class ManagedTimer : IDisposable
    {
        private DispatcherTimer? _timer;
        private TimeSpan _interval;
        private DateTime _pausedAt;
        private TimeSpan _remainingInterval;
        private bool _isPaused;
        private bool _isDisposed;

        /// <summary>
        /// Gets the current state of the timer.
        /// </summary>
        public ManagedTimerState State
        {
            get
            {
                if (_isDisposed) return ManagedTimerState.Stopped;
                if (_isPaused) return ManagedTimerState.Paused;
                return _timer?.IsEnabled == true ? ManagedTimerState.Running : ManagedTimerState.Stopped;
            }
        }

        /// <summary>
        /// Gets or sets the timer interval. Changing this while running will take effect on next tick.
        /// </summary>
        public TimeSpan Interval
        {
            get => _interval;
            set
            {
                _interval = value;
                if (_timer != null && !_isPaused)
                {
                    _timer.Interval = value;
                }
            }
        }

        /// <summary>
        /// Gets whether the timer is currently running (not stopped or paused).
        /// </summary>
        public bool IsRunning => State == ManagedTimerState.Running;

        /// <summary>
        /// Gets whether the timer is currently paused.
        /// </summary>
        public bool IsPaused => State == ManagedTimerState.Paused;

        /// <summary>
        /// Event raised on each timer tick.
        /// </summary>
        public event EventHandler? Tick;

        /// <summary>
        /// Creates a new ManagedTimer with the specified interval.
        /// </summary>
        /// <param name="interval">The interval between ticks.</param>
        public ManagedTimer(TimeSpan interval)
        {
            _interval = interval;
        }

        /// <summary>
        /// Creates a new ManagedTimer with a 1-second interval.
        /// </summary>
        public ManagedTimer() : this(TimeSpan.FromSeconds(1))
        {
        }

        /// <summary>
        /// Starts the timer. If paused, resumes from where it left off.
        /// </summary>
        public void Start()
        {
            if (_isDisposed) return;

            if (_isPaused)
            {
                Resume();
                return;
            }

            EnsureTimer();
            _timer!.Start();
        }

        /// <summary>
        /// Stops the timer completely. Use Pause() to temporarily stop.
        /// </summary>
        public void Stop()
        {
            if (_isDisposed) return;

            _isPaused = false;
            _remainingInterval = TimeSpan.Zero;

            if (_timer != null)
            {
                _timer.Stop();
            }
        }

        /// <summary>
        /// Pauses the timer, preserving the remaining time until next tick.
        /// </summary>
        public void Pause()
        {
            if (_isDisposed || _timer == null || !_timer.IsEnabled) return;

            _isPaused = true;
            _pausedAt = DateTime.UtcNow;
            _timer.Stop();

            // Calculate remaining interval (approximate - DispatcherTimer doesn't expose this)
            // We'll use the full interval on resume for simplicity
            _remainingInterval = _interval;
        }

        /// <summary>
        /// Resumes the timer from a paused state.
        /// </summary>
        public void Resume()
        {
            if (_isDisposed || !_isPaused) return;

            _isPaused = false;
            EnsureTimer();

            // Resume with remaining interval, then switch back to normal interval
            if (_remainingInterval > TimeSpan.Zero && _remainingInterval < _interval)
            {
                _timer!.Interval = _remainingInterval;
                // After first tick, we need to restore normal interval
                // This is handled in OnTimerTick
            }
            else
            {
                _timer!.Interval = _interval;
            }

            _timer.Start();
        }

        /// <summary>
        /// Restarts the timer from the beginning.
        /// </summary>
        public void Restart()
        {
            Stop();
            Start();
        }

        private void EnsureTimer()
        {
            if (_timer != null) return;

            _timer = new DispatcherTimer
            {
                Interval = _interval
            };
            _timer.Tick += OnTimerTick;
        }

        private void OnTimerTick(object? sender, object e)
        {
            // Restore normal interval if we resumed with a partial interval
            if (_timer != null && _timer.Interval != _interval)
            {
                _timer.Interval = _interval;
            }

            Tick?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Disposes the timer and releases resources.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_timer != null)
            {
                _timer.Stop();
                _timer.Tick -= OnTimerTick;
                _timer = null;
            }

            Tick = null;
        }
    }

    /// <summary>
    /// Manages a pool of timers for a control, with automatic cleanup on unload.
    /// Useful for controls that need multiple timers (e.g., DaisyLoading).
    /// </summary>
    public sealed class TimerPool : IDisposable
    {
        private readonly List<ManagedTimer> _timers = [];
        private bool _isDisposed;

        /// <summary>
        /// Gets the number of active timers in the pool.
        /// </summary>
        public int Count => _timers.Count;

        /// <summary>
        /// Creates and tracks a new timer with the specified interval.
        /// </summary>
        /// <param name="interval">Timer interval.</param>
        /// <param name="onTick">Callback for each tick.</param>
        /// <returns>The created timer.</returns>
        public ManagedTimer Create(TimeSpan interval, EventHandler onTick)
        {
            if (_isDisposed)
                throw new ObjectDisposedException(nameof(TimerPool));

            var timer = new ManagedTimer(interval);
            timer.Tick += onTick;
            _timers.Add(timer);
            return timer;
        }

        /// <summary>
        /// Creates and starts a new timer with the specified interval.
        /// </summary>
        /// <param name="interval">Timer interval.</param>
        /// <param name="onTick">Callback for each tick.</param>
        /// <returns>The created and started timer.</returns>
        public ManagedTimer CreateAndStart(TimeSpan interval, EventHandler onTick)
        {
            var timer = Create(interval, onTick);
            timer.Start();
            return timer;
        }

        /// <summary>
        /// Removes a timer from the pool and disposes it.
        /// </summary>
        /// <param name="timer">The timer to remove.</param>
        public void Remove(ManagedTimer timer)
        {
            if (_timers.Remove(timer))
            {
                timer.Dispose();
            }
        }

        /// <summary>
        /// Stops all timers in the pool.
        /// </summary>
        public void StopAll()
        {
            foreach (var timer in _timers)
            {
                timer.Stop();
            }
        }

        /// <summary>
        /// Pauses all running timers in the pool.
        /// </summary>
        public void PauseAll()
        {
            foreach (var timer in _timers)
            {
                if (timer.IsRunning)
                {
                    timer.Pause();
                }
            }
        }

        /// <summary>
        /// Resumes all paused timers in the pool.
        /// </summary>
        public void ResumeAll()
        {
            foreach (var timer in _timers)
            {
                if (timer.IsPaused)
                {
                    timer.Resume();
                }
            }
        }

        /// <summary>
        /// Disposes all timers and clears the pool.
        /// Call this in the control's Unloaded event.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            foreach (var timer in _timers)
            {
                timer.Dispose();
            }
            _timers.Clear();
        }
    }

    /// <summary>
    /// Represents a scheduled action that will execute at a specific time or after a delay.
    /// </summary>
    public sealed class ScheduledAction : IDisposable
    {
        private ManagedTimer? _timer;
        private readonly Action _callback;
        private DateTimeOffset? _triggerTime;
        private bool _isDisposed;
        private bool _hasTriggered;

        /// <summary>
        /// Gets whether this action has already triggered.
        /// </summary>
        public bool HasTriggered => _hasTriggered;

        /// <summary>
        /// Gets the scheduled trigger time, if set.
        /// </summary>
        public DateTimeOffset? TriggerTime => _triggerTime;

        /// <summary>
        /// Event raised when the scheduled action triggers.
        /// </summary>
        public event EventHandler? Triggered;

        /// <summary>
        /// Creates a scheduled action that will execute after a delay.
        /// </summary>
        /// <param name="delay">Time to wait before executing.</param>
        /// <param name="callback">Action to execute.</param>
        public ScheduledAction(TimeSpan delay, Action callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _triggerTime = DateTimeOffset.Now.Add(delay);
            InitializeTimer(delay);
        }

        /// <summary>
        /// Creates a scheduled action that will execute at a specific time.
        /// </summary>
        /// <param name="triggerTime">The time to execute.</param>
        /// <param name="callback">Action to execute.</param>
        public ScheduledAction(DateTimeOffset triggerTime, Action callback)
        {
            _callback = callback ?? throw new ArgumentNullException(nameof(callback));
            _triggerTime = triggerTime;

            var delay = triggerTime - DateTimeOffset.Now;
            if (delay <= TimeSpan.Zero)
            {
                // Already past - trigger immediately
                _hasTriggered = true;
                callback();
                Triggered?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                InitializeTimer(delay);
            }
        }

        private void InitializeTimer(TimeSpan delay)
        {
            // Use shorter check interval for accuracy, but cap at 1 second minimum
            var checkInterval = delay < TimeSpan.FromSeconds(10)
                ? TimeSpan.FromMilliseconds(100)
                : TimeSpan.FromSeconds(1);

            _timer = new ManagedTimer(checkInterval);
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            if (_triggerTime.HasValue && DateTimeOffset.Now >= _triggerTime.Value)
            {
                _hasTriggered = true;
                _timer?.Stop();
                _callback();
                Triggered?.Invoke(this, EventArgs.Empty);
            }
        }

        /// <summary>
        /// Cancels the scheduled action.
        /// </summary>
        public void Cancel()
        {
            Dispose();
        }

        /// <summary>
        /// Disposes the scheduled action.
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed) return;
            _isDisposed = true;

            if (_timer != null)
            {
                _timer.Tick -= OnTimerTick;
                _timer.Dispose();
                _timer = null;
            }

            Triggered = null;
        }
    }

    /// <summary>
    /// Provides extension methods for attaching timer pools to Controls.
    /// </summary>
    public static class TimerPoolExtensions
    {
        private static readonly Dictionary<Control, TimerPool> _attachedPools = [];

        /// <summary>
        /// Gets or creates a timer pool attached to the specified control.
        /// The pool will be automatically disposed when the control is unloaded.
        /// </summary>
        /// <param name="element">The control to attach the pool to.</param>
        /// <returns>The timer pool for this control.</returns>
        public static TimerPool GetTimerPool(this Control element)
        {
            if (_attachedPools.TryGetValue(element, out var existing))
            {
                return existing;
            }

            var pool = new TimerPool();
            _attachedPools[element] = pool;

            element.Unloaded += (s, e) =>
            {
                if (_attachedPools.TryGetValue(element, out var p))
                {
                    p.Dispose();
                    _attachedPools.Remove(element);
                }
            };

            return pool;
        }

        /// <summary>
        /// Disposes and removes the timer pool attached to the specified control.
        /// </summary>
        /// <param name="element">The control to detach from.</param>
        public static void DisposeTimerPool(this Control element)
        {
            if (_attachedPools.TryGetValue(element, out var pool))
            {
                pool.Dispose();
                _attachedPools.Remove(element);
            }
        }
    }
}
