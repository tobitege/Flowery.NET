using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Default implementation of ICompositeUserProvider.
/// Orchestrates multiple user providers with automatic ID prefixing.
/// </summary>
public class CompositeUserProvider : ICompositeUserProvider
{
    public const char IdDelimiter = ':';

    public string ProviderKey => "composite";
    public string DisplayName => "All Users";
    public string ImplementationVersion => "1.0.0";

    public bool SupportsAvatars => _providers.Values.Any(p => p.SupportsAvatars);
    public bool SupportsPresence => _providers.Values.Any(p => p.SupportsPresence);
    public bool SupportsRealtime => _providers.Values.Any(p => p.SupportsRealtime);

        public event Action? UsersChanged;

        private readonly Dictionary<string, IUserProvider> _providers = new();
        private readonly Dictionary<string, Action> _providerHandlers = new();

        public string DefaultProviderKey { get; set; } = "local";

    public bool IsSingleProviderMode => _providers.Count <= 1;

    public IReadOnlyList<string> RegisteredProviderKeys => _providers.Keys.ToList();

    public CompositeUserProvider()
    {
    }

    public void RegisterProvider(IUserProvider provider)
    {
        if (string.IsNullOrEmpty(provider.ProviderKey))
            throw new ArgumentException("Provider must have a ProviderKey");

        if (_providers.TryGetValue(provider.ProviderKey, out var existing))
        {
            if (_providerHandlers.TryGetValue(provider.ProviderKey, out var handler))
            {
                existing.UsersChanged -= handler;
                _providerHandlers.Remove(provider.ProviderKey);
            }
        }

        _providers[provider.ProviderKey] = provider;

        // Subscribe to provider changes
        Action handlerAction = () => UsersChanged?.Invoke();
        provider.UsersChanged += handlerAction;
        _providerHandlers[provider.ProviderKey] = handlerAction;

        // Update default if this is the first provider
        if (_providers.Count == 1)
        {
            DefaultProviderKey = provider.ProviderKey;
        }
        else if (string.IsNullOrWhiteSpace(DefaultProviderKey) || !_providers.ContainsKey(DefaultProviderKey))
        {
            DefaultProviderKey = provider.ProviderKey;
        }

        UsersChanged?.Invoke();
    }

    public bool UnregisterProvider(string providerKey)
    {
        if (_providers.TryGetValue(providerKey, out var provider))
        {
            if (_providerHandlers.TryGetValue(providerKey, out var handler))
            {
                provider.UsersChanged -= handler;
                _providerHandlers.Remove(providerKey);
            }

            _providers.Remove(providerKey);

            if (string.Equals(DefaultProviderKey, providerKey, StringComparison.Ordinal))
            {
                DefaultProviderKey = _providers.Keys.FirstOrDefault() ?? string.Empty;
            }

            UsersChanged?.Invoke();
            return true;
        }
        return false;
    }

    public IUserProvider? GetProviderByKey(string providerKey)
    {
        return _providers.TryGetValue(providerKey, out var provider) ? provider : null;
    }

    public string ComposeId(string providerKey, string rawId)
    {
        if (IsSingleProviderMode)
        {
            // No prefix in single-provider mode
            return rawId;
        }
        return $"{providerKey}{IdDelimiter}{rawId}";
    }

    public (string ProviderKey, string RawId) ParseId(string compositeId)
    {
        if (string.IsNullOrEmpty(compositeId))
            return (DefaultProviderKey, compositeId);

        var delimiterIndex = compositeId.IndexOf(IdDelimiter);

        if (delimiterIndex <= 0)
        {
            // No prefix found — use default provider
            return (DefaultProviderKey, compositeId);
        }

        var providerKey = compositeId[..delimiterIndex];
        var rawId = compositeId[(delimiterIndex + 1)..];

        // Validate provider exists
        if (!_providers.ContainsKey(providerKey))
        {
            // Unknown provider — treat as unprefixed
            return (DefaultProviderKey, compositeId);
        }

        return (providerKey, rawId);
    }

    public async Task<IFlowUser?> GetUserByCompositeIdAsync(
        string compositeId,
        CancellationToken cancellation = default)
    {
        var (providerKey, rawId) = ParseId(compositeId);

        if (_providers.TryGetValue(providerKey, out var provider))
        {
            return await provider.GetUserByIdAsync(rawId, cancellation);
        }

        return null;
    }

    public async Task<IEnumerable<IFlowUser>> GetAllUsersAsync(CancellationToken cancellation = default)
    {
        var allUsers = new List<IFlowUser>();

        foreach (var provider in _providers.Values)
        {
            var users = await provider.GetAllUsersAsync(cancellation);
            allUsers.AddRange(users);
        }

        return allUsers;
    }

    public async Task<IFlowUser?> GetUserByIdAsync(string rawId, CancellationToken cancellation = default)
    {
        // In composite mode, GetUserByIdAsync expects a composite ID
        return await GetUserByCompositeIdAsync(rawId, cancellation);
    }

    public async Task<IEnumerable<IFlowUser>> SearchUsersAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellation = default)
    {
        var allResults = new List<IFlowUser>();
        var perProviderMax = Math.Max(5, maxResults / Math.Max(1, _providers.Count));

        foreach (var provider in _providers.Values)
        {
            var results = await provider.SearchUsersAsync(query, perProviderMax, cancellation);
            allResults.AddRange(results);
        }

        return allResults.Take(maxResults);
    }

    public async Task<IReadOnlyDictionary<string, IEnumerable<IFlowUser>>> SearchAllProvidersAsync(
        string query,
        int maxResultsPerProvider = 10,
        CancellationToken cancellation = default)
    {
        var results = new Dictionary<string, IEnumerable<IFlowUser>>();

        foreach (var entry in _providers)
        {
            var users = await entry.Value.SearchUsersAsync(query, maxResultsPerProvider, cancellation);
            results[entry.Key] = users;
        }

        return results;
    }

    public async Task<IFlowUser?> GetCurrentUserAsync(CancellationToken cancellation = default)
    {
        // Try default provider first
        if (_providers.TryGetValue(DefaultProviderKey, out var defaultProvider))
        {
            var user = await defaultProvider.GetCurrentUserAsync(cancellation);
            if (user != null) return user;
        }

        // Fall back to first provider that returns a current user
        foreach (var provider in _providers.Values)
        {
            var user = await provider.GetCurrentUserAsync(cancellation);
            if (user != null) return user;
        }

        return null;
    }

    public async Task RefreshAsync(CancellationToken cancellation = default)
    {
        foreach (var provider in _providers.Values)
        {
            await provider.RefreshAsync(cancellation);
        }
    }
}
