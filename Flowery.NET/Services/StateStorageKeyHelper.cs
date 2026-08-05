using System;
using System.Text;

namespace Flowery.Services
{
    /// <summary>
    /// Helper for composing scoped state storage keys.
    /// </summary>
    public static class StateStorageKeyHelper
    {
        /// <summary>
        /// Builds a key by appending non-empty segments using "." as the separator.
        /// </summary>
        public static string BuildScopedKey(string baseKey, params string?[] segments)
        {
            if (string.IsNullOrWhiteSpace(baseKey))
                throw new ArgumentException("Base key must be provided.", nameof(baseKey));

            var builder = new StringBuilder(baseKey.Trim());
            if (segments == null || segments.Length == 0)
                return builder.ToString();

            foreach (var segment in segments)
            {
                if (string.IsNullOrWhiteSpace(segment))
                    continue;

                builder.Append('.').Append(segment.Trim());
            }

            return builder.ToString();
        }
    }
}
