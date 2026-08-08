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
internal sealed class CompositeUserProvider : ICompositeUserProvider
{
    public const char IdDelimiter = ':';

    private readonly object _syncRoot = new();
    private readonly Dictionary<string, IUserProvider> _providers = new(StringComparer.Ordinal);
    private IReadOnlyList<CompositeUserProviderErrorEventArgs> _lastProviderErrors =
        Array.Empty<CompositeUserProviderErrorEventArgs>();
    private string _defaultProviderKey = "local";

    public string ProviderKey => "composite";
    public string DisplayName => "All Users";
    public string ImplementationVersion => "2.0.0";

    public bool SupportsAvatars => GetProviderSnapshot().Any(entry => entry.Value.SupportsAvatars);
    public bool SupportsPresence => GetProviderSnapshot().Any(entry => entry.Value.SupportsPresence);
    public bool SupportsRealtime => GetProviderSnapshot().Any(entry => entry.Value.SupportsRealtime);

    public event Action? UsersChanged;

    /// <summary>
    /// Raised for each child-provider failure that was isolated from a composite operation.
    /// </summary>
    public event EventHandler<CompositeUserProviderErrorEventArgs>? ProviderFailed;

    /// <summary>
    /// Gets the isolated failures from the most recently completed composite operation.
    /// </summary>
    public IReadOnlyList<CompositeUserProviderErrorEventArgs> LastProviderErrors
    {
        get
        {
            lock (_syncRoot)
            {
                return _lastProviderErrors;
            }
        }
    }

    public string DefaultProviderKey
    {
        get
        {
            lock (_syncRoot)
            {
                return _defaultProviderKey;
            }
        }
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            lock (_syncRoot)
            {
                _defaultProviderKey = value.Trim();
            }
        }
    }

    public bool IsSingleProviderMode
    {
        get
        {
            lock (_syncRoot)
            {
                return _providers.Count <= 1;
            }
        }
    }

    public IReadOnlyList<string> RegisteredProviderKeys =>
        GetProviderSnapshot().Select(entry => entry.Key).ToArray();

    public CompositeUserProvider()
    {
    }

    public void RegisterProvider(IUserProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        _ = FlowUserIdHelper.Compose(provider.ProviderKey, "validation");
        provider.UsersChanged += OnRegisteredProviderUsersChanged;

        IUserProvider? existing;
        lock (_syncRoot)
        {
            _providers.TryGetValue(provider.ProviderKey, out existing);
            _providers[provider.ProviderKey] = provider;

            if (_providers.Count == 1
                || string.IsNullOrWhiteSpace(_defaultProviderKey)
                || !_providers.ContainsKey(_defaultProviderKey))
            {
                _defaultProviderKey = provider.ProviderKey;
            }
        }

        if (existing != null)
        {
            existing.UsersChanged -= OnRegisteredProviderUsersChanged;
        }

        UsersChanged?.Invoke();
    }

    public bool UnregisterProvider(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            return false;

        IUserProvider? provider;
        lock (_syncRoot)
        {
            if (!_providers.Remove(providerKey, out provider))
                return false;

            if (string.Equals(_defaultProviderKey, providerKey, StringComparison.Ordinal))
            {
                _defaultProviderKey = _providers.Keys.FirstOrDefault() ?? string.Empty;
            }
        }

        provider.UsersChanged -= OnRegisteredProviderUsersChanged;
        UsersChanged?.Invoke();
        return true;
    }

    public IUserProvider? GetProviderByKey(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
            return null;

        lock (_syncRoot)
        {
            return _providers.TryGetValue(providerKey, out var provider) ? provider : null;
        }
    }

    private void OnRegisteredProviderUsersChanged()
    {
        UsersChanged?.Invoke();
    }

    private KeyValuePair<string, IUserProvider>[] GetProviderSnapshot()
    {
        lock (_syncRoot)
        {
            return _providers.ToArray();
        }
    }

    private void PublishProviderErrors(IReadOnlyList<CompositeUserProviderErrorEventArgs> errors)
    {
        var errorSnapshot = errors.ToArray();
        lock (_syncRoot)
        {
            _lastProviderErrors = errorSnapshot;
        }

        foreach (var error in errorSnapshot)
        {
            ProviderFailed?.Invoke(this, error);
        }
    }

    public string ComposeId(string providerKey, string rawId)
    {
        return FlowUserIdHelper.Compose(providerKey, rawId);
    }

    public (string ProviderKey, string RawId) ParseId(string compositeId)
    {
        return FlowUserIdHelper.Parse(compositeId);
    }

    public async Task<IFlowUser?> GetUserByCompositeIdAsync(
        string compositeId,
        CancellationToken cancellation = default)
    {
        var (providerKey, rawId) = ParseId(compositeId);

        var provider = GetProviderByKey(providerKey);
        if (provider != null)
        {
            return await provider.GetUserByIdAsync(rawId, cancellation);
        }

        return null;
    }

    private readonly record struct ProviderOperationResult<T>(
        string ProviderKey,
        T? Value,
        Exception? Error);

    private static async Task<ProviderOperationResult<IReadOnlyList<IFlowUser>>>
        GetAllUsersFromProviderAsync(
            KeyValuePair<string, IUserProvider> entry,
            CancellationToken cancellation)
    {
        try
        {
            var users = await entry.Value.GetAllUsersAsync(cancellation);
            return new ProviderOperationResult<IReadOnlyList<IFlowUser>>(
                entry.Key,
                users?.ToArray() ?? Array.Empty<IFlowUser>(),
                Error: null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ProviderOperationResult<IReadOnlyList<IFlowUser>>(
                entry.Key,
                Value: null,
                ex);
        }
    }

    public async Task<IEnumerable<IFlowUser>> GetAllUsersAsync(CancellationToken cancellation = default)
    {
        var providers = GetProviderSnapshot();
        var operations = new Task<ProviderOperationResult<IReadOnlyList<IFlowUser>>>[providers.Length];
        for (var index = 0; index < providers.Length; index++)
        {
            operations[index] = GetAllUsersFromProviderAsync(providers[index], cancellation);
        }

        var providerResults = await Task.WhenAll(operations);
        var allUsers = new List<IFlowUser>();
        var errors = new List<CompositeUserProviderErrorEventArgs>();
        foreach (var result in providerResults)
        {
            if (result.Error != null)
            {
                errors.Add(new CompositeUserProviderErrorEventArgs(
                    result.ProviderKey,
                    CompositeUserProviderOperation.GetAllUsers,
                    result.Error));
                continue;
            }

            if (result.Value != null)
            {
                allUsers.AddRange(result.Value);
            }
        }

        PublishProviderErrors(errors);
        return allUsers;
    }

    public async Task<IFlowUser?> GetUserByIdAsync(string rawId, CancellationToken cancellation = default)
    {
        // In composite mode, GetUserByIdAsync expects a composite ID
        return await GetUserByCompositeIdAsync(rawId, cancellation);
    }

    private static async Task<ProviderOperationResult<IReadOnlyList<IFlowUser>>>
        SearchProviderAsync(
            KeyValuePair<string, IUserProvider> entry,
            string query,
            int maxResults,
            CancellationToken cancellation)
    {
        try
        {
            var users = await entry.Value.SearchUsersAsync(query, maxResults, cancellation);
            return new ProviderOperationResult<IReadOnlyList<IFlowUser>>(
                entry.Key,
                users?.Take(maxResults).ToArray() ?? Array.Empty<IFlowUser>(),
                Error: null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ProviderOperationResult<IReadOnlyList<IFlowUser>>(
                entry.Key,
                Value: null,
                Error: ex);
        }
    }

    public async Task<IEnumerable<IFlowUser>> SearchUsersAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellation = default)
    {
        if (maxResults <= 0)
        {
            PublishProviderErrors(Array.Empty<CompositeUserProviderErrorEventArgs>());
            return Array.Empty<IFlowUser>();
        }

        var providers = GetProviderSnapshot();
        var perProviderMax = Math.Max(5, maxResults / Math.Max(1, providers.Length));
        var operations = new Task<ProviderOperationResult<IReadOnlyList<IFlowUser>>>[providers.Length];
        for (var index = 0; index < providers.Length; index++)
        {
            operations[index] = SearchProviderAsync(
                providers[index],
                query,
                perProviderMax,
                cancellation);
        }

        var providerResults = await Task.WhenAll(operations);
        var allResults = new List<IFlowUser>();
        var errors = new List<CompositeUserProviderErrorEventArgs>();
        foreach (var result in providerResults)
        {
            if (result.Error != null)
            {
                errors.Add(new CompositeUserProviderErrorEventArgs(
                    result.ProviderKey,
                    CompositeUserProviderOperation.SearchUsers,
                    result.Error));
                continue;
            }

            if (result.Value != null)
            {
                allResults.AddRange(result.Value);
            }
        }

        PublishProviderErrors(errors);
        return allResults.Take(maxResults).ToArray();
    }

    public async Task<IReadOnlyDictionary<string, IEnumerable<IFlowUser>>> SearchAllProvidersAsync(
        string query,
        int maxResultsPerProvider = 10,
        CancellationToken cancellation = default)
    {
        if (maxResultsPerProvider <= 0)
        {
            PublishProviderErrors(Array.Empty<CompositeUserProviderErrorEventArgs>());
            return new Dictionary<string, IEnumerable<IFlowUser>>(StringComparer.Ordinal);
        }

        var providers = GetProviderSnapshot();
        var operations = new Task<ProviderOperationResult<IReadOnlyList<IFlowUser>>>[providers.Length];
        for (var index = 0; index < providers.Length; index++)
        {
            operations[index] = SearchProviderAsync(
                providers[index],
                query,
                maxResultsPerProvider,
                cancellation);
        }

        var providerResults = await Task.WhenAll(operations);
        var results = new Dictionary<string, IEnumerable<IFlowUser>>(StringComparer.Ordinal);
        var errors = new List<CompositeUserProviderErrorEventArgs>();
        foreach (var result in providerResults)
        {
            if (result.Error != null)
            {
                errors.Add(new CompositeUserProviderErrorEventArgs(
                    result.ProviderKey,
                    CompositeUserProviderOperation.SearchAllProviders,
                    result.Error));
                continue;
            }

            results[result.ProviderKey] = result.Value ?? Array.Empty<IFlowUser>();
        }

        PublishProviderErrors(errors);
        return results;
    }

    private static async Task<ProviderOperationResult<IFlowUser>> GetCurrentUserFromProviderAsync(
        KeyValuePair<string, IUserProvider> entry,
        CancellationToken cancellation)
    {
        try
        {
            return new ProviderOperationResult<IFlowUser>(
                entry.Key,
                await entry.Value.GetCurrentUserAsync(cancellation),
                Error: null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ProviderOperationResult<IFlowUser>(
                entry.Key,
                Value: null,
                Error: ex);
        }
    }

    public async Task<IFlowUser?> GetCurrentUserAsync(CancellationToken cancellation = default)
    {
        var providers = GetProviderSnapshot();
        var defaultProviderKey = DefaultProviderKey;
        var orderedProviders = new List<KeyValuePair<string, IUserProvider>>(providers.Length);
        foreach (var entry in providers)
        {
            if (string.Equals(entry.Key, defaultProviderKey, StringComparison.Ordinal))
            {
                orderedProviders.Add(entry);
                break;
            }
        }

        foreach (var entry in providers)
        {
            if (!string.Equals(entry.Key, defaultProviderKey, StringComparison.Ordinal))
            {
                orderedProviders.Add(entry);
            }
        }

        var errors = new List<CompositeUserProviderErrorEventArgs>();
        foreach (var entry in orderedProviders)
        {
            var result = await GetCurrentUserFromProviderAsync(entry, cancellation);
            if (result.Error != null)
            {
                errors.Add(new CompositeUserProviderErrorEventArgs(
                    result.ProviderKey,
                    CompositeUserProviderOperation.GetCurrentUser,
                    result.Error));
                continue;
            }

            if (result.Value != null)
            {
                PublishProviderErrors(errors);
                return result.Value;
            }
        }

        PublishProviderErrors(errors);
        return null;
    }

    private static async Task<ProviderOperationResult<bool>> RefreshProviderAsync(
        KeyValuePair<string, IUserProvider> entry,
        CancellationToken cancellation)
    {
        try
        {
            await entry.Value.RefreshAsync(cancellation);
            return new ProviderOperationResult<bool>(entry.Key, Value: true, Error: null);
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new ProviderOperationResult<bool>(entry.Key, Value: false, Error: ex);
        }
    }

    public async Task RefreshAsync(CancellationToken cancellation = default)
    {
        var providers = GetProviderSnapshot();
        var operations = new Task<ProviderOperationResult<bool>>[providers.Length];
        for (var index = 0; index < providers.Length; index++)
        {
            operations[index] = RefreshProviderAsync(providers[index], cancellation);
        }

        var providerResults = await Task.WhenAll(operations);
        var errors = new List<CompositeUserProviderErrorEventArgs>();
        foreach (var result in providerResults)
        {
            if (result.Error != null)
            {
                errors.Add(new CompositeUserProviderErrorEventArgs(
                    result.ProviderKey,
                    CompositeUserProviderOperation.Refresh,
                    result.Error));
            }
        }

        PublishProviderErrors(errors);
    }
}

/// <summary>
/// Identifies a composite operation that isolated a child-provider failure.
/// </summary>
internal enum CompositeUserProviderOperation
{
    GetAllUsers,
    SearchUsers,
    SearchAllProviders,
    GetCurrentUser,
    Refresh
}

/// <summary>
/// Describes a child-provider failure isolated by <see cref="CompositeUserProvider"/>.
/// </summary>
internal sealed class CompositeUserProviderErrorEventArgs : EventArgs
{
    public CompositeUserProviderErrorEventArgs(
        string providerKey,
        CompositeUserProviderOperation operation,
        Exception exception)
    {
        ProviderKey = string.IsNullOrWhiteSpace(providerKey)
            ? throw new ArgumentException("Provider key must be provided.", nameof(providerKey))
            : providerKey;
        Operation = operation;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
    }

    public string ProviderKey { get; }
    public CompositeUserProviderOperation Operation { get; }
    public Exception Exception { get; }
}
