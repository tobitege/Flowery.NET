using System;
using System.Threading.Tasks;
using Flowery.Localization;
using Flowery.Theming;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Partial class containing all dialog-related methods for FlowKanban.
    /// All dialogs use themed DaisyButton controls for visual consistency.
    /// </summary>
    public partial class FlowKanban
    {
        #region Dialog Execution Methods

        private async void ExecuteAddColumn()
        {
            if (!CanExecuteColumnOperation())
                return;

            var result = await ShowInputDialogAsync(
                FloweryLocalization.GetString("Kanban_AddSection"),
                string.Empty,
                FloweryLocalization.GetString("Kanban_NewColumnPlaceholder"),
                closeOnEnter: true);

            if (result != null)
            {
                var column = new FlowKanbanColumnData { Title = result };
                Board.Columns.Add(column);
                UpdateKeyboardColumnIndex(column);
                QueueFocusAction(() => FocusColumnHeader(column));
            }
        }

        private async void ExecuteRemoveColumn(FlowKanbanColumnData? column)
        {
            if (column == null || !CanExecuteColumnOperation(column))
                return;

            if (!ConfirmColumnRemovals)
            {
                Board.Columns.Remove(column);
                return;
            }

            var confirmed = await ShowConfirmDialogAsync(
                FloweryLocalization.GetString("Common_ConfirmDelete"),
                FloweryLocalization.GetString("Kanban_DeleteColumnConfirm"),
                FloweryLocalization.GetString("Common_Delete"));

            if (confirmed)
            {
                Board.Columns.Remove(column);
            }
        }

        private async void ExecuteEditColumn(FlowKanbanColumnData? column)
        {
            if (column == null || !CanExecuteColumnOperation(column))
                return;

            var xamlRoot = TopLevel;
            if (xamlRoot == null)
            {
                return;
            }

            await FlowKanbanColumnEditorDialog.ShowAsync(column, xamlRoot);
        }

        private async void ExecuteEditBoard()
        {
            var xamlRoot = TopLevel;
            if (xamlRoot == null)
            {
                return;
            }

            await FlowBoardEditorDialog.ShowAsync(Board, xamlRoot);
        }

        private async void ExecuteRenameBoard()
        {
            var result = await ShowInputDialogAsync(
                FloweryLocalization.GetString("Kanban_Board_RenameTitle"),
                Board.Title,
                FloweryLocalization.GetString("Kanban_Board_TitlePlaceholder"),
                closeOnEnter: true);

            if (result != null)
            {
                Board.Title = result;
            }
        }

        private async void ExecuteRemoveCard(FlowTask? task)
        {
            if (task == null) return;

            if (!ConfirmCardRemovals)
            {
                RemoveCard(task);
                return;
            }

            var confirmed = await ShowConfirmDialogAsync(
                FloweryLocalization.GetString("Common_ConfirmDelete"),
                FloweryLocalization.GetString("Kanban_DeleteCardConfirm"),
                FloweryLocalization.GetString("Common_Delete"));

            if (confirmed)
            {
                RemoveCard(task);
            }
        }

        private void RemoveCard(FlowTask task)
        {
            foreach (var col in Board.Columns)
            {
                if (col.Tasks.Contains(task))
                {
                    ExecuteCommand(new DeleteCardCommand(col, task));
                    break;
                }
            }
        }

        private async void ExecuteOpenSettings()
        {
            if (TopLevel == null) return;
            await FlowKanbanSettingsDialog.ShowAsync(this, TopLevel);
        }

        private async void ExecuteShowKeyboardHelp()
        {
            if (TopLevel == null) return;
            await FlowKanbanKeyboardHelpDialog.ShowAsync(TopLevel);
        }

        #endregion

        #region Themed Dialog Helpers

        /// <summary>
        /// Shows a themed input dialog with a single text input field.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="initialValue">Initial value for the input.</param>
        /// <param name="placeholder">Placeholder text for the input.</param>
        /// <param name="closeOnEnter">Whether Enter confirms the dialog.</param>
        /// <returns>The entered text if saved, null if cancelled.</returns>
        private Task<string?> ShowInputDialogAsync(string title, string initialValue, string placeholder, bool closeOnEnter = false)
        {
            if (TopLevel == null)
                return Task.FromResult<string?>(null);

            return FlowKanbanInputDialog.ShowAsync(title, initialValue, placeholder, TopLevel, closeOnEnter);
        }

        /// <summary>
        /// Shows a themed confirmation dialog.
        /// </summary>
        /// <param name="title">Dialog title.</param>
        /// <param name="message">Confirmation message.</param>
        /// <param name="confirmText">Text for the confirm button (default: "Delete").</param>
        /// <returns>True if confirmed, false if cancelled.</returns>
        private Task<bool> ShowConfirmDialogAsync(string title, string message, string? confirmText = null)
        {
            if (TopLevel == null)
                return Task.FromResult(false);

            return FlowKanbanConfirmDialog.ShowAsync(
                title,
                message,
                confirmText ?? FloweryLocalization.GetString("Common_Delete"),
                DaisyButtonVariant.Error,
                TopLevel);
        }

        /// <summary>
        /// Creates content for a simple dialog with title, content, and action buttons.
        /// </summary>
        private static Control CreateSimpleDialogContent(
            string title,
            Control mainContent,
            out DaisyButton primaryButton,
            out DaisyButton cancelButton,
            string? primaryText = null,
            DaisyButtonVariant primaryVariant = DaisyButtonVariant.Success)
        {
            var container = new StackPanel
            {
                Spacing = 16,
                MinWidth = 280
            };

            // Title
            container.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
            });

            // Main content
            container.Children.Add(mainContent);

            // Buttons
            var buttonPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 12,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            primaryButton = new DaisyButton
            {
                Content = primaryText ?? FloweryLocalization.GetString("Common_Save"),
                Variant = primaryVariant,
                MinWidth = 80
            };

            cancelButton = new DaisyButton
            {
                Content = FloweryLocalization.GetString("Common_Cancel"),
                Variant = DaisyButtonVariant.Error,
                MinWidth = 80
            };

            buttonPanel.Children.Add(primaryButton);
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

        #endregion

        #region FlowKanbanDialogBase Dialogs

        private sealed partial class FlowKanbanInputDialog : FlowKanbanDialogBase
        {
            private readonly TaskCompletionSource<string?> _tcs = new();
            private readonly TopLevel _xamlRoot;
            private bool _isClosing;

            private readonly DaisyInput _input;
            private readonly DaisyButton _saveButton;
            private readonly DaisyButton _cancelButton;

            private FlowKanbanInputDialog(
                string title,
                string initialValue,
                string placeholder,
                TopLevel xamlRoot,
                bool closeOnEnter)
            {
                AutomationProperties.SetName(this, title);
                _xamlRoot = xamlRoot;

                _input = new DaisyInput
                {
                    Text = initialValue,
                    PlaceholderText = placeholder,
                    Variant = DaisyInputVariant.Bordered,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                AutomationProperties.SetName(_input, title);

                Content = CreateSimpleDialogContent(title, _input, out _saveButton, out _cancelButton);
                IsCloseOnEnterEnabled = closeOnEnter;
                ApplySmartSizingWithAutoHeight(xamlRoot);

                _saveButton.Click += OnSaveClicked;
                _cancelButton.Click += OnCancelClicked;
            }

            public static Task<string?> ShowAsync(
                string title,
                string initialValue,
                string placeholder,
                TopLevel xamlRoot,
                bool closeOnEnter)
            {
                if (xamlRoot == null)
                    return Task.FromResult<string?>(null);

                var dialog = new FlowKanbanInputDialog(title, initialValue, placeholder, xamlRoot, closeOnEnter);
                return dialog.ShowInternalAsync();
            }

            private Task<string?> ShowInternalAsync()
            {

                IsOpen = true;
                _input.Focus();
                _input.SelectAll();
                return _tcs.Task;
            }

            protected override bool OnEnterKeyRequested()
            {
                CommitAndClose();
                return true;
            }

            private void OnSaveClicked(object? sender, RoutedEventArgs e)
            {
                CommitAndClose();
            }

            private void OnCancelClicked(object? sender, RoutedEventArgs e)
            {
                Close(null);
            }

            protected override void OnDialogOpenChanged(bool isOpen)
            {
                if (!isOpen && !_isClosing)
                {
                    Close(null);
                }
            }

            private void CommitAndClose()
            {
                var text = _input.Text?.Trim();
                Close(string.IsNullOrEmpty(text) ? null : text);
            }

            private void Close(string? result)
            {
                if (_isClosing)
                    return;

                _isClosing = true;
                if (IsOpen)
                    IsOpen = false;



                _tcs.TrySetResult(result);
            }
        }

        private sealed partial class FlowKanbanColumnEditorDialog : FlowKanbanDialogBase
        {
            private readonly TaskCompletionSource<bool> _tcs = new();
            private readonly FlowKanbanColumnData _column;
            private readonly TopLevel _xamlRoot;
            private bool _isClosing;

            private readonly DaisyInput _titleInput;
            private readonly DaisyTextArea _policyBox;
            private readonly DaisyButton _saveButton;
            private readonly DaisyButton _cancelButton;

            private FlowKanbanColumnEditorDialog(FlowKanbanColumnData column, TopLevel xamlRoot)
            {
                _column = column;
                _xamlRoot = xamlRoot;

                var contentStack = new StackPanel
                {
                    Spacing = 12,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };

                var titleLabel = new TextBlock
                {
                    Text = FloweryLocalization.GetString("Kanban_RenameSection"),
                    FontSize = 11,
                    Opacity = 0.7,
                    Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
                };
                contentStack.Children.Add(titleLabel);

                _titleInput = new DaisyInput
                {
                    Text = column.Title,
                    PlaceholderText = FloweryLocalization.GetString("Kanban_NewColumnPlaceholder"),
                    Variant = DaisyInputVariant.Bordered,
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                AutomationProperties.SetLabeledBy(_titleInput, titleLabel);
                contentStack.Children.Add(_titleInput);

                var policyLabel = new TextBlock
                {
                    Text = FloweryLocalization.GetString("Kanban_Policy_Info"),
                    FontSize = 11,
                    Opacity = 0.7,
                    Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
                };
                contentStack.Children.Add(policyLabel);

                _policyBox = new DaisyTextArea
                {
                    Text = column.PolicyText ?? string.Empty,
                    Variant = DaisyInputVariant.Bordered,
                    PlaceholderText = FloweryLocalization.GetString("Kanban_Policies_Placeholder"),
                    HorizontalAlignment = HorizontalAlignment.Stretch
                };
                FlowKanbanControlFactory.SetTextAreaRows(_policyBox, 2, 4);
                AutomationProperties.SetLabeledBy(_policyBox, policyLabel);
                contentStack.Children.Add(_policyBox);

                var dialogTitle = string.IsNullOrWhiteSpace(column.Title)
                    ? FloweryLocalization.GetString("Kanban_RenameSection")
                    : column.Title;

                AutomationProperties.SetName(this, dialogTitle);
                Content = CreateSimpleDialogContent(dialogTitle, contentStack, out _saveButton, out _cancelButton);
                ApplySmartSizingWithAutoHeight(xamlRoot);

                _saveButton.Click += OnSaveClicked;
                _cancelButton.Click += OnCancelClicked;
            }

            public static Task<bool> ShowAsync(FlowKanbanColumnData column, TopLevel xamlRoot)
            {
                if (xamlRoot == null)
                    return Task.FromResult(false);

                var dialog = new FlowKanbanColumnEditorDialog(column, xamlRoot);
                return dialog.ShowInternalAsync();
            }

            private Task<bool> ShowInternalAsync()
            {

                IsOpen = true;
                _titleInput.Focus();
                _titleInput.SelectAll();
                return _tcs.Task;
            }

            private void OnSaveClicked(object? sender, RoutedEventArgs e)
            {
                CommitAndClose();
            }

            private void OnCancelClicked(object? sender, RoutedEventArgs e)
            {
                Close(false);
            }

            protected override void OnDialogOpenChanged(bool isOpen)
            {
                if (!isOpen && !_isClosing)
                {
                    Close(false);
                }
            }

            private void CommitAndClose()
            {
                var title = _titleInput.Text?.Trim();
                if (!string.IsNullOrWhiteSpace(title))
                {
                    _column.Title = title;
                }

                var policy = _policyBox.Text?.Trim();
                _column.PolicyText = string.IsNullOrWhiteSpace(policy) ? null : policy;

                Close(true);
            }

            private void Close(bool saved)
            {
                if (_isClosing)
                    return;

                _isClosing = true;
                if (IsOpen)
                    IsOpen = false;



                _tcs.TrySetResult(saved);
            }
        }

        private sealed partial class FlowKanbanConfirmDialog : FlowKanbanDialogBase
        {
            private readonly TaskCompletionSource<bool> _tcs = new();
            private readonly TopLevel _xamlRoot;
            private bool _isClosing;

            private readonly DaisyButton _confirmButton;
            private readonly DaisyButton _cancelButton;

            private FlowKanbanConfirmDialog(
                string title,
                string message,
                string confirmText,
                DaisyButtonVariant confirmVariant,
                TopLevel xamlRoot)
            {
                AutomationProperties.SetName(this, title);
                _xamlRoot = xamlRoot;

                var messageBlock = new TextBlock
                {
                    Text = message,
                    TextWrapping = TextWrapping.Wrap,
                    Foreground = DaisyResourceLookup.GetBrush("DaisyBaseContentBrush")
                };

                Content = CreateSimpleDialogContent(
                    title,
                    messageBlock,
                    out _confirmButton,
                    out _cancelButton,
                    confirmText,
                    confirmVariant);

                ApplySmartSizingWithAutoHeight(xamlRoot);

                _confirmButton.Click += OnConfirmClicked;
                _cancelButton.Click += OnCancelClicked;
            }

            public static Task<bool> ShowAsync(
                string title,
                string message,
                string confirmText,
                DaisyButtonVariant confirmVariant,
                TopLevel xamlRoot)
            {
                if (xamlRoot == null)
                    return Task.FromResult(false);

                var dialog = new FlowKanbanConfirmDialog(title, message, confirmText, confirmVariant, xamlRoot);
                return dialog.ShowInternalAsync();
            }

            private Task<bool> ShowInternalAsync()
            {

                IsOpen = true;
                return _tcs.Task;
            }

            private void OnConfirmClicked(object? sender, RoutedEventArgs e)
            {
                Close(true);
            }

            private void OnCancelClicked(object? sender, RoutedEventArgs e)
            {
                Close(false);
            }

            protected override void OnDialogOpenChanged(bool isOpen)
            {
                if (!isOpen && !_isClosing)
                {
                    Close(false);
                }
            }

            private void Close(bool confirmed)
            {
                if (_isClosing)
                    return;

                _isClosing = true;
                if (IsOpen)
                    IsOpen = false;



                _tcs.TrySetResult(confirmed);
            }
        }

        private sealed partial class FlowKanbanKeyboardHelpDialog : FlowKanbanDialogBase
        {
            private readonly TaskCompletionSource<bool> _tcs = new();
            private readonly TopLevel _xamlRoot;
            private bool _isClosing;

            private readonly DaisyButton _closeButton;

            private FlowKanbanKeyboardHelpDialog(TopLevel xamlRoot)
            {
                AutomationProperties.SetName(
                    this,
                    FloweryLocalization.GetString("Kanban_KeyboardHelp_Title"));
                _xamlRoot = xamlRoot;

                Content = CreateDialogLayout(xamlRoot, out _closeButton);
                IsDraggable = true;
                ApplySmartSizingWithAutoHeight(xamlRoot);

                _closeButton.Click += OnCloseClicked;
            }

            public static Task ShowAsync(TopLevel xamlRoot)
            {
                if (xamlRoot == null)
                    return Task.CompletedTask;

                var dialog = new FlowKanbanKeyboardHelpDialog(xamlRoot);
                return dialog.ShowInternalAsync();
            }

            private Task ShowInternalAsync()
            {

                IsOpen = true;
                Dispatcher.UIThread.Post(() => _closeButton.Focus());
                return _tcs.Task;
            }

            private void OnCloseClicked(object? sender, RoutedEventArgs e)
            {
                Close();
            }

            protected override void OnDialogOpenChanged(bool isOpen)
            {
                if (!isOpen && !_isClosing)
                {
                    Close();
                }
            }

            private void Close()
            {
                if (_isClosing)
                    return;

                _isClosing = true;
                if (IsOpen)
                    IsOpen = false;



                _tcs.TrySetResult(true);
            }

            private static Control CreateDialogLayout(TopLevel xamlRoot, out DaisyButton closeButton)
            {
                var header = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 4,
                    Margin = new Thickness(16, 16, 16, 0)
                };

                header.Children.Add(CreateTextBlock(
                    FloweryLocalization.GetString("Kanban_KeyboardHelp_Title"),
                    "TitleTextBlockStyle"));

                var subtitle = CreateTextBlock(
                    FloweryLocalization.GetString("Kanban_KeyboardHelp_Subtitle"),
                    "BodyTextBlockStyle");
                subtitle.Opacity = 0.7;
                header.Children.Add(subtitle);

                var shortcuts = new StackPanel
                {
                    Orientation = Orientation.Vertical,
                    Spacing = 12,
                    Margin = new Thickness(16, 0, 16, 0)
                };

                shortcuts.Children.Add(CreateShortcutRow("Tab / Shift+Tab", "Kanban_KeyboardHelp_Tab"));
                shortcuts.Children.Add(CreateShortcutRow("Arrow keys", "Kanban_KeyboardHelp_Arrows"));
                shortcuts.Children.Add(CreateShortcutRow("Ctrl + B", "Kanban_KeyboardHelp_AddSection"));
                shortcuts.Children.Add(CreateShortcutRow("Ctrl + D", "Kanban_KeyboardHelp_DeleteSection"));
                shortcuts.Children.Add(CreateShortcutRow("Ctrl + T", "Kanban_KeyboardHelp_RenameSection"));
                shortcuts.Children.Add(CreateShortcutRow("Ctrl + N", "Kanban_KeyboardHelp_AddCard"));
                shortcuts.Children.Add(CreateShortcutRow("Ctrl + F", "Kanban_KeyboardHelp_Search"));
                shortcuts.Children.Add(CreateShortcutRow("Ctrl + Shift + Arrow", "Kanban_KeyboardHelp_MoveCard"));
                shortcuts.Children.Add(CreateShortcutRow("Ctrl + Alt + Left/Right", "Kanban_KeyboardHelp_SwitchSection"));
                shortcuts.Children.Add(CreateShortcutRow("Ctrl + + / -", "Kanban_KeyboardHelp_Zoom"));

                var footer = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Spacing = 12,
                    Margin = new Thickness(16, 0, 24, 16)
                };

                closeButton = new DaisyButton
                {
                    Content = FloweryLocalization.GetString("Common_Close"),
                    Variant = DaisyButtonVariant.Primary,
                    MinWidth = 80
                };

                footer.Children.Add(closeButton);

                var container = FlowKanbanDialogBase.CreateDialogContent(xamlRoot, header, shortcuts, footer);
                var chromePadding = FlowKanbanDialogBase.OverlayChromePadding;
                var minWidth = FlowKanbanDialogBase.AbsoluteMinWidth - chromePadding;
                container.Width = Math.Max(minWidth, container.Width - chromePadding);
                container.Height = double.NaN;
                if (container.RowDefinitions.Count > 1)
                {
                    container.RowDefinitions[1].Height = GridLength.Auto;
                }
                return container;
            }

            private static Grid CreateShortcutRow(string keys, string descriptionKey)
            {
                var row = new Grid
                {
                    ColumnDefinitions =
                    {
                        new ColumnDefinition { Width = GridLength.Auto },
                        new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) }
                    },
                    ColumnSpacing = 12
                };

                var keyDisplay = new DaisyKbd
                {
                    Content = keys,
                    Size = DaisySize.Small,
                    VerticalAlignment = VerticalAlignment.Center
                };

                var description = CreateTextBlock(
                    FloweryLocalization.GetString(descriptionKey),
                    "BodyTextBlockStyle");
                description.TextWrapping = TextWrapping.Wrap;
                description.VerticalAlignment = VerticalAlignment.Center;

                Grid.SetColumn(keyDisplay, 0);
                Grid.SetColumn(description, 1);

                row.Children.Add(keyDisplay);
                row.Children.Add(description);

                return row;
            }

            private static TextBlock CreateTextBlock(string text, string styleKey)
            {
                var role = styleKey == "TitleTextBlockStyle"
                    ? FlowKanbanTextRole.Title
                    : FlowKanbanTextRole.Body;
                return FlowKanbanControlFactory.CreateTextBlock(text, role);
            }
        }

        #endregion
    }
}
