using System;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows.Input;
using Flowery.Localization;
using Flowery.Theming;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// A lightweight task card for the Kanban board.
    /// </summary>
    public partial class FlowTaskCard : FlowKanbanContentControl
    {
        static FlowTaskCard()
        {
            TitleProperty.Changed.AddClassHandler<FlowTaskCard>(OnTitleChanged);
            TaskProperty.Changed.AddClassHandler<FlowTaskCard>(OnTaskChanged);
            IsSelectedProperty.Changed.AddClassHandler<FlowTaskCard>(OnIsSelectedChanged);
            PaletteProperty.Changed.AddClassHandler<FlowTaskCard>((control, _) => control.ApplyPalette());
            CardSizeProperty.Changed.AddClassHandler<FlowTaskCard>((control, _) => control.ApplySizing());
        }

        private const int TitleLongPressMilliseconds = 500;
        private const double TitleLongPressMoveThreshold = 8;
        private FlowKanban? _parentKanban;
        private DaisyButton? _closeButton;
        private TextBlock? _titleTextBlock;
        private Border? _rootBorder;
        private IBrush? _defaultBorderBrush;
        private Thickness _defaultBorderThickness;
        private FlowTask? _trackedTask;
        private ObservableCollection<FlowSubtask>? _trackedSubtasksCollection;
        private readonly HashSet<FlowSubtask> _trackedSubtasks = new();
        private bool _isPointerOver;
        private bool _isKeyboardFocusVisible;
        private bool _isLocalizationSubscribed;
        private DispatcherTimer? _titleLongPressTimer;
        private bool _isTitlePressTracking;
        private int _titlePressPointerId;
        private Point _titlePressStartPoint;
        private static readonly IBrush TransparentBorderBrush = new SolidColorBrush(Colors.Transparent);

        public FlowTaskCard()
        {
            Focusable = true;
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
            GotFocus += OnCardGotFocus;
            LostFocus += OnCardLostFocus;
            KeyDown += OnCardKeyDown;
            PointerPressed += OnCardPointerPressed;
            UpdateSelectionBadgePlacement();
        }

        protected override AutomationPeer OnCreateAutomationPeer() =>
            new FlowTaskCardAutomationPeer(this);

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            if (_trackedTask == null && Task != null)
            {
                UpdateTaskBinding(null, Task);
            }

            // Find parent FlowKanban and subscribe to its size changes
            _parentKanban = FindParentKanban();
            if (_parentKanban != null)
            {
                CardSize = _parentKanban.BoardSize;
                _parentKanban.BoardSizeChanged += OnParentSizeChanged;
                ApplySizing();
            }

            if (!_isLocalizationSubscribed)
            {
                FloweryLocalization.CultureChanged += OnLocalizationCultureChanged;
                _isLocalizationSubscribed = true;
            }

            UpdateFocusVisualState();
            UpdateSelectionBadgePlacement();
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            // Unsubscribe from parent
            if (_parentKanban != null)
            {
                _parentKanban.BoardSizeChanged -= OnParentSizeChanged;
                _parentKanban = null;
            }

            if (_isLocalizationSubscribed)
            {
                FloweryLocalization.CultureChanged -= OnLocalizationCultureChanged;
                _isLocalizationSubscribed = false;
            }

            DetachTitleTextBlock();

            if (_trackedTask != null)
            {
                _trackedTask.PropertyChanged -= OnTaskPropertyChanged;
                _trackedTask = null;
            }

            AttachSubtasksCollection(null);
        }

        private void OnLocalizationCultureChanged(object? sender, CultureInfo culture)
        {
            RefreshLocalization();
        }

        private void OnParentSizeChanged(object? sender, DaisySize newSize)
        {
            CardSize = newSize;
            ApplySizing();
        }

        private FlowKanban? FindParentKanban()
        {
            return FlowKanbanVisualTree.FindAncestor<FlowKanban>(this, includeSelf: false);
        }

        #region Title
        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                                nameof(Title),
                                string.Empty);

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }
        #endregion

        #region AssigneeText
        public static readonly StyledProperty<string> AssigneeTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(AssigneeText),
                string.Empty);

        public string AssigneeText
        {
            get => (string)GetValue(AssigneeTextProperty);
            set => SetValue(AssigneeTextProperty, value);
        }
        #endregion

        #region AssigneeInitials
        public static readonly StyledProperty<string> AssigneeInitialsProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(AssigneeInitials),
                string.Empty);

        public string AssigneeInitials
        {
            get => (string)GetValue(AssigneeInitialsProperty);
            set => SetValue(AssigneeInitialsProperty, value);
        }
        #endregion

        #region Assignee metadata
        public static readonly StyledProperty<IImage?> AssigneeAvatarSourceProperty =
            AvaloniaProperty.Register<FlowTaskCard, IImage?>(
                nameof(AssigneeAvatarSource),
                default!);

        public IImage? AssigneeAvatarSource
        {
            get => (IImage?)GetValue(AssigneeAvatarSourceProperty);
            set => SetValue(AssigneeAvatarSourceProperty, value);
        }

        public static readonly StyledProperty<bool> HasAssigneeAvatarProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasAssigneeAvatar),
                false);

        public bool HasAssigneeAvatar
        {
            get => (bool)GetValue(HasAssigneeAvatarProperty);
            set => SetValue(HasAssigneeAvatarProperty, value);
        }

        public static readonly StyledProperty<string> AssigneeRolesTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(AssigneeRolesText),
                string.Empty);

        public string AssigneeRolesText
        {
            get => (string)GetValue(AssigneeRolesTextProperty);
            set => SetValue(AssigneeRolesTextProperty, value);
        }

        public static readonly StyledProperty<bool> HasAssigneeRolesProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasAssigneeRoles),
                false);

        public bool HasAssigneeRoles
        {
            get => (bool)GetValue(HasAssigneeRolesProperty);
            set => SetValue(HasAssigneeRolesProperty, value);
        }
        #endregion

        #region HasAssignee
        public static readonly StyledProperty<bool> HasAssigneeProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasAssignee),
                false);

        public bool HasAssignee
        {
            get => (bool)GetValue(HasAssigneeProperty);
            set => SetValue(HasAssigneeProperty, value);
        }
        #endregion

        #region StartDateText
        public static readonly StyledProperty<string> StartDateTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(StartDateText),
                string.Empty);

        public string StartDateText
        {
            get => (string)GetValue(StartDateTextProperty);
            set => SetValue(StartDateTextProperty, value);
        }
        #endregion

        #region EndDateText
        public static readonly StyledProperty<string> EndDateTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(EndDateText),
                string.Empty);

        public string EndDateText
        {
            get => (string)GetValue(EndDateTextProperty);
            set => SetValue(EndDateTextProperty, value);
        }
        #endregion

        #region HasStartDate
        public static readonly StyledProperty<bool> HasStartDateProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasStartDate),
                false);

        public bool HasStartDate
        {
            get => (bool)GetValue(HasStartDateProperty);
            set => SetValue(HasStartDateProperty, value);
        }
        #endregion

        #region HasEndDate
        public static readonly StyledProperty<bool> HasEndDateProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasEndDate),
                false);

        public bool HasEndDate
        {
            get => (bool)GetValue(HasEndDateProperty);
            set => SetValue(HasEndDateProperty, value);
        }
        #endregion

        #region HasDateRange
        public static readonly StyledProperty<bool> HasDateRangeProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasDateRange),
                false);

        public bool HasDateRange
        {
            get => (bool)GetValue(HasDateRangeProperty);
            set => SetValue(HasDateRangeProperty, value);
        }
        #endregion

        #region HasFooter
        public static readonly StyledProperty<bool> HasFooterProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasFooter),
                false);

        public bool HasFooter
        {
            get => (bool)GetValue(HasFooterProperty);
            set => SetValue(HasFooterProperty, value);
        }
        #endregion

        #region WorkItemNumberText
        public static readonly StyledProperty<string> WorkItemNumberTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(WorkItemNumberText),
                string.Empty);

        public string WorkItemNumberText
        {
            get => (string)GetValue(WorkItemNumberTextProperty);
            set => SetValue(WorkItemNumberTextProperty, value);
        }
        #endregion

        #region HasWorkItemNumber
        public static readonly StyledProperty<bool> HasWorkItemNumberProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasWorkItemNumber),
                false);

        public bool HasWorkItemNumber
        {
            get => (bool)GetValue(HasWorkItemNumberProperty);
            set => SetValue(HasWorkItemNumberProperty, value);
        }
        #endregion

        #region Task
        public static readonly StyledProperty<FlowTask?> TaskProperty =
            AvaloniaProperty.Register<FlowTaskCard, FlowTask?>(
                                nameof(Task),
                                default!);

        public FlowTask? Task
        {
            get => (FlowTask?)GetValue(TaskProperty);
            set => SetValue(TaskProperty, value);
        }
        #endregion

        #region SubtaskSummaryText
        public static readonly StyledProperty<string> SubtaskSummaryTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(SubtaskSummaryText),
                string.Empty);

        public string SubtaskSummaryText
        {
            get => (string)GetValue(SubtaskSummaryTextProperty);
            set => SetValue(SubtaskSummaryTextProperty, value);
        }
        #endregion

        #region HasSubtaskSummary
        public static readonly StyledProperty<bool> HasSubtaskSummaryProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasSubtaskSummary),
                false);

        public bool HasSubtaskSummary
        {
            get => (bool)GetValue(HasSubtaskSummaryProperty);
            set => SetValue(HasSubtaskSummaryProperty, value);
        }
        #endregion

        #region DueDateText
        public static readonly StyledProperty<string> DueDateTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(DueDateText),
                string.Empty);

        public string DueDateText
        {
            get => (string)GetValue(DueDateTextProperty);
            set => SetValue(DueDateTextProperty, value);
        }
        #endregion

        #region HasDueDate
        public static readonly StyledProperty<bool> HasDueDateProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasDueDate),
                false);

        public bool HasDueDate
        {
            get => (bool)GetValue(HasDueDateProperty);
            set => SetValue(HasDueDateProperty, value);
        }
        #endregion

        #region PriorityText
        public static readonly StyledProperty<string> PriorityTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(PriorityText),
                string.Empty);

        public string PriorityText
        {
            get => (string)GetValue(PriorityTextProperty);
            set => SetValue(PriorityTextProperty, value);
        }
        #endregion

        #region HasPriority
        public static readonly StyledProperty<bool> HasPriorityProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasPriority),
                false);

        public bool HasPriority
        {
            get => (bool)GetValue(HasPriorityProperty);
            set => SetValue(HasPriorityProperty, value);
        }
        #endregion

        #region ProgressText
        public static readonly StyledProperty<string> ProgressTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(ProgressText),
                string.Empty);

        public string ProgressText
        {
            get => (string)GetValue(ProgressTextProperty);
            set => SetValue(ProgressTextProperty, value);
        }
        #endregion

        #region HasProgress
        public static readonly StyledProperty<bool> HasProgressProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(HasProgress),
                false);

        public bool HasProgress
        {
            get => (bool)GetValue(HasProgressProperty);
            set => SetValue(HasProgressProperty, value);
        }
        #endregion

        #region IsBlocked
        public static readonly StyledProperty<bool> IsBlockedProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(IsBlocked),
                false);

        public bool IsBlocked
        {
            get => (bool)GetValue(IsBlockedProperty);
            set => SetValue(IsBlockedProperty, value);
        }
        #endregion

        #region BlockedBadgeText
        public static readonly StyledProperty<string> BlockedBadgeTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(BlockedBadgeText),
                string.Empty);

        public string BlockedBadgeText
        {
            get => (string)GetValue(BlockedBadgeTextProperty);
            set => SetValue(BlockedBadgeTextProperty, value);
        }
        #endregion

        #region BlockedDetailText
        public static readonly StyledProperty<string> BlockedDetailTextProperty =
            AvaloniaProperty.Register<FlowTaskCard, string>(
                nameof(BlockedDetailText),
                string.Empty);

        public string BlockedDetailText
        {
            get => (string)GetValue(BlockedDetailTextProperty);
            set => SetValue(BlockedDetailTextProperty, value);
        }
        #endregion

        #region CloseCommand
        public static readonly StyledProperty<ICommand?> CloseCommandProperty =
            AvaloniaProperty.Register<FlowTaskCard, ICommand?>(
                nameof(CloseCommand),
                default!);

        public ICommand? CloseCommand
        {
            get => (ICommand?)GetValue(CloseCommandProperty);
            set => SetValue(CloseCommandProperty, value);
        }
        #endregion

        #region CloseCommandParameter
        public static readonly StyledProperty<object?> CloseCommandParameterProperty =
            AvaloniaProperty.Register<FlowTaskCard, object?>(
                nameof(CloseCommandParameter),
                default!);

        public object? CloseCommandParameter
        {
            get => (object?)GetValue(CloseCommandParameterProperty);
            set => SetValue(CloseCommandParameterProperty, value);
        }
        #endregion

        #region EditCommand
        public static readonly StyledProperty<ICommand?> EditCommandProperty =
            AvaloniaProperty.Register<FlowTaskCard, ICommand?>(
                nameof(EditCommand),
                default!);

        /// <summary>
        /// Command invoked when the card is double-tapped for editing.
        /// </summary>
        public ICommand? EditCommand
        {
            get => (ICommand?)GetValue(EditCommandProperty);
            set => SetValue(EditCommandProperty, value);
        }
        #endregion

        #region IsSelected
        public static readonly StyledProperty<bool> IsSelectedProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                                nameof(IsSelected),
                                false);

        public bool IsSelected
        {
            get => (bool)GetValue(IsSelectedProperty);
            set => SetValue(IsSelectedProperty, value);
        }

        private static void OnIsSelectedChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowTaskCard card && e.NewValue is bool isSelected)
            {
                card.UpdateSelectionState(isSelected);
            }
        }
        #endregion

        #region SelectionBadgeColumn
        public static readonly StyledProperty<int> SelectionBadgeColumnProperty =
            AvaloniaProperty.Register<FlowTaskCard, int>(
                nameof(SelectionBadgeColumn),
                2);

        public int SelectionBadgeColumn
        {
            get => (int)GetValue(SelectionBadgeColumnProperty);
            private set => SetValue(SelectionBadgeColumnProperty, value);
        }
        #endregion

        #region ActionPillHorizontalAlignment
        public static readonly StyledProperty<HorizontalAlignment> ActionPillHorizontalAlignmentProperty =
            AvaloniaProperty.Register<FlowTaskCard, HorizontalAlignment>(
                nameof(ActionPillHorizontalAlignment),
                HorizontalAlignment.Right);

        public HorizontalAlignment ActionPillHorizontalAlignment
        {
            get => (HorizontalAlignment)GetValue(ActionPillHorizontalAlignmentProperty);
            private set => SetValue(ActionPillHorizontalAlignmentProperty, value);
        }
        #endregion

        #region ActionPillOffsetX
        public static readonly StyledProperty<double> ActionPillOffsetXProperty =
            AvaloniaProperty.Register<FlowTaskCard, double>(
                nameof(ActionPillOffsetX),
                0d);

        public double ActionPillOffsetX
        {
            get => (double)GetValue(ActionPillOffsetXProperty);
            private set => SetValue(ActionPillOffsetXProperty, value);
        }
        #endregion

        #region IsActionPillVisible
        public static readonly StyledProperty<bool> IsActionPillVisibleProperty =
            AvaloniaProperty.Register<FlowTaskCard, bool>(
                nameof(IsActionPillVisible),
                false);

        public bool IsActionPillVisible
        {
            get => (bool)GetValue(IsActionPillVisibleProperty);
            private set => SetValue(IsActionPillVisibleProperty, value);
        }
        #endregion

        #region ActionPillOpacity
        public static readonly StyledProperty<double> ActionPillOpacityProperty =
            AvaloniaProperty.Register<FlowTaskCard, double>(
                nameof(ActionPillOpacity),
                0d);

        public double ActionPillOpacity
        {
            get => (double)GetValue(ActionPillOpacityProperty);
            private set => SetValue(ActionPillOpacityProperty, value);
        }
        #endregion

        #region Palette
        public static readonly StyledProperty<DaisyColor> PaletteProperty =
            AvaloniaProperty.Register<FlowTaskCard, DaisyColor>(
                                nameof(Palette),
                                DaisyColor.Default);

        public DaisyColor Palette
        {
            get => (DaisyColor)GetValue(PaletteProperty);
            set => SetValue(PaletteProperty, value);
        }
        #endregion

        #region CardSize
        public static readonly StyledProperty<DaisySize> CardSizeProperty =
            AvaloniaProperty.Register<FlowTaskCard, DaisySize>(
                                nameof(CardSize),
                                DaisySize.Medium);

        /// <summary>
        /// The size tier for this card's visual elements.
        /// </summary>
        public DaisySize CardSize
        {
            get => (DaisySize)GetValue(CardSizeProperty);
            set => SetValue(CardSizeProperty, value);
        }
        #endregion

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            ApplyPalette();
            ApplySizing();

            _rootBorder = e.NameScope.Find<Border>("PART_Root");
            if (_rootBorder != null)
            {
                _defaultBorderBrush ??= _rootBorder.BorderBrush;
                if (_defaultBorderThickness == default)
                {
                    _defaultBorderThickness = _rootBorder.BorderThickness;
                }
            }

            // Find close button
            if (_closeButton != null)
            {
                _closeButton.GotFocus -= OnCardGotFocus;
                _closeButton.LostFocus -= OnCardLostFocus;
            }

            _closeButton = e.NameScope.Find<DaisyButton>("PART_CloseBtn");
            UpdateCloseButtonVisibility();
            UpdateAutomationProperties();
            if (_closeButton != null)
            {
                AutomationProperties.SetName(_closeButton, FloweryLocalization.GetString("Common_Delete"));
                _closeButton.GotFocus += OnCardGotFocus;
                _closeButton.LostFocus += OnCardLostFocus;
            }
            AttachTitleTextBlock(e.NameScope);

            // Enable double-tap for editing
            DoubleTapped += OnDoubleTapped;

            // Enable hover to show close button
            PointerEntered += OnPointerEntered;
            PointerExited += OnPointerExited;

            UpdateTaskVisuals();
            ApplySelectionVisual();
        }

        private static void OnTitleChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowTaskCard card)
            {
                card.UpdateTitleToolTip();
                card.UpdateAutomationProperties();
            }
        }

        private void OnCardGotFocus(object? sender, RoutedEventArgs e)
        {
            UpdateCloseButtonVisibility();
            UpdateFocusVisualState();
        }

        private void OnCardLostFocus(object? sender, RoutedEventArgs e)
        {
            if (IsFocusWithin())
                return;

            UpdateCloseButtonVisibility();
            UpdateFocusVisualState();
        }

        private void OnPointerEntered(object? sender, PointerEventArgs e)
        {
            _isPointerOver = true;
            UpdateCloseButtonVisibility();
        }

        private void OnPointerExited(object? sender, PointerEventArgs e)
        {
            _isPointerOver = false;
            UpdateCloseButtonVisibility();
        }

        private static async Task StartTaskDragAsync(PointerPressedEventArgs e, FlowTask task)
        {
            var item = DataTransferItem.CreateText(task.Id);
            item.Set(FlowKanban.TaskDragFormat, task.Id);
            var data = new DataTransfer();
            data.Add(item);
            await DragDrop.DoDragDropAsync(e, data, DragDropEffects.Move);
        }

        private void OnDoubleTapped(object? sender, TappedEventArgs e)
        {
            if (IsMobileSelectionMode())
            {
                var task = CloseCommandParameter as FlowTask ?? Task ?? _trackedTask;
                if (task != null)
                {
                    task.IsSelected = !task.IsSelected;
                    e.Handled = true;
                }
                return;
            }

            if (TryExecuteEdit())
                e.Handled = true;
        }

        private void OnCardKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
                return;

            if (TryExecuteEdit())
                e.Handled = true;
        }

        private void OnCardPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (IsMobileSelectionMode())
                return;

            var task = CloseCommandParameter as FlowTask ?? Task ?? _trackedTask;
            if (task == null)
                return;

            var column = FindAncestor<FlowKanbanColumn>(this);
            if (column != null)
            {
                var modifiers = e.KeyModifiers;
                if (column.HandleTaskSelection(task, modifiers.HasFlag(KeyModifiers.Shift), modifiers.HasFlag(KeyModifiers.Control)))
                {
                    e.Handled = true;
                }
            }
            else if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            {
                task.IsSelected = !task.IsSelected;
            }
            else if (!e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            {
                task.IsSelected = true;
            }

            if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            {
                _ = StartTaskDragAsync(e, task);
            }
        }

        internal bool TryExecuteEdit()
        {
            var task = CloseCommandParameter as FlowTask ?? Task;
            if (task == null)
                return false;

            if (EditCommand?.CanExecute(task) == true)
            {
                EditCommand.Execute(task);
                return true;
            }

            return false;
        }

        private void ApplyPalette()
        {
            if (Palette == DaisyColor.Default)
            {
                var secondaryBackground = DaisyResourceLookup.GetBrush("DaisySecondaryBrush");
                if (secondaryBackground != null)
                {
                    Background = CloneBrush(secondaryBackground) ?? secondaryBackground;
                }
                else
                {
                    ClearValue(BackgroundProperty);
                }

                var secondaryContent = DaisyResourceLookup.GetBrush("DaisySecondaryContentBrush");
                if (secondaryContent != null)
                {
                    Foreground = CloneBrush(secondaryContent) ?? secondaryContent;
                }
                else
                {
                    ClearValue(ForegroundProperty);
                }
            }
            else
            {
                var (bg, fg) = DaisyResourceLookup.GetPaletteBrushes(Palette.ToString());
                if (bg != null) Background = CloneBrush(bg) ?? bg;
                if (fg != null) Foreground = CloneBrush(fg) ?? fg;
            }

            SyncTitleForeground();
        }

        private void ApplySizing()
        {
            Padding = FlowKanbanResources.GetCardPadding(CardSize);
            CornerRadius = FlowKanbanResources.GetCardCornerRadius(CardSize);
            var titleSize = FlowKanbanResources.GetCardTitleFontSize(CardSize);
            FontSize = Math.Max(1, titleSize - 1);
            UpdateTitleToolTip();
        }

        private static void OnTaskChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowTaskCard card)
            {
                card.UpdateTaskBinding(e.OldValue as FlowTask, e.NewValue as FlowTask);
            }
        }

        private void UpdateTaskBinding(FlowTask? oldTask, FlowTask? newTask)
        {
            if (oldTask != null)
            {
                oldTask.PropertyChanged -= OnTaskPropertyChanged;
            }

            AttachSubtasksCollection(null);
            _trackedTask = newTask;

            if (newTask != null)
            {
                newTask.PropertyChanged += OnTaskPropertyChanged;
                AttachSubtasksCollection(newTask.Subtasks);
            }

            UpdateTaskVisuals();
            UpdateAutomationProperties();
            IsSelected = newTask?.IsSelected ?? false;
        }

        private void OnTaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(FlowTask.PlannedEndDate), StringComparison.Ordinal))
            {
                UpdateDueDateText();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowTask.Title), StringComparison.Ordinal))
            {
                UpdateAutomationProperties();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowTask.IsBlocked), StringComparison.Ordinal)
                     || string.Equals(e.PropertyName, nameof(FlowTask.BlockedReason), StringComparison.Ordinal)
                     || string.Equals(e.PropertyName, nameof(FlowTask.BlockedSince), StringComparison.Ordinal))
            {
                UpdateBlockedVisuals();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowTask.Priority), StringComparison.Ordinal)
                     || string.Equals(e.PropertyName, nameof(FlowTask.ProgressPercent), StringComparison.Ordinal))
            {
                UpdatePriorityAndProgress();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowTask.IsSelected), StringComparison.Ordinal))
            {
                if (sender is FlowTask task)
                {
                    IsSelected = task.IsSelected;
                }
            }
            else if (string.Equals(e.PropertyName, nameof(FlowTask.WorkItemNumber), StringComparison.Ordinal))
            {
                UpdateWorkItemNumber();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowTask.Assignee), StringComparison.Ordinal)
                     || string.Equals(e.PropertyName, nameof(FlowTask.AssigneeAvatarSource), StringComparison.Ordinal)
                     || string.Equals(e.PropertyName, nameof(FlowTask.AssigneeRoles), StringComparison.Ordinal))
            {
                UpdateAssignee();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowTask.PlannedStartDate), StringComparison.Ordinal)
                     || string.Equals(e.PropertyName, nameof(FlowTask.PlannedEndDate), StringComparison.Ordinal))
            {
                UpdateDateRange();
            }
            else if (string.Equals(e.PropertyName, nameof(FlowTask.Subtasks), StringComparison.Ordinal)
                     && sender is FlowTask task)
            {
                AttachSubtasksCollection(task.Subtasks);
                UpdateSubtaskSummary();
            }
        }

        private void OnSubtasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Reset)
            {
                DetachSubtasks();
                if (_trackedSubtasksCollection != null)
                {
                    AttachSubtasks(_trackedSubtasksCollection);
                }

                UpdateSubtaskSummary();
                return;
            }

            if (e.OldItems != null)
            {
                foreach (FlowSubtask subtask in e.OldItems)
                {
                    UntrackSubtask(subtask);
                }
            }

            if (e.NewItems != null)
            {
                foreach (FlowSubtask subtask in e.NewItems)
                {
                    TrackSubtask(subtask);
                }
            }

            UpdateSubtaskSummary();
        }

        private void OnSubtaskPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(FlowSubtask.IsCompleted), StringComparison.Ordinal))
            {
                UpdateSubtaskSummary();
            }
        }

        private void AttachSubtasks(IEnumerable<FlowSubtask> subtasks)
        {
            foreach (var subtask in subtasks)
            {
                TrackSubtask(subtask);
            }
        }

        private void AttachSubtasksCollection(ObservableCollection<FlowSubtask>? subtasks)
        {
            if (ReferenceEquals(_trackedSubtasksCollection, subtasks))
                return;

            if (_trackedSubtasksCollection != null)
            {
                _trackedSubtasksCollection.CollectionChanged -= OnSubtasksCollectionChanged;
            }

            DetachSubtasks();
            _trackedSubtasksCollection = subtasks;
            if (_trackedSubtasksCollection == null)
                return;

            _trackedSubtasksCollection.CollectionChanged += OnSubtasksCollectionChanged;
            AttachSubtasks(_trackedSubtasksCollection);
        }

        private void DetachSubtasks()
        {
            foreach (var subtask in _trackedSubtasks.ToArray())
            {
                subtask.PropertyChanged -= OnSubtaskPropertyChanged;
            }

            _trackedSubtasks.Clear();
        }

        private void TrackSubtask(FlowSubtask subtask)
        {
            if (_trackedSubtasks.Add(subtask))
            {
                subtask.PropertyChanged += OnSubtaskPropertyChanged;
            }
        }

        private void UntrackSubtask(FlowSubtask subtask)
        {
            if (_trackedSubtasks.Remove(subtask))
            {
                subtask.PropertyChanged -= OnSubtaskPropertyChanged;
            }
        }

        private void UpdateTaskVisuals()
        {
            UpdateWorkItemNumber();
            UpdateAssignee();
            UpdateDateRange();
            UpdateSubtaskSummary();
            UpdateDueDateText();
            UpdateBlockedVisuals();
            UpdatePriorityAndProgress();
        }

        private void UpdateAssignee()
        {
            var task = Task ?? _trackedTask;
            var assignee = task?.Assignee?.Trim();
            if (string.IsNullOrWhiteSpace(assignee))
            {
                HasAssignee = false;
                AssigneeText = string.Empty;
                AssigneeInitials = string.Empty;
                AssigneeAvatarSource = null;
                HasAssigneeAvatar = false;
                AssigneeRolesText = string.Empty;
                HasAssigneeRoles = false;
            }
            else
            {
                HasAssignee = true;
                AssigneeText = assignee;
                AssigneeInitials = BuildInitials(assignee);
                AssigneeAvatarSource = task?.AssigneeAvatarSource;
                HasAssigneeAvatar = AssigneeAvatarSource is not null;
                AssigneeRolesText = task?.AssigneeRoles is { Count: > 0 } roles
                    ? string.Join(", ", roles)
                    : string.Empty;
                HasAssigneeRoles = AssigneeRolesText.Length > 0;
            }

            UpdateFooterVisibility();
        }

        private void UpdateDateRange()
        {
            var task = Task ?? _trackedTask;
            if (task?.PlannedStartDate is { } startDate)
            {
                HasStartDate = true;
                StartDateText = FormatRelativeDate(startDate);
            }
            else
            {
                HasStartDate = false;
                StartDateText = string.Empty;
            }

            if (task?.PlannedEndDate is { } endDate)
            {
                HasEndDate = true;
                EndDateText = FormatRelativeDate(endDate);
            }
            else
            {
                HasEndDate = false;
                EndDateText = string.Empty;
            }

            HasDateRange = HasStartDate && HasEndDate;
            UpdateFooterVisibility();
        }

        private void UpdateFooterVisibility()
        {
            HasFooter = HasAssignee || HasStartDate || HasEndDate || HasProgress;
        }

        private static string FormatRelativeDate(DateTime date)
        {
            var today = DateTime.Today;
            var days = (date.Date - today).Days;

            if (days == 0)
                return FloweryLocalization.GetString("Kanban_Date_Today", "Today");

            if (days == -1)
                return FloweryLocalization.GetString("Kanban_Date_Yesterday", "Yesterday");

            if (days == 1)
                return FloweryLocalization.GetString("Kanban_Date_Tomorrow", "Tomorrow");

            if (days < 0)
            {
                return string.Format(
                    CultureInfo.CurrentUICulture,
                    FloweryLocalization.GetString("Kanban_Date_DaysAgo", "{0} days ago"),
                    Math.Abs(days));
            }

            return string.Format(
                CultureInfo.CurrentUICulture,
                FloweryLocalization.GetString("Kanban_Date_InDays", "in {0} days"),
                days);
        }

        private void UpdateWorkItemNumber()
        {
            var task = Task ?? _trackedTask;
            if (task == null || task.WorkItemNumber <= 0)
            {
                HasWorkItemNumber = false;
                WorkItemNumberText = string.Empty;
                return;
            }

            HasWorkItemNumber = true;
            WorkItemNumberText = task.WorkItemNumber.ToString(CultureInfo.CurrentUICulture);
        }

        private void UpdateSubtaskSummary()
        {
            var task = Task ?? _trackedTask;
            var count = task?.Subtasks.Count ?? 0;

            if (count <= 0)
            {
                HasSubtaskSummary = false;
                SubtaskSummaryText = string.Empty;
                return;
            }

            HasSubtaskSummary = true;
            var completed = task?.Subtasks.Count(s => s.IsCompleted) ?? 0;
            if (completed <= 0)
            {
                SubtaskSummaryText = count.ToString(CultureInfo.CurrentUICulture);
            }
            else
            {
                SubtaskSummaryText = string.Concat(
                    completed.ToString(CultureInfo.CurrentUICulture),
                    "/",
                    count.ToString(CultureInfo.CurrentUICulture));
            }
        }

        private void UpdateDueDateText()
        {
            var task = Task ?? _trackedTask;
            if (task?.PlannedEndDate is not { } dueDate)
            {
                HasDueDate = false;
                DueDateText = string.Empty;
                return;
            }

            var today = DateTime.Today;
            var daysUntil = (dueDate.Date - today).Days;

            if (daysUntil >= 0 && daysUntil <= 7)
            {
                if (daysUntil == 1)
                {
                    DueDateText = FloweryLocalization.GetString("Kanban_DueInDay", "in 1 day");
                }
                else
                {
                    DueDateText = string.Format(
                        CultureInfo.CurrentUICulture,
                        FloweryLocalization.GetString("Kanban_DueInDays", "in {0} days"),
                        daysUntil);
                }
            }
            else
            {
                DueDateText = dueDate.ToString("d", CultureInfo.CurrentUICulture);
            }

            HasDueDate = true;
        }

        private void UpdatePriorityAndProgress()
        {
            var task = Task ?? _trackedTask;
            if (task == null)
            {
                HasPriority = false;
                PriorityText = string.Empty;
                HasProgress = false;
                ProgressText = string.Empty;
                UpdateFooterVisibility();
                return;
            }

            if (task.Priority == FlowTaskPriority.Normal)
            {
                HasPriority = false;
                PriorityText = string.Empty;
            }
            else
            {
                HasPriority = true;
                PriorityText = GetPriorityLabel(task.Priority);
            }

            if (task.ProgressPercent > 0)
            {
                HasProgress = true;
                ProgressText = string.Concat(task.ProgressPercent.ToString(CultureInfo.CurrentUICulture), "%");
            }
            else
            {
                HasProgress = false;
                ProgressText = string.Empty;
            }

            UpdateFooterVisibility();
        }

        private static string GetPriorityLabel(FlowTaskPriority priority)
        {
            return priority switch
            {
                FlowTaskPriority.Low => FloweryLocalization.GetString("Kanban_Priority_Low", "Low"),
                FlowTaskPriority.High => FloweryLocalization.GetString("Kanban_Priority_High", "High"),
                FlowTaskPriority.Urgent => FloweryLocalization.GetString("Kanban_Priority_Urgent", "Urgent"),
                FlowTaskPriority.Normal => FloweryLocalization.GetString("Kanban_Priority_Normal", "Normal"),
                _ => priority.ToString()
            };
        }

        private void UpdateBlockedVisuals()
        {
            var task = Task ?? _trackedTask;
            if (task?.IsBlocked != true)
            {
                IsBlocked = false;
                BlockedBadgeText = string.Empty;
                BlockedDetailText = string.Empty;
                return;
            }

            IsBlocked = true;
            BlockedBadgeText = FloweryLocalization.GetString("Kanban_Blocked", "Blocked");

            if (!string.IsNullOrWhiteSpace(task.BlockedReason))
            {
                BlockedDetailText = task.BlockedReason!;
                return;
            }

            if (task.BlockedDays is { } days)
            {
                BlockedDetailText = string.Format(
                    CultureInfo.CurrentUICulture,
                    FloweryLocalization.GetString("Kanban_BlockedDays", "Blocked {0}d"),
                    days);
                return;
            }

            BlockedDetailText = FloweryLocalization.GetString("Kanban_Blocked", "Blocked");
        }

        protected override void OnThemeChanged(string themeName)
        {
            base.OnThemeChanged(themeName);
            ApplyPalette();
            ApplySizing();
            ApplySelectionVisual();
        }

        internal void RefreshTheme()
        {
            ApplyPalette();
            ApplySizing();
        }

        internal void RefreshLocalization()
        {
            UpdateTaskVisuals();
            UpdateAutomationProperties();
            UpdateSelectionBadgePlacement();
            if (_closeButton != null)
            {
                AutomationProperties.SetName(_closeButton, FloweryLocalization.GetString("Common_Delete"));
            }
        }

        private void AttachTitleTextBlock(INameScope nameScope)
        {
            DetachTitleTextBlock();
            _titleTextBlock = nameScope.Find<TextBlock>("PART_TitleText");
            if (_titleTextBlock == null)
                return;

            _titleTextBlock.SizeChanged += OnTitleSizeChanged;
            _titleTextBlock.Loaded += OnTitleLoaded;
            _titleTextBlock.PointerPressed += OnTitlePointerPressed;
            _titleTextBlock.PointerMoved += OnTitlePointerMoved;
            _titleTextBlock.PointerReleased += OnTitlePointerReleased;
            _titleTextBlock.PointerCaptureLost += OnTitlePointerCaptureLost;
            UpdateTitleToolTip();
            SyncTitleForeground();
        }

        private void DetachTitleTextBlock()
        {
            if (_titleTextBlock != null)
            {
                _titleTextBlock.SizeChanged -= OnTitleSizeChanged;
                _titleTextBlock.Loaded -= OnTitleLoaded;
                _titleTextBlock.PointerPressed -= OnTitlePointerPressed;
                _titleTextBlock.PointerMoved -= OnTitlePointerMoved;
                _titleTextBlock.PointerReleased -= OnTitlePointerReleased;
                _titleTextBlock.PointerCaptureLost -= OnTitlePointerCaptureLost;
                _titleTextBlock = null;
            }

            CancelTitleLongPress();
        }

        private void OnTitleSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            UpdateTitleToolTip();
        }

        private void OnTitleLoaded(object? sender, RoutedEventArgs e)
        {
            UpdateTitleToolTip();
        }

        private void UpdateTitleToolTip()
        {
            if (_titleTextBlock == null)
                return;

            var title = Title ?? string.Empty;
            if (string.IsNullOrWhiteSpace(title))
            {
                ToolTip.SetTip(_titleTextBlock, null);
                return;
            }

            var availableWidth = _titleTextBlock.Bounds.Width;
            if (double.IsNaN(availableWidth) || availableWidth <= 1)
            {
                ToolTip.SetTip(_titleTextBlock, null);
                return;
            }

            var isTrimmed = IsTitleTrimmed(title, _titleTextBlock, availableWidth);

            ToolTip.SetTip(_titleTextBlock, isTrimmed ? title : null);
        }

        private void OnTitlePointerPressed(object? sender, PointerEventArgs e)
        {
            if (!IsMobileSelectionMode())
                return;

            if (e.Pointer.Type != PointerType.Touch)
                return;

            if (sender is not Control element)
                return;

            _titlePressPointerId = e.Pointer.Id;
            _titlePressStartPoint = e.GetCurrentPoint(element).Position;
            _isTitlePressTracking = true;
            StartTitleLongPressTimer();
        }

        private void OnTitlePointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isTitlePressTracking || e.Pointer.Id != _titlePressPointerId)
                return;

            if (sender is not Control element)
                return;

            var current = e.GetCurrentPoint(element).Position;
            var dx = current.X - _titlePressStartPoint.X;
            var dy = current.Y - _titlePressStartPoint.Y;
            if ((dx * dx) + (dy * dy) > (TitleLongPressMoveThreshold * TitleLongPressMoveThreshold))
            {
                CancelTitleLongPress();
            }
        }

        private void OnTitlePointerReleased(object? sender, PointerEventArgs e)
        {
            if (e.Pointer.Id != _titlePressPointerId)
                return;

            CancelTitleLongPress();
        }

        private void OnTitlePointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            CancelTitleLongPress();
        }

        private void StartTitleLongPressTimer()
        {
            _titleLongPressTimer ??= new DispatcherTimer();
            _titleLongPressTimer.Stop();
            _titleLongPressTimer.Interval = TimeSpan.FromMilliseconds(TitleLongPressMilliseconds);
            _titleLongPressTimer.Tick -= OnTitleLongPressTimerTick;
            _titleLongPressTimer.Tick += OnTitleLongPressTimerTick;
            _titleLongPressTimer.Start();
        }

        private void OnTitleLongPressTimerTick(object? sender, EventArgs args)
        {
            _titleLongPressTimer?.Stop();

            if (!_isTitlePressTracking)
                return;

            _isTitlePressTracking = false;
            if (TryExecuteEdit())
            {
                Focus();
            }
        }

        private void CancelTitleLongPress()
        {
            _isTitlePressTracking = false;
            _titlePressPointerId = 0;
            _titleLongPressTimer?.Stop();
        }

        private void SyncTitleForeground()
        {
            if (_titleTextBlock == null)
                return;

            if (_titleTextBlock.IsSet(TextBlock.ForegroundProperty))
                return;

            if (Foreground is IBrush brush)
                _titleTextBlock.Foreground = brush;
        }

        private void UpdateCloseButtonVisibility()
        {
            var isVisible = _isPointerOver || IsFocusWithin();
            if (_closeButton != null)
            {
                _closeButton.IsHitTestVisible = isVisible;
                _closeButton.Opacity = isVisible ? 0.7 : 0;
            }

            var showPill = IsSelected || isVisible;
            IsActionPillVisible = showPill;
            ActionPillOpacity = showPill ? 1 : 0;
        }

        private void UpdateFocusVisualState()
        {
            var shouldHighlight = IsFocusWithin() && IsKeyboardFocusActive();
            if (shouldHighlight == _isKeyboardFocusVisible)
                return;

            _isKeyboardFocusVisible = shouldHighlight;
            Classes.Set("keyboard-focus", shouldHighlight);
        }

        private bool IsKeyboardFocusActive()
        {
            if (TopLevel == null)
                return false;

            var focused = FlowKanbanVisualTree.GetFocusedElement(this) as Control;
            return focused?.Classes.Contains(":focus-visible") == true;
        }

        private void UpdateAutomationProperties()
        {
            var title = Title;
            if (string.IsNullOrWhiteSpace(title))
                title = Task?.Title ?? string.Empty;

            if (string.IsNullOrWhiteSpace(title))
            {
                ClearValue(AutomationProperties.NameProperty);
            }
            else
            {
                AutomationProperties.SetName(this, title);
            }

            var task = Task ?? _trackedTask;
            if (task == null)
            {
                ClearValue(AutomationProperties.AutomationIdProperty);
            }
            else
            {
                AutomationProperties.SetAutomationId(this, $"kanban-card-{task.Id}");
            }
        }

        private void UpdateSelectionState(bool isSelected)
        {
            ApplySelectionVisual();

            if (_trackedTask != null && _trackedTask.IsSelected != isSelected)
            {
                _trackedTask.IsSelected = isSelected;
            }

            UpdateCloseButtonVisibility();
        }

        private void UpdateSelectionBadgePlacement()
        {
            SelectionBadgeColumn = FloweryLocalization.Instance.IsRtl ? 0 : 2;
            ActionPillHorizontalAlignment = FloweryLocalization.Instance.IsRtl
                ? HorizontalAlignment.Left
                : HorizontalAlignment.Right;
            ActionPillOffsetX = FloweryLocalization.Instance.IsRtl ? -2 : 2;
        }

        private void ApplySelectionVisual()
        {
            if (_rootBorder == null)
                return;

            if (!IsSelected)
            {
                _rootBorder.BorderThickness = _defaultBorderThickness;
                _rootBorder.BorderBrush = TransparentBorderBrush;
                return;
            }

            var accent = DaisyResourceLookup.GetBrush("DaisyAccentBrush");
            if (accent != null)
                _rootBorder.BorderBrush = accent;
            _rootBorder.BorderThickness = _defaultBorderThickness;
        }

        private bool IsFocusWithin()
        {
            if (TopLevel == null)
                return false;

            var focused = FlowKanbanVisualTree.GetFocusedElement(this);
            if (focused == null)
                return false;

            return FindAncestor<FlowTaskCard>(focused) == this;
        }

        private static T? FindAncestor<T>(AvaloniaObject? element) where T : AvaloniaObject
        {
            return FlowKanbanVisualTree.FindAncestor<T>(element);
        }

        private static bool IsTitleTrimmed(string text, TextBlock reference, double availableWidth)
        {
            const int maxLines = 2;
            var limitedHeight = MeasureTextHeight(text, reference, availableWidth, maxLines);
            var fullHeight = MeasureTextHeight(text, reference, availableWidth, null);

            return fullHeight > limitedHeight + 1;
        }

        private static double MeasureTextHeight(string text, TextBlock reference, double width, int? maxLines)
        {
            var measuringText = new TextBlock
            {
                Text = text,
                FontFamily = reference.FontFamily,
                FontSize = reference.FontSize,
                FontStyle = reference.FontStyle,
                FontWeight = reference.FontWeight,
                FontStretch = reference.FontStretch,
                TextWrapping = TextWrapping.Wrap,
                TextTrimming = TextTrimming.CharacterEllipsis
            };

            if (reference.LineHeight > 0)
            {
                measuringText.LineHeight = reference.LineHeight;
            }

            if (maxLines.HasValue)
            {
                measuringText.MaxLines = maxLines.Value;
            }

            measuringText.Measure(new Size(width, double.PositiveInfinity));
            return measuringText.DesiredSize.Height;
        }

        private static IBrush? CloneBrush(IBrush? brush)
        {
            if (brush is SolidColorBrush scb)
            {
                return new SolidColorBrush(scb.Color);
            }

            return brush;
        }

        private static string BuildInitials(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            var trimmed = name.Trim();
            var parts = trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
                return string.Empty;

            if (parts.Length == 1)
            {
                var single = parts[0];
                if (single.Length >= 2)
                {
                    return string.Concat(single[0], single[1]).ToUpper(CultureInfo.CurrentUICulture);
                }

                return single.ToUpper(CultureInfo.CurrentUICulture);
            }

            var first = parts[0][0];
            var last = parts[^1][0];
            return string.Concat(first, last).ToUpper(CultureInfo.CurrentUICulture);
        }

        private static bool IsMobileSelectionMode()
        {
            return OperatingSystem.IsAndroid() || OperatingSystem.IsIOS();
        }
    }

    internal sealed class FlowTaskCardAutomationPeer : ControlAutomationPeer, IInvokeProvider
    {
        public FlowTaskCardAutomationPeer(FlowTaskCard owner)
            : base(owner)
        {
        }

        private new FlowTaskCard Owner => (FlowTaskCard)base.Owner;

        public void Invoke()
        {
            if (!Owner.IsEnabled)
            {
                throw new InvalidOperationException("The Kanban task card is disabled.");
            }

            if (!Owner.TryExecuteEdit())
            {
                throw new InvalidOperationException("The Kanban task card edit action is unavailable.");
            }
        }

        protected override AutomationControlType GetAutomationControlTypeCore() =>
            AutomationControlType.ListItem;

        protected override string GetClassNameCore() => nameof(FlowTaskCard);

        protected override bool IsContentElementCore() =>
            FlowKanbanVisualTree.IsAutomationVisible(Owner);

        protected override bool IsControlElementCore() =>
            FlowKanbanVisualTree.IsAutomationVisible(Owner);
    }
}
