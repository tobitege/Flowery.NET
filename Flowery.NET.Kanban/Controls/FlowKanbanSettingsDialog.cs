using System;
using System.Threading.Tasks;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Theming;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Modal dialog for configuring Kanban settings.
    /// Uses FlowKanbanDialogBase for smart sizing and theming.
    /// </summary>
    public sealed partial class FlowKanbanSettingsDialog : FlowKanbanDialogBase
    {
        private sealed class KanbanSettingsState
        {
            public bool ConfirmColumnRemovals { get; set; }
            public bool ConfirmCardRemovals { get; set; }
            public bool AutoSaveAfterEdits { get; set; }
            public bool AutoExpandCardDetails { get; set; }
            public bool EnableUndoRedo { get; set; }
            public FlowKanbanAddCardPlacement AddCardPlacement { get; set; }
            public FlowKanbanCompactColumnSizingMode CompactColumnSizingMode { get; set; }
            public int CompactManualCardCount { get; set; }
            public bool ShowArchiveColumn { get; set; }
            public bool ShowWelcomeMessage { get; set; }
            public string WelcomeMessageTitle { get; set; } = string.Empty;
            public string WelcomeMessageSubtitle { get; set; } = string.Empty;
            public double ColumnWidth { get; set; }
        }

        private readonly FlowKanban _kanban;
        private readonly KanbanSettingsState _state;
        private readonly TopLevel _xamlRoot;
        private readonly TaskCompletionSource<bool> _tcs = new();
        private bool _isClosing;

        private DaisyButton _saveButton = null!;
        private DaisyButton _cancelButton = null!;
        private DaisyToggle? _firstToggle;
        private DaisyToggle _welcomeMessageToggle = null!;
        private DaisyInput _welcomeTitleBox = null!;
        private DaisyTextArea _welcomeMessageBox = null!;

        // Parameterless constructor for runtime control construction tests.
        public FlowKanbanSettingsDialog()
        {
            AutomationProperties.SetName(
                this,
                FloweryLocalization.GetString("Kanban_Settings_Title"));
            _kanban = new FlowKanban();
            _xamlRoot = null!;
            _state = new KanbanSettingsState
            {
                ConfirmColumnRemovals = _kanban.ConfirmColumnRemovals,
                ConfirmCardRemovals = _kanban.ConfirmCardRemovals,
                AutoSaveAfterEdits = _kanban.AutoSaveAfterEdits,
                AutoExpandCardDetails = _kanban.AutoExpandCardDetails,
                EnableUndoRedo = _kanban.EnableUndoRedo,
                AddCardPlacement = _kanban.AddCardPlacement,
                CompactColumnSizingMode = _kanban.CompactColumnSizingMode,
                CompactManualCardCount = Math.Clamp(_kanban.CompactManualCardCount, 1, 20),
                ShowArchiveColumn = !_kanban.Board.IsArchiveColumnHidden && !string.IsNullOrWhiteSpace(_kanban.Board.ArchiveColumnId),
                ShowWelcomeMessage = _kanban.ShowWelcomeMessage,
                WelcomeMessageTitle = _kanban.WelcomeMessageTitle,
                WelcomeMessageSubtitle = _kanban.WelcomeMessageSubtitle,
                ColumnWidth = _kanban.ColumnWidth
            };

            Content = new Grid();
        }

        private FlowKanbanSettingsDialog(FlowKanban kanban, TopLevel xamlRoot)
        {
            AutomationProperties.SetName(
                this,
                FloweryLocalization.GetString("Kanban_Settings_Title"));
            _kanban = kanban;
            _xamlRoot = xamlRoot;

            _state = new KanbanSettingsState
            {
                ConfirmColumnRemovals = kanban.ConfirmColumnRemovals,
                ConfirmCardRemovals = kanban.ConfirmCardRemovals,
                AutoSaveAfterEdits = kanban.AutoSaveAfterEdits,
                AutoExpandCardDetails = kanban.AutoExpandCardDetails,
                EnableUndoRedo = kanban.EnableUndoRedo,
                AddCardPlacement = kanban.AddCardPlacement,
                CompactColumnSizingMode = kanban.CompactColumnSizingMode,
                CompactManualCardCount = Math.Clamp(kanban.CompactManualCardCount, 1, 20),
                ShowArchiveColumn = !kanban.Board.IsArchiveColumnHidden && !string.IsNullOrWhiteSpace(kanban.Board.ArchiveColumnId),
                ShowWelcomeMessage = kanban.ShowWelcomeMessage,
                WelcomeMessageTitle = kanban.WelcomeMessageTitle,
                WelcomeMessageSubtitle = kanban.WelcomeMessageSubtitle,
                ColumnWidth = kanban.ColumnWidth
            };

            Content = CreateDialogContent(
                _state,
                kanban.MinColumnWidth,
                kanban.MaxColumnWidth,
                xamlRoot,
                out _saveButton,
                out _cancelButton,
                out _firstToggle,
                out _welcomeMessageToggle,
                out _welcomeTitleBox,
                out _welcomeMessageBox);

            IsDraggable = true;
            IsDialogResizingEnabled = true;
            ApplySmartSizingWithAutoHeight(xamlRoot);

            _saveButton.Click += OnSaveClicked;
            _cancelButton.Click += OnCancelClicked;
        }

        public static Task<bool> ShowAsync(FlowKanban kanban, TopLevel xamlRoot)
        {
            if (kanban == null || xamlRoot == null)
                return Task.FromResult(false);

            var dialog = new FlowKanbanSettingsDialog(kanban, xamlRoot);
            return dialog.ShowInternalAsync();
        }

        private Task<bool> ShowInternalAsync()
        {

            IsOpen = true;
            Dispatcher.UIThread.Post(() => _firstToggle?.Focus());
            return _tcs.Task;
        }

        private void OnSaveClicked(object? sender, RoutedEventArgs e)
        {
            _state.ShowWelcomeMessage = _welcomeMessageToggle.IsChecked == true;
            _state.WelcomeMessageTitle = string.IsNullOrWhiteSpace(_welcomeTitleBox.Text)
                ? string.Empty
                : _welcomeTitleBox.Text;
            _state.WelcomeMessageSubtitle = string.IsNullOrWhiteSpace(_welcomeMessageBox.Text)
                ? string.Empty
                : _welcomeMessageBox.Text;

            _kanban.ConfirmColumnRemovals = _state.ConfirmColumnRemovals;
            _kanban.ConfirmCardRemovals = _state.ConfirmCardRemovals;
            _kanban.AutoSaveAfterEdits = _state.AutoSaveAfterEdits;
            _kanban.AutoExpandCardDetails = _state.AutoExpandCardDetails;
            _kanban.EnableUndoRedo = _state.EnableUndoRedo;
            _kanban.AddCardPlacement = _state.AddCardPlacement;
            _kanban.CompactColumnSizingMode = _state.CompactColumnSizingMode;
            _kanban.CompactManualCardCount = _state.CompactManualCardCount;
            _kanban.ColumnWidth = _state.ColumnWidth;
            var hasArchiveColumn = !string.IsNullOrWhiteSpace(_kanban.Board.ArchiveColumnId);
            if (hasArchiveColumn || _state.ShowArchiveColumn)
            {
                _kanban.Board.IsArchiveColumnHidden = !_state.ShowArchiveColumn;
                if (_state.ShowArchiveColumn)
                {
                    var manager = new FlowKanbanManager(_kanban, autoAttach: false);
                    manager.EnsureArchiveColumn();
                }
            }
            _kanban.ShowWelcomeMessage = _state.ShowWelcomeMessage;
            _kanban.WelcomeMessageTitle = _state.WelcomeMessageTitle;
            _kanban.WelcomeMessageSubtitle = _state.WelcomeMessageSubtitle;
            _kanban.RefreshLayoutAfterSettingsChange();
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

        private static Control CreateDialogContent(
            KanbanSettingsState state,
            double minColumnWidth,
            double maxColumnWidth,
            TopLevel xamlRoot,
            out DaisyButton saveButton,
            out DaisyButton cancelButton,
            out DaisyToggle? firstToggle,
            out DaisyToggle welcomeMessageToggle,
            out DaisyInput welcomeTitleBox,
            out DaisyTextArea welcomeMessageBox)
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
                Margin = new Thickness(16),
                RowSpacing = 16,
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

            var headerStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 4
            };
            headerStack.Children.Add(new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Settings_Title"),
                TextWrapping = TextWrapping.Wrap
            });
            headerStack.Children.Add(new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Settings_Subtitle"),
                Opacity = 0.7,
                TextWrapping = TextWrapping.Wrap
            });
            var sectionBackground = DaisyResourceLookup.GetBrush("DaisyBase200Brush");
            var sectionBorderBrush = DaisyResourceLookup.GetBrush("DaisyBase300Brush");

            var headerCard = new Border
            {
                Background = sectionBackground,
                BorderBrush = sectionBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(16, 12, 16, 12),
                Child = headerStack
            };
            Grid.SetRow(headerCard, 0);
            outerContainer.Children.Add(headerCard);

            var settingsStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 16,
                Margin = new Thickness(0, 0, 16, 0)
            };

            settingsStack.Children.Add(CreateToggleRow(
                FloweryLocalization.GetString("Kanban_Settings_ConfirmColumnRemovals"),
                state.ConfirmColumnRemovals,
                value => state.ConfirmColumnRemovals = value,
                out var firstToggleLocal));
            settingsStack.Children.Add(CreateToggleRow(
                FloweryLocalization.GetString("Kanban_Settings_ConfirmCardRemovals"),
                state.ConfirmCardRemovals,
                value => state.ConfirmCardRemovals = value,
                out _));
            settingsStack.Children.Add(CreateToggleRow(
                FloweryLocalization.GetString("Kanban_Settings_AutoSaveAfterEdits"),
                state.AutoSaveAfterEdits,
                value => state.AutoSaveAfterEdits = value,
                out _));
            settingsStack.Children.Add(CreateToggleRow(
                FloweryLocalization.GetString("Kanban_Settings_AutoExpandCardDetails"),
                state.AutoExpandCardDetails,
                value => state.AutoExpandCardDetails = value,
                out _));
            settingsStack.Children.Add(CreateToggleRow(
                FloweryLocalization.GetString("Kanban_Settings_EnableUndoRedo"),
                state.EnableUndoRedo,
                value => state.EnableUndoRedo = value,
                out _));
            var addCardPlacementStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8
            };
            var addCardPlacementLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Settings_AddCardPlacement"),
                TextWrapping = TextWrapping.Wrap
            };
            var addCardPlacementSelect = new DaisySelect
            {
                PlaceholderText = FloweryLocalization.GetString("Select_Placeholder"),
                Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var addCardBottomItem = new FlowKanbanSelectOption<FlowKanbanAddCardPlacement>(
                FloweryLocalization.GetString("Kanban_Settings_AddCardPlacement_Bottom"),
                FlowKanbanAddCardPlacement.Bottom);
            var addCardTopItem = new FlowKanbanSelectOption<FlowKanbanAddCardPlacement>(
                FloweryLocalization.GetString("Kanban_Settings_AddCardPlacement_Top"),
                FlowKanbanAddCardPlacement.Top);
            var addCardBothItem = new FlowKanbanSelectOption<FlowKanbanAddCardPlacement>(
                FloweryLocalization.GetString("Kanban_Settings_AddCardPlacement_Both"),
                FlowKanbanAddCardPlacement.Both);
            addCardPlacementSelect.Items.Add(addCardBottomItem);
            addCardPlacementSelect.Items.Add(addCardTopItem);
            addCardPlacementSelect.Items.Add(addCardBothItem);
            addCardPlacementSelect.SelectedItem = state.AddCardPlacement switch
            {
                FlowKanbanAddCardPlacement.Top => addCardTopItem,
                FlowKanbanAddCardPlacement.Both => addCardBothItem,
                _ => addCardBottomItem
            };
            addCardPlacementSelect.SelectionChanged += (s, e) =>
            {
                if (addCardPlacementSelect.SelectedItem is FlowKanbanSelectOption<FlowKanbanAddCardPlacement> item)
                {
                    state.AddCardPlacement = item.Value;
                }
            };
            AutomationProperties.SetLabeledBy(addCardPlacementSelect, addCardPlacementLabel);
            addCardPlacementStack.Children.Add(addCardPlacementLabel);
            addCardPlacementStack.Children.Add(addCardPlacementSelect);
            var addCardAndWidthRow = new FlowKanbanWrapPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            addCardAndWidthRow.Children.Add(addCardPlacementStack);

            var columnWidthStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8
            };
            var columnWidthLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Settings_ColumnWidth"),
                TextWrapping = TextWrapping.Wrap
            };
            var columnWidthRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var columnWidthInput = new DaisyNumericUpDown
            {
                Minimum = (decimal)minColumnWidth,
                Maximum = (decimal)maxColumnWidth,
                Increment = 4m,
                Value = (decimal)state.ColumnWidth,
                Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(columnWidthInput, columnWidthLabel);
            FlowKanbanProperty.Observe<decimal?>(columnWidthInput, DaisyNumericUpDown.ValueProperty, value =>
            {
                if (value is { } width)
                {
                    state.ColumnWidth = (double)width;
                }
            });
            var columnWidthResetButton = new DaisyButton
            {
                Content = FloweryLocalization.GetString("Kanban_Settings_ColumnWidth_Reset"),
                Variant = DaisyButtonVariant.Secondary,
                Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            columnWidthResetButton.Click += (_, _) =>
            {
                columnWidthInput.Value = (decimal)FlowKanban.DefaultColumnWidth;
            };
            columnWidthRow.Children.Add(columnWidthInput);
            columnWidthRow.Children.Add(columnWidthResetButton);
            columnWidthStack.Children.Add(columnWidthLabel);
            columnWidthStack.Children.Add(columnWidthRow);
            addCardAndWidthRow.Children.Add(columnWidthStack);
            settingsStack.Children.Add(addCardAndWidthRow);

            var compactSizingStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8
            };
            var compactSizingLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Settings_CompactColumnSizing"),
                TextWrapping = TextWrapping.Wrap
            };
            var compactSizingSelect = new DaisySelect
            {
                PlaceholderText = FloweryLocalization.GetString("Select_Placeholder"),
                Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            var compactAdaptiveItem = new FlowKanbanSelectOption<FlowKanbanCompactColumnSizingMode>(
                FloweryLocalization.GetString("Kanban_Settings_CompactColumnSizing_Adaptive"),
                FlowKanbanCompactColumnSizingMode.Adaptive);
            var compactManualItem = new FlowKanbanSelectOption<FlowKanbanCompactColumnSizingMode>(
                FloweryLocalization.GetString("Kanban_Settings_CompactColumnSizing_Manual"),
                FlowKanbanCompactColumnSizingMode.Manual);
            compactSizingSelect.Items.Add(compactAdaptiveItem);
            compactSizingSelect.Items.Add(compactManualItem);
            compactSizingSelect.SelectedItem = state.CompactColumnSizingMode == FlowKanbanCompactColumnSizingMode.Manual
                ? compactManualItem
                : compactAdaptiveItem;
            compactSizingSelect.SelectedIndex = state.CompactColumnSizingMode == FlowKanbanCompactColumnSizingMode.Manual ? 1 : 0;
            AutomationProperties.SetLabeledBy(compactSizingSelect, compactSizingLabel);
            compactSizingStack.Children.Add(compactSizingLabel);
            compactSizingStack.Children.Add(compactSizingSelect);

            var compactCardCountStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8
            };
            var compactCardCountLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Settings_CompactColumnCardCount"),
                TextWrapping = TextWrapping.Wrap
            };
            var compactCardCountValue = new TextBlock
            {
                Text = state.CompactManualCardCount.ToString(),
                Opacity = 0.7,
                HorizontalAlignment = HorizontalAlignment.Left
            };
            var compactCardCountRange = new DaisyRange
            {
                Minimum = 1,
                Maximum = 20,
                TickFrequency = 1,
                IsSnapToTickEnabled = true,
                Value = state.CompactManualCardCount,
                Size = DaisySize.Small,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(compactCardCountRange, compactCardCountLabel);
            FlowKanbanProperty.Observe<double>(compactCardCountRange, DaisyRange.ValueProperty, value =>
            {
                var count = (int)Math.Round(value);
                count = Math.Clamp(count, 1, 20);
                if (Math.Abs(value - count) > 0.001)
                {
                    compactCardCountRange.Value = count;
                }
                state.CompactManualCardCount = count;
                compactCardCountValue.Text = count.ToString();
            });
            compactCardCountStack.Children.Add(compactCardCountLabel);
            compactCardCountStack.Children.Add(compactCardCountRange);
            compactCardCountStack.Children.Add(compactCardCountValue);

            var compactOptionsRow = new FlowKanbanWrapPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 16,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            compactOptionsRow.Children.Add(compactSizingStack);
            compactOptionsRow.Children.Add(compactCardCountStack);
            settingsStack.Children.Add(compactOptionsRow);

            void UpdateCompactCardCountState()
            {
                var selectedIndex = compactSizingSelect.SelectedIndex;
                var mode = selectedIndex == 1
                    ? FlowKanbanCompactColumnSizingMode.Manual
                    : FlowKanbanCompactColumnSizingMode.Adaptive;
                state.CompactColumnSizingMode = mode;

                var isManual = mode == FlowKanbanCompactColumnSizingMode.Manual;
                compactCardCountStack.IsHitTestVisible = isManual;
                compactCardCountStack.Opacity = isManual ? 1.0 : 0.55;
            }

            UpdateCompactCardCountState();
            void OnCompactSizingChanged()
            {
                UpdateCompactCardCountState();
            }

            compactSizingSelect.SelectionChanged += (_, args) =>
            {
                OnCompactSizingChanged();
            };
            compactSizingSelect.Loaded += (_, _) => UpdateCompactCardCountState();
            FlowKanbanDispatcher.Post(UpdateCompactCardCountState);

            settingsStack.Children.Add(CreateToggleRow(
                FloweryLocalization.GetString("Kanban_Settings_ShowArchiveColumn"),
                state.ShowArchiveColumn,
                value => state.ShowArchiveColumn = value,
                out _));
            settingsStack.Children.Add(CreateToggleRow(
                FloweryLocalization.GetString("Kanban_Settings_ShowWelcomeMessage"),
                state.ShowWelcomeMessage,
                value => state.ShowWelcomeMessage = value,
                out var showWelcomeToggle));

            var welcomeTitleStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8
            };
            var welcomeTitleLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Settings_WelcomeTitle"),
                TextWrapping = TextWrapping.Wrap
            };
            welcomeTitleBox = new DaisyInput
            {
                Text = state.WelcomeMessageTitle,
                Variant = DaisyInputVariant.Bordered,
                Size = DaisySize.Small,
                PlaceholderText = FloweryLocalization.GetString("Kanban_Home_Title"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            AutomationProperties.SetLabeledBy(welcomeTitleBox, welcomeTitleLabel);
            welcomeTitleStack.Children.Add(welcomeTitleLabel);
            welcomeTitleStack.Children.Add(welcomeTitleBox);
            settingsStack.Children.Add(welcomeTitleStack);

            var welcomeMessageStack = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Spacing = 8
            };
            var welcomeMessageLabel = new TextBlock
            {
                Text = FloweryLocalization.GetString("Kanban_Settings_WelcomeMessage"),
                TextWrapping = TextWrapping.Wrap
            };
            welcomeMessageBox = new DaisyTextArea
            {
                Text = state.WelcomeMessageSubtitle,
                Variant = DaisyInputVariant.Bordered,
                PlaceholderText = FloweryLocalization.GetString("Kanban_Home_Subtitle"),
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            FlowKanbanControlFactory.SetTextAreaRows(welcomeMessageBox, 2, 4);
            AutomationProperties.SetLabeledBy(welcomeMessageBox, welcomeMessageLabel);
            welcomeMessageStack.Children.Add(welcomeMessageLabel);
            welcomeMessageStack.Children.Add(welcomeMessageBox);
            settingsStack.Children.Add(welcomeMessageStack);

            var scrollViewer = new ScrollViewer
            {
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
                Content = FlowKanbanDialogBase.ReserveVerticalScrollBarSpace(settingsStack)
            };
            var contentCard = new Border
            {
                Background = sectionBackground,
                BorderBrush = sectionBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Child = scrollViewer
            };
            Grid.SetRow(contentCard, 1);
            outerContainer.Children.Add(contentCard);

            var buttonPanel = FlowKanbanDialogBase.CreateStandardButtonFooter(out saveButton, out cancelButton);
            buttonPanel.Margin = new Thickness(0, 0, 48, 0);
            var footerCard = new Border
            {
                Background = sectionBackground,
                BorderBrush = sectionBorderBrush,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(12),
                Child = buttonPanel
            };
            Grid.SetRow(footerCard, 2);
            outerContainer.Children.Add(footerCard);

            firstToggle = firstToggleLocal;
            welcomeMessageToggle = showWelcomeToggle;

            return outerContainer;
        }

        private static Grid CreateToggleRow(
            string label,
            bool isOn,
            System.Action<bool> onChanged,
            out DaisyToggle toggle)
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
                Text = label,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(labelText, 0);
            row.Children.Add(labelText);

            var localToggle = new DaisyToggle
            {
                IsChecked = isOn,
                Variant = DaisyToggleVariant.Success,
                VerticalAlignment = VerticalAlignment.Center
            };
            AutomationProperties.SetLabeledBy(localToggle, labelText);
            localToggle.IsCheckedChanged += (s, e) => onChanged(localToggle.IsChecked == true);

            toggle = localToggle;

            Grid.SetColumn(localToggle, 1);
            row.Children.Add(localToggle);

            return row;
        }
    }
}
