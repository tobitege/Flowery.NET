using System.Collections.ObjectModel;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Modal dialog for editing task cards.
    /// Uses DaisyTabs to organize FlowTask attributes into tabbed sections.
    /// Leverages FlowKanbanDialogBase for smart sizing and theming.
    /// </summary>
    public sealed partial class FlowTaskEditorDialog : FlowKanbanDialogBase
    {
        private const int MaxBlockedProgress = 99;

        private static readonly DaisyColor[] ColorSwatches =
        [
            DaisyColor.Error,
            DaisyColor.Warning,
            DaisyColor.Secondary,
            DaisyColor.Accent,
            DaisyColor.Success,
            DaisyColor.Info,
            DaisyColor.Primary,
            DaisyColor.Neutral,
            DaisyColor.Default
        ];

        private sealed class TaskEditState
        {
            public DaisyColor Palette { get; set; }
            public FlowTaskPriority Priority { get; set; }
            public int ProgressPercent { get; set; }

            public DateTime? PlannedStartDate { get; set; }
            public DateTime? PlannedEndDate { get; set; }
            public DateTime? ActualStartDate { get; set; }
            public DateTime? ActualEndDate { get; set; }

            public double? EstimatedHours { get; set; }
            public double? ActualHours { get; set; }
            public double? EstimatedDays { get; set; }
            public double? ActualDays { get; set; }

            public string? Assignee { get; set; }
            public string? AssigneeId { get; set; }
            public string? Tags { get; set; }
            public bool IsBlocked { get; set; }
            public string? BlockedReason { get; set; }
            public DateTime? BlockedSince { get; set; }
        }

        private readonly FlowTask _task;
        private readonly TaskEditState _editState;
        private readonly System.Collections.Generic.List<FlowSubtask> _editSubtasks;
        private readonly TopLevel _xamlRoot;
        private readonly TaskCompletionSource<bool> _tcs = new();
        private bool _isClosing;

        private DaisyInput _titleBox = null!;
        private DaisyTextArea _descriptionBox = null!;
        private DaisyButton _saveButton = null!;
        private DaisyButton _cancelButton = null!;

        // Parameterless constructor for runtime control construction tests.
        public FlowTaskEditorDialog()
        {
            AutomationProperties.SetName(
                this,
                FloweryLocalization.GetString("Kanban_Editor_EditTask"));
            _task = new FlowTask();
            _xamlRoot = null!;
            _editState = new TaskEditState
            {
                Palette = _task.Palette,
                Priority = _task.Priority,
                ProgressPercent = _task.ProgressPercent,
                PlannedStartDate = _task.PlannedStartDate,
                PlannedEndDate = _task.PlannedEndDate,
                ActualStartDate = _task.ActualStartDate,
                ActualEndDate = _task.ActualEndDate,
                EstimatedHours = _task.EstimatedHours,
                ActualHours = _task.ActualHours,
                EstimatedDays = _task.EstimatedDays,
                ActualDays = _task.ActualDays,
                Assignee = _task.Assignee,
                AssigneeId = _task.AssigneeId,
                Tags = _task.Tags,
                IsBlocked = _task.IsBlocked,
                BlockedReason = _task.BlockedReason,
                BlockedSince = _task.BlockedSince
            };
            _editSubtasks = new System.Collections.Generic.List<FlowSubtask>();

            Content = new Grid();
        }

        private FlowTaskEditorDialog(
            FlowTask task,
            TopLevel xamlRoot,
            System.Collections.Generic.IEnumerable<string>? availableTags,
            System.Collections.Generic.IEnumerable<FlowTaskAssigneeOption>? availableAssignees,
            bool autoExpandSections)
        {
            AutomationProperties.SetName(
                this,
                FloweryLocalization.GetString("Kanban_Editor_EditTask"));
            _task = task;
            _xamlRoot = xamlRoot;

            _editState = new TaskEditState
            {
                Palette = task.Palette,
                Priority = task.Priority,
                ProgressPercent = task.ProgressPercent,
                PlannedStartDate = task.PlannedStartDate,
                PlannedEndDate = task.PlannedEndDate,
                ActualStartDate = task.ActualStartDate,
                ActualEndDate = task.ActualEndDate,
                EstimatedHours = task.EstimatedHours,
                ActualHours = task.ActualHours,
                EstimatedDays = task.EstimatedDays,
                ActualDays = task.ActualDays,
                Assignee = task.Assignee,
                AssigneeId = task.AssigneeId,
                Tags = task.Tags,
                IsBlocked = task.IsBlocked,
                BlockedReason = task.BlockedReason,
                BlockedSince = task.BlockedSince
            };

            _editSubtasks = task.Subtasks.Select(s => new FlowSubtask
            {
                Id = s.Id,
                Title = s.Title,
                IsCompleted = s.IsCompleted
            }).ToList();

            Content = CreateEditorContent(
                task,
                _editState,
                _editSubtasks,
                availableTags,
                availableAssignees,
                autoExpandSections,
                xamlRoot,
                out _titleBox,
                out _descriptionBox,
                out _saveButton,
                out _cancelButton);

            IsDraggable = true;
            IsDialogResizingEnabled = true;
            ApplySmartSizing(xamlRoot);

            _saveButton.Click += OnSaveClicked;
            _cancelButton.Click += OnCancelClicked;
        }

        public static Task<bool> ShowAsync(FlowTask task, TopLevel xamlRoot)
        {
            return ShowAsyncWithAssignees(task, xamlRoot, null, null, true);
        }

        public static Task<bool> ShowAsync(
            FlowTask task,
            TopLevel xamlRoot,
            System.Collections.Generic.IEnumerable<string>? availableTags,
            bool autoExpandSections = true)
        {
            return ShowAsyncWithAssignees(task, xamlRoot, availableTags, null, autoExpandSections);
        }

        public static Task<bool> ShowAsync(
            FlowTask task,
            TopLevel xamlRoot,
            System.Collections.Generic.IEnumerable<string>? availableTags,
            System.Collections.Generic.IEnumerable<string>? availableAssignees,
            bool autoExpandSections = true)
        {
            var options = BuildAssigneeOptionsFromNames(availableAssignees);
            return ShowAsyncWithAssignees(task, xamlRoot, availableTags, options, autoExpandSections);
        }

        public static Task<bool> ShowAsyncWithAssignees(
            FlowTask task,
            TopLevel xamlRoot,
            System.Collections.Generic.IEnumerable<string>? availableTags,
            System.Collections.Generic.IEnumerable<FlowTaskAssigneeOption>? availableAssignees,
            bool autoExpandSections = true)
        {
            if (task == null || xamlRoot == null)
                return Task.FromResult(false);

            var dialog = new FlowTaskEditorDialog(task, xamlRoot, availableTags, availableAssignees, autoExpandSections);
            return dialog.ShowInternalAsync();
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
            _task.Title = _titleBox.Text?.Trim() ?? _task.Title;
            _task.Description = _descriptionBox.Text?.Trim() ?? "";
            _task.Palette = _editState.Palette;
            _task.Priority = _editState.Priority;
            if (_editState.IsBlocked && _editState.ProgressPercent >= 100)
            {
                _editState.ProgressPercent = MaxBlockedProgress;
            }
            _task.ProgressPercent = _editState.ProgressPercent;

            _task.PlannedStartDate = _editState.PlannedStartDate;
            _task.PlannedEndDate = _editState.PlannedEndDate;
            _task.ActualStartDate = _editState.ActualStartDate;
            _task.ActualEndDate = _editState.ActualEndDate;

            _task.EstimatedHours = _editState.EstimatedHours;
            _task.ActualHours = _editState.ActualHours;
            _task.EstimatedDays = _editState.EstimatedDays;
            _task.ActualDays = _editState.ActualDays;

            _task.Assignee = _editState.Assignee;
            _task.AssigneeId = _editState.AssigneeId;
            _task.Tags = _editState.Tags;
            if (_editState.IsBlocked)
            {
                _task.IsBlocked = true;
                _task.BlockedReason = string.IsNullOrWhiteSpace(_editState.BlockedReason)
                    ? null
                    : _editState.BlockedReason;
                if (_editState.BlockedSince.HasValue)
                {
                    _task.BlockedSince = _editState.BlockedSince;
                }
            }
            else
            {
                _task.IsBlocked = false;
            }

            _task.Subtasks.Clear();
            foreach (var subtask in _editSubtasks)
            {
                _task.Subtasks.Add(subtask);
            }

            Close(result: true);
        }

        private void OnCancelClicked(object? sender, RoutedEventArgs e)
        {
            Close(result: false);
        }

        protected override void OnDialogOpenChanged(bool isOpen)
        {
            if (!isOpen && !_isClosing)
            {
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

        private static Control CreateEditorContent(
            FlowTask task,
            TaskEditState editState,
            System.Collections.Generic.List<FlowSubtask> subtasks,
            System.Collections.Generic.IEnumerable<string>? availableTags,
            System.Collections.Generic.IEnumerable<FlowTaskAssigneeOption>? availableAssignees,
            bool autoExpandSections,
            TopLevel xamlRoot,
            out DaisyInput titleBox,
            out DaisyTextArea descriptionBox,
            out DaisyButton saveButton,
            out DaisyButton cancelButton)
        {
            _ = autoExpandSections;
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

            var headerStack = new StackPanel { Spacing = 4, Margin = new Thickness(0, 0, 0, 12) };
            headerStack.Children.Add(new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Editor_Subtitle"),
                FontSize = 12,
                Opacity = 0.7,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            });
            Grid.SetRow(headerStack, 0);
            outerContainer.Children.Add(headerStack);

            var tabs = new DaisyTabs
            {
                Variant = DaisyTabVariant.Boxed,
                Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            var overviewContent = CreateOverviewTabContent(task, editState, availableAssignees, out titleBox, out descriptionBox);
            FlowKanbanControlFactory.AddTab(
                tabs,
                FloweryLocalization.GetString("Kanban_Metrics_Overview"),
                WrapTabContent(overviewContent));

            FlowKanbanControlFactory.AddTab(
                tabs,
                FloweryLocalization.GetString("Kanban_Editor_SectionDetails"),
                WrapTabContent(CreateStatusTabContent(editState, availableTags)));

            FlowKanbanControlFactory.AddTab(
                tabs,
                FloweryLocalization.GetString("Kanban_Editor_Subtasks"),
                WrapTabContent(CreateSubtasksTabContent(subtasks)));

            FlowKanbanControlFactory.AddTab(
                tabs,
                FloweryLocalization.GetString("Kanban_Editor_SectionEffort"),
                WrapTabContent(CreateEffortContent(editState)));

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

        private static Control WrapTabContent(Control content)
        {
            return new ScrollViewer
            {
                Content = FlowKanbanDialogBase.ReserveVerticalScrollBarSpace(content),
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 8, 16, 0)
            };
        }

        private static Control CreateOverviewTabContent(
            FlowTask task,
            TaskEditState editState,
            System.Collections.Generic.IEnumerable<FlowTaskAssigneeOption>? availableAssignees,
            out DaisyInput titleBox,
            out DaisyTextArea descriptionBox)
        {
            var contentStack = new StackPanel { Spacing = 12 };

            titleBox = new DaisyInput
            {
                Text = task.Title,
                Variant = DaisyInputVariant.Bordered,
                Size = DaisySize.Large,
                PlaceholderText = FloweryLocalization.GetString("Kanban_Editor_TitlePlaceholder"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetName(
                titleBox,
                FloweryLocalization.GetString("Kanban_Editor_TitlePlaceholder"));
            contentStack.Children.Add(titleBox);

            var descriptionLabel = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Description"));
            contentStack.Children.Add(descriptionLabel);
            descriptionBox = new DaisyTextArea
            {
                Text = task.Description,
                Variant = DaisyInputVariant.Bordered,
                PlaceholderText = FloweryLocalization.GetString("Kanban_Editor_DescriptionPlaceholder"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            FlowKanbanControlFactory.SetTextAreaRows(descriptionBox, 3, 5);
            AutomationProperties.SetLabeledBy(descriptionBox, descriptionLabel);
            contentStack.Children.Add(descriptionBox);

            contentStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_SectionScheduling")));
            contentStack.Children.Add(CreateSchedulingContent(editState));

            contentStack.Children.Add(new DaisyDivider());
            contentStack.Children.Add(CreateAssigneeEditor(editState, availableAssignees));

            return contentStack;
        }

        private static Control CreateStatusTabContent(
            TaskEditState editState,
            System.Collections.Generic.IEnumerable<string>? availableTags)
        {
            var contentStack = new StackPanel { Spacing = 12 };

            var tagsLabel = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Tags"));
            contentStack.Children.Add(tagsLabel);
            var selectedTags = new ObservableCollection<string>(ParseTags(editState.Tags));
            var availableTagList = BuildAvailableTags(availableTags, selectedTags);

            var tagPicker = new DaisyTagPicker
            {
                Tags = availableTagList,
                SelectedTags = selectedTags,
                Title = FloweryLocalization.GetString("Kanban_Tags_SelectedTitle"),
                Size = DaisySize.ExtraSmall,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(tagPicker, tagsLabel);

            void SyncTagsFromSelection()
            {
                var tagText = BuildTagText(selectedTags);
                editState.Tags = string.IsNullOrWhiteSpace(tagText) ? null : tagText;
            }

            tagPicker.SelectionChanged += (s, e) => SyncTagsFromSelection();
            contentStack.Children.Add(tagPicker);

            var statusRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                ColumnSpacing = 12
            };

            var priorityStack = new StackPanel { Spacing = 4 };
            var priorityLabel = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Priority"));
            priorityStack.Children.Add(priorityLabel);
            var priorityCombo = new DaisySelect
            {
                PlaceholderText = FloweryLocalization.GetString("Kanban_Editor_Priority"),
                Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(priorityCombo, priorityLabel);
            var priorityValues = Enum.GetValues(typeof(FlowTaskPriority)).Cast<FlowTaskPriority>().ToArray();
            foreach (var p in priorityValues)
            {
                priorityCombo.Items.Add(new FlowKanbanSelectOption<FlowTaskPriority>(p.ToString(), p));
            }
            priorityCombo.SelectedIndex = (int)editState.Priority;
            priorityCombo.SelectionChanged += (s, e) =>
            {
                if (priorityCombo.SelectedItem is FlowKanbanSelectOption<FlowTaskPriority> item)
                    editState.Priority = item.Value;
            };
            priorityStack.Children.Add(priorityCombo);
            Grid.SetColumn(priorityStack, 0);
            statusRow.Children.Add(priorityStack);

            var progressStack = new StackPanel { Spacing = 4 };
            var progressLabelText = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Progress"));
            progressStack.Children.Add(progressLabelText);
            var progressRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };
            var progressMax = editState.IsBlocked ? MaxBlockedProgress : 100;
            if (editState.ProgressPercent > progressMax)
            {
                editState.ProgressPercent = progressMax;
            }
            var progressSlider = new Slider
            {
                Minimum = 0,
                Maximum = progressMax,
                Value = editState.ProgressPercent,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(progressSlider, progressLabelText);
            var progressLabel = new TextBlock
            {
                Text = $"{editState.ProgressPercent}%",
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
                MinWidth = 36
            };
            progressSlider.ValueChanged += (s, e) =>
            {
                editState.ProgressPercent = (int)e.NewValue;
                progressLabel.Text = $"{editState.ProgressPercent}%";
            };
            Grid.SetColumn(progressSlider, 0);
            Grid.SetColumn(progressLabel, 1);
            progressRow.Children.Add(progressSlider);
            progressRow.Children.Add(progressLabel);
            progressStack.Children.Add(progressRow);
            Grid.SetColumn(progressStack, 1);
            statusRow.Children.Add(progressStack);

            contentStack.Children.Add(statusRow);

            contentStack.Children.Add(new DaisyDivider());
            contentStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_Blocked")));

            var blockedRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 12
            };
            var blockedToggleLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Blocked"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            var blockedToggle = new DaisyToggle
            {
                IsChecked = editState.IsBlocked,
                Variant = DaisyToggleVariant.Success,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetLabeledBy(blockedToggle, blockedToggleLabel);
            Grid.SetColumn(blockedToggleLabel, 0);
            Grid.SetColumn(blockedToggle, 1);
            blockedRow.Children.Add(blockedToggleLabel);
            blockedRow.Children.Add(blockedToggle);
            contentStack.Children.Add(blockedRow);

            var blockedReasonStack = new StackPanel { Spacing = 4 };
            var blockedReasonLabel = CreateSectionLabel(FloweryLocalization.GetString("Kanban_BlockedReason"));
            blockedReasonStack.Children.Add(blockedReasonLabel);
            var blockedReasonBox = new DaisyInput
            {
                Text = editState.BlockedReason ?? string.Empty,
                Variant = DaisyInputVariant.Bordered,
                Size = DaisySize.Small,
                PlaceholderText = FloweryLocalization.GetString("Kanban_BlockedReasonPlaceholder"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(blockedReasonBox, blockedReasonLabel);
            blockedReasonBox.TextChanged += (s, e) =>
            {
                editState.BlockedReason = string.IsNullOrWhiteSpace(blockedReasonBox.Text)
                    ? null
                    : blockedReasonBox.Text;
            };
            blockedReasonStack.Children.Add(blockedReasonBox);
            contentStack.Children.Add(blockedReasonStack);

            var blockedSinceStack = new StackPanel { Spacing = 4 };
            blockedSinceStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_BlockedSince")));
            var blockedSinceText = new TextBlock
            {
                Text = editState.BlockedSince.HasValue
                    ? editState.BlockedSince.Value.ToString("g", CultureInfo.CurrentUICulture)
                    : FloweryLocalization.GetString("Kanban_BlockedSinceEmpty"),
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap
            };
            blockedSinceStack.Children.Add(blockedSinceText);
            contentStack.Children.Add(blockedSinceStack);

            void UpdateProgressClamp()
            {
                var max = blockedToggle.IsChecked == true ? MaxBlockedProgress : 100;
                progressSlider.Maximum = max;
                if (progressSlider.Value > max)
                {
                    progressSlider.Value = max;
                }
            }

            void UpdateBlockedVisibility()
            {
                var isVisible = blockedToggle.IsChecked == true;
                blockedReasonStack.IsVisible = isVisible;
                blockedSinceStack.IsVisible = isVisible;
            }

            blockedToggle.IsCheckedChanged += (s, e) =>
            {
                editState.IsBlocked = blockedToggle.IsChecked == true;
                if (blockedToggle.IsChecked == true && !editState.BlockedSince.HasValue)
                {
                    editState.BlockedSince = DateTime.Now;
                    blockedSinceText.Text = editState.BlockedSince.Value.ToString("g", CultureInfo.CurrentUICulture);
                }
                UpdateBlockedVisibility();
                UpdateProgressClamp();
            };
            UpdateBlockedVisibility();
            UpdateProgressClamp();

            contentStack.Children.Add(new DaisyDivider());
            contentStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_CardColor")));
            contentStack.Children.Add(CreateColorSwatchPanel(editState));

            return contentStack;
        }

        private static Control CreateSchedulingContent(TaskEditState editState)
        {
            var contentStack = new StackPanel { Spacing = 12 };

            var datesGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                },
                RowSpacing = 8
            };

            var plannedHeader = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Planned"));
            plannedHeader.FontWeight = FontWeights.Bold;
            Grid.SetColumn(plannedHeader, 0);
            Grid.SetRow(plannedHeader, 0);
            datesGrid.Children.Add(plannedHeader);

            var actualHeader = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Actual"));
            actualHeader.FontWeight = FontWeights.Bold;
            Grid.SetRow(actualHeader, 3);
            datesGrid.Children.Add(actualHeader);

            var plannedStartStack = CreateDatePickerWithLabel(
                FloweryLocalization.GetString("Kanban_Editor_StartDate"),
                editState.PlannedStartDate,
                date => editState.PlannedStartDate = date);
            Grid.SetRow(plannedStartStack, 1);
            datesGrid.Children.Add(plannedStartStack);

            var actualStartStack = CreateDatePickerWithLabel(
                FloweryLocalization.GetString("Kanban_Editor_StartDate"),
                editState.ActualStartDate,
                date => editState.ActualStartDate = date);
            Grid.SetRow(actualStartStack, 4);
            datesGrid.Children.Add(actualStartStack);

            var plannedEndStack = CreateDatePickerWithLabel(
                FloweryLocalization.GetString("Kanban_Editor_EndDate"),
                editState.PlannedEndDate,
                date => editState.PlannedEndDate = date);
            Grid.SetRow(plannedEndStack, 2);
            datesGrid.Children.Add(plannedEndStack);

            var actualEndStack = CreateDatePickerWithLabel(
                FloweryLocalization.GetString("Kanban_Editor_EndDate"),
                editState.ActualEndDate,
                date => editState.ActualEndDate = date);
            Grid.SetRow(actualEndStack, 5);
            datesGrid.Children.Add(actualEndStack);

            contentStack.Children.Add(datesGrid);

            var overdueWarning = new Border
            {
                Background = DaisyResourceLookup.GetBrush("DaisyWarningBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                IsVisible = false,
                Margin = new Thickness(0, 4, 0, 0)
            };
            overdueWarning.Child = CreateIconLabel(
                "DaisyIconWarning",
                FloweryLocalization.GetString("Kanban_Editor_OverdueWarning"),
                new SolidColorBrush(Colors.White),
                FontWeights.SemiBold);

            if (editState.PlannedEndDate.HasValue &&
                editState.PlannedEndDate.Value < DateTime.Now &&
                !editState.ActualEndDate.HasValue)
            {
                overdueWarning.IsVisible = true;
            }
            contentStack.Children.Add(overdueWarning);

            return contentStack;
        }

        private static Control CreateEffortContent(TaskEditState editState)
        {
            var contentStack = new StackPanel { Spacing = 12 };

            var effortGrid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                },
                RowDefinitions =
                {
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto },
                    new RowDefinition { Height = GridLength.Auto }
                },
                ColumnSpacing = 12,
                RowSpacing = 8
            };

            var hoursHeader = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Hours"));
            hoursHeader.FontWeight = FontWeights.Bold;
            Grid.SetColumn(hoursHeader, 0);
            Grid.SetRow(hoursHeader, 0);
            effortGrid.Children.Add(hoursHeader);

            var daysHeader = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Days"));
            daysHeader.FontWeight = FontWeights.Bold;
            Grid.SetColumn(daysHeader, 1);
            Grid.SetRow(daysHeader, 0);
            effortGrid.Children.Add(daysHeader);

            var estHoursStack = CreateNumericInputWithLabel(
                FloweryLocalization.GetString("Kanban_Editor_Estimated"),
                editState.EstimatedHours,
                val => editState.EstimatedHours = val);
            Grid.SetColumn(estHoursStack, 0);
            Grid.SetRow(estHoursStack, 1);
            effortGrid.Children.Add(estHoursStack);

            var estDaysStack = CreateNumericInputWithLabel(
                FloweryLocalization.GetString("Kanban_Editor_Estimated"),
                editState.EstimatedDays,
                val => editState.EstimatedDays = val);
            Grid.SetColumn(estDaysStack, 1);
            Grid.SetRow(estDaysStack, 1);
            effortGrid.Children.Add(estDaysStack);

            var actHoursStack = CreateNumericInputWithLabel(
                FloweryLocalization.GetString("Kanban_Editor_Actual"),
                editState.ActualHours,
                val => editState.ActualHours = val);
            Grid.SetColumn(actHoursStack, 0);
            Grid.SetRow(actHoursStack, 2);
            effortGrid.Children.Add(actHoursStack);

            var actDaysStack = CreateNumericInputWithLabel(
                FloweryLocalization.GetString("Kanban_Editor_Actual"),
                editState.ActualDays,
                val => editState.ActualDays = val);
            Grid.SetColumn(actDaysStack, 1);
            Grid.SetRow(actDaysStack, 2);
            effortGrid.Children.Add(actDaysStack);

            contentStack.Children.Add(effortGrid);

            var tipBorder = new Border
            {
                Background = DaisyResourceLookup.GetBrush("DaisyInfoBrush"),
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(12, 8, 12, 8),
                Opacity = 0.8,
                Margin = new Thickness(0, 4, 0, 0)
            };
            tipBorder.Child = CreateIconLabel(
                "DaisyIconInfo",
                FloweryLocalization.GetString("Kanban_Editor_EffortTip"),
                new SolidColorBrush(Colors.White));
            contentStack.Children.Add(tipBorder);

            return contentStack;
        }

        private static Control CreateSubtasksTabContent(System.Collections.Generic.List<FlowSubtask> subtasks)
        {
            var contentStack = new StackPanel { Spacing = 12 };

            contentStack.Children.Add(CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Subtasks")));
            var subtasksList = new StackPanel { Spacing = 4 };
            RefreshSubtasksList(subtasksList, subtasks);
            contentStack.Children.Add(subtasksList);

            var addTaskBtn = new DaisyButton
            {
                Content = FloweryLocalization.GetString("Kanban_Editor_AddTask"),
                IconSymbol = Flowery.Enums.DaisyIconSymbol.Add,
                IconPlacement = IconPlacement.Left,
                Variant = DaisyButtonVariant.Primary,
                ButtonStyle = DaisyButtonStyle.Outline,
                Size = DaisySize.Small
            };
            AutomationProperties.SetName(addTaskBtn, FloweryLocalization.GetString("Kanban_Editor_AddTask"));
            addTaskBtn.Click += (s, e) =>
            {
                subtasks.Add(new FlowSubtask { Title = "" });
                RefreshSubtasksList(subtasksList, subtasks, subtasks.Count - 1);
            };
            contentStack.Children.Add(addTaskBtn);

            return contentStack;
        }

        private static Control CreateAssigneeEditor(
            TaskEditState editState,
            System.Collections.Generic.IEnumerable<FlowTaskAssigneeOption>? availableAssignees)
        {
            var contentStack = new StackPanel { Spacing = 8 };

            var assigneeLabel = CreateSectionLabel(FloweryLocalization.GetString("Kanban_Editor_Assignee"));
            contentStack.Children.Add(assigneeLabel);

            var assigneeRow = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8
            };

            var assigneeInput = new DaisyInput
            {
                Text = editState.Assignee ?? string.Empty,
                Variant = DaisyInputVariant.Bordered,
                Size = DaisySize.Small,
                PlaceholderText = FloweryLocalization.GetString("Kanban_Lanes_Unassigned"),
                IsReadOnly = true,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(assigneeInput, assigneeLabel);
            Grid.SetColumn(assigneeInput, 0);
            assigneeRow.Children.Add(assigneeInput);

            var clearButton = new DaisyButton
            {
                Content = FloweryLocalization.GetString("Common_Clear"),
                Variant = DaisyButtonVariant.Neutral,
                ButtonStyle = DaisyButtonStyle.Outline,
                Size = DaisySize.ExtraSmall,
                MinWidth = 72,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetName(clearButton, FloweryLocalization.GetString("Common_Clear"));
            Grid.SetColumn(clearButton, 1);
            assigneeRow.Children.Add(clearButton);

            contentStack.Children.Add(assigneeRow);

            var assigneeOptions = BuildAvailableAssignees(availableAssignees);
            var assigneeSelect = new DaisySelect
            {
                ItemsSource = assigneeOptions,
                PlaceholderText = FloweryLocalization.GetString("Select_Placeholder", "Pick one"),
                Variant = DaisySelectVariant.Bordered,
                Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch,
                IsEnabled = assigneeOptions.Count > 0
            };
            AutomationProperties.SetLabeledBy(assigneeSelect, assigneeLabel);
            contentStack.Children.Add(assigneeSelect);

            void SetAssignee(FlowTaskAssigneeOption? option)
            {
                var displayName = option?.DisplayName;
                var normalizedName = string.IsNullOrWhiteSpace(displayName) ? null : displayName.Trim();
                editState.Assignee = normalizedName;
                editState.AssigneeId = string.IsNullOrWhiteSpace(option?.Id) ? null : option.Id;
                assigneeInput.Text = normalizedName ?? string.Empty;
                clearButton.IsEnabled = !string.IsNullOrWhiteSpace(normalizedName);
            }

            assigneeSelect.SelectionChanged += (s, e) =>
            {
                SetAssignee(assigneeSelect.SelectedItem as FlowTaskAssigneeOption);
            };

            clearButton.Click += (s, e) =>
            {
                assigneeSelect.SelectedItem = null;
                SetAssignee(null);
            };

            clearButton.IsEnabled = !string.IsNullOrWhiteSpace(editState.Assignee);
            if (!string.IsNullOrWhiteSpace(editState.AssigneeId))
            {
                var current = assigneeOptions.FirstOrDefault(option =>
                    string.Equals(option.Id, editState.AssigneeId, StringComparison.Ordinal));
                if (current != null)
                {
                    assigneeSelect.SelectedItem = current;
                }
            }
            else if (!string.IsNullOrWhiteSpace(editState.Assignee))
            {
                var current = assigneeOptions.FirstOrDefault(option =>
                    string.Equals(option.DisplayName, editState.Assignee, StringComparison.OrdinalIgnoreCase));
                if (current != null)
                {
                    assigneeSelect.SelectedItem = current;
                }
            }

            return contentStack;
        }

        private static System.Collections.Generic.List<FlowTaskAssigneeOption> BuildAssigneeOptionsFromNames(
            System.Collections.Generic.IEnumerable<string>? availableAssignees)
        {
            var options = new System.Collections.Generic.List<FlowTaskAssigneeOption>();
            if (availableAssignees == null)
                return options;

            foreach (var assignee in availableAssignees)
            {
                var trimmed = assignee?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                options.Add(new FlowTaskAssigneeOption(null, trimmed));
            }

            return options;
        }

        private static System.Collections.Generic.List<FlowTaskAssigneeOption> BuildAvailableAssignees(
            System.Collections.Generic.IEnumerable<FlowTaskAssigneeOption>? availableAssignees)
        {
            var assignees = new System.Collections.Generic.List<FlowTaskAssigneeOption>();
            var seenIds = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            var seenNames = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            if (availableAssignees == null)
                return assignees;

            foreach (var assignee in availableAssignees)
            {
                if (assignee == null)
                    continue;

                var name = assignee.DisplayName?.Trim();
                var id = assignee.Id;

                if (string.IsNullOrWhiteSpace(name))
                {
                    name = string.IsNullOrWhiteSpace(id) ? null : id;
                }

                if (string.IsNullOrWhiteSpace(name))
                    continue;

                if (!string.IsNullOrWhiteSpace(id))
                {
                    if (!seenIds.Add(id))
                        continue;
                }
                else if (!seenNames.Add(name))
                {
                    continue;
                }

                assignees.Add(new FlowTaskAssigneeOption(id, name));
            }

            return assignees;
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

        private static ObservableCollection<string> BuildAvailableTags(
            System.Collections.Generic.IEnumerable<string>? availableTags,
            System.Collections.Generic.IEnumerable<string> selectedTags)
        {
            var tags = new ObservableCollection<string>();
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddTag(string? tag)
            {
                var trimmed = tag?.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    return;

                if (seen.Add(trimmed))
                    tags.Add(trimmed);
            }

            if (availableTags != null)
            {
                foreach (var tag in availableTags)
                {
                    AddTag(tag);
                }
            }

            foreach (var tag in selectedTags)
            {
                AddTag(tag);
            }

            return tags;
        }

        private static System.Collections.Generic.List<string> ParseTags(string? tagsText)
        {
            var tags = new System.Collections.Generic.List<string>();
            if (string.IsNullOrWhiteSpace(tagsText))
                return tags;

            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var part in tagsText.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = part.Trim();
                if (string.IsNullOrWhiteSpace(trimmed))
                    continue;

                if (seen.Add(trimmed))
                    tags.Add(trimmed);
            }

            return tags;
        }

        private static string BuildTagText(System.Collections.Generic.IEnumerable<string> tags)
        {
            return string.Join(", ", tags);
        }

        private static StackPanel CreateDatePickerWithLabel(string label, DateTime? initialValue, Action<DateTime?> onChanged)
        {
            var stack = new StackPanel { Spacing = 4 };
            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Opacity = 0.7,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            };
            stack.Children.Add(labelText);

            var datePicker = new DatePicker
            {
                SelectedDate = initialValue.HasValue ? new DateTimeOffset(initialValue.Value) : null,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(datePicker, labelText);
            datePicker.SelectedDateChanged += (s, e) =>
            {
                onChanged(datePicker.SelectedDate?.DateTime);
            };
            stack.Children.Add(datePicker);
            return stack;
        }

        private static StackPanel CreateNumericInputWithLabel(string label, double? initialValue, Action<double?> onChanged)
        {
            var stack = new StackPanel { Spacing = 4 };
            var labelText = new TextBlock
            {
                Text = label,
                FontSize = 11,
                Opacity = 0.7,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            };
            stack.Children.Add(labelText);

            var numBox = new DaisyNumericUpDown
            {
                Value = initialValue.HasValue ? (decimal)initialValue.Value : null,
                Minimum = 0,
                Maximum = 10000,
                Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(numBox, labelText);
            numBox.LostFocus += (s, e) =>
            {
                onChanged(numBox.Value.HasValue ? (double)numBox.Value.Value : null);
            };
            stack.Children.Add(numBox);
            return stack;
        }

        private static Control CreateColorSwatchPanel(TaskEditState editState)
        {
            var panel = new FlowKanbanWrapPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalSpacing = 8,
                VerticalSpacing = 8
            };

            BuildColorSwatches(panel, editState);
            return panel;
        }

        private static void BuildColorSwatches(FlowKanbanWrapPanel panel, TaskEditState editState)
        {
            panel.Children.Clear();

            foreach (var color in ColorSwatches)
            {
                var colorCopy = color;
                var swatch = new Border
                {
                    Width = 28,
                    Height = 28,
                    CornerRadius = new CornerRadius(14),
                    Background = GetColorBrush(color),
                    BorderBrush = color == editState.Palette
                        ? DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
                        : new SolidColorBrush(Colors.Transparent),
                    BorderThickness = new Thickness(2)
                };

                if (color == editState.Palette)
                {
                    swatch.Child = CreateIconPath("DaisyIconCheck", new SolidColorBrush(Colors.White), 12);
                }

                swatch.PointerPressed += (s, e) =>
                {
                    editState.Palette = colorCopy;
                    BuildColorSwatches(panel, editState);
                };

                panel.Children.Add(swatch);
            }
        }

        private static void RefreshSubtasksList(
            StackPanel panel,
            System.Collections.Generic.List<FlowSubtask> subtasks,
            int? editIndex = null)
        {
            panel.Children.Clear();

            for (int i = 0; i < subtasks.Count; i++)
            {
                var index = i;
                var subtask = subtasks[i];
                var subtaskCopy = subtask;
                var row = new Grid();
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
                row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

                var checkbox = new DaisyCheckBox
                {
                    IsChecked = subtask.IsCompleted,
                    VerticalAlignment = VerticalAlignment.Center,
                    Size = DaisySize.Small
                };
                checkbox.IsCheckedChanged += (s, e) => subtaskCopy.IsCompleted = checkbox.IsChecked == true;
                Grid.SetColumn(checkbox, 0);
                row.Children.Add(checkbox);

                if (editIndex == index)
                {
                    var editBox = new DaisyInput
                    {
                        Text = subtask.Title,
                        VerticalAlignment = VerticalAlignment.Center,
                        Variant = DaisyInputVariant.Bordered,
                        Size = DaisySize.Small
                    };
                    AutomationProperties.SetName(
                        editBox,
                        FloweryLocalization.GetString("Kanban_Editor_Subtasks"));
                    editBox.Loaded += (s, e) => editBox.Focus();
                    editBox.LostFocus += (s, e) =>
                    {
                        subtaskCopy.Title = editBox.Text?.Trim() ?? subtaskCopy.Title;
                        RefreshSubtasksList(panel, subtasks);
                    };
                    editBox.KeyDown += (s, e) =>
                    {
                        if (e.Key == Key.Enter)
                        {
                            e.Handled = true;
                            subtaskCopy.Title = editBox.Text?.Trim() ?? subtaskCopy.Title;
                            RefreshSubtasksList(panel, subtasks);
                        }
                        else if (e.Key == Key.Escape)
                        {
                            e.Handled = true;
                            RefreshSubtasksList(panel, subtasks);
                        }
                    };
                    Grid.SetColumn(editBox, 1);
                    row.Children.Add(editBox);
                }
                else
                {
                    var titleText = new TextBlock
                    {
                        Text = subtask.Title,
                        VerticalAlignment = VerticalAlignment.Center,
                        Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush"),
                        Margin = new Thickness(4, 0, 0, 0)
                    };
                    titleText.Tapped += (s, e) =>
                    {
                        RefreshSubtasksList(panel, subtasks, index);
                    };
                    Grid.SetColumn(titleText, 1);
                    row.Children.Add(titleText);
                }

                var deleteBtn = new DaisyButton
                {
                    Content = new DaisyIconText
                    {
                        IconData = FlowKanbanControlFactory.GetIconGeometry("DaisyIconClose"),
                        IconSize = 14
                    },
                    Variant = DaisyButtonVariant.Error,
                    Size = DaisySize.ExtraSmall,
                    Opacity = 0.7
                };
                AutomationProperties.SetName(deleteBtn, FloweryLocalization.GetString("Common_Delete"));
                deleteBtn.Click += (s, e) =>
                {
                    subtasks.Remove(subtaskCopy);
                    RefreshSubtasksList(panel, subtasks);
                };
                Grid.SetColumn(deleteBtn, 2);
                row.Children.Add(deleteBtn);

                panel.Children.Add(row);
            }
        }

        private static IBrush? GetColorBrush(DaisyColor color)
        {
            return color switch
            {
                DaisyColor.Primary => DaisyResourceLookup.GetBrush("DaisyPrimaryBrush"),
                DaisyColor.Secondary => DaisyResourceLookup.GetBrush("DaisySecondaryBrush"),
                DaisyColor.Accent => DaisyResourceLookup.GetBrush("DaisyAccentBrush"),
                DaisyColor.Neutral => DaisyResourceLookup.GetBrush("DaisyNeutralBrush"),
                DaisyColor.Info => DaisyResourceLookup.GetBrush("DaisyInfoBrush"),
                DaisyColor.Success => DaisyResourceLookup.GetBrush("DaisySuccessBrush"),
                DaisyColor.Warning => DaisyResourceLookup.GetBrush("DaisyWarningBrush"),
                DaisyColor.Error => DaisyResourceLookup.GetBrush("DaisyErrorBrush"),
                _ => DaisyResourceLookup.GetBrush("DaisyBase300Brush")
            };
        }

        private static Control CreateIconLabel(
            string iconKey,
            string text,
            IBrush foreground,
            FontWeight? fontWeight = null,
            double iconSize = 12)
        {
            var panel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 6
            };

            panel.Children.Add(CreateIconPath(iconKey, foreground, iconSize));

            var textBlock = new TextBlock
            {
                Text = text,
                Foreground = foreground,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            if (fontWeight.HasValue)
            {
                textBlock.FontWeight = fontWeight.Value;
            }

            panel.Children.Add(textBlock);

            return panel;
        }

        private static Path CreateIconPath(string iconKey, IBrush foreground, double size)
        {
            var pathData = FloweryPathHelpers.GetIconPathData(iconKey);
            var geometry = !string.IsNullOrWhiteSpace(pathData)
                ? FloweryPathHelpers.ParseGeometry(pathData)
                : FloweryPathHelpers.CreateEllipseGeometry(size / 2, size / 2, size / 2, size / 2);

            return new Path
            {
                Data = geometry,
                Fill = foreground,
                Width = size,
                Height = size,
                Stretch = Stretch.Uniform,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
    }

    public sealed class FlowTaskAssigneeOption
    {
        public FlowTaskAssigneeOption(string? id, string displayName, bool isResolved = true)
        {
            Id = string.IsNullOrWhiteSpace(id) ? null : id;
            DisplayName = displayName ?? string.Empty;
            IsResolved = isResolved;
        }

        public string? Id { get; }
        public string DisplayName { get; }
        public bool IsResolved { get; }

        public override string ToString() => DisplayName;
    }
}
