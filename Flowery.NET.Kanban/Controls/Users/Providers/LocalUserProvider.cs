namespace Flowery.NET.Kanban.Controls.Users;

/// <summary>
/// Default local user provider. Ships as the out-of-box experience.
/// Creates a single "Local User" representing the current machine user.
/// </summary>
public class LocalUserProvider : IUserProvider
{
    private const string LocalUserStateKey = "FlowKanban.LocalUserId";
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
    public string ImplementationVersion => "1.0.0";
    public bool SupportsAvatars => false;
    public bool SupportsPresence => false;
    public bool SupportsRealtime => false;

    public event Action? UsersChanged;

    private readonly IStateStorage _stateStorage;
    private readonly List<FlowUser> _users = new();
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
        // Initialize with system user
        InitializeDefaultUser();
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

            var user = new FlowUser(
                id: Guid.NewGuid().ToString(),
                displayName: name,
                providerKey: ProviderKey)
            {
                Email = null
            };

            _users.Add(user);
        }
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
        return Task.FromResult<IEnumerable<IFlowUser>>(_users.Cast<IFlowUser>());
    }

    public Task<IFlowUser?> GetUserByIdAsync(string rawId, CancellationToken cancellation = default)
    {
        var user = _users.FirstOrDefault(u => u.RawId == rawId);
        return Task.FromResult<IFlowUser?>(user);
    }

    public Task<IEnumerable<IFlowUser>> SearchUsersAsync(
        string query,
        int maxResults = 20,
        CancellationToken cancellation = default)
    {
        var results = _users
            .Where(u => u.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                        (u.Email?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(maxResults);

        return Task.FromResult<IEnumerable<IFlowUser>>(results.Cast<IFlowUser>());
    }

    public Task<IFlowUser?> GetCurrentUserAsync(CancellationToken cancellation = default)
    {
        return Task.FromResult<IFlowUser?>(_currentUser);
    }

    public Task RefreshAsync(CancellationToken cancellation = default)
    {
        // Local provider doesn't need refresh
        return Task.CompletedTask;
    }

    /// <summary>
    /// Adds a local user manually.
    /// </summary>
    public FlowUser AddUser(string displayName, string? email = null)
    {
        var user = new FlowUser(
            id: Guid.NewGuid().ToString(),
            displayName: displayName,
            providerKey: ProviderKey)
        {
            Email = email
        };

        _users.Add(user);
        UsersChanged?.Invoke();

        return user;
    }

    /// <summary>
    /// Removes a local user by ID.
    /// </summary>
    public bool RemoveUser(string rawId)
    {
        var user = _users.FirstOrDefault(u => u.RawId == rawId);
        if (user != null && user != _currentUser)
        {
            _users.Remove(user);
            UsersChanged?.Invoke();
            return true;
        }
        return false;
    }
}
