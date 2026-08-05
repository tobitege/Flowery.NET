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
        /// <remarks>Implementations must propagate failures to the caller.</remarks>
        void SaveLines(string key, IEnumerable<string> lines);

        /// <summary>Deletes the value stored under <paramref name="key"/>.</summary>
        /// <remarks>Implementations must propagate failures to the caller.</remarks>
        void Delete(string key);

        /// <summary>Moves a stored value to a new key.</summary>
        /// <remarks>
        /// Implementations must propagate failures to the caller, including a missing source value.
        /// </remarks>
        void Rename(string sourceKey, string targetKey);

        /// <summary>Returns stored keys that start with <paramref name="prefix"/>.</summary>
        IEnumerable<string> GetKeys(string prefix);
    }
}
