using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Theming;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Modal dialog for editing board properties.
    /// Leverages FlowKanbanDialogBase for smart sizing and theming.
    /// </summary>
    public sealed partial class FlowBoardEditorDialog : FlowKanbanDialogBase
    {
        public sealed class FlowBoardEditorResult
        {
            public FlowBoardEditorResult(FlowKanbanData board, bool includeDemoData)
            {
                Board = board ?? throw new ArgumentNullException(nameof(board));
                IncludeDemoData = includeDemoData;
            }

            public FlowKanbanData Board { get; }
            public bool IncludeDemoData { get; }
        }

        private sealed class BoardEditState
        {
            public string Title { get; set; } = string.Empty;
            public string Description { get; set; } = string.Empty;
            public string CreatedBy { get; set; } = string.Empty;
            public DateTime CreatedAt { get; set; }
            public bool EnableSwimlanes { get; set; }
            public bool AutoArchiveDoneEnabled { get; set; }
            public int AutoArchiveDoneDays { get; set; } = 14;
            public string? DoneColumnId { get; set; }
            public ObservableCollection<FlowKanbanLane> Lanes { get; } = new();
            public Dictionary<string, string?> LaneReassignments { get; } = new();
            public ObservableCollection<string> Tags { get; } = new();
            public Dictionary<string, string?> ColumnPolicies { get; } = new();
            public bool IncludeDemoData { get; set; }
        }

        private sealed class LaneEditorContext
        {
            public LaneEditorContext(
                BoardEditState editState,
                StackPanel lanesPanel,
                TextBlock emptyText,
                DaisyInput addLaneInput,
                TopLevel xamlRoot)
            {
                EditState = editState;
                LanesPanel = lanesPanel;
                EmptyText = emptyText;
                AddLaneInput = addLaneInput;
                TopLevel = xamlRoot;
            }

            public BoardEditState EditState { get; }
            public StackPanel LanesPanel { get; }
            public TextBlock EmptyText { get; }
            public DaisyInput AddLaneInput { get; }
            public TopLevel TopLevel { get; }
        }

        private sealed class LaneActionContext
        {
            public LaneActionContext(LaneEditorContext editor, FlowKanbanLane lane)
            {
                Editor = editor;
                Lane = lane;
            }

            public LaneEditorContext Editor { get; }
            public FlowKanbanLane Lane { get; }
        }

        private sealed class LaneMoveContext
        {
            public LaneMoveContext(LaneEditorContext editor, FlowKanbanLane lane, int delta)
            {
                Editor = editor;
                Lane = lane;
                Delta = delta;
            }

            public LaneEditorContext Editor { get; }
            public FlowKanbanLane Lane { get; }
            public int Delta { get; }
        }

        private sealed class LaneDeleteDecision
        {
            public LaneDeleteDecision(bool confirmed, string? fallbackLaneId)
            {
                Confirmed = confirmed;
                FallbackLaneId = fallbackLaneId;
            }

            public bool Confirmed { get; }
            public string? FallbackLaneId { get; }
        }

        private sealed partial class LaneDeleteDialog : FlowKanbanDialogBase
        {
            private readonly TaskCompletionSource<LaneDeleteDecision> _tcs = new();
            private readonly TopLevel _xamlRoot;
            private readonly DaisySelect? _fallbackSelect;
            private readonly DaisyButton _confirmButton;
            private readonly DaisyButton _cancelButton;
            private bool _isClosing;

            private LaneDeleteDialog(TopLevel xamlRoot, IReadOnlyList<FlowKanbanLane> remainingLanes)
            {
                AutomationProperties.SetName(
                    this,
                    FloweryLocalization.GetString("Common_ConfirmDelete"));
                _xamlRoot = xamlRoot;

                Content = CreateDialogContent(
                    remainingLanes,
                    out _confirmButton,
                    out _cancelButton,
                    out _fallbackSelect);

                ApplySmartSizingWithAutoHeight(xamlRoot);

                _confirmButton.Click += OnConfirmClicked;
                _cancelButton.Click += OnCancelClicked;
            }

            public static Task<LaneDeleteDecision> ShowAsync(TopLevel xamlRoot, IReadOnlyList<FlowKanbanLane> remainingLanes)
            {
                if (xamlRoot == null)
                    return Task.FromResult(new LaneDeleteDecision(false, null));

                var dialog = new LaneDeleteDialog(xamlRoot, remainingLanes);
                return dialog.ShowInternalAsync();
            }

            private Task<LaneDeleteDecision> ShowInternalAsync()
            {

                IsOpen = true;
                return _tcs.Task;
            }

            private void OnConfirmClicked(object? sender, RoutedEventArgs e)
            {
                var fallbackLaneId = GetSelectedFallbackLaneId(_fallbackSelect);
                Close(new LaneDeleteDecision(true, fallbackLaneId));
            }

            private void OnCancelClicked(object? sender, RoutedEventArgs e)
            {
                Close(new LaneDeleteDecision(false, null));
            }

            protected override void OnDialogOpenChanged(bool isOpen)
            {
                if (!isOpen && !_isClosing)
                {
                    Close(new LaneDeleteDecision(false, null));
                }
            }

            private void Close(LaneDeleteDecision decision)
            {
                if (_isClosing)
                    return;

                _isClosing = true;
                if (IsOpen)
                    IsOpen = false;



                _tcs.TrySetResult(decision);
            }

            private static Control CreateDialogContent(
                IReadOnlyList<FlowKanbanLane> remainingLanes,
                out DaisyButton confirmButton,
                out DaisyButton cancelButton,
                out DaisySelect? fallbackSelect)
            {
                var container = new StackPanel
                {
                    Spacing = 16,
                    MinWidth = 280
                };

                container.Children.Add(new TextBlock
                {
                    Text = FloweryLocalization.GetString("Common_ConfirmDelete"),
                    FontSize = 18,
                    FontWeight = FontWeights.Bold,
                    Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
                });

                var message = remainingLanes.Count > 0
                    ? FloweryLocalization.GetString("Kanban_Lanes_DeleteMessage")
                    : FloweryLocalization.GetString("Kanban_Lanes_NoFallbackMessage");

                container.Children.Add(new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
                });

                fallbackSelect = null;
                if (remainingLanes.Count > 0)
                {
                    var fallbackLabel = CreateSectionLabel(
                        FloweryLocalization.GetString("Kanban_Lanes_FallbackLabel"));
                    container.Children.Add(fallbackLabel);

                    fallbackSelect = new DaisySelect
                    {
                        PlaceholderText = FloweryLocalization.GetString("Select_Placeholder"),
                        // Size = DaisySize.Small,
                        HorizontalAlignment = HorizontalAlignment.Stretch
                    };
                    AutomationProperties.SetLabeledBy(fallbackSelect, fallbackLabel);

                    foreach (var remaining in remainingLanes)
                    {
                        fallbackSelect.Items.Add(
                            new FlowKanbanSelectOption<string?>(remaining.Title, remaining.Id));
                    }

                    fallbackSelect.Items.Add(
                        new FlowKanbanSelectOption<string?>(
                            FloweryLocalization.GetString("Kanban_Lanes_NoLaneOption"),
                            null));

                    fallbackSelect.SelectedIndex = 0;
                    container.Children.Add(fallbackSelect);
                }

                var buttonPanel = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    Spacing = 12,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                confirmButton = new DaisyButton
                {
                    Content = FloweryLocalization.GetString("Common_Delete"),
                    Variant = DaisyButtonVariant.Error,
                    // Size = DaisySize.Medium,
                    MinWidth = 80
                };

                cancelButton = new DaisyButton
                {
                    Content = FloweryLocalization.GetString("Common_Cancel"),
                    Variant = DaisyButtonVariant.Error,
                    // Size = DaisySize.Medium,
                    MinWidth = 80
                };

                buttonPanel.Children.Add(confirmButton);
                buttonPanel.Children.Add(cancelButton);

                var sectionBackground = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
                var sectionBorderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush");
                var footerCard = new Border
                {
                    Background = sectionBackground,
                    BorderBrush = sectionBorderBrush,
                    BorderThickness = new Thickness(1),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(12),
                    Child = buttonPanel
                };

                container.Children.Add(footerCard);

                return container;
            }
        }

        private readonly FlowKanbanData _board;
        private readonly BoardEditState _editState;
        private readonly TopLevel _xamlRoot;
        private readonly bool _isNewBoard;
        private readonly TaskCompletionSource<bool> _tcs = new();
        private bool _isClosing;
        private bool _isInitialHeightLocked;
        private bool _isHeightLockPending;
        private double _heightLockLastHeight;
        private double _heightLockLastWidth;
        private int _heightLockStableCount;

        protected override double DialogBoundsMargin => IsDialogResizingEnabled ? 24 : base.DialogBoundsMargin;

        private DaisyInput _titleBox = null!;
        private DaisyTextArea _descriptionBox = null!;
        private DaisyInput _createdByBox = null!;
        private DaisyButton _saveButton = null!;
        private DaisyButton _cancelButton = null!;
        public bool IncludeDemoData => _editState.IncludeDemoData;

        // Parameterless constructor for runtime control construction tests.
        public FlowBoardEditorDialog()
        {
            AutomationProperties.SetName(
                this,
                FloweryLocalization.GetString("Kanban_Board_EditTitle"));
            _board = new FlowKanbanData();
            _xamlRoot = null!;
            _isNewBoard = false;
            _editState = new BoardEditState
            {
                Title = _board.Title,
                Description = _board.Description,
                CreatedBy = _board.CreatedBy,
                CreatedAt = _board.CreatedAt,
                EnableSwimlanes = _board.GroupBy == FlowKanbanGroupBy.Lane,
                AutoArchiveDoneEnabled = _board.AutoArchiveDoneEnabled,
                AutoArchiveDoneDays = _board.AutoArchiveDoneDays,
                DoneColumnId = _board.DoneColumnId
            };
            InitializeTagState(_board, _editState);
            InitializePolicyState(_board, _editState);

            Content = new Grid();
        }

        private FlowBoardEditorDialog(FlowKanbanData board, TopLevel xamlRoot, bool isNewBoard)
        {
            AutomationProperties.SetName(
                this,
                FloweryLocalization.GetString("Kanban_Board_EditTitle"));
            _board = board;
            _xamlRoot = xamlRoot;
            _isNewBoard = isNewBoard;

            _editState = new BoardEditState
            {
                Title = board.Title,
                Description = board.Description,
                CreatedBy = board.CreatedBy,
                CreatedAt = board.CreatedAt,
                EnableSwimlanes = board.GroupBy == FlowKanbanGroupBy.Lane,
                AutoArchiveDoneEnabled = board.AutoArchiveDoneEnabled,
                AutoArchiveDoneDays = board.AutoArchiveDoneDays,
                DoneColumnId = board.DoneColumnId
            };
            InitializeLaneState(board, _editState);
            InitializeTagState(board, _editState);
            InitializePolicyState(board, _editState);

            Content = CreateEditorContent(
                board,
                _editState,
                xamlRoot,
                _isNewBoard,
                out _titleBox,
                out _descriptionBox,
                out _createdByBox,
                out _saveButton,
                out _cancelButton);

            IsDraggable = true;
            IsDialogResizingEnabled = true;
            ApplySmartSizingWithAutoHeight(xamlRoot);

            _saveButton.Click += OnSaveClicked;
            _cancelButton.Click += OnCancelClicked;
        }

        public static Task<bool> ShowAsync(FlowKanbanData board, TopLevel xamlRoot)
        {
            if (board == null || xamlRoot == null)
                return Task.FromResult(false);

            var dialog = new FlowBoardEditorDialog(board, xamlRoot, isNewBoard: false);
            return dialog.ShowInternalAsync();
        }

        public static async Task<FlowBoardEditorResult?> ShowNewAsync(TopLevel xamlRoot)
        {
            if (xamlRoot == null)
                return null;

            var board = new FlowKanbanData
            {
                Title = string.Empty,
                Description = string.Empty,
                CreatedBy = string.Empty,
                CreatedAt = DateTime.Now
            };
            board.Tags.Clear();
            board.Lanes.Clear();
            board.Columns.Clear();

            var dialog = new FlowBoardEditorDialog(board, xamlRoot, isNewBoard: true);
            var saved = await dialog.ShowInternalAsync();
            if (!saved)
                return null;

            return new FlowBoardEditorResult(board, dialog.IncludeDemoData);
        }

        private Task<bool> ShowInternalAsync()
        {

            IsOpen = true;
            _titleBox.Focus();
            _titleBox.SelectAll();
            return _tcs.Task;
        }

        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            _board.Title = _titleBox.Text?.Trim() ?? _board.Title;
            _board.Description = _descriptionBox.Text?.Trim() ?? "";
            _board.CreatedBy = _createdByBox.Text?.Trim() ?? "";
            ApplyBoardTags(_board, _editState.Tags);
            ApplyLaneEdits(_board, _editState);
            ApplyPolicyEdits(_board, _editState);
            if (_editState.AutoArchiveDoneEnabled && string.IsNullOrWhiteSpace(_editState.DoneColumnId))
            {
                _editState.DoneColumnId = ResolveDefaultDoneColumnId(_board);
            }
            _board.AutoArchiveDoneEnabled = _editState.AutoArchiveDoneEnabled;
            _board.AutoArchiveDoneDays = Math.Max(1, _editState.AutoArchiveDoneDays);
            _board.DoneColumnId = _editState.DoneColumnId;
            _board.GroupBy = _editState.EnableSwimlanes ? FlowKanbanGroupBy.Lane : FlowKanbanGroupBy.None;
            Close(result: true);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Close(result: false);
        }

        protected override void OnDialogOpenChanged(bool isOpen)
        {
            if (isOpen && !_isInitialHeightLocked)
            {
                StartHeightLock();
                return;
            }

            if (!isOpen && !_isClosing)
            {
                StopHeightLock();
                Close(result: false);
            }
        }

        private void Close(bool result)
        {
            if (_isClosing)
                return;

            _isClosing = true;
            if (IsOpen)
                IsOpen = false;



            _tcs.TrySetResult(result);
        }

        private void StartHeightLock()
        {
            if (_isHeightLockPending || _isInitialHeightLocked)
                return;

            _isHeightLockPending = true;
            _heightLockLastHeight = 0;
            _heightLockLastWidth = 0;
            _heightLockStableCount = 0;
            LayoutUpdated += OnLayoutUpdatedForHeightLock;
        }

        private void StopHeightLock()
        {
            if (!_isHeightLockPending)
                return;

            _isHeightLockPending = false;
            LayoutUpdated -= OnLayoutUpdatedForHeightLock;
        }

        private void OnLayoutUpdatedForHeightLock(object? sender, object e)
        {
            _ = sender;
            _ = e;
            TryLockInitialDialogHeight();
        }

        private void TryLockInitialDialogHeight()
        {
            if (_isInitialHeightLocked)
                return;

            var dialogHost = FindDialogHost();
            if (dialogHost == null)
                return;

            var height = dialogHost.Bounds.Height;
            if (height <= 0 || double.IsNaN(height))
                return;

            var width = dialogHost.Bounds.Width;
            if (width <= 0 || double.IsNaN(width))
            {
                width = DialogWidth;
            }

            var heightDelta = Math.Abs(height - _heightLockLastHeight);
            var widthDelta = Math.Abs(width - _heightLockLastWidth);
            var isStable = heightDelta <= 0.5 && widthDelta <= 0.5;
            if (isStable)
            {
                _heightLockStableCount++;
            }
            else
            {
                _heightLockStableCount = 0;
            }

            _heightLockLastHeight = height;
            _heightLockLastWidth = width;

            if (_heightLockStableCount < 2)
                return;

            _isInitialHeightLocked = true;
            StopHeightLock();

            DialogWidth = width;
            DialogHeight = height;
            SetDialogSize(width, height);
        }

        private Grid? FindDialogHost()
        {
            return DialogHost;
        }

        private static void InitializeLaneState(FlowKanbanData board, BoardEditState editState)
        {
            foreach (var lane in board.Lanes)
            {
                editState.Lanes.Add(CloneLane(lane));
            }
        }

        private static void InitializeTagState(FlowKanbanData board, BoardEditState editState)
        {
            editState.Tags.Clear();
            foreach (var tag in board.Tags)
            {
                var trimmed = tag?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                editState.Tags.Add(trimmed);
            }
        }

        private static void InitializePolicyState(FlowKanbanData board, BoardEditState editState)
        {
            editState.ColumnPolicies.Clear();
            foreach (var column in board.Columns)
            {
                editState.ColumnPolicies[column.Id] = column.PolicyText;
            }
        }

        private static FlowKanbanLane CloneLane(FlowKanbanLane lane)
        {
            return new FlowKanbanLane
            {
                Id = lane.Id,
                Title = lane.Title,
                Description = lane.Description,
                CustomFields = lane.CustomFields != null
                    ? new Dictionary<string, JsonElement>(lane.CustomFields)
                    : null
            };
        }

        private static void ApplyLaneEdits(FlowKanbanData board, BoardEditState editState)
        {
            var updatedLanes = new ObservableCollection<FlowKanbanLane>();
            foreach (var lane in editState.Lanes)
            {
                updatedLanes.Add(CloneLane(lane));
            }

            board.Lanes = updatedLanes;

            if (editState.LaneReassignments.Count == 0 && updatedLanes.Count == 0)
                return;

            var validLaneIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var lane in updatedLanes)
            {
                if (!string.IsNullOrWhiteSpace(lane.Id))
                    validLaneIds.Add(lane.Id);
            }

            foreach (var column in board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    if (string.IsNullOrWhiteSpace(task.LaneId))
                        continue;

                    if (editState.LaneReassignments.TryGetValue(task.LaneId, out var fallbackLaneId))
                    {
                        if (!string.IsNullOrWhiteSpace(fallbackLaneId) && validLaneIds.Contains(fallbackLaneId))
                        {
                            task.LaneId = fallbackLaneId;
                        }
                        else
                        {
                            task.LaneId = null;
                        }

                        continue;
                    }

                    if (!validLaneIds.Contains(task.LaneId))
                    {
                        task.LaneId = null;
                    }
                }
            }
        }

        private static void ApplyPolicyEdits(FlowKanbanData board, BoardEditState editState)
        {
            foreach (var column in board.Columns)
            {
                if (editState.ColumnPolicies.TryGetValue(column.Id, out var policy))
                {
                    column.PolicyText = string.IsNullOrWhiteSpace(policy) ? null : policy;
                }
            }
        }

        private static void ApplyBoardTags(FlowKanbanData board, IEnumerable<string> selectedTags)
        {
            board.Tags ??= new ObservableCollection<string>();
            board.Tags.Clear();

            foreach (var tag in selectedTags)
            {
                var trimmed = tag?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                board.Tags.Add(trimmed);
            }
        }

        private static string? ResolveDefaultDoneColumnId(FlowKanbanData board)
        {
            if (board == null || board.Columns.Count == 0)
                return null;

            if (!string.IsNullOrWhiteSpace(board.DoneColumnId))
            {
                foreach (var column in board.Columns)
                {
                    if (string.Equals(column.Id, board.DoneColumnId, StringComparison.Ordinal))
                        return board.DoneColumnId;
                }
            }

            var doneTitle = FloweryLocalization.GetString("Kanban_Column_Done", "Done");
            foreach (var column in board.Columns)
            {
                if (string.Equals(column.Id, board.ArchiveColumnId, StringComparison.Ordinal))
                    continue;

                if (!string.IsNullOrWhiteSpace(column.Title)
                    && string.Equals(column.Title.Trim(), doneTitle, StringComparison.OrdinalIgnoreCase))
                {
                    return column.Id;
                }
            }

            foreach (var column in board.Columns)
            {
                if (!string.Equals(column.Id, board.ArchiveColumnId, StringComparison.Ordinal))
                    return column.Id;
            }

            return null;
        }

        private static Control CreateEditorContent(
            FlowKanbanData board,
            BoardEditState editState,
            TopLevel xamlRoot,
            bool isNewBoard,
            out DaisyInput titleBox,
            out DaisyTextArea descriptionBox,
            out DaisyInput createdByBox,
            out DaisyButton saveButton,
            out DaisyButton cancelButton)
        {
            var minContentHeight = Math.Max(
                0,
                FlowKanbanDialogBase.AbsoluteMinHeight
                - FlowKanbanDialogBase.OverlayChromePadding
                - FlowKanbanDialogBase.DialogMargin);

            var outerContainer = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                MinHeight = minContentHeight,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = new GridLength(1, GridUnitType.Star) },
                    new RowDefinition { Height = GridLength.Auto }
                }
            };

            // var headerStack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 12) };
            // headerStack.Children.Add(new TextBlock
            // {
            //     Text = FloweryLocalization.GetString("Kanban_Board_EditTitle"),
            //     FontSize = 20,
            //     FontWeight = FontWeights.Bold,
            //     Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            // });
            // headerStack.Children.Add(new TextBlock
            // {
            //     Text = FloweryLocalization.GetString("Kanban_Board_EditSubtitle"),
            //     FontSize = 12,
            //     Opacity = 0.7,
            //     Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            // });
            // Grid.SetRow(headerStack, 0);
            // outerContainer.Children.Add(headerStack);

            Control WrapTabContent(Control content)
            {
                return new ScrollViewer
                {
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                    Content = FlowKanbanDialogBase.ReserveVerticalScrollBarSpace(content)
                };
            }

            StackPanel CreateTabStack()
            {
                return new StackPanel
                {
                    Spacing = 16,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    Margin = new Thickness(0, 12, 16, 0)
                };
            }

            var generalStack = CreateTabStack();

            var titleLabel = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Board_Title"));
            generalStack.Children.Add(titleLabel);
            titleBox = new DaisyInput
            {
                Text = editState.Title,
                Variant = DaisyInputVariant.Bordered,
                // Size = DaisySize.Medium,
                PlaceholderText = FloweryLocalization.GetString("Kanban_Board_TitlePlaceholder"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(titleBox, titleLabel);
            generalStack.Children.Add(titleBox);

            var descriptionLabel = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Board_Description"));
            generalStack.Children.Add(descriptionLabel);
            descriptionBox = new DaisyTextArea
            {
                Text = editState.Description,
                Variant = DaisyInputVariant.Bordered,
                PlaceholderText = FloweryLocalization.GetString("Kanban_Board_DescriptionPlaceholder"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            FlowKanbanControlFactory.SetTextAreaRows(descriptionBox, 2, 2);
            AutomationProperties.SetLabeledBy(descriptionBox, descriptionLabel);
            generalStack.Children.Add(descriptionBox);

            if (isNewBoard)
            {
                generalStack.Children.Add(CreateDemoDataToggleRow(editState));
            }

            generalStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_Lanes_Title")));
            generalStack.Children.Add(CreateLaneManagementSection(editState, xamlRoot));

            var tagsLabel = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Tags"));
            generalStack.Children.Add(tagsLabel);
            generalStack.Children.Add(CreateTagsSection(board, editState, tagsLabel));

            var cardAgingStack = CreateTabStack();
            cardAgingStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_DoneAging_Title")));
            cardAgingStack.Children.Add(CreateDoneAgingSection(board, editState));

            var policiesStack = CreateTabStack();
            policiesStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_Policies_Title")));
            policiesStack.Children.Add(CreateColumnPolicySection(board, editState));

            var createdStack = CreateTabStack();
            var createdByLabel = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Board_CreatedBy"));
            createdStack.Children.Add(createdByLabel);
            createdByBox = new DaisyInput
            {
                Text = editState.CreatedBy,
                Variant = DaisyInputVariant.Bordered,
                // Size = DaisySize.Small,
                PlaceholderText = FloweryLocalization.GetString("Kanban_Board_CreatedByPlaceholder"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(createdByBox, createdByLabel);
            createdStack.Children.Add(createdByBox);

            if (!isNewBoard)
            {
                createdStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_Board_CreatedAt")));
                var createdAtText = new TextBlock
                {
                    Text = editState.CreatedAt.ToString("g"),
                    FontSize = 13,
                    Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
                    Opacity = 0.8
                };
                createdStack.Children.Add(createdAtText);

                createdStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_Board_Id")));
                var idText = new TextBlock
                {
                    Text = board.Id,
                    FontSize = 11,
                    FontFamily = new FontFamily("Consolas, Courier New"),
                    Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
                    Opacity = 0.6,
                    TextWrapping = TextWrapping.Wrap
                };
                createdStack.Children.Add(idText);
            }

            var tabSize = IsMobilePlatform() ? DaisySize.Small : DaisySize.Medium;
            var tabs = new DaisyTabs
            {
                Variant = DaisyTabVariant.Boxed,
                // Size = tabSize,
                TabWidthMode = DaisyTabWidthMode.Equal,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch
            };

            FlowKanbanControlFactory.AddTab(
                tabs,
                FloweryLocalization.GetString("Kanban_Board_EditTitle"),
                WrapTabContent(generalStack));
            FlowKanbanControlFactory.AddTab(
                tabs,
                FloweryLocalization.GetString("Kanban_DoneAging_Title"),
                WrapTabContent(cardAgingStack));
            FlowKanbanControlFactory.AddTab(
                tabs,
                FloweryLocalization.GetString("Kanban_Policies_Title"),
                WrapTabContent(policiesStack));
            FlowKanbanControlFactory.AddTab(
                tabs,
                FloweryLocalization.GetString("Kanban_Board_CreatedAt"),
                WrapTabContent(createdStack));
            Grid.SetRow(tabs, 1);
            outerContainer.Children.Add(tabs);

            var sectionBackground = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
            var sectionBorderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush");
            var buttonPanel = FlowKanbanDialogBase.CreateStandardButtonFooter(
                out saveButton,
                out cancelButton);
            buttonPanel.Margin = new Thickness(0, 0, 48, 0);

            var footerCard = new Border
            {
                Background = sectionBackground,
                BorderBrush = sectionBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Margin = new Thickness(0, 12, 0, 0),
                Child = buttonPanel
            };

            Grid.SetRow(footerCard, 2);
            outerContainer.Children.Add(footerCard);

            return outerContainer;
        }

        private static bool IsMobilePlatform()
        {
            return OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
        }

        private static Control CreateLaneManagementSection(BoardEditState editState, TopLevel xamlRoot)
        {
            var section = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            section.Children.Add(CreateSwimlaneToggleRow(editState));

            var addRow = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            var addInput = new DaisyInput
            {
                PlaceholderText = FloweryLocalization.GetString("Kanban_Lanes_AddPlaceholder"),
                Variant = DaisyInputVariant.Bordered,
                // Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetName(addInput, FloweryLocalization.GetString("Kanban_Lanes_AddPlaceholder"));

            var addButton = new DaisyButton
            {
                Content = new DaisyIconText
                {
                    IconData = FlowKanbanControlFactory.GetIconGeometry("DaisyIconPlus"),
                    Text = FloweryLocalization.GetString("Kanban_Lanes_AddButton"),
                    IconPlacement = IconPlacement.Left,
                    IconSize = 14
                },
                Variant = DaisyButtonVariant.Primary,
                // Size = DaisySize.Small,
                MinWidth = 96
            };
            AutomationProperties.SetName(addButton, FloweryLocalization.GetString("Kanban_Lanes_AddButton"));

            Grid.SetColumn(addInput, 0);
            Grid.SetColumn(addButton, 1);
            addRow.Children.Add(addInput);
            addRow.Children.Add(addButton);
            section.Children.Add(addRow);

            var emptyText = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Lanes_Empty"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
                Opacity = 0.7
            };
            section.Children.Add(emptyText);

            var lanesPanel = new StackPanel { Spacing = 8 };
            section.Children.Add(lanesPanel);

            var context = new LaneEditorContext(editState, lanesPanel, emptyText, addInput, xamlRoot);
            addButton.Tag = context;
            addButton.Click += OnAddLaneClicked;
            addInput.Tag = context;
            addInput.KeyDown += OnAddLaneInputKeyDown;

            RebuildLaneList(context);

            return section;
        }

        private static Control CreateColumnPolicySection(FlowKanbanData board, BoardEditState editState)
        {
            var section = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (board.Columns.Count == 0)
            {
                section.Children.Add(new TextBlock
                {
                    Text = FloweryLocalization.GetString("Kanban_Policies_Empty"),
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
                    Opacity = 0.7
                });
                return section;
            }

            foreach (var column in board.Columns)
            {
                var columnStack = new StackPanel { Spacing = 6 };
                var columnLabel = new TextBlock
                {
                    Text = column.Title,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
                };
                columnStack.Children.Add(columnLabel);

                editState.ColumnPolicies.TryGetValue(column.Id, out var existingPolicy);
                var policyBox = new DaisyTextArea
                {
                    Text = existingPolicy ?? column.PolicyText ?? string.Empty,
                    Variant = DaisyInputVariant.Bordered,
                    PlaceholderText = FloweryLocalization.GetString("Kanban_Policies_Placeholder"),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                FlowKanbanControlFactory.SetTextAreaRows(policyBox, 2, 4);
                AutomationProperties.SetLabeledBy(policyBox, columnLabel);
                policyBox.TextChanged += (s, e) =>
                {
                    editState.ColumnPolicies[column.Id] = string.IsNullOrWhiteSpace(policyBox.Text)
                        ? null
                        : policyBox.Text;
                };

                columnStack.Children.Add(policyBox);
                section.Children.Add(columnStack);
            }

            return section;
        }

        private static Control CreateDoneAgingSection(FlowKanbanData board, BoardEditState editState)
        {
            var section = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            if (editState.AutoArchiveDoneEnabled && string.IsNullOrWhiteSpace(editState.DoneColumnId))
            {
                editState.DoneColumnId = ResolveDefaultDoneColumnId(board);
            }

            var toggleRow = new Grid
            {
                ColumnSpacing = 12,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            var toggleLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_DoneAging_Enable"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            };
            Grid.SetColumn(toggleLabel, 0);
            toggleRow.Children.Add(toggleLabel);

            var toggle = new DaisyToggle
            {
                IsChecked = editState.AutoArchiveDoneEnabled,
                Variant = DaisyToggleVariant.Success,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetLabeledBy(toggle, toggleLabel);
            Grid.SetColumn(toggle, 1);
            toggleRow.Children.Add(toggle);
            section.Children.Add(toggleRow);

            section.Children.Add(new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_DoneAging_Description"),
                TextWrapping = TextWrapping.Wrap,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
                Opacity = 0.7
            });

            var columnStack = new StackPanel { Spacing = 6 };
            var columnLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_DoneAging_ColumnLabel"),
                FontSize = 11,
                Opacity = 0.7,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            };
            var columnSelect = new DaisySelect
            {
                PlaceholderText = FloweryLocalization.GetString("Select_Placeholder"),
                // Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var noneItem = new FlowKanbanSelectOption<string?>(
                FloweryLocalization.GetString("Kanban_DoneAging_NoColumnOption"),
                null);
            columnSelect.Items.Add(noneItem);

            foreach (var column in board.Columns)
            {
                if (string.Equals(column.Id, board.ArchiveColumnId, StringComparison.Ordinal))
                    continue;

                columnSelect.Items.Add(
                    new FlowKanbanSelectOption<string?>(column.Title, column.Id));
            }

            SetDoneColumnSelection(columnSelect, editState.DoneColumnId);
            columnSelect.SelectionChanged += (s, e) =>
            {
                editState.DoneColumnId = GetSelectedDoneColumnId(columnSelect);
            };
            AutomationProperties.SetLabeledBy(columnSelect, columnLabel);
            columnStack.Children.Add(columnLabel);
            columnStack.Children.Add(columnSelect);
            section.Children.Add(columnStack);

            var daysStack = new StackPanel { Spacing = 6 };
            var daysLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_DoneAging_DaysLabel"),
                FontSize = 11,
                Opacity = 0.7,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            };
            var daysInput = new DaisyNumericUpDown
            {
                Value = editState.AutoArchiveDoneDays > 0 ? editState.AutoArchiveDoneDays : 14,
                Minimum = 1,
                Maximum = 3650,
                Increment = 1,
                MaxDecimalPlaces = 0,
                // Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            daysInput.LostFocus += (s, e) =>
            {
                var value = daysInput.Value.HasValue ? (int)daysInput.Value.Value : editState.AutoArchiveDoneDays;
                editState.AutoArchiveDoneDays = Math.Max(1, value);
                daysInput.Value = editState.AutoArchiveDoneDays;
            };
            AutomationProperties.SetLabeledBy(daysInput, daysLabel);
            daysStack.Children.Add(daysLabel);
            daysStack.Children.Add(daysInput);
            section.Children.Add(daysStack);

            columnSelect.IsEnabled = editState.AutoArchiveDoneEnabled;
            daysInput.IsEnabled = editState.AutoArchiveDoneEnabled;

            toggle.IsCheckedChanged += (s, e) =>
            {
                editState.AutoArchiveDoneEnabled = toggle.IsChecked == true;
                if (toggle.IsChecked == true && string.IsNullOrWhiteSpace(editState.DoneColumnId))
                {
                    editState.DoneColumnId = ResolveDefaultDoneColumnId(board);
                    SetDoneColumnSelection(columnSelect, editState.DoneColumnId);
                }

                columnSelect.IsEnabled = editState.AutoArchiveDoneEnabled;
                daysInput.IsEnabled = editState.AutoArchiveDoneEnabled;
            };

            return section;
        }

        private static Control CreateTagsSection(
            FlowKanbanData board,
            BoardEditState editState,
            TextBlock tagsLabel)
        {
            var section = new StackPanel
            {
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var tagLibrary = editState.Tags;

            var tagPicker = new DaisyTagPicker
            {
                Tags = tagLibrary,
                Title = FloweryLocalization.GetString("Kanban_Tags_SelectedTitle"),
                // Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(tagPicker, tagsLabel);

            var tagInput = new DaisyInput
            {
                PlaceholderText = FloweryLocalization.GetString("Kanban_Tags_AddPlaceholder"),
                Variant = DaisyInputVariant.Bordered,
                // Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetName(tagInput, FloweryLocalization.GetString("Kanban_Tags_AddPlaceholder"));

            var addButton = new DaisyButton
            {
                Content = FloweryLocalization.GetString("Common_Add"),
                Variant = DaisyButtonVariant.Primary,
                // Size = DaisySize.Small,
                MinWidth = 80
            };
            AutomationProperties.SetName(addButton, FloweryLocalization.GetString("Common_Add"));

            bool ContainsTag(ObservableCollection<string> list, string candidate)
            {
                foreach (var item in list)
                {
                    if (string.Equals(item, candidate, StringComparison.OrdinalIgnoreCase))
                        return true;
                }

                return false;
            }

            void AddTagFromInput()
            {
                var text = tagInput.Text?.Trim();
                if (string.IsNullOrWhiteSpace(text))
                    return;

                if (!ContainsTag(tagLibrary, text))
                {
                    tagLibrary.Add(text);
                }

                tagInput.Text = string.Empty;
            }

            addButton.Click += (s, e) => AddTagFromInput();
            tagInput.KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    AddTagFromInput();
                    e.Handled = true;
                }
            };

            var addRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };

            Grid.SetColumn(tagInput, 0);
            Grid.SetColumn(addButton, 1);
            addRow.Children.Add(tagInput);
            addRow.Children.Add(addButton);

            section.Children.Add(addRow);
            section.Children.Add(tagPicker);

#if DEBUG
            var importButton = new DaisyButton
            {
                Content = "Import from Cards",
                Variant = DaisyButtonVariant.Secondary,
                // Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Left,
                MinWidth = 140
            };
            importButton.Click += (_, _) => ImportTagsFromCards(board, tagLibrary);
            section.Children.Add(importButton);
#endif

            return section;
        }

        private static Control CreateDemoDataToggleRow(BoardEditState editState)
        {
            var row = new Grid
            {
                ColumnSpacing = 12,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            var labelText = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Board_AddDemoData"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            };
            Grid.SetColumn(labelText, 0);
            row.Children.Add(labelText);

            var toggle = new DaisyToggle
            {
                IsChecked = editState.IncludeDemoData,
                Variant = DaisyToggleVariant.Success,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = editState
            };
            AutomationProperties.SetLabeledBy(toggle, labelText);
            toggle.IsCheckedChanged += OnDemoDataToggleChanged;
            Grid.SetColumn(toggle, 1);
            row.Children.Add(toggle);

            return row;
        }

        private static void OnDemoDataToggleChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is not DaisyToggle toggle || toggle.Tag is not BoardEditState editState)
                return;

            editState.IncludeDemoData = toggle.IsChecked == true;
        }

        private static ObservableCollection<string> BuildAvailableTags(
            FlowKanbanData board,
            IEnumerable<string> selectedTags)
        {
            var tags = new ObservableCollection<string>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddTag(string? tag)
            {
                var trimmed = tag?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    return;

                if (seen.Add(trimmed))
                    tags.Add(trimmed);
            }

            foreach (var tag in board.Tags)
            {
                AddTag(tag);
            }

            foreach (var column in board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    if (string.IsNullOrWhiteSpace(task.Tags))
                        continue;

                    var parts = task.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        AddTag(part);
                    }
                }
            }

            foreach (var tag in selectedTags)
            {
                AddTag(tag);
            }

            return tags;
        }

#if DEBUG
        private static void ImportTagsFromCards(FlowKanbanData board, ObservableCollection<string> tagLibrary)
        {
            if (board == null)
                return;

            var seen = new HashSet<string>(tagLibrary, StringComparer.OrdinalIgnoreCase);
            foreach (var column in board.Columns)
            {
                foreach (var task in column.Tasks)
                {
                    if (string.IsNullOrWhiteSpace(task.Tags))
                        continue;

                    var parts = task.Tags.Split(',', StringSplitOptions.RemoveEmptyEntries);
                    foreach (var part in parts)
                    {
                        var trimmed = part.Trim();
                        if (string.IsNullOrWhiteSpace(trimmed))
                            continue;

                        if (seen.Add(trimmed))
                            tagLibrary.Add(trimmed);
                    }
                }
            }
        }
#endif

        private static Control CreateSwimlaneToggleRow(BoardEditState editState)
        {
            var row = new Grid
            {
                ColumnSpacing = 12,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            var labelText = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Settings_EnableSwimlanes"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            };
            Grid.SetColumn(labelText, 0);
            row.Children.Add(labelText);

            var toggle = new DaisyToggle
            {
                IsChecked = editState.EnableSwimlanes,
                Variant = DaisyToggleVariant.Success,
                VerticalAlignment = VerticalAlignment.Center,
                Tag = editState
            };
            AutomationProperties.SetLabeledBy(toggle, labelText);
            toggle.IsCheckedChanged += OnSwimlaneToggleChanged;
            Grid.SetColumn(toggle, 1);
            row.Children.Add(toggle);

            return row;
        }

        private static void OnSwimlaneToggleChanged(object? sender, RoutedEventArgs e)
        {
            if (sender is not DaisyToggle toggle || toggle.Tag is not BoardEditState editState)
                return;

            editState.EnableSwimlanes = toggle.IsChecked == true;
        }

        private static void OnAddLaneInputKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (sender is not DaisyInput input || input.Tag is not LaneEditorContext context)
                return;

            TryAddLane(context);
            e.Handled = true;
        }

        private static void OnAddLaneClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is not DaisyButton button || button.Tag is not LaneEditorContext context)
                return;

            TryAddLane(context);
        }

        private static void TryAddLane(LaneEditorContext context)
        {
            var text = context.AddLaneInput.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
                return;

            context.EditState.Lanes.Add(new FlowKanbanLane { Title = text });
            context.AddLaneInput.Text = string.Empty;
            RebuildLaneList(context);
        }

        private static void RebuildLaneList(LaneEditorContext context)
        {
            context.LanesPanel.Children.Clear();

            if (context.EditState.Lanes.Count == 0)
            {
                context.EmptyText.IsVisible = true;
                return;
            }

            context.EmptyText.IsVisible = false;

            var laneCount = context.EditState.Lanes.Count;
            for (int i = 0; i < laneCount; i++)
            {
                var lane = context.EditState.Lanes[i];
                context.LanesPanel.Children.Add(CreateLaneRow(context, lane, i, laneCount));
            }
        }

        private static Control CreateLaneRow(
            LaneEditorContext context,
            FlowKanbanLane lane,
            int index,
            int totalCount)
        {
            var row = new Grid
            {
                ColumnSpacing = 8,
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                }
            };

            var titleInput = new DaisyInput
            {
                Text = lane.Title,
                PlaceholderText = FloweryLocalization.GetString("Kanban_Lanes_AddPlaceholder"),
                Variant = DaisyInputVariant.Bordered,
                // Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Tag = lane
            };
            AutomationProperties.SetName(
                titleInput,
                string.IsNullOrWhiteSpace(lane.Title)
                    ? FloweryLocalization.GetString("Kanban_Lanes_AddPlaceholder")
                    : lane.Title);
            titleInput.LostFocus += OnLaneTitleLostFocus;
            Grid.SetColumn(titleInput, 0);
            row.Children.Add(titleInput);

            var actionsPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 4,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var moveUpButton = CreateLaneActionButton(
                "DaisyIconChevronUp",
                FloweryLocalization.GetString("Kanban_Lanes_MoveUp"),
                DaisyButtonVariant.Default);
            moveUpButton.IsEnabled = index > 0;
            moveUpButton.Tag = new LaneMoveContext(context, lane, -1);
            moveUpButton.Click += OnLaneMoveClicked;
            actionsPanel.Children.Add(moveUpButton);

            var moveDownButton = CreateLaneActionButton(
                "DaisyIconChevronDown",
                FloweryLocalization.GetString("Kanban_Lanes_MoveDown"),
                DaisyButtonVariant.Default);
            moveDownButton.IsEnabled = index < totalCount - 1;
            moveDownButton.Tag = new LaneMoveContext(context, lane, 1);
            moveDownButton.Click += OnLaneMoveClicked;
            actionsPanel.Children.Add(moveDownButton);

            var deleteButton = CreateLaneActionButton(
                "DaisyIconDelete",
                FloweryLocalization.GetString("Kanban_Lanes_Delete"),
                DaisyButtonVariant.Default);
            deleteButton.Tag = new LaneActionContext(context, lane);
            deleteButton.Opacity = 0.8;
            deleteButton.Click += OnLaneDeleteClicked;
            actionsPanel.Children.Add(deleteButton);

            Grid.SetColumn(actionsPanel, 1);
            row.Children.Add(actionsPanel);

            return row;
        }

        private static DaisyButton CreateLaneActionButton(
            string iconKey,
            string automationName,
            DaisyButtonVariant variant)
        {
            var button = new DaisyButton
            {
                Content = new DaisyIconText
                {
                    IconData = FlowKanbanControlFactory.GetIconGeometry(iconKey),
                    IconSize = 14
                },
                Variant = variant,
                // Size = DaisySize.ExtraSmall,
                MinWidth = 32,
                MinHeight = 32
            };

            AutomationProperties.SetName(button, automationName);
            return button;
        }

        private static void OnLaneTitleLostFocus(object? sender, RoutedEventArgs e)
        {
            if (sender is not DaisyInput input || input.Tag is not FlowKanbanLane lane)
                return;

            var text = input.Text?.Trim();
            if (string.IsNullOrWhiteSpace(text))
            {
                input.Text = lane.Title;
                return;
            }

            lane.Title = text;
        }

        private static void OnLaneMoveClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is not DaisyButton button || button.Tag is not LaneMoveContext context)
                return;

            var lanes = context.Editor.EditState.Lanes;
            var currentIndex = lanes.IndexOf(context.Lane);
            if (currentIndex < 0)
                return;

            var newIndex = Math.Clamp(currentIndex + context.Delta, 0, lanes.Count - 1);
            if (newIndex == currentIndex)
                return;

            lanes.RemoveAt(currentIndex);
            lanes.Insert(newIndex, context.Lane);
            RebuildLaneList(context.Editor);
        }

        private static async void OnLaneDeleteClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is not DaisyButton button || button.Tag is not LaneActionContext context)
                return;

            var remaining = new List<FlowKanbanLane>();
            foreach (var lane in context.Editor.EditState.Lanes)
            {
                if (!ReferenceEquals(lane, context.Lane))
                {
                    remaining.Add(lane);
                }
            }

            var decision = await ShowLaneDeleteDialogAsync(
                context.Editor.TopLevel,
                remaining);

            if (!decision.Confirmed)
                return;

            context.Editor.EditState.LaneReassignments[context.Lane.Id] = decision.FallbackLaneId;
            context.Editor.EditState.Lanes.Remove(context.Lane);
            RebuildLaneList(context.Editor);
        }

        private static async Task<LaneDeleteDecision> ShowLaneDeleteDialogAsync(
            TopLevel xamlRoot,
            IReadOnlyList<FlowKanbanLane> remainingLanes)
        {
            return await LaneDeleteDialog.ShowAsync(xamlRoot, remainingLanes);
        }

        private static string? GetSelectedFallbackLaneId(DaisySelect? select)
        {
            if (select?.SelectedItem is FlowKanbanSelectOption<string?> { Value: { } laneId })
            {
                return string.IsNullOrWhiteSpace(laneId) ? null : laneId;
            }

            return null;
        }

        private static string? GetSelectedDoneColumnId(DaisySelect select)
        {
            if (select.SelectedItem is FlowKanbanSelectOption<string?> { Value: { } columnId })
            {
                return string.IsNullOrWhiteSpace(columnId) ? null : columnId;
            }

            return null;
        }

        private static void SetDoneColumnSelection(DaisySelect select, string? doneColumnId)
        {
            if (!string.IsNullOrWhiteSpace(doneColumnId))
            {
                foreach (var entry in select.Items)
                {
                    if (entry is FlowKanbanSelectOption<string?> { Value: { } columnId } item
                        && string.Equals(columnId, doneColumnId, StringComparison.Ordinal))
                    {
                        select.SelectedItem = item;
                        return;
                    }
                }
            }

            if (select.Items.Count > 0)
            {
                select.SelectedIndex = 0;
            }
        }

        private static TextBlock CreateSectionLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 12,
                FontWeight = FontWeights.SemiBold,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
                Opacity = 0.85
            };
        }
    }
}
