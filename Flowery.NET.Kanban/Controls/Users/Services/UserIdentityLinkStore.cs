using System.Text.Json;
using System.Text.Json.Serialization;
using Flowery.Services;

namespace Flowery.NET.Kanban.Controls.Users;

internal sealed class UserIdentityLinkStore
{
    private const string StorageKey = "FlowKanban.IdentityLinks";
    private const string LocalProviderKey = "local";
    private readonly IStateStorage _stateStorage;
    private readonly List<UserIdentityLink> _links = new();

    public UserIdentityLinkStore(IStateStorage stateStorage)
    {
        _stateStorage = stateStorage ?? throw new ArgumentNullException(nameof(stateStorage));
        Load();
    }

    public Exception? LoadError { get; private set; }

    public void Reload()
    {
        Load();
    }

    public string ResolveLinkedUserId(IFlowUser user)
    {
        var canonicalId = FlowUserIdHelper.Resolve(user);
        if (string.Equals(user.ProviderKey, LocalProviderKey, StringComparison.Ordinal))
            return canonicalId;

        var link = FindLink(user.ProviderKey, user.RawId);
        return link == null
            ? canonicalId
            : FlowUserIdHelper.Compose(LocalProviderKey, link.LocalUserId);
    }

    public UserIdentityLink? FindLink(string providerKey, string subject)
    {
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        var normalizedProviderKey = providerKey.Trim();
        var normalizedSubject = subject.Trim();
        return _links.FirstOrDefault(link =>
            string.Equals(link.ProviderKey, normalizedProviderKey, StringComparison.Ordinal) &&
            string.Equals(link.Subject, normalizedSubject, StringComparison.Ordinal));
    }

    public void SetLink(string providerKey, string subject, string localUserId, string localDisplayName)
    {
        if (string.IsNullOrWhiteSpace(providerKey) ||
            string.IsNullOrWhiteSpace(subject) ||
            string.IsNullOrWhiteSpace(localUserId))
        {
            throw new ArgumentException("Provider key, subject, and local user ID must be provided.");
        }

        var normalizedProviderKey = providerKey.Trim();
        var normalizedSubject = subject.Trim();
        var normalizedLocalUserId = localUserId.Trim();
        var normalizedDisplayName = localDisplayName?.Trim() ?? string.Empty;
        MutateAndSave(() =>
        {
            var existing = FindLink(normalizedProviderKey, normalizedSubject);
            if (existing != null)
            {
                existing.LocalUserId = normalizedLocalUserId;
                existing.LocalDisplayName = normalizedDisplayName;
                return;
            }

            _links.Add(new UserIdentityLink
            {
                ProviderKey = normalizedProviderKey,
                Subject = normalizedSubject,
                LocalUserId = normalizedLocalUserId,
                LocalDisplayName = normalizedDisplayName
            });
        });
    }

    public bool RemoveLink(string providerKey, string subject)
    {
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(subject))
        {
            return false;
        }

        var existing = FindLink(providerKey, subject);
        if (existing == null)
        {
            return false;
        }

        MutateAndSave(() => _links.Remove(existing));
        return true;
    }

    public bool RemoveLinksForProvider(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return false;
        }

        var normalizedProviderKey = providerKey.Trim();
        var removed = _links.Count(link =>
            string.Equals(link.ProviderKey, normalizedProviderKey, StringComparison.Ordinal));

        if (removed <= 0)
        {
            return false;
        }

        MutateAndSave(() => _links.RemoveAll(link =>
            string.Equals(link.ProviderKey, normalizedProviderKey, StringComparison.Ordinal)));
        return true;
    }

    private void Load()
    {
        _links.Clear();
        LoadError = null;

        try
        {
            var lines = _stateStorage.LoadLines(StorageKey);
            if (lines.Count == 0)
                return;

            var json = string.Join(string.Empty, lines);
            if (string.IsNullOrWhiteSpace(json))
                return;

            var items = JsonSerializer.Deserialize(json, UserIdentityLinkJsonContext.Default.ListUserIdentityLink);
            if (items != null)
            {
                if (items.Any(link => string.IsNullOrWhiteSpace(link.ProviderKey)
                                      || string.IsNullOrWhiteSpace(link.Subject)
                                      || string.IsNullOrWhiteSpace(link.LocalUserId)))
                {
                    throw new JsonException("Persisted identity links contain an invalid identity.");
                }

                _links.AddRange(items);
            }
        }
        catch (Exception ex)
        {
            _links.Clear();
            LoadError = ex;
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_links, UserIdentityLinkJsonContext.Default.ListUserIdentityLink);
        _stateStorage.SaveLines(StorageKey, [json]);
    }

    private void MutateAndSave(Action mutation)
    {
        var snapshot = _links.Select(link => link.Copy()).ToList();
        mutation();
        try
        {
            Save();
        }
        catch
        {
            _links.Clear();
            _links.AddRange(snapshot);
            throw;
        }
    }
}

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(List<UserIdentityLink>))]
internal partial class UserIdentityLinkJsonContext : JsonSerializerContext
{
}

internal sealed class UserIdentityLink
{
    public string ProviderKey { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string LocalUserId { get; set; } = string.Empty;
    public string LocalDisplayName { get; set; } = string.Empty;

    public UserIdentityLink Copy()
    {
        return new UserIdentityLink
        {
            ProviderKey = ProviderKey,
            Subject = Subject,
            LocalUserId = LocalUserId,
            LocalDisplayName = LocalDisplayName
        };
    }
}
