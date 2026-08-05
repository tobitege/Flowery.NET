using System;
using Flowery.Localization;
using Flowery.Theming;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// An inline add card control that toggles between a button and a text input.
    /// </summary>
    public partial class FlowKanbanAddCard : FlowKanbanContentControl
    {
        static FlowKanbanAddCard()
        {
            AddCardSizeProperty.Changed.AddClassHandler<FlowKanbanAddCard>((control, _) => control.ApplySizing());
            AddCardTextProperty.Changed.AddClassHandler<FlowKanbanAddCard>(OnAutomationContextChanged);
            AddCardPlaceholderTextProperty.Changed.AddClassHandler<FlowKanbanAddCard>(OnAutomationContextChanged);
            ColumnDataProperty.Changed.AddClassHandler<FlowKanbanAddCard>(OnAutomationContextChanged);
            LaneIdProperty.Changed.AddClassHandler<FlowKanbanAddCard>(OnAutomationContextChanged);
            InsertAtTopProperty.Changed.AddClassHandler<FlowKanbanAddCard>(OnAutomationContextChanged);
            IsEditingProperty.Changed.AddClassHandler<FlowKanbanAddCard>(OnIsEditingChanged);
        }

        private DaisyButton? _addButton;
        private Grid? _inputPanel;
        private DaisyInput? _titleInput;
        private FlowKanban? _parentKanban;

        public FlowKanbanAddCard()
        {
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            // Find parent FlowKanban and subscribe to its size changes
            _parentKanban = FindParentKanban();
            if (_parentKanban != null)
            {
                AddCardSize = _parentKanban.BoardSize;
                _parentKanban.BoardSizeChanged += OnParentSizeChanged;
                ApplySizing();
            }
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_parentKanban != null)
            {
                _parentKanban.BoardSizeChanged -= OnParentSizeChanged;
                _parentKanban = null;
            }
        }

        private void OnParentSizeChanged(object? sender, DaisySize newSize)
        {
            AddCardSize = newSize;
            ApplySizing();
        }

        private FlowKanban? FindParentKanban()
        {
            return FlowKanbanVisualTree.FindAncestor<FlowKanban>(this, includeSelf: false);
        }

        #region AddCardSize
        public static readonly StyledProperty<DaisySize> AddCardSizeProperty =
            AvaloniaProperty.Register<FlowKanbanAddCard, DaisySize>(
                                nameof(AddCardSize),
                                DaisySize.Medium);

        public DaisySize AddCardSize
        {
            get => (DaisySize)GetValue(AddCardSizeProperty);
            set => SetValue(AddCardSizeProperty, value);
        }
        #endregion

        #region AddCardText
        public static readonly StyledProperty<string> AddCardTextProperty =
            AvaloniaProperty.Register<FlowKanbanAddCard, string>(
                                nameof(AddCardText),
                                FloweryLocalization.GetString("Kanban_AddCard"));

        public string AddCardText
        {
            get => (string)GetValue(AddCardTextProperty);
            set => SetValue(AddCardTextProperty, value);
        }

        #endregion

        #region AddCardPlaceholderText
        public static readonly StyledProperty<string> AddCardPlaceholderTextProperty =
            AvaloniaProperty.Register<FlowKanbanAddCard, string>(
                                nameof(AddCardPlaceholderText),
                                FloweryLocalization.GetString("Kanban_AddCardPlaceholder"));

        public string AddCardPlaceholderText
        {
            get => (string)GetValue(AddCardPlaceholderTextProperty);
            set => SetValue(AddCardPlaceholderTextProperty, value);
        }
        #endregion

        #region AddCardConfirmText
        public static readonly StyledProperty<string> AddCardConfirmTextProperty =
            AvaloniaProperty.Register<FlowKanbanAddCard, string>(
                nameof(AddCardConfirmText),
                FloweryLocalization.GetString("Common_Add"));

        public string AddCardConfirmText
        {
            get => (string)GetValue(AddCardConfirmTextProperty);
            set => SetValue(AddCardConfirmTextProperty, value);
        }
        #endregion

        #region AddCardCancelText
        public static readonly StyledProperty<string> AddCardCancelTextProperty =
            AvaloniaProperty.Register<FlowKanbanAddCard, string>(
                nameof(AddCardCancelText),
                FloweryLocalization.GetString("Common_Cancel"));

        public string AddCardCancelText
        {
            get => (string)GetValue(AddCardCancelTextProperty);
            set => SetValue(AddCardCancelTextProperty, value);
        }
        #endregion

        private void ApplySizing()
        {
            FontSize = FlowKanbanResources.GetCardTitleFontSize(AddCardSize);
        }

        #region ColumnData
        public static readonly StyledProperty<FlowKanbanColumnData?> ColumnDataProperty =
            AvaloniaProperty.Register<FlowKanbanAddCard, FlowKanbanColumnData?>(
                                nameof(ColumnData),
                                default!);

        public FlowKanbanColumnData? ColumnData
        {
            get => (FlowKanbanColumnData?)GetValue(ColumnDataProperty);
            set => SetValue(ColumnDataProperty, value);
        }
        #endregion

        #region LaneId
        public static readonly StyledProperty<string?> LaneIdProperty =
            AvaloniaProperty.Register<FlowKanbanAddCard, string?>(
                                nameof(LaneId),
                                default!);

        public string? LaneId
        {
            get => (string?)GetValue(LaneIdProperty);
            set => SetValue(LaneIdProperty, value);
        }
        #endregion

        #region InsertAtTop
        public static readonly StyledProperty<bool> InsertAtTopProperty =
            AvaloniaProperty.Register<FlowKanbanAddCard, bool>(
                                nameof(InsertAtTop),
                                false);

        public bool InsertAtTop
        {
            get => (bool)GetValue(InsertAtTopProperty);
            set => SetValue(InsertAtTopProperty, value);
        }
        #endregion

        #region IsEditing
        public static readonly StyledProperty<bool> IsEditingProperty =
            AvaloniaProperty.Register<FlowKanbanAddCard, bool>(
                                nameof(IsEditing),
                                false);

        public bool IsEditing
        {
            get => (bool)GetValue(IsEditingProperty);
            set => SetValue(IsEditingProperty, value);
        }

        private static void OnIsEditingChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanAddCard control)
            {
                control.UpdateVisualState();
            }
        }
        #endregion

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            _addButton = e.NameScope.Find<DaisyButton>("PART_AddButton");
            _inputPanel = e.NameScope.Find<Grid>("PART_InputPanel");
            _titleInput = e.NameScope.Find<DaisyInput>("PART_TitleInput");
            UpdateAutomationProperties();

            if (_addButton != null)
            {
                _addButton.Click += OnAddButtonClick;
            }

            if (_titleInput != null)
            {
                _titleInput.AcceptsReturn = false;
                _titleInput.TextWrapping = TextWrapping.NoWrap;
                _titleInput.KeyDown += OnTitleInputKeyDown;
                _titleInput.LostFocus += OnTitleInputLostFocus;
            }

            var cancelButton = e.NameScope.Find<DaisyButton>("PART_CancelButton");
            if (cancelButton != null)
            {
                cancelButton.Click += OnCancelButtonClick;
            }

            var confirmButton = e.NameScope.Find<DaisyButton>("PART_ConfirmButton");
            if (confirmButton != null)
            {
                confirmButton.Click += OnConfirmButtonClick;
            }

            UpdateVisualState();
        }

        internal bool FocusForKeyboard()
        {
            if (IsEditing && _titleInput != null)
            {
                _titleInput.Focus();
                _titleInput.SelectAll();
                return true;
            }

            if (_addButton != null)
            {
                _addButton.Focus(NavigationMethod.Directional);
                return true;
            }

            return false;
        }

        internal void BeginInlineAdd()
        {
            IsEditing = true;
            _titleInput?.Focus();
            _titleInput?.SelectAll();
        }

        private void UpdateAutomationProperties()
        {
            if (_addButton != null)
            {
                DaisyAccessibility.ApplyAutomationProperties(this, _addButton, AddCardText);
                SetContextualAutomationId(_addButton, "button");
            }

            if (_titleInput != null)
            {
                DaisyAccessibility.ApplyAutomationProperties(
                    this,
                    _titleInput,
                    AddCardPlaceholderText);
                SetContextualAutomationId(_titleInput, "title");
            }
        }

        private static void OnAutomationContextChanged(
            AvaloniaObject sender,
            AvaloniaPropertyChangedEventArgs _)
        {
            if (sender is FlowKanbanAddCard control)
            {
                control.UpdateAutomationProperties();
            }
        }

        private void SetContextualAutomationId(Control target, string role)
        {
            if (ColumnData?.Id is not { Length: > 0 } columnId)
            {
                return;
            }

            var laneId = string.IsNullOrWhiteSpace(LaneId) ? "all" : LaneId;
            var placement = InsertAtTop ? "top" : "bottom";
            AutomationProperties.SetAutomationId(
                target,
                $"kanban-add-card-{columnId}-{laneId}-{placement}-{role}");
        }

        private void OnAddButtonClick(object? sender, RoutedEventArgs e)
        {
            IsEditing = true;
        }

        private void OnTitleInputKeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                AddCard();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelAdd();
                e.Handled = true;
            }
        }

        private void OnTitleInputLostFocus(object? sender, RoutedEventArgs e)
        {
            // Don't cancel if focus moved to confirm/cancel buttons
            // This is handled by the button clicks
        }

        private void OnCancelButtonClick(object? sender, RoutedEventArgs e)
        {
            CancelAdd();
        }

        private void OnConfirmButtonClick(object? sender, RoutedEventArgs e)
        {
            AddCard();
        }

        private void AddCard()
        {
            if (ColumnData == null || _titleInput == null)
                return;

            var title = _titleInput.Text?.Trim();
            if (!string.IsNullOrEmpty(title))
            {
                string? laneId = null;
                if (!string.IsNullOrWhiteSpace(LaneId))
                {
                    laneId = FlowKanban.IsUnassignedLaneId(LaneId) ? null : LaneId;
                }
                else if (_parentKanban?.IsLaneGroupingEnabled == true && _parentKanban.Board.Lanes.Count > 0)
                {
                    var candidateLaneId = _parentKanban.Board.Lanes[0].Id;
                    laneId = string.IsNullOrWhiteSpace(candidateLaneId) ? null : candidateLaneId;
                }

                var task = new FlowTask { Title = title, LaneId = laneId };
                FlowKanbanWorkItemNumberHelper.EnsureTaskNumber(_parentKanban?.Board, task);
                if (InsertAtTop)
                {
                    ColumnData.Tasks.Insert(0, task);
                }
                else
                {
                    ColumnData.Tasks.Add(task);
                }
                FlowKanbanDoneColumnHelper.UpdateCompletedAtOnAdd(_parentKanban?.Board, ColumnData, task);
            }

            _titleInput.Text = string.Empty;
            IsEditing = false;
        }

        private void CancelAdd()
        {
            if (_titleInput != null)
            {
                _titleInput.Text = string.Empty;
            }
            IsEditing = false;
        }

        private void UpdateVisualState()
        {
            if (_addButton != null)
            {
                _addButton.IsVisible = !IsEditing;
            }
            if (_inputPanel != null)
            {
                _inputPanel.IsVisible = IsEditing;
            }
            if (IsEditing)
            {
                _titleInput?.Focus();
                _titleInput?.SelectAll();
            }
        }
    }
}
