using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Flowery.NET.Kanban.Controls.Users;

namespace Flowery.NET.Kanban.Controls
{
    public partial class FlowKanban
    {
        private bool _isEnsuringGlobalAdmin;
        private IUserProvider? _trackedUserProvider;
        private bool _isCurrentUserGlobalAdmin;

        public static readonly StyledProperty<IUserProvider?> UserProviderProperty =
            AvaloniaProperty.Register<FlowKanban, IUserProvider?>(
                                nameof(UserProvider),
                                default!);

        /// <summary>
        /// User provider used for resolving identities and current user context.
        /// </summary>
        public IUserProvider? UserProvider
        {
            get => (IUserProvider?)GetValue(UserProviderProperty);
            set => SetValue(UserProviderProperty, value);
        }

        /// <summary>
        /// Gets the stored global admin user ID, if any.
        /// </summary>
        public string? GlobalAdminId => LoadGlobalAdminId();

        /// <summary>
        /// Default user provider applied when no instance-specific provider is set.
        /// </summary>
        public static IUserProvider? DefaultUserProvider { get; set; }

        private static void OnUserProviderChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                kanban.CancelUserSettingsLoad();
                kanban.AttachUserProvider(e.NewValue as IUserProvider);
                _ = kanban.EnsureGlobalAdminAsync();
                _ = kanban.RefreshAdminStateAsync();
                kanban.StartUserSettingsLoad(forceReload: true);
                _ = kanban.EnsureAssigneeIdsAndFilterOptionsAsync();
            }
        }

        private void ApplyDefaultUserProvider()
        {
            if (UserProvider != null)
                return;

            if (DefaultUserProvider == null)
            {
                DefaultUserProvider = new LocalUserProvider();
            }

            UserProvider = DefaultUserProvider;
        }

        private void AttachUserProvider(IUserProvider? provider)
        {
            if (ReferenceEquals(_trackedUserProvider, provider))
                return;

            DetachUserProvider();

            if (provider == null)
                return;

            _trackedUserProvider = provider;
            _trackedUserProvider.UsersChanged += OnUserProviderUsersChanged;
        }

        private void DetachUserProvider()
        {
            if (_trackedUserProvider == null)
                return;

            _trackedUserProvider.UsersChanged -= OnUserProviderUsersChanged;
            _trackedUserProvider = null;
        }

        private void OnUserProviderUsersChanged()
        {
            _ = EnsureGlobalAdminAsync();
            _ = RefreshAdminStateAsync();
            StartUserSettingsLoad(forceReload: true);
            _ = EnsureAssigneeIdsAndFilterOptionsAsync();
        }

        private async Task EnsureAssigneeIdsAndFilterOptionsAsync()
        {
            var provider = UserProvider;
            if (provider == null)
            {
                ClearInvalidAssigneeIds(Array.Empty<string>());
                ClearAssigneeFilterOptions();
                return;
            }

            var refreshVersion = ++_assigneeFilterRefreshVersion;
            try
            {
                var users = await provider.GetAllUsersAsync();
                if (refreshVersion != _assigneeFilterRefreshVersion || !ReferenceEquals(provider, UserProvider))
                    return;

                var userList = new List<IFlowUser>();
                foreach (var user in users)
                {
                    if (user != null)
                    {
                        userList.Add(user);
                    }
                }

                var validIds = BuildValidAssigneeIds(userList);
                ClearInvalidAssigneeIds(validIds);
                UpdateAssigneeFilterOptionsFromUsers(userList);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FlowKanban assignee validation failed: {ex.GetType().Name} - {ex.Message}");
            }
        }

        private async Task EnsureAssigneeIdsValidAsync()
        {
            var provider = UserProvider;
            if (provider == null)
            {
                ClearInvalidAssigneeIds(Array.Empty<string>());
                return;
            }

            try
            {
                var users = await provider.GetAllUsersAsync();
                if (!ReferenceEquals(provider, UserProvider))
                    return;

                var validIds = BuildValidAssigneeIds(users);
                ClearInvalidAssigneeIds(validIds);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FlowKanban assignee validation failed: {ex.GetType().Name} - {ex.Message}");
            }
        }

        private static HashSet<string> BuildValidAssigneeIds(IEnumerable<IFlowUser> users)
        {
            var validIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var user in users)
            {
                if (user == null || string.IsNullOrWhiteSpace(user.Id))
                    continue;

                validIds.Add(user.Id.Trim());
            }

            return validIds;
        }

        private void ClearInvalidAssigneeIds(IReadOnlyCollection<string> validIds)
        {
            if (Board?.Columns == null)
                return;

            var hasValidIds = validIds.Count > 0;
            foreach (var column in Board.Columns)
            {
                if (column?.Tasks == null)
                    continue;

                foreach (var task in column.Tasks)
                {
                    if (task == null || string.IsNullOrWhiteSpace(task.AssigneeId))
                        continue;

                    if (!hasValidIds || !validIds.Contains(task.AssigneeId))
                    {
                        task.AssigneeId = null;
                    }
                }
            }
        }

        private static string? LoadGlobalAdminId()
        {
            var raw = LoadStateText(GlobalAdminStorageKey);
            if (string.IsNullOrWhiteSpace(raw))
                return null;

            return raw.Trim();
        }

        private static void SaveGlobalAdminId(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId))
                return;

            SaveStateText(GlobalAdminStorageKey, userId.Trim());
        }

        private bool HasGlobalAdmin()
        {
            return !string.IsNullOrWhiteSpace(LoadGlobalAdminId());
        }

        /// <summary>
        /// Ensures a global admin exists by assigning the current user on first run.
        /// </summary>
        public async Task EnsureGlobalAdminAsync(CancellationToken cancellation = default)
        {
            if (_isEnsuringGlobalAdmin || HasGlobalAdmin())
                return;

            var provider = UserProvider;
            if (provider == null)
                return;

            _isEnsuringGlobalAdmin = true;
            try
            {
                if (HasGlobalAdmin())
                    return;

                var currentUser = await provider.GetCurrentUserAsync(cancellation);
                if (currentUser == null || string.IsNullOrWhiteSpace(currentUser.Id))
                    return;

                SaveGlobalAdminId(currentUser.Id);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"FlowKanban global admin bootstrap failed: {ex.GetType().Name} - {ex.Message}");
            }
            finally
            {
                _isEnsuringGlobalAdmin = false;
            }
        }

        private async Task RefreshAdminStateAsync(CancellationToken cancellation = default)
        {
            var isAdmin = await IsCurrentUserGlobalAdminAsync(cancellation);
            if (_isCurrentUserGlobalAdmin == isAdmin)
                return;

            _isCurrentUserGlobalAdmin = isAdmin;
        }

        private async Task<bool> IsCurrentUserGlobalAdminAsync(CancellationToken cancellation = default)
        {
            await EnsureGlobalAdminAsync(cancellation);

            var globalAdminId = LoadGlobalAdminId();
            if (string.IsNullOrWhiteSpace(globalAdminId))
                return false;

            var provider = UserProvider;
            if (provider == null)
                return false;

            var currentUser = await provider.GetCurrentUserAsync(cancellation);
            var currentUserId = ResolveUserId(currentUser);
            if (string.IsNullOrWhiteSpace(currentUserId))
                return false;

            return string.Equals(currentUserId, globalAdminId, StringComparison.Ordinal);
        }
    }
}
