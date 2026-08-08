using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace Flowery.NET.Kanban.Controls
{
    public partial class FlowKanban
    {
        public event EventHandler<FlowKanbanBoardExportRequestedEventArgs>? BoardExportRequested;

        #region CurrentView
        public static readonly StyledProperty<FlowKanbanView> CurrentViewProperty =
            AvaloniaProperty.Register<FlowKanban, FlowKanbanView>(
                                nameof(CurrentView),
                                FlowKanbanView.Board);

        public FlowKanbanView CurrentView
        {
            get => (FlowKanbanView)GetValue(CurrentViewProperty);
            set => SetValue(CurrentViewProperty, value);
        }

        private static void OnCurrentViewChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanban kanban)
            {
                kanban.UpdateViewState();
                if (kanban.CurrentView == FlowKanbanView.Home)
                {
                    kanban.RefreshBoards();
                }

                if (!kanban._isApplyingSettings)
                {
                    kanban.TrySaveSettings(out _);
                }
            }
        }
        #endregion

        #region IsStandardLayoutVisible
        public static readonly StyledProperty<bool> IsStandardLayoutVisibleProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsStandardLayoutVisible),
                true);

        public bool IsStandardLayoutVisible
        {
            get => (bool)GetValue(IsStandardLayoutVisibleProperty);
            private set => SetValue(IsStandardLayoutVisibleProperty, value);
        }
        #endregion

        #region IsSwimlaneLayoutVisible
        public static readonly StyledProperty<bool> IsSwimlaneLayoutVisibleProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsSwimlaneLayoutVisible),
                false);

        public bool IsSwimlaneLayoutVisible
        {
            get => (bool)GetValue(IsSwimlaneLayoutVisibleProperty);
            private set => SetValue(IsSwimlaneLayoutVisibleProperty, value);
        }
        #endregion

        #region IsCompactLayoutVisible
        public static readonly StyledProperty<bool> IsCompactLayoutVisibleProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsCompactLayoutVisible),
                false);

        public bool IsCompactLayoutVisible
        {
            get => (bool)GetValue(IsCompactLayoutVisibleProperty);
            private set => SetValue(IsCompactLayoutVisibleProperty, value);
        }
        #endregion

        #region IsHomeViewActive
        public static readonly StyledProperty<bool> IsHomeViewActiveProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsHomeViewActive),
                false);

        public bool IsHomeViewActive
        {
            get => (bool)GetValue(IsHomeViewActiveProperty);
            private set => SetValue(IsHomeViewActiveProperty, value);
        }
        #endregion

        #region IsBoardViewActive
        public static readonly StyledProperty<bool> IsBoardViewActiveProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsBoardViewActive),
                true);

        public bool IsBoardViewActive
        {
            get => (bool)GetValue(IsBoardViewActiveProperty);
            private set => SetValue(IsBoardViewActiveProperty, value);
        }
        #endregion

        #region IsBoardStatusBarVisible
        public static readonly StyledProperty<bool> IsBoardStatusBarVisibleProperty =
            AvaloniaProperty.Register<FlowKanban, bool>(
                nameof(IsBoardStatusBarVisible),
                true);

        public bool IsBoardStatusBarVisible
        {
            get => (bool)GetValue(IsBoardStatusBarVisibleProperty);
            private set => SetValue(IsBoardStatusBarVisibleProperty, value);
        }
        #endregion

        #region Boards
        public static readonly StyledProperty<ObservableCollection<FlowBoardMetadata>> BoardsProperty =
            AvaloniaProperty.Register<FlowKanban, ObservableCollection<FlowBoardMetadata>>(
                nameof(Boards),
                default!);

        public ObservableCollection<FlowBoardMetadata> Boards
        {
            get
            {
                if (GetValue(BoardsProperty) is not ObservableCollection<FlowBoardMetadata> boards)
                {
                    boards = new ObservableCollection<FlowBoardMetadata>();
                    SetValue(BoardsProperty, boards);
                }

                return boards;
            }
            private set => SetValue(BoardsProperty, value);
        }
        #endregion

        #region Commands
        public static readonly StyledProperty<ICommand> ShowHomeCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ShowHomeCommand),
                default!);

        public ICommand ShowHomeCommand
        {
            get => (ICommand)GetValue(ShowHomeCommandProperty);
            set => SetValue(ShowHomeCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> OpenBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(OpenBoardCommand),
                default!);

        public ICommand OpenBoardCommand
        {
            get => (ICommand)GetValue(OpenBoardCommandProperty);
            set => SetValue(OpenBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> CreateBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(CreateBoardCommand),
                default!);

        public ICommand CreateBoardCommand
        {
            get => (ICommand)GetValue(CreateBoardCommandProperty);
            set => SetValue(CreateBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> CreateDemoBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(CreateDemoBoardCommand),
                default!);

        public ICommand CreateDemoBoardCommand
        {
            get => (ICommand)GetValue(CreateDemoBoardCommandProperty);
            set => SetValue(CreateDemoBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> RenameBoardHomeCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(RenameBoardHomeCommand),
                default!);

        public ICommand RenameBoardHomeCommand
        {
            get => (ICommand)GetValue(RenameBoardHomeCommandProperty);
            set => SetValue(RenameBoardHomeCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> DeleteBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(DeleteBoardCommand),
                default!);

        public ICommand DeleteBoardCommand
        {
            get => (ICommand)GetValue(DeleteBoardCommandProperty);
            set => SetValue(DeleteBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> DuplicateBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(DuplicateBoardCommand),
                default!);

        public ICommand DuplicateBoardCommand
        {
            get => (ICommand)GetValue(DuplicateBoardCommandProperty);
            set => SetValue(DuplicateBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand> ExportBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanban, ICommand>(
                nameof(ExportBoardCommand),
                default!);

        public ICommand ExportBoardCommand
        {
            get => (ICommand)GetValue(ExportBoardCommandProperty);
            set => SetValue(ExportBoardCommandProperty, value);
        }
        #endregion

        public void RefreshBoards()
        {
            var boards = ListAvailableBoards();
            Boards.Clear();
            foreach (var board in boards)
            {
                Boards.Add(board);
            }
        }

        public IReadOnlyList<FlowBoardMetadata> ListAvailableBoards()
        {
            return BoardStore.ListBoards();
        }

        public bool TryLoadBoard(
            string boardId,
            out Exception? error,
            bool useCompactLayout = false,
            string? compactColumnId = null,
            string? compactColumnTitle = null)
        {
            try
            {
                var loaded = LoadBoardStateCore(boardId);
                if (loaded)
                {
                    ApplyCompactLayoutOptions(useCompactLayout, compactColumnId, compactColumnTitle);
                }
                error = null;
                return loaded;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        public bool TryRenameBoard(string boardId, string newTitle, out Exception? error)
        {
            try
            {
                var data = LoadBoardData(boardId);
                if (data == null)
                {
                    error = null;
                    return false;
                }

                data.Title = newTitle;
                SaveBoardStateCore(data, updateLastBoardId: false);
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        public bool TryDeleteBoard(string boardId, out Exception? error)
        {
            try
            {
                if (!BoardStore.TryDeleteBoard(boardId, out error))
                    return false;

                if (string.Equals(_lastBoardId, boardId, StringComparison.Ordinal))
                {
                    ClearLastBoardId();
                }

                if (string.Equals(Board.Id, boardId, StringComparison.Ordinal))
                {
                    Board = new FlowKanbanData();
                }

                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                return false;
            }
        }

        public bool TryDuplicateBoard(string boardId, out string? newBoardId, out Exception? error)
        {
            try
            {
                var data = LoadBoardData(boardId);
                if (data == null)
                {
                    error = null;
                    newBoardId = null;
                    return false;
                }

                var existingTitles = GetExistingBoardTitles();
                data.Title = BuildDuplicateTitle(data.Title, existingTitles);
                data.Id = Guid.NewGuid().ToString();
                data.CreatedAt = DateTime.Now;
                SaveBoardStateCore(data, updateLastBoardId: false);
                newBoardId = data.Id;
                error = null;
                return true;
            }
            catch (Exception ex)
            {
                error = ex;
                newBoardId = null;
                return false;
            }
        }

        private IReadOnlyCollection<string> GetExistingBoardTitles()
        {
            var titles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var board in ListAvailableBoards())
            {
                if (!string.IsNullOrWhiteSpace(board.Title))
                {
                    titles.Add(board.Title.Trim());
                }
            }

            return titles;
        }

        private static string BuildDuplicateTitle(string? originalTitle, IReadOnlyCollection<string> existingTitles)
        {
            var baseTitle = StripDuplicateSuffix(originalTitle);
            if (string.IsNullOrWhiteSpace(baseTitle))
            {
                baseTitle = FloweryLocalization.GetString("Kanban_Board_Untitled", "Untitled Board");
            }

            if (!existingTitles.Contains(baseTitle))
            {
                return baseTitle;
            }

            var counter = 1;
            while (true)
            {
                var candidate = $"{baseTitle} ({counter})";
                if (!existingTitles.Contains(candidate))
                {
                    return candidate;
                }

                counter++;
            }
        }

        private static string StripDuplicateSuffix(string? title)
        {
            if (string.IsNullOrWhiteSpace(title))
                return string.Empty;

            var trimmed = title.Trim();
            var endParen = trimmed.LastIndexOf(')');
            if (endParen != trimmed.Length - 1)
                return trimmed;

            var startParen = trimmed.LastIndexOf(" (", StringComparison.Ordinal);
            if (startParen <= 0)
                return trimmed;

            var numberSpan = trimmed.AsSpan(startParen + 2, trimmed.Length - startParen - 3);
            if (numberSpan.Length == 0)
                return trimmed;

            foreach (var ch in numberSpan)
            {
                if (ch < '0' || ch > '9')
                    return trimmed;
            }

            return trimmed[..startParen];
        }

        public bool TryExportBoard(string boardId, out string? json, out Exception? error)
        {
            try
            {
                return BoardStore.TryExportBoard(boardId, out json, out error);
            }
            catch (Exception ex)
            {
                error = ex;
                json = null;
                return false;
            }
        }

        private void ExecuteShowHome()
        {
            CurrentView = FlowKanbanView.Home;
        }

        private async void ExecuteOpenBoard(FlowBoardMetadata? metadata)
        {
            if (metadata == null)
                return;

            var (loaded, error) = await TryLoadBoardAsync(metadata.Id);
            if (loaded)
            {
                CurrentView = FlowKanbanView.Board;
                return;
            }

            if (error != null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load board '{metadata.Id}': {error.Message}");
            }
        }

        private async Task<(bool loaded, Exception? error)> TryLoadBoardAsync(string boardId)
        {
            if (string.IsNullOrWhiteSpace(boardId))
                return (false, null);

            FlowKanbanData? data = null;
            Exception? error = null;

            await Task.Run(() =>
            {
                if (!BoardStore.TryLoadBoard(boardId, out var loadedData, out var loadError))
                {
                    error = loadError;
                    return;
                }

                data = loadedData;
            }).ConfigureAwait(false);

            if (data == null)
                return (false, error);

            await ApplyLoadedBoardAsync(data, boardId);
            return (true, null);
        }

        private Task ApplyLoadedBoardAsync(FlowKanbanData data, string boardId)
        {
            return FlowKanbanDispatcher.InvokeAsync(() => ApplyLoadedBoard(data, boardId));
        }

        private void ApplyLoadedBoard(FlowKanbanData data, string boardId)
        {
            if (string.IsNullOrWhiteSpace(data.Id) || !string.Equals(data.Id, boardId, StringComparison.Ordinal))
            {
                data.Id = boardId;
            }

            _suppressAutoSave = true;
            try
            {
                Board = data;
            }
            finally
            {
                _suppressAutoSave = false;
            }

            UpdateLastBoardId(data.Id);
            _ = ObserveAssigneeRefreshAsync(RefreshAssigneesAsync());
        }

        private async void ExecuteCreateBoard()
        {
            var xamlRoot = TopLevel;
            if (xamlRoot == null)
                return;

            var result = await FlowBoardEditorDialog.ShowNewAsync(xamlRoot);
            if (result == null)
                return;

            var board = result.Board;
            board.Columns = CreateDefaultColumns(result.IncludeDemoData);
            board.CreatedAt = DateTime.Now;

            _suppressAutoSave = true;
            try
            {
                Board = board;
            }
            finally
            {
                _suppressAutoSave = false;
            }

            TrySaveBoard(out _);
            RefreshBoards();
            CurrentView = FlowKanbanView.Board;
        }

        private void ExecuteCreateDemoBoard()
        {
            CreateBoardInternal(includeDemoTasks: true);
        }

        private async void ExecuteRenameBoard(FlowBoardMetadata? metadata)
        {
            if (metadata == null)
                return;

            var newTitle = await ShowInputDialogAsync(
                FloweryLocalization.GetString("Kanban_Board_RenameTitle"),
                metadata.Title,
                FloweryLocalization.GetString("Kanban_Board_TitlePlaceholder"),
                closeOnEnter: true);

            if (string.IsNullOrWhiteSpace(newTitle))
                return;

            if (TryRenameBoard(metadata.Id, newTitle, out var error))
            {
                RefreshBoards();
                return;
            }

            if (error != null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to rename board '{metadata.Id}': {error.Message}");
            }
        }

        private async void ExecuteDeleteBoard(FlowBoardMetadata? metadata)
        {
            if (metadata == null)
                return;

            var wasCurrent = string.Equals(Board.Id, metadata.Id, StringComparison.Ordinal);
            var confirmed = await ShowConfirmDialogAsync(
                FloweryLocalization.GetString("Common_ConfirmDelete"),
                FloweryLocalization.GetString("Kanban_Home_DeleteConfirmMessage"),
                FloweryLocalization.GetString("Common_Delete"));

            if (!confirmed)
                return;

            if (TryDeleteBoard(metadata.Id, out var error))
            {
                RefreshBoards();

                if (wasCurrent)
                {
                    CurrentView = FlowKanbanView.Home;
                }
                else if (CurrentView == FlowKanbanView.Board && Boards.Count == 0)
                {
                    CurrentView = FlowKanbanView.Home;
                }

                return;
            }

            if (error != null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to delete board '{metadata.Id}': {error.Message}");
            }
        }

        private void ExecuteDuplicateBoard(FlowBoardMetadata? metadata)
        {
            if (metadata == null)
                return;

            if (TryDuplicateBoard(metadata.Id, out _, out var error))
            {
                RefreshBoards();
                return;
            }

            if (error != null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to duplicate board '{metadata.Id}': {error.Message}");
            }
        }

        private async void ExecuteExportBoard(FlowBoardMetadata? metadata)
        {
            if (metadata == null)
                return;

            if (TryExportBoard(metadata.Id, out var json, out var error))
            {
                var payload = new FlowKanbanBoardExportRequestedEventArgs(metadata.Id, metadata.Title, json ?? string.Empty);
                if (BoardExportRequested != null)
                {
                    BoardExportRequested.Invoke(this, payload);
                }
                else
                {
                    if (!await TrySaveExportToFileAsync(payload))
                    {
                        await CopyTextToClipboardAsync(payload.Json);
                    }
                }
                return;
            }

            if (error != null)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to export board '{metadata.Id}': {error.Message}");
            }
        }

        private async Task CopyTextToClipboardAsync(string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            try
            {
                var clipboard = TopLevel?.Clipboard;
                if (clipboard is { })
                {
                    await clipboard.SetTextAsync(text);
                }
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException)
            {
                // Clipboard is unavailable; ignore to avoid crashing.
            }
        }

        private static string BuildExportFileName(FlowKanbanBoardExportRequestedEventArgs payload)
        {
            var title = string.IsNullOrWhiteSpace(payload.BoardTitle) ? "Kanban Board" : payload.BoardTitle.Trim();
            var safeTitle = string.Join("_", title.Split(System.IO.Path.GetInvalidFileNameChars(), StringSplitOptions.RemoveEmptyEntries)).Trim();
            if (string.IsNullOrWhiteSpace(safeTitle))
            {
                safeTitle = "Kanban Board";
            }

            return safeTitle;
        }

        private async Task<bool> TrySaveExportToFileAsync(FlowKanbanBoardExportRequestedEventArgs payload)
        {
            if (string.IsNullOrWhiteSpace(payload.Json))
                return false;

            try
            {
                var storageProvider = TopLevel?.StorageProvider;
                if (storageProvider?.CanSave != true)
                    return false;

                var file = await storageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
                {
                    Title = FloweryLocalization.GetString("Kanban_Home_Export"),
                    SuggestedFileName = BuildExportFileName(payload),
                    DefaultExtension = "json",
                    FileTypeChoices = [JsonFileType]
                });
                if (file is null)
                    return false;

                await using var stream = await file.OpenWriteAsync();
                stream.SetLength(0);
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(payload.Json);
                return true;
            }
            catch (Exception ex) when (ex is COMException || ex is UnauthorizedAccessException || ex is NotSupportedException || ex is InvalidOperationException || ex is IOException || ex is ArgumentException)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to save board export: {ex.Message}");
                return false;
            }
        }

        private void CreateBoardInternal(bool includeDemoTasks)
        {
            var board = BuildDefaultBoard(includeDemoTasks);

            _suppressAutoSave = true;
            try
            {
                Board = board;
            }
            finally
            {
                _suppressAutoSave = false;
            }

            TrySaveBoard(out _);
            RefreshBoards();
            CurrentView = FlowKanbanView.Board;
        }

        private static FlowKanbanData BuildDefaultBoard(bool includeDemoTasks)
        {
            var board = new FlowKanbanData();
            if (includeDemoTasks)
            {
                board.Title = FloweryLocalization.GetString("Kanban_Home_DemoBoardTitle");
            }

            board.Columns = CreateDefaultColumns(includeDemoTasks);
            board.DoneColumnId = ResolveDefaultDoneColumnId(board.Columns, board.ArchiveColumnId);
            board.CreatedAt = DateTime.Now;
            FlowKanbanWorkItemNumberHelper.EnsureBoardNumbers(board);
            return board;
        }

        private static ObservableCollection<FlowKanbanColumnData> CreateDefaultColumns(bool includeDemoTasks)
        {
            var columns = new ObservableCollection<FlowKanbanColumnData>
            {
                new() { Title = FloweryLocalization.GetString("Kanban_Column_Backlog") },
                new() { Title = FloweryLocalization.GetString("Kanban_Column_ToDo") },
                new() { Title = FloweryLocalization.GetString("Kanban_Column_InProgress") },
                new() { Title = FloweryLocalization.GetString("Kanban_Column_Done") }
            };

            if (includeDemoTasks)
            {
                PopulateDemoTasks(columns);
            }

            return columns;
        }

        private static void PopulateDemoTasks(IList<FlowKanbanColumnData> columns)
        {
            if (columns == null || columns.Count == 0)
                return;

            const int totalTasks = 200;
            var perColumn = new int[columns.Count];
            var baseCount = totalTasks / columns.Count;
            var remainder = totalTasks % columns.Count;
            for (var i = 0; i < columns.Count; i++)
            {
                perColumn[i] = baseCount + (i < remainder ? 1 : 0);
            }

            var demoAssignees = new[]
            {
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
            };

            var actions = new[]
            {
                "Design",
                "Implement",
                "Audit",
                "Refactor",
                "Draft",
                "Ship",
                "Review",
                "Fix",
                "Instrument",
                "QA",
                "Align",
                "Prototype",
                "Launch",
                "Optimize",
                "Document",
                "Automate",
                "Plan",
                "Migrate",
                "Polish",
                "Stabilize"
            };

            var targets = new[]
            {
                "onboarding flow",
                "billing webhook",
                "mobile settings",
                "analytics dashboard",
                "release checklist",
                "search filters",
                "notification pipeline",
                "permissions model",
                "email templates",
                "customer support macros",
                "API rate limits",
                "feature flags",
                "CI pipeline",
                "error reporting",
                "data export",
                "SLA dashboard",
                "usage tracking",
                "accessibility pass",
                "security review",
                "performance budget",
                "design system updates",
                "pricing page",
                "in-app tours",
                "status page",
                "integration docs",
                "subscription sync",
                "latency alerts",
                "user profile editor",
                "task bulk actions",
                "kanban metrics",
                "archival flow",
                "import wizard",
                "audit logs",
                "mobile offline cache"
            };

            var descriptionHeads = new[]
            {
                "Clarify scope with stakeholders.",
                "Coordinate with QA for acceptance criteria.",
                "Align UX with design system.",
                "Validate edge cases and error states.",
                "Cover analytics and event naming.",
                "Ensure rollback plan is documented.",
                "Test on mobile and desktop.",
                "Verify localization coverage.",
                "Check performance budget and memory usage.",
                "Update docs and release notes.",
                "Confirm API contract with backend.",
                "Add feature flag for staged rollout."
            };

            var descriptionTails = new[]
            {
                "Target completion by end of sprint.",
                "Requires API updates.",
                "Blocked until security sign-off.",
                "Includes telemetry dashboard.",
                "Needs user research review.",
                "Coordinate with marketing launch.",
                "Sync with backend team.",
                "Add migration notes.",
                "Include accessibility audit.",
                "Watch crash logs for first release."
            };

            var tags = new[]
            {
                "design",
                "frontend",
                "backend",
                "analytics",
                "infra",
                "docs",
                "bug",
                "performance",
                "release",
                "security",
                "mobile",
                "web",
                "ux",
                "qa",
                "ops",
                "compliance",
                "onboarding",
                "billing",
                "notifications",
                "search",
                "settings",
                "accessibility",
                "payments"
            };

            var rng = new Random(20260127);
            var today = DateTime.Today;
            var taskIndex = 0;

            for (var columnIndex = 0; columnIndex < columns.Count; columnIndex++)
            {
                var column = columns[columnIndex];
                for (var i = 0; i < perColumn[columnIndex]; i++)
                {
                    var action = actions[rng.Next(actions.Length)];
                    var target = targets[rng.Next(targets.Length)];
                    var title = $"{action} {target}";

                    var head = descriptionHeads[rng.Next(descriptionHeads.Length)];
                    var tail = descriptionTails[rng.Next(descriptionTails.Length)];
                    var description = $"{head} {tail}";

                    var primaryTag = tags[taskIndex % tags.Length];
                    var secondaryTag = tags[(taskIndex * 7 + 3) % tags.Length];
                    if (string.Equals(primaryTag, secondaryTag, StringComparison.OrdinalIgnoreCase))
                    {
                        secondaryTag = tags[(taskIndex + 5) % tags.Length];
                    }

                    var priority = GetDemoPriority(rng, taskIndex);
                    var palette = GetDemoPalette(priority);

                    var assignee = demoAssignees[taskIndex % demoAssignees.Length];
                    var progress = GetDemoProgress(rng, columnIndex);

                    var task = new FlowTask
                    {
                        Title = title,
                        Description = description,
                        Tags = $"{primaryTag}, {secondaryTag}",
                        Assignee = assignee,
                        AssigneeId = $"demo-{assignee.ToLowerInvariant()}",
                        Priority = priority,
                        Palette = palette,
                        ProgressPercent = progress
                    };

                    ApplyDemoSchedule(task, rng, today, columnIndex, progress);
                    ApplyDemoEffort(task, rng, columnIndex, progress);

                    column.Tasks.Add(task);
                    taskIndex++;
                }
            }
        }

        private static FlowTaskPriority GetDemoPriority(Random rng, int taskIndex)
        {
            var roll = rng.Next(100);
            if (roll < 8 || taskIndex % 29 == 0)
                return FlowTaskPriority.Urgent;
            if (roll < 25 || taskIndex % 11 == 0)
                return FlowTaskPriority.High;
            if (roll < 40)
                return FlowTaskPriority.Low;
            return FlowTaskPriority.Normal;
        }

        private static DaisyColor GetDemoPalette(FlowTaskPriority priority)
        {
            return priority switch
            {
                FlowTaskPriority.Urgent => DaisyColor.Error,
                FlowTaskPriority.High => DaisyColor.Warning,
                FlowTaskPriority.Low => DaisyColor.Info,
                _ => DaisyColor.Default
            };
        }

        private static int GetDemoProgress(Random rng, int columnIndex)
        {
            return columnIndex switch
            {
                0 => 0,
                1 => rng.Next(0, 20),
                2 => rng.Next(20, 85),
                _ => 100
            };
        }

        private static void ApplyDemoSchedule(FlowTask task, Random rng, DateTime today, int columnIndex, int progress)
        {
            DateTime plannedStart;
            DateTime plannedEnd;

            switch (columnIndex)
            {
                case 0:
                    plannedStart = today.AddDays(rng.Next(7, 45));
                    plannedEnd = plannedStart.AddDays(rng.Next(5, 20));
                    break;
                case 1:
                    plannedStart = today.AddDays(rng.Next(0, 14));
                    plannedEnd = plannedStart.AddDays(rng.Next(3, 14));
                    break;
                case 2:
                    plannedStart = today.AddDays(-rng.Next(1, 10));
                    plannedEnd = today.AddDays(rng.Next(3, 21));
                    break;
                default:
                    plannedStart = today.AddDays(-rng.Next(40, 90));
                    plannedEnd = today.AddDays(-rng.Next(5, 20));
                    break;
            }

            task.PlannedStartDate = plannedStart;
            task.PlannedEndDate = plannedEnd;

            if (columnIndex >= 2)
            {
                task.ActualStartDate = plannedStart.AddDays(rng.Next(0, 3));
            }

            if (progress >= 100)
            {
                task.ActualEndDate = plannedEnd.AddDays(rng.Next(0, 2));
                task.CompletedAt = task.ActualEndDate;
            }
        }

        private static void ApplyDemoEffort(FlowTask task, Random rng, int columnIndex, int progress)
        {
            var estimatedHours = 2 + (rng.Next(0, 10) * 1.5);
            task.EstimatedHours = estimatedHours;

            if (columnIndex >= 2)
            {
                var multiplier = progress >= 100 ? 0.8 + (rng.NextDouble() * 0.6) : 0.3 + (rng.NextDouble() * 0.6);
                task.ActualHours = Math.Round(estimatedHours * multiplier, 1);
            }
        }

        private static string? ResolveDefaultDoneColumnId(
            IReadOnlyList<FlowKanbanColumnData> columns,
            string? archiveColumnId)
        {
            if (columns == null || columns.Count == 0)
                return null;

            var doneTitle = FloweryLocalization.GetString("Kanban_Column_Done", "Done");
            foreach (var column in columns)
            {
                if (string.Equals(column.Id, archiveColumnId, StringComparison.Ordinal))
                    continue;

                if (!string.IsNullOrWhiteSpace(column.Title)
                    && string.Equals(column.Title.Trim(), doneTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return column.Id;
                }
            }

            for (var i = columns.Count - 1; i >= 0; i--)
            {
                var column = columns[i];
                if (!string.Equals(column.Id, archiveColumnId, StringComparison.Ordinal))
                    return column.Id;
            }

            return null;
        }

        private FlowKanbanData? LoadBoardData(string boardId)
        {
            if (string.IsNullOrWhiteSpace(boardId))
                return null;

            if (!BoardStore.TryLoadBoard(boardId, out var data, out _))
                return null;

            return data;
        }

        private void UpdateViewState()
        {
            var isHome = CurrentView == FlowKanbanView.Home;
            IsHomeViewActive = isHome;
            IsBoardViewActive = CurrentView == FlowKanbanView.Board;
            UpdateBoardStatusBarVisibility();
            UpdateCompactLayoutState();
            UpdateLayoutVisibilityState();
        }

        private void UpdateBoardStatusBarVisibility()
        {
            IsBoardStatusBarVisible = HasStatusMessage || (IsBoardViewActive && IsStatusBarVisible);
        }

        private void UpdateLayoutVisibilityState()
        {
            var isBoardActive = IsBoardViewActive;
            var isCompact = IsCompactLayoutEnabled;
            IsCompactLayoutVisible = isBoardActive && isCompact;
            IsStandardLayoutVisible = isBoardActive && IsStandardLayoutEnabled && !isCompact;
            IsSwimlaneLayoutVisible = isBoardActive && IsSwimlaneLayoutEnabled && !isCompact;
            UpdateLayoutItemsSources();
            ScheduleCompactColumnSizingUpdate();
        }

        private void ClearLastBoardId()
        {
            _lastBoardId = null;
            TrySaveSettings(out _);
        }

    }

    public enum FlowKanbanView
    {
        Board,
        Home
    }

    public sealed class FlowKanbanBoardExportRequestedEventArgs : EventArgs
    {
        public FlowKanbanBoardExportRequestedEventArgs(string boardId, string boardTitle, string json)
        {
            BoardId = boardId;
            BoardTitle = boardTitle;
            Json = json;
        }

        public string BoardId { get; }
        public string BoardTitle { get; }
        public string Json { get; }
    }
}
