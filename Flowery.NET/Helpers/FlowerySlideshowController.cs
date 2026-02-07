using System;
using System.Collections.Generic;
using Avalonia.Threading;
using Flowery.Enums;

namespace Flowery.Helpers
{
    /// <summary>
    /// Reusable slideshow controller that handles automatic navigation with timer.
    /// Supports sequential (Slideshow) and non-repeating random (Random) modes.
    /// </summary>
    /// <param name="navigateAction">Called with the target index when auto-advancing.</param>
    /// <param name="getItemCount">Returns the total number of items.</param>
    /// <param name="getCurrentIndex">Returns the current selected index.</param>
    public sealed class FlowerySlideshowController(
        Action<int> navigateAction,
        Func<int> getItemCount,
        Func<int> getCurrentIndex) : IDisposable
    {
        private readonly Action<int> _navigateAction = navigateAction ?? throw new ArgumentNullException(nameof(navigateAction));
        private readonly Func<int> _getItemCount = getItemCount ?? throw new ArgumentNullException(nameof(getItemCount));
        private readonly Func<int> _getCurrentIndex = getCurrentIndex ?? throw new ArgumentNullException(nameof(getCurrentIndex));

        private DispatcherTimer? _timer;
        private List<int>? _shuffledIndices;
        private int _shufflePosition;
        private readonly Random _random = new();

        private FlowerySlideshowMode _mode = FlowerySlideshowMode.Manual;
        private double _interval = 3.0;

        /// <summary>
        /// Gets or sets the slideshow mode. Changes take effect immediately.
        /// </summary>
        public FlowerySlideshowMode Mode
        {
            get => _mode;
            set
            {
                if (_mode != value)
                {
                    _mode = value;
                    Restart();
                }
            }
        }

        /// <summary>
        /// Gets or sets the interval in seconds between automatic transitions.
        /// </summary>
        public double Interval
        {
            get => _interval;
            set
            {
                double newInterval = Math.Max(0.5, value);
                if (Math.Abs(_interval - newInterval) > 0.001)
                {
                    _interval = newInterval;
                    if (_timer is not null)
                    {
                        _timer.Interval = TimeSpan.FromSeconds(_interval);
                    }
                }
            }
        }

        /// <summary>
        /// Gets whether the controller is currently running.
        /// </summary>
        public bool IsRunning => _timer is not null;

        /// <summary>
        /// Starts or restarts the slideshow timer based on current mode.
        /// </summary>
        public void Start()
        {
            Stop();

            int itemCount = _getItemCount();
            if (_mode == FlowerySlideshowMode.Manual || itemCount <= 1)
            {
                return;
            }

            if (_mode is FlowerySlideshowMode.Random or FlowerySlideshowMode.Kiosk)
            {
                InitializeShuffledOrder();
            }

            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_interval)
            };
            _timer.Tick += OnTimerTick;
            _timer.Start();
        }

        /// <summary>
        /// Stops the slideshow timer.
        /// </summary>
        public void Stop()
        {
            if (_timer is not null)
            {
                _timer.Stop();
                _timer.Tick -= OnTimerTick;
                _timer = null;
            }
        }

        /// <summary>
        /// Restarts the timer (useful after mode or interval changes).
        /// </summary>
        public void Restart()
        {
            if (IsRunning || _mode != FlowerySlideshowMode.Manual)
            {
                Start();
            }
        }

        /// <summary>
        /// Call this when the item count changes to reinitialize shuffle order if needed.
        /// </summary>
        public void OnItemCountChanged()
        {
            if (_mode is FlowerySlideshowMode.Random or FlowerySlideshowMode.Kiosk)
            {
                InitializeShuffledOrder();
            }
        }

        /// <summary>
        /// Gets or sets whether navigation wraps around (last→first, first→last).
        /// </summary>
        public bool WrapAround { get; set; }

        /// <summary>
        /// Navigate to the previous item.
        /// </summary>
        public void Previous()
        {
            if (!CanGoPrevious)
            {
                return;
            }

            int itemCount = _getItemCount();
            if (itemCount == 0)
            {
                return;
            }

            int currentIndex = _getCurrentIndex();
            int newIndex = WrapAround ? currentIndex == 0 ? itemCount - 1 : currentIndex - 1 : Math.Max(0, currentIndex - 1);
            _navigateAction(newIndex);
        }

        /// <summary>
        /// Navigate to the next item.
        /// </summary>
        public void Next()
        {
            if (!CanGoNext)
            {
                return;
            }

            int itemCount = _getItemCount();
            if (itemCount == 0)
            {
                return;
            }

            int currentIndex = _getCurrentIndex();
            int newIndex = WrapAround ? currentIndex == itemCount - 1 ? 0 : currentIndex + 1 : Math.Min(itemCount - 1, currentIndex + 1);
            _navigateAction(newIndex);
        }

        /// <summary>
        /// Navigate to a specific index (clamped to valid range).
        /// </summary>
        public void GoTo(int index)
        {
            int itemCount = _getItemCount();
            if (itemCount == 0)
            {
                return;
            }

            int clampedIndex = index < 0 ? 0 : (index >= itemCount ? itemCount - 1 : index);
            _navigateAction(clampedIndex);
        }

        /// <summary>
        /// Gets whether navigation can go to the previous item.
        /// </summary>
        public bool CanGoPrevious
        {
            get
            {
                int itemCount = _getItemCount();
                return itemCount <= 1 ? false : WrapAround || _getCurrentIndex() > 0;
            }
        }

        /// <summary>
        /// Gets whether navigation can go to the next item.
        /// </summary>
        public bool CanGoNext
        {
            get
            {
                int itemCount = _getItemCount();
                return itemCount <= 1 ? false : WrapAround || _getCurrentIndex() < itemCount - 1;
            }
        }

        private void OnTimerTick(object? sender, EventArgs e)
        {
            int itemCount = _getItemCount();
            if (itemCount == 0)
            {
                return;
            }

            int nextIndex;
            if (_mode == FlowerySlideshowMode.Slideshow)
            {
                // Sequential infinite loop
                nextIndex = (_getCurrentIndex() + 1) % itemCount;
            }
            else // Random or Kiosk - both use shuffled slide order
            {
                nextIndex = GetNextRandomIndex();
            }

            _navigateAction(nextIndex);
        }

        private void InitializeShuffledOrder()
        {
            int itemCount = _getItemCount();
            int currentIndex = _getCurrentIndex();

            // Fisher-Yates shuffle
            _shuffledIndices = new List<int>(itemCount);
            for (int i = 0; i < itemCount; i++)
            {
                _shuffledIndices.Add(i);
            }

            for (int i = _shuffledIndices.Count - 1; i > 0; i--)
            {
                int j = _random.Next(i + 1);
                (_shuffledIndices[i], _shuffledIndices[j]) = (_shuffledIndices[j], _shuffledIndices[i]);
            }

            // Start at position 0, but skip if it's the current index
            _shufflePosition = 0;
            if (_shuffledIndices.Count > 1 && _shuffledIndices[0] == currentIndex)
            {
                _shufflePosition = 1;
            }
        }

        private int GetNextRandomIndex()
        {
            int itemCount = _getItemCount();

            if (_shuffledIndices == null || _shuffledIndices.Count != itemCount)
            {
                InitializeShuffledOrder();
            }

            if (_shuffledIndices == null || _shuffledIndices.Count == 0)
            {
                return 0;
            }

            if (_shufflePosition >= _shuffledIndices.Count)
            {
                InitializeShuffledOrder();
            }

            if (_shuffledIndices == null || _shuffledIndices.Count == 0)
            {
                return 0;
            }

            var nextIndex = _shuffledIndices[_shufflePosition];
            _shufflePosition++;
            return nextIndex;
        }

        public void Dispose()
        {
            Stop();
            _shuffledIndices = null;
        }
    }
}
