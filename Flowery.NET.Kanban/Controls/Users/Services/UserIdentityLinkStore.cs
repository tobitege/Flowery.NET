using System.Text.Json;
using System.Text.Json.Serialization;
using Flowery.Services;

namespace Flowery.NET.Kanban.Controls.Users;

internal sealed class UserIdentityLinkStore
{
    private const string StorageKey = "FlowKanban.IdentityLinks";
    private readonly IStateStorage _stateStorage;
    private readonly List<UserIdentityLink> _links = new();

    public UserIdentityLinkStore(IStateStorage stateStorage)
    {
        _stateStorage = stateStorage ?? throw new ArgumentNullException(nameof(stateStorage));
        Load();
    }

    public UserIdentityLink? FindLink(string providerKey, string subject)
    {
        if (string.IsNullOrWhiteSpace(providerKey) || string.IsNullOrWhiteSpace(subject))
        {
            return null;
        }

        return _links.FirstOrDefault(link =>
            string.Equals(link.ProviderKey, providerKey, StringComparison.Ordinal) &&
            string.Equals(link.Subject, subject, StringComparison.Ordinal));
    }

    public void SetLink(string providerKey, string subject, string localUserId, string localDisplayName)
    {
        if (string.IsNullOrWhiteSpace(providerKey) ||
            string.IsNullOrWhiteSpace(subject) ||
            string.IsNullOrWhiteSpace(localUserId))
        {
            return;
        }

        var existing = FindLink(providerKey, subject);
        if (existing != null)
        {
            existing.LocalUserId = localUserId;
            existing.LocalDisplayName = localDisplayName;
            Save();
            return;
        }

        _links.Add(new UserIdentityLink
        {
            ProviderKey = providerKey,
            Subject = subject,
            LocalUserId = localUserId,
            LocalDisplayName = localDisplayName
        });

        Save();
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

        _links.Remove(existing);
        Save();
        return true;
    }

    public bool RemoveLinksForProvider(string providerKey)
    {
        if (string.IsNullOrWhiteSpace(providerKey))
        {
            return false;
        }

        var removed = _links.RemoveAll(link =>
            string.Equals(link.ProviderKey, providerKey, StringComparison.Ordinal));

        if (removed <= 0)
        {
            return false;
        }

        Save();
        return true;
    }

    private void Load()
    {
        _links.Clear();

        var lines = _stateStorage.LoadLines(StorageKey);
        if (lines.Count == 0)
        {
            return;
        }

        var json = string.Join(string.Empty, lines);
        if (string.IsNullOrWhiteSpace(json))
        {
            return;
        }

        try
        {
            var items = JsonSerializer.Deserialize(json, UserIdentityLinkJsonContext.Default.ListUserIdentityLink);
            if (items != null)
            {
                _links.AddRange(items);
            }
        }
        catch
        {
            // Ignore invalid persisted data.
        }
    }

    private void Save()
    {
        try
        {
            var json = JsonSerializer.Serialize(_links, UserIdentityLinkJsonContext.Default.ListUserIdentityLink);
            _stateStorage.SaveLines(StorageKey, new[] { json });
        }
        catch
        {
            // Ignore persistence failures.
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
}
