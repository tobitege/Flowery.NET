using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Optional caching layer for user data.
/// Interface version: 1.0
/// </summary>
public interface IUserCacheService
{
    /// <summary>
    /// Cache TTL in seconds.
    /// </summary>
    int CacheTtlSeconds { get; set; }

    /// <summary>
    /// Maximum entries in cache.
    /// </summary>
    int MaxCacheSize { get; set; }

    /// <summary>
    /// Gets a cached user or fetches from provider.
    /// </summary>
    Task<IFlowUser?> GetOrFetchUserAsync(
        string compositeId,
        Func<Task<IFlowUser?>> fetchFunc,
        CancellationToken cancellation = default);

    /// <summary>
    /// Invalidates a specific user's cache entry.
    /// </summary>
    void Invalidate(string compositeId);

    /// <summary>
    /// Clears entire cache.
    /// </summary>
    void Clear();

    /// <summary>
    /// Preloads users into cache.
    /// </summary>
    Task PreloadAsync(IEnumerable<IFlowUser> users);
}
