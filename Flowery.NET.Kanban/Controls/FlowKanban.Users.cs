using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Flowery.NET.Kanban.Controls
{
    public partial class FlowKanban
    {
        private IFlowKanbanAssigneeAdapter? _trackedAssigneeAdapter;
        private CancellationTokenSource? _assigneeRefreshCancellation;
        private long _assigneeRefreshVersion;

        public static readonly StyledProperty<IFlowKanbanAssigneeAdapter?> AssigneeAdapterProperty =
            AvaloniaProperty.Register<FlowKanban, IFlowKanbanAssigneeAdapter?>(
                nameof(AssigneeAdapter),
                default!);

        /// <summary>
        /// Adapter used to map host-owned assignee IDs to display names, avatars, and roles.
        /// </summary>
        public IFlowKanbanAssigneeAdapter? AssigneeAdapter
        {
            get => (IFlowKanbanAssigneeAdapter?)GetValue(AssigneeAdapterProperty);
            set => SetValue(AssigneeAdapterProperty, value);
        }

        /// <summary>
        /// Raised when an assignee adapter callback fails.
        /// </summary>
        public event EventHandler<FlowKanbanAssigneeAdapterFailedEventArgs>? AssigneeAdapterFailed;

        private static void OnAssigneeAdapterChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is not FlowKanban kanban)
                return;

            kanban.CancelAssigneeRefresh();
            kanban.DetachAssigneeAdapter();
            kanban.UpdateAssigneeFilterOptions(Array.Empty<FlowTaskAssigneeOption>());
            if (!kanban.IsLoaded)
                return;

            kanban.AttachAssigneeAdapter(e.NewValue as IFlowKanbanAssigneeAdapter);
            _ = ObserveAssigneeRefreshAsync(kanban.RefreshAssigneesAsync());
        }

        private void AttachAssigneeAdapter(IFlowKanbanAssigneeAdapter? adapter)
        {
            if (ReferenceEquals(_trackedAssigneeAdapter, adapter))
                return;

            DetachAssigneeAdapter();
            if (adapter is null)
                return;

            _trackedAssigneeAdapter = adapter;
            _trackedAssigneeAdapter.AssigneesChanged += OnAssigneesChanged;
        }

        private void DetachAssigneeAdapter()
        {
            if (_trackedAssigneeAdapter is null)
                return;

            _trackedAssigneeAdapter.AssigneesChanged -= OnAssigneesChanged;
            _trackedAssigneeAdapter = null;
        }

        private void OnAssigneesChanged(object? sender, EventArgs e)
        {
            var adapter = _trackedAssigneeAdapter;
            if (adapter is null)
                return;

            FlowKanbanDispatcher.RunOrPost(() =>
            {
                if (!IsLoaded || !ReferenceEquals(_trackedAssigneeAdapter, adapter))
                    return;

                _ = ObserveAssigneeRefreshAsync(RefreshAssigneesAsync());
            });
        }

        /// <summary>
        /// Reloads assignee data from <see cref="AssigneeAdapter"/>.
        /// Superseded requests and requests canceled by unloading the control do not update the board.
        /// </summary>
        public async Task RefreshAssigneesAsync(CancellationToken cancellationToken = default)
        {
            var adapter = await FlowKanbanDispatcher.InvokeAsync(() => AssigneeAdapter)
                .ConfigureAwait(false);
            var version = Interlocked.Increment(ref _assigneeRefreshVersion);
            var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var previousCancellation = Interlocked.Exchange(ref _assigneeRefreshCancellation, cancellation);
            previousCancellation?.Cancel();

            try
            {
                if (adapter is null)
                {
                    await ApplyResolvedAssigneesAsync(
                            adapter,
                            version,
                            Array.Empty<FlowKanbanAssignee>(),
                            cancellation.Token)
                        .ConfigureAwait(false);
                    return;
                }

                IReadOnlyList<FlowKanbanAssignee> suppliedAssignees;
                try
                {
                    suppliedAssignees = await adapter.GetAssigneesAsync(cancellation.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    await ReportAssigneeAdapterFailureAsync(
                            adapter,
                            version,
                            FlowKanbanAssigneeAdapterOperation.LoadAssignees,
                            ex)
                        .ConfigureAwait(false);
                    return;
                }

                cancellation.Token.ThrowIfCancellationRequested();
                if (!IsCurrentAssigneeRefresh(version))
                    return;

                var resolvedAssignees = NormalizeResolvedAssignees(suppliedAssignees);
                var unresolvedIds = await FlowKanbanDispatcher.InvokeAsync(() =>
                        CollectUnresolvedAssigneeIds(resolvedAssignees))
                    .ConfigureAwait(false);

                foreach (var assigneeId in unresolvedIds)
                {
                    cancellation.Token.ThrowIfCancellationRequested();
                    if (!IsCurrentAssigneeRefresh(version))
                        return;

                    FlowKanbanAssignee? resolvedAssignee;
                    try
                    {
                        resolvedAssignee = await adapter.ResolveAssigneeAsync(assigneeId, cancellation.Token)
                            .ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        await ReportAssigneeAdapterFailureAsync(
                                adapter,
                                version,
                                FlowKanbanAssigneeAdapterOperation.ResolveAssignee,
                                ex,
                                assigneeId)
                            .ConfigureAwait(false);
                        continue;
                    }

                    if (resolvedAssignee is not null
                        && !string.Equals(resolvedAssignee.Id, assigneeId, StringComparison.Ordinal))
                    {
                        await ReportAssigneeAdapterFailureAsync(
                                adapter,
                                version,
                                FlowKanbanAssigneeAdapterOperation.ResolveAssignee,
                                new InvalidOperationException(
                                    $"The assignee callback returned ID '{resolvedAssignee.Id}' " +
                                    $"for requested ID '{assigneeId}'."),
                                assigneeId)
                            .ConfigureAwait(false);
                        continue;
                    }

                    if (resolvedAssignee is not null)
                    {
                        resolvedAssignees.Add(resolvedAssignee);
                    }
                }

                await ApplyResolvedAssigneesAsync(
                        adapter,
                        version,
                        resolvedAssignees,
                        cancellation.Token)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;
            }
            finally
            {
                Interlocked.CompareExchange(ref _assigneeRefreshCancellation, null, cancellation);
                cancellation.Dispose();
            }
        }

        private static List<FlowKanbanAssignee> NormalizeResolvedAssignees(
            IEnumerable<FlowKanbanAssignee>? assignees)
        {
            var normalized = new List<FlowKanbanAssignee>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var assignee in assignees ?? Array.Empty<FlowKanbanAssignee>())
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

            return normalized;
        }

        private string[] CollectUnresolvedAssigneeIds(
            IReadOnlyList<FlowKanbanAssignee> resolvedAssignees)
        {
            var resolvedIds = resolvedAssignees
                .Select(assignee => assignee.Id)
                .ToHashSet(StringComparer.Ordinal);
            var unresolvedIds = new List<string>();
            var seenIds = new HashSet<string>(StringComparer.Ordinal);

            foreach (var column in Board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    var assigneeId = task.AssigneeId;
                    if (string.IsNullOrWhiteSpace(assigneeId)
                        || resolvedIds.Contains(assigneeId)
                        || !seenIds.Add(assigneeId))
                    {
                        continue;
                    }

                    unresolvedIds.Add(assigneeId);
                }
            }

            return unresolvedIds.ToArray();
        }

        private async Task ApplyResolvedAssigneesAsync(
            IFlowKanbanAssigneeAdapter? adapter,
            long version,
            IReadOnlyList<FlowKanbanAssignee> resolvedAssignees,
            CancellationToken cancellationToken)
        {
            await FlowKanbanDispatcher.InvokeAsync(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!IsCurrentAssigneeRefresh(version)
                    || !ReferenceEquals(adapter, AssigneeAdapter))
                {
                    return;
                }

                ApplyResolvedAssignees(resolvedAssignees);
            }).ConfigureAwait(false);
        }

        private void ApplyResolvedAssignees(IReadOnlyList<FlowKanbanAssignee> resolvedAssignees)
        {
            var assigneesById = resolvedAssignees
                .ToDictionary(assignee => assignee.Id, StringComparer.Ordinal);

            foreach (var column in Board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    var assigneeId = task.AssigneeId;
                    if (!string.IsNullOrWhiteSpace(assigneeId)
                        && assigneesById.TryGetValue(assigneeId, out var assignee))
                    {
                        if (!string.Equals(task.Assignee, assignee.DisplayName, StringComparison.Ordinal))
                            task.Assignee = assignee.DisplayName;
                        task.AssigneeAvatarSource = assignee.AvatarSource;
                        task.AssigneeRoles = assignee.Roles;
                    }
                    else
                    {
                        task.AssigneeAvatarSource = null;
                        task.AssigneeRoles = Array.Empty<string>();
                    }
                }
            }

            var filterOptions = resolvedAssignees
                .Select(assignee => new FlowTaskAssigneeOption(assignee.Id, assignee.DisplayName))
                .ToArray();
            UpdateAssigneeFilterOptions(filterOptions);
        }

        private async Task ReportAssigneeAdapterFailureAsync(
            IFlowKanbanAssigneeAdapter adapter,
            long version,
            FlowKanbanAssigneeAdapterOperation operation,
            Exception exception,
            string? assigneeId = null)
        {
            await FlowKanbanDispatcher.InvokeAsync(() =>
            {
                if (!IsCurrentAssigneeRefresh(version)
                    || !ReferenceEquals(adapter, AssigneeAdapter))
                {
                    return;
                }

                AssigneeAdapterFailed?.Invoke(
                    this,
                    new FlowKanbanAssigneeAdapterFailedEventArgs(operation, exception, assigneeId));
            }).ConfigureAwait(false);
        }

        private bool IsCurrentAssigneeRefresh(long version)
        {
            return version == Volatile.Read(ref _assigneeRefreshVersion);
        }

        private void CancelAssigneeRefresh()
        {
            Interlocked.Increment(ref _assigneeRefreshVersion);
            Interlocked.Exchange(ref _assigneeRefreshCancellation, null)?.Cancel();
        }

        private static async Task ObserveAssigneeRefreshAsync(Task refreshTask)
        {
            try
            {
                await refreshTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
                // Adapter failures are reported through AssigneeAdapterFailed.
            }
        }

        private bool CanExecuteColumnOperation()
        {
            return true;
        }

        private static bool CanExecuteColumnOperation(FlowKanbanColumnData? column)
        {
            return column is not null;
        }
    }
}
