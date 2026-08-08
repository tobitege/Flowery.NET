namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Default local user provider. Ships as the out-of-box experience.
/// Creates a single "Local User" representing the current machine user.
/// </summary>
internal sealed class LocalUserProvider : IUserProvider
{
    private const string LocalUserStateKey = "FlowKanban.LocalUserId";
    private const string LocalUsersStateKey = "FlowKanban.LocalUsers.v1";
    private const string DemoUserIdPrefix = "demo-";
    private static readonly string[] DemoUserNames =
    [
        "Sam",
        "Dario",
        "Max",
        "Demis",
        "Adam",
        "Lucy",
        "Anita",
        "Sue",
        "Eric",
        "Forrest"
    ];

    public string ProviderKey => "local";
    public string DisplayName => "Local Users";
    public string ImplementationVersion => "1.1.0";
    public bool SupportsAvatars => false;
    public bool SupportsPresence => false;
    public bool SupportsRealtime => false;

    public event Action? UsersChanged;

    private readonly IStateStorage _stateStorage;
    private readonly List<FlowUser> _users = new();
    private readonly HashSet<string> _removedDemoUserIds = new(StringComparer.Ordinal);
    private FlowUser? _currentUser;

    public LocalUserProvider()
        : this(StateStorageProvider.Instance, includeDemoUsers: false)
    {
    }

    public LocalUserProvider(bool includeDemoUsers)
        : this(StateStorageProvider.Instance, includeDemoUsers)
    {
    }

    public LocalUserProvider(IStateStorage stateStorage, bool includeDemoUsers = false)
    {
        _stateStorage = stateStorage ?? throw new ArgumentNullException(nameof(stateStorage));
        InitializeDefaultUser();
        LoadPersistedUsers();
        if (includeDemoUsers)
        {
            InitializeDemoUsers();
        }
    }

    private void InitializeDefaultUser()
    {
        var userName = Environment.UserName;
        var machineName = Environment.MachineName;
        var rawId = LoadOrCreateUserId();

        _currentUser = new FlowUser(
            id: rawId,
            displayName: userName,
            providerKey: ProviderKey)
        {
            Email = null,
            Department = machineName
    };

        _users.Add(_currentUser);
    }

    private void InitializeDemoUsers()
    {
        foreach (var name in DemoUserNames)
        {
            if (string.IsNullOrWhiteSpace(name))
                continue;

            var rawId = GetDemoUserId(name);
            if (_removedDemoUserIds.Contains(rawId) || _users.Any(user => user.RawId == rawId))
                continue;

            var user = new FlowUser(
                id: rawId,
                displayName: name,
                providerKey: ProviderKey)
            {
                Email = null
            };

            _users.Add(user);
        }
    }

    private static string GetDemoUserId(string displayName)
    {
        return DemoUserIdPrefix + displayName.Trim().ToLowerInvariant();
    }

    private static bool IsDemoUser(FlowUser user)
    {
        return user.RawId.StartsWith(DemoUserIdPrefix, StringComparison.Ordinal);
    }

    private void LoadPersistedUsers()
    {
        var stored = _stateStorage.LoadLines(LocalUsersStateKey);
        if (stored.Count == 0)
            return;

        var json = string.Join(string.Empty, stored);
        if (string.IsNullOrWhiteSpace(json))
            return;

        LocalUserDirectoryState? state;
        try
        {
            state = System.Text.Json.JsonSerializer.Deserialize(
                json,
                LocalUserProviderJsonContext.Default.LocalUserDirectoryState);
        }
        catch (System.Text.Json.JsonException ex)
        {
            throw new System.IO.InvalidDataException("The persisted local user directory is invalid.", ex);
        }

        if (state == null)
            throw new System.IO.InvalidDataException("The persisted local user directory is empty.");

        foreach (var rawId in state.RemovedDemoUserIds ?? [])
        {
            if (!string.IsNullOrWhiteSpace(rawId))
            {
                _removedDemoUserIds.Add(rawId.Trim());
            }
        }

        foreach (var record in state.Users ?? [])
        {
            var rawId = record.RawId?.Trim();
            var displayName = record.DisplayName?.Trim();
            if (string.IsNullOrWhiteSpace(rawId) || string.IsNullOrWhiteSpace(displayName))
                throw new System.IO.InvalidDataException("The persisted local user directory contains an invalid user.");

            if (_users.Any(user => string.Equals(user.RawId, rawId, StringComparison.Ordinal)))
                throw new System.IO.InvalidDataException($"The persisted local user directory contains duplicate ID '{rawId}'.");

            _users.Add(new FlowUser(rawId, displayName, ProviderKey)
            {
                Email = string.IsNullOrWhiteSpace(record.Email) ? null : record.Email.Trim()
            });
        }
    }

    private void SavePersistedUsers()
    {
        var state = new LocalUserDirectoryState
        {
            Users = _users
                .Where(user => !ReferenceEquals(user, _currentUser) && !IsDemoUser(user))
                .Select(user => new LocalUserRecord
                {
                    RawId = user.RawId,
                    DisplayName = user.DisplayName,
                    Email = user.Email
                })
                .ToList(),
            RemovedDemoUserIds = _removedDemoUserIds
                .OrderBy(rawId => rawId, StringComparer.Ordinal)
                .ToList()
        };
        var json = System.Text.Json.JsonSerializer.Serialize(
            state,
            LocalUserProviderJsonContext.Default.LocalUserDirectoryState);
        _stateStorage.SaveLines(LocalUsersStateKey, [json]);
    }

    private string LoadOrCreateUserId()
    {
        var stored = _stateStorage.LoadLines(LocalUserStateKey);
        if (stored.Count > 0 && !string.IsNullOrWhiteSpace(stored[0]))
        {
            return stored[0].Trim();
        }

        var newId = Guid.NewGuid().ToString();
        _stateStorage.SaveLines(LocalUserStateKey, new[] { newId });
        return newId;
    }

    public Task<IEnumerable<IFlowUser>> GetAllUsersAsync(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        return Task.FromResult<IEnumerable<IFlowUser>>(_users.Cast<IFlowUser>().ToArray());
    }

    public Task<IFlowUser?> GetUserByIdAsync(string rawId, CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var user = _users.FirstOrDefault(u => u.RawId == rawId);
        return Task.FromResult<IFlowUser?>(user);
    }

    public Task<IEnumerable<IFlowUser>> SearchUsersAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        var results = _users
            .Where(u => u.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (u.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(maxResults)
            .Cast<IFlowUser>()
            .ToArray();

        return Task.FromResult<IEnumerable<IFlowUser>>(results);
    }

    public Task<IFlowUser?> GetCurrentUserAsync(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        return Task.FromResult<IFlowUser?>(_currentUser);
    }

    public Task RefreshAsync(CancellationToken cancellation = default)
    {
        cancellation.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a local user manually.
    /// </summary>
    public FlowUser AddUser(string displayName, string? email = null)
    {
        var normalizedDisplayName = displayName?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedDisplayName))
            throw new ArgumentException("Display name must be provided.", nameof(displayName));

        if (_users.Any(user => string.Equals(user.DisplayName, normalizedDisplayName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A local user named '{normalizedDisplayName}' already exists.");

        var normalizedEmail = NormalizeEmail(email);
        var user = new FlowUser(
            id: Guid.NewGuid().ToString(),
            displayName: normalizedDisplayName,
            providerKey: ProviderKey)
        {
            Email = normalizedEmail
        };

        _users.Add(user);
        try
        {
            SavePersistedUsers();
        }
        catch
        {
            _users.Remove(user);
            throw;
        }

        UsersChanged?.Invoke();

        return user;
    }

    private static string? NormalizeEmail(string? email)
    {
        var normalized = email?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (!System.Net.Mail.MailAddress.TryCreate(normalized, out var parsed) ||
            !string.Equals(parsed.Address, normalized, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Email address is invalid.", nameof(email));
        }

        return parsed.Address;
    }

    /// <summary>
    /// Removes a local user by ID.
    /// </summary>
    public bool RemoveUser(string rawId)
    {
        var index = _users.FindIndex(user => string.Equals(user.RawId, rawId, StringComparison.Ordinal));
        if (index < 0 || ReferenceEquals(_users[index], _currentUser))
            return false;

        var user = _users[index];
        var isDemoUser = IsDemoUser(user);
        _users.RemoveAt(index);
        if (isDemoUser)
        {
            _removedDemoUserIds.Add(user.RawId);
        }

        try
        {
            SavePersistedUsers();
        }
        catch
        {
            _users.Insert(index, user);
            if (isDemoUser)
            {
                _removedDemoUserIds.Remove(user.RawId);
            }
            throw;
        }

        UsersChanged?.Invoke();
        return true;
    }
}

internal sealed class LocalUserDirectoryState
{
    public List<LocalUserRecord> Users { get; set; } = [];
    public List<string> RemovedDemoUserIds { get; set; } = [];
}

internal sealed class LocalUserRecord
{
    public string RawId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
}

[System.Text.Json.Serialization.JsonSourceGenerationOptions(
    GenerationMode = System.Text.Json.Serialization.JsonSourceGenerationMode.Metadata)]
[System.Text.Json.Serialization.JsonSerializable(typeof(LocalUserDirectoryState))]
internal partial class LocalUserProviderJsonContext : System.Text.Json.Serialization.JsonSerializerContext
{
}
