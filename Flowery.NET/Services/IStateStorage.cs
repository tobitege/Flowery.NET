using System.Collections.Generic;

namespace Flowery.Services
{
    /// <summary>
    /// Abstraction for persisting key-value state across platforms.
    /// Desktop uses file storage, Browser/WASM uses localStorage.
    /// </summary>
    public interface IStateStorage
    {
        /// <summary>
        /// Loads state lines from persistent storage.
        /// </summary>
        /// <param name="key">Storage key (used as filename on Desktop, localStorage key on Browser)</param>
        /// <returns>Lines of state data, or empty if not found</returns>
        IReadOnlyList<string> LoadLines(string key);

        /// <summary>
        /// Saves state lines to persistent storage.
        /// </summary>
        /// <param name="key">Storage key</param>
        /// <param name="lines">Lines of state data to persist</param>
        void SaveLines(string key, IEnumerable<string> lines);
    }
}
