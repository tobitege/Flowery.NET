using System;

namespace Flowery.Services
{
    /// <summary>
    /// Provides access to the platform-appropriate state storage.
    /// On Desktop platforms, defaults to FileStateStorage.
    /// For Browser/WASM, call Configure() during app initialization to set a localStorage-based implementation.
    /// </summary>
    public static class StateStorageProvider
    {
        private static IStateStorage? _instance;
        private static readonly object Lock = new object();

        /// <summary>
        /// Gets the current state storage instance.
        /// Returns FileStateStorage by default if not configured.
        /// </summary>
        public static IStateStorage Instance
        {
            get
            {
                if (_instance != null)
                    return _instance;

                lock (Lock)
                {
                    if (_instance == null)
                        _instance = new FileStateStorage();
                }

                return _instance;
            }
        }

        /// <summary>
        /// Configures the state storage implementation.
        /// Call this at app startup before any state operations.
        /// </summary>
        /// <param name="storage">The storage implementation to use</param>
        public static void Configure(IStateStorage storage)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            lock (Lock)
            {
                _instance = storage;
            }
        }

        /// <summary>
        /// Resets the provider to uninitialized state. For testing purposes.
        /// </summary>
        internal static void Reset()
        {
            lock (Lock)
            {
                _instance = null;
            }
        }
    }
}
