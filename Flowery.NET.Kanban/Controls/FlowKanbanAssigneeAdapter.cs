using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls;

/// <summary>
/// Host-supplied assignee data. The ID is stable; avatar and roles are transient UI metadata.
/// </summary>
public sealed class FlowKanbanAssignee
{
    public FlowKanbanAssignee(
        string id,
        string displayName,
        IImage? avatarSource = null,
        IEnumerable<string>? roles = null)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("An assignee ID is required.", nameof(id));

        Id = id;
        DisplayName = string.IsNullOrWhiteSpace(displayName) ? Id : displayName.Trim();
        AvatarSource = avatarSource;
        Roles = Array.AsReadOnly(
            (roles ?? Array.Empty<string>())
                .Where(role => !string.IsNullOrWhiteSpace(role))
                .Select(role => role.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray());
    }

    public string Id { get; }
    public string DisplayName { get; }

    /// <summary>
    /// Avatar displayed by task cards. The host application owns its lifetime.
    /// </summary>
    public IImage? AvatarSource { get; }

    /// <summary>
    /// Descriptive host roles. FlowKanban does not use them for authorization.
    /// </summary>
    public IReadOnlyList<string> Roles { get; }
}

/// <summary>
/// Supplies stable assignee IDs, display names, avatars, and roles to <see cref="FlowKanban"/>.
/// Authentication and third-party service integration remain the responsibility of the host application.
/// </summary>
public interface IFlowKanbanAssigneeAdapter
{
    /// <summary>
    /// Raised when the host application's assignee data changed and the board should reload it.
    /// </summary>
    event EventHandler? AssigneesChanged;

    /// <summary>
    /// Gets the assignees available for task editing and filtering.
    /// </summary>
    Task<IReadOnlyList<FlowKanbanAssignee>> GetAssigneesAsync(
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolves one stable assignee ID to its current display data.
    /// Return <see langword="null"/> when the ID is unknown.
    /// </summary>
    Task<FlowKanbanAssignee?> ResolveAssigneeAsync(
        string assigneeId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default assignee adapter for directly supplied data or host callbacks.
/// </summary>
public sealed class FlowKanbanAssigneeAdapter : IFlowKanbanAssigneeAdapter
{
    private readonly object _syncRoot = new();
    private readonly Func<CancellationToken, Task<IReadOnlyList<FlowKanbanAssignee>>>? _getAssigneesAsync;
    private readonly Func<string, CancellationToken, Task<FlowKanbanAssignee?>>? _resolveAssigneeAsync;
    private IReadOnlyList<FlowKanbanAssignee> _assignees = Array.Empty<FlowKanbanAssignee>();
    private long _assigneeSnapshotVersion;

    /// <summary>
    /// Creates an empty adapter that can be populated through <see cref="SetAssignees"/>.
    /// </summary>
    public FlowKanbanAssigneeAdapter()
    {
    }

    /// <summary>
    /// Creates an adapter with directly supplied assignee data.
    /// </summary>
    public FlowKanbanAssigneeAdapter(IEnumerable<FlowKanbanAssignee> assignees)
    {
        ArgumentNullException.ThrowIfNull(assignees);
        _assignees = NormalizeAssignees(assignees);
    }

    /// <summary>
    /// Creates an adapter that loads assignees through host callbacks.
    /// </summary>
    public FlowKanbanAssigneeAdapter(
        Func<CancellationToken, Task<IReadOnlyList<FlowKanbanAssignee>>> getAssigneesAsync,
        Func<string, CancellationToken, Task<FlowKanbanAssignee?>>? resolveAssigneeAsync = null)
    {
        ArgumentNullException.ThrowIfNull(getAssigneesAsync);
        _getAssigneesAsync = getAssigneesAsync;
        _resolveAssigneeAsync = resolveAssigneeAsync;
    }

    /// <summary>
    /// Creates an adapter that resolves individual IDs through a host callback.
    /// </summary>
    public FlowKanbanAssigneeAdapter(
        Func<string, CancellationToken, Task<FlowKanbanAssignee?>> resolveAssigneeAsync)
    {
        ArgumentNullException.ThrowIfNull(resolveAssigneeAsync);
        _resolveAssigneeAsync = resolveAssigneeAsync;
    }

    public event EventHandler? AssigneesChanged;

    /// <summary>
    /// Replaces the directly supplied assignee data and notifies attached boards.
    /// </summary>
    public void SetAssignees(IEnumerable<FlowKanbanAssignee> assignees)
    {
        ArgumentNullException.ThrowIfNull(assignees);
        var snapshot = NormalizeAssignees(assignees);
        Interlocked.Increment(ref _assigneeSnapshotVersion);
        lock (_syncRoot)
        {
            _assignees = snapshot;
        }

        NotifyAssigneesChanged();
    }

    /// <summary>
    /// Notifies attached boards that callback-backed assignee data changed.
    /// </summary>
    public void NotifyAssigneesChanged()
    {
        AssigneesChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task<IReadOnlyList<FlowKanbanAssignee>> GetAssigneesAsync(
        CancellationToken cancellationToken = default)
    {
        if (_getAssigneesAsync is null)
        {
            lock (_syncRoot)
            {
                return _assignees;
            }
        }

        var version = Interlocked.Increment(ref _assigneeSnapshotVersion);
        var assignees = await _getAssigneesAsync(cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();
        var snapshot = NormalizeAssignees(assignees ?? Array.Empty<FlowKanbanAssignee>());
        lock (_syncRoot)
        {
            if (version == Volatile.Read(ref _assigneeSnapshotVersion))
                _assignees = snapshot;
        }

        return snapshot;
    }

    public async Task<FlowKanbanAssignee?> ResolveAssigneeAsync(
        string assigneeId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(assigneeId))
            return null;

        var requestedId = assigneeId;
        if (_resolveAssigneeAsync is not null)
        {
            var assignee = await _resolveAssigneeAsync(requestedId, cancellationToken)
                .ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (assignee is not null && !string.Equals(assignee.Id, requestedId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The assignee callback returned ID '{assignee.Id}' for requested ID '{requestedId}'.");
            }

            return assignee;
        }

        lock (_syncRoot)
        {
            foreach (var assignee in _assignees)
            {
                if (string.Equals(assignee.Id, requestedId, StringComparison.Ordinal))
                    return assignee;
            }
        }

        return null;
    }

    private static IReadOnlyList<FlowKanbanAssignee> NormalizeAssignees(
        IEnumerable<FlowKanbanAssignee> assignees)
    {
        var normalized = new List<FlowKanbanAssignee>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var assignee in assignees)
        {
            if (assignee is null)
                continue;

            if (!seenIds.Add(assignee.Id))
                continue;

            normalized.Add(new FlowKanbanAssignee(
                assignee.Id,
                assignee.DisplayName,
                assignee.AvatarSource,
                assignee.Roles));
        }

        return Array.AsReadOnly(normalized.ToArray());
    }
}

/// <summary>
/// Identifies an assignee adapter operation that failed.
/// </summary>
public enum FlowKanbanAssigneeAdapterOperation
{
    LoadAssignees,
    ResolveAssignee
}

/// <summary>
/// Describes an assignee adapter callback failure reported by <see cref="FlowKanban"/>.
/// </summary>
public sealed class FlowKanbanAssigneeAdapterFailedEventArgs : EventArgs
{
    public FlowKanbanAssigneeAdapterFailedEventArgs(
        FlowKanbanAssigneeAdapterOperation operation,
        Exception exception,
        string? assigneeId = null)
    {
        Operation = operation;
        Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        AssigneeId = assigneeId;
    }

    public FlowKanbanAssigneeAdapterOperation Operation { get; }
    public Exception Exception { get; }
    public string? AssigneeId { get; }
}
