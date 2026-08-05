using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Kanban.Controls;
using Flowery.NET.Kanban.Controls.Users;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

public class KanbanDialogTests
{
    [AvaloniaFact]
    public async Task When_TaskEditorIsCancelledByEscape_EditsAreDiscardedAndFocusReturns()
    {
        var task = new FlowTask
        {
            Title = "Original task",
            Description = "Original description"
        };
        task.Subtasks.Add(new FlowSubtask { Title = "Original subtask" });

        var hostButton = new Button { Content = "Open editor" };
        var window = CreateWindow(hostButton);
        FlowTaskEditorDialog? dialog = null;

        try
        {
            Assert.True(hostButton.Focus());
            var showTask = FlowTaskEditorDialog.ShowAsync(task, window);
            FlushLayout(window);

            dialog = window.GetVisualDescendants().OfType<FlowTaskEditorDialog>().Single();
            var titleInput = FindInput(dialog, task.Title);
            var descriptionInput = dialog.GetVisualDescendants().OfType<DaisyTextArea>().First();

            titleInput.Text = "Cancelled title";
            descriptionInput.Text = "Cancelled description";
            window.KeyPress(
                Key.Escape,
                RawInputModifiers.None,
                PhysicalKey.Escape,
                keySymbol: null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(showTask.IsCompleted);
            Assert.False(await showTask);
            Assert.Equal("Original task", task.Title);
            Assert.Equal("Original description", task.Description);
            Assert.False(task.Subtasks.Single().IsCompleted);
            Assert.DoesNotContain(
                window.GetVisualDescendants(),
                visual => visual is FlowTaskEditorDialog);
            Assert.Same(hostButton, window.FocusManager?.GetFocusedElement());
        }
        finally
        {
            CloseDialog(dialog);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task When_HostCloses_OpenTaskEditorCompletesAsCancelled()
    {
        var window = CreateWindow(new Border());
        FlowTaskEditorDialog? dialog = null;

        try
        {
            var showTask = FlowTaskEditorDialog.ShowAsync(
                new FlowTask { Title = "Pending task" },
                window);
            FlushLayout(window);
            dialog = window.GetVisualDescendants().OfType<FlowTaskEditorDialog>().Single();

            window.Close();
            Dispatcher.UIThread.RunJobs();

            Assert.True(showTask.IsCompleted);
            Assert.False(await showTask);
        }
        finally
        {
            CloseDialog(dialog);
            if (window.IsVisible)
            {
                window.Close();
            }
        }
    }

    [AvaloniaFact]
    public async Task When_TaskEditorRealizes_ItExposesTheSharedModalContract()
    {
        var task = new FlowTask { Title = "Automation task" };
        var window = CreateWindow(new Border());
        FlowTaskEditorDialog? dialog = null;

        try
        {
            var showTask = FlowTaskEditorDialog.ShowAsync(task, window);
            FlushLayout(window);
            dialog = window.GetVisualDescendants().OfType<FlowTaskEditorDialog>().Single();

            Assert.Equal(
                KeyboardNavigationMode.Cycle,
                KeyboardNavigation.GetTabNavigation(dialog));

            var dialogPeer = Assert.IsAssignableFrom<AutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(dialog));
            Assert.Equal(AutomationControlType.Window, dialogPeer.GetAutomationControlType());
            Assert.False(string.IsNullOrWhiteSpace(dialogPeer.GetName()));
            Assert.True(dialogPeer.IsControlElement());
            Assert.True(dialogPeer.IsContentElement());

            var titleInput = FindInput(dialog, task.Title);
            var titlePeer = Assert.IsAssignableFrom<AutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(titleInput));
            var valueProvider = Assert.IsAssignableFrom<IValueProvider>(titlePeer);
            Assert.Equal(task.Title, valueProvider.Value);

            AssertActualFooter(dialog);
            Assert.InRange(dialog.Bounds.Width, 1, window.Bounds.Width);
            Assert.InRange(dialog.Bounds.Height, 1, window.Bounds.Height);

            var cancelButton = FindActionButton(dialog, "Common_Cancel");
            Assert.True(cancelButton.Focus());
            window.KeyPress(
                Key.Tab,
                RawInputModifiers.None,
                PhysicalKey.Tab,
                keySymbol: null);
            Dispatcher.UIThread.RunJobs();
            var focusedElement = window.FocusManager?.GetFocusedElement() as Visual;
            Assert.NotNull(focusedElement);
            Assert.True(
                ReferenceEquals(dialog, focusedElement)
                || dialog.IsVisualAncestorOf(focusedElement));

            Invoke(cancelButton);
            Assert.False(await showTask);
        }
        finally
        {
            CloseDialog(dialog);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task When_TaskEditorRealizes_DatePickersStayWithinDialogBounds()
    {
        var window = CreateWindow(new Border());
        FlowTaskEditorDialog? dialog = null;

        try
        {
            var showTask = FlowTaskEditorDialog.ShowAsync(
                new FlowTask { Title = "Scheduling task" },
                window);
            FlushLayout(window);
            dialog = window.GetVisualDescendants().OfType<FlowTaskEditorDialog>().Single();

            var schedulingLabel = dialog.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(textBlock => string.Equals(
                    textBlock.Text,
                    FloweryLocalization.GetString("Kanban_Editor_SectionScheduling"),
                    StringComparison.Ordinal));
            var overviewContent = Assert.IsType<StackPanel>(schedulingLabel.GetVisualParent());
            var schedulingIndex = overviewContent.Children.IndexOf(schedulingLabel);
            Assert.True(schedulingIndex > 0);
            Assert.IsNotType<DaisyDivider>(overviewContent.Children[schedulingIndex - 1]);

            var datePickers = dialog.GetVisualDescendants().OfType<DatePicker>().ToArray();
            Assert.Equal(4, datePickers.Length);

            var pickerBounds = datePickers.Select(datePicker =>
            {
                var position = LayoutTestAssertions.GetPosition(datePicker, dialog);
                return (
                    X: position.X,
                    Y: position.Y,
                    Width: datePicker.Bounds.Width,
                    Height: datePicker.Bounds.Height);
            }).ToArray();

            foreach (var bounds in pickerBounds)
            {
                Assert.InRange(bounds.X, -0.5, dialog.Bounds.Width - bounds.Width + 0.5);
            }

            var scrollViewer = datePickers[0].GetVisualAncestors().OfType<ScrollViewer>().First();
            var verticalScrollBar = scrollViewer.GetVisualDescendants()
                .OfType<Avalonia.Controls.Primitives.ScrollBar>()
                .Where(scrollBar => scrollBar.Orientation == Avalonia.Layout.Orientation.Vertical)
                .OrderByDescending(scrollBar => scrollBar.Bounds.Height)
                .First();
            var scrollBarPosition = LayoutTestAssertions.GetPosition(verticalScrollBar, dialog);
            foreach (var bounds in pickerBounds)
            {
                Assert.True(
                    bounds.X + bounds.Width <= scrollBarPosition.X + 0.5,
                    "A date picker extends beneath the vertical scrollbar.");
            }

            for (var firstIndex = 0; firstIndex < pickerBounds.Length; firstIndex++)
            {
                for (var secondIndex = firstIndex + 1; secondIndex < pickerBounds.Length; secondIndex++)
                {
                    var first = pickerBounds[firstIndex];
                    var second = pickerBounds[secondIndex];
                    var horizontallySeparated = first.X + first.Width <= second.X + 0.5
                                                || second.X + second.Width <= first.X + 0.5;
                    var verticallySeparated = first.Y + first.Height <= second.Y + 0.5
                                              || second.Y + second.Height <= first.Y + 0.5;

                    Assert.True(
                        horizontallySeparated || verticallySeparated,
                        $"Date pickers {firstIndex} and {secondIndex} overlap.");
                }
            }

            Invoke(FindActionButton(dialog, "Common_Cancel"));
            Assert.False(await showTask);
        }
        finally
        {
            CloseDialog(dialog);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task When_TaskEditorSaves_ChangesAreCommittedOnlyAfterSave()
    {
        var task = new FlowTask
        {
            Title = "Before",
            Description = "Before description"
        };
        var window = CreateWindow(new Border());
        FlowTaskEditorDialog? dialog = null;

        try
        {
            var showTask = FlowTaskEditorDialog.ShowAsync(task, window);
            FlushLayout(window);
            dialog = window.GetVisualDescendants().OfType<FlowTaskEditorDialog>().Single();

            FindInput(dialog, task.Title).Text = "After";
            var description = dialog.GetVisualDescendants().OfType<DaisyTextArea>().First();
            description.Text = "After description";
            Assert.Equal("Before", task.Title);
            Assert.Equal("Before description", task.Description);

            Invoke(FindActionButton(dialog, "Common_Save"));

            Assert.True(await showTask);
            Assert.Equal("After", task.Title);
            Assert.Equal("After description", task.Description);
        }
        finally
        {
            CloseDialog(dialog);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task When_BoardEditorCancelsAndSaves_ChangesRemainIsolatedUntilSave()
    {
        var board = new FlowKanbanData
        {
            Title = "Original board",
            Description = "Original description",
            CreatedBy = "Original author"
        };
        var window = CreateWindow(new Border());
        FlowBoardEditorDialog? dialog = null;

        try
        {
            var cancelledTask = FlowBoardEditorDialog.ShowAsync(board, window);
            FlushLayout(window);
            dialog = window.GetVisualDescendants().OfType<FlowBoardEditorDialog>().Single();
            AssertActualFooter(dialog);

            FindInput(dialog, board.Title).Text = "Cancelled board";
            Invoke(FindActionButton(dialog, "Common_Cancel"));

            Assert.False(await cancelledTask);
            Assert.Equal("Original board", board.Title);

            var savedTask = FlowBoardEditorDialog.ShowAsync(board, window);
            FlushLayout(window);
            dialog = window.GetVisualDescendants().OfType<FlowBoardEditorDialog>().Single();
            FindInput(dialog, board.Title).Text = "Saved board";
            Invoke(FindActionButton(dialog, "Common_Save"));

            Assert.True(await savedTask);
            Assert.Equal("Saved board", board.Title);
        }
        finally
        {
            CloseDialog(dialog);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task When_SettingsDialogCancelsAndSaves_ChangesRemainIsolatedUntilSave()
    {
        var previousStorage = StateStorageProvider.Instance;
        StateStorageProvider.Configure(new DialogStateStorage());
        var kanban = new FlowKanban
        {
            AutoSaveAfterEdits = false,
            ConfirmColumnRemovals = true
        };
        var window = CreateWindow(kanban);
        FlowKanbanSettingsDialog? dialog = null;

        try
        {
            var cancelledTask = FlowKanbanSettingsDialog.ShowAsync(kanban, window);
            FlushLayout(window);
            dialog = window.GetVisualDescendants().OfType<FlowKanbanSettingsDialog>().Single();
            AssertActualFooter(dialog);

            var confirmRemovalToggle = FindLabeledToggle(
                dialog,
                FloweryLocalization.GetString("Kanban_Settings_ConfirmColumnRemovals"));
            confirmRemovalToggle.IsChecked = false;
            Invoke(FindActionButton(dialog, "Common_Cancel"));

            Assert.False(await cancelledTask);
            Assert.True(kanban.ConfirmColumnRemovals);

            var savedTask = FlowKanbanSettingsDialog.ShowAsync(kanban, window);
            FlushLayout(window);
            dialog = window.GetVisualDescendants().OfType<FlowKanbanSettingsDialog>().Single();
            confirmRemovalToggle = FindLabeledToggle(
                dialog,
                FloweryLocalization.GetString("Kanban_Settings_ConfirmColumnRemovals"));
            confirmRemovalToggle.IsChecked = false;
            Invoke(FindActionButton(dialog, "Common_Save"));

            Assert.True(await savedTask);
            Assert.False(kanban.ConfirmColumnRemovals);
        }
        finally
        {
            CloseDialog(dialog);
            window.Close();
            StateStorageProvider.Configure(previousStorage);
        }
    }

    [AvaloniaFact]
    public void When_InputColumnAndConfirmDialogsRun_OnlyConfirmedCommandsMutateBoard()
    {
        var previousStorage = StateStorageProvider.Instance;
        StateStorageProvider.Configure(new DialogStateStorage());
        var kanban = new FlowKanban { AutoSaveAfterEdits = false };
        kanban.Board.Columns.Clear();
        var window = CreateWindow(kanban);

        try
        {
            kanban.AddColumnCommand.Execute(null);
            FlushLayout(window);
            var inputDialog = FindDialog(window, "FlowKanbanInputDialog");
            AssertDialogContract(
                inputDialog,
                window,
                FloweryLocalization.GetString("Kanban_AddSection"));
            var input = inputDialog.GetVisualDescendants().OfType<DaisyInput>().Single();
            var inputPeer = Assert.IsAssignableFrom<AutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(input));
            var inputValue = Assert.IsAssignableFrom<IValueProvider>(inputPeer);
            Assert.False(inputValue.IsReadOnly);
            inputValue.SetValue("Added column");
            Assert.True(input.Focus());
            window.KeyPress(
                Key.Enter,
                RawInputModifiers.None,
                PhysicalKey.Enter,
                keySymbol: null);
            Dispatcher.UIThread.RunJobs();

            var column = Assert.Single(kanban.Board.Columns);
            Assert.Equal("Added column", column.Title);

            kanban.AddColumnCommand.Execute(null);
            FlushLayout(window);
            inputDialog = FindDialog(window, "FlowKanbanInputDialog");
            input = inputDialog.GetVisualDescendants().OfType<DaisyInput>().Single();
            input.Text = "Discarded column";
            Assert.True(input.Focus());
            window.KeyPress(
                Key.Escape,
                RawInputModifiers.None,
                PhysicalKey.Escape,
                keySymbol: null);
            Dispatcher.UIThread.RunJobs();
            Assert.Single(kanban.Board.Columns);

            kanban.EditColumnCommand.Execute(column);
            FlushLayout(window);
            var columnDialog = FindDialog(window, "FlowKanbanColumnEditorDialog");
            AssertDialogContract(columnDialog, window, column.Title);
            FindInput(columnDialog, column.Title).Text = "Cancelled rename";
            Invoke(FindActionButton(columnDialog, "Common_Cancel"));
            Assert.Equal("Added column", column.Title);

            kanban.EditColumnCommand.Execute(column);
            FlushLayout(window);
            columnDialog = FindDialog(window, "FlowKanbanColumnEditorDialog");
            FindInput(columnDialog, column.Title).Text = "Renamed column";
            Invoke(FindActionButton(columnDialog, "Common_Save"));
            Assert.Equal("Renamed column", column.Title);

            kanban.RemoveColumnCommand.Execute(column);
            FlushLayout(window);
            var confirmDialog = FindDialog(window, "FlowKanbanConfirmDialog");
            AssertDialogContract(
                confirmDialog,
                window,
                FloweryLocalization.GetString("Common_ConfirmDelete"));
            var cancelButton = FindActionButton(confirmDialog, "Common_Cancel");
            AssertMediumActionButton(cancelButton);
            Invoke(cancelButton);
            Assert.Single(kanban.Board.Columns);

            kanban.RemoveColumnCommand.Execute(column);
            FlushLayout(window);
            confirmDialog = FindDialog(window, "FlowKanbanConfirmDialog");
            var deleteButton = FindActionButton(confirmDialog, "Common_Delete");
            AssertMediumActionButton(deleteButton);
            Invoke(deleteButton);
            Assert.Empty(kanban.Board.Columns);
        }
        finally
        {
            CloseOpenDialogs(window);
            window.Close();
            StateStorageProvider.Configure(previousStorage);
        }
    }

    [AvaloniaFact]
    public async Task When_LaneDeleteDialogConfirms_FallbackIsCommittedOnlyWithBoardSave()
    {
        var firstLane = new FlowKanbanLane { Id = "lane-a", Title = "Lane A" };
        var secondLane = new FlowKanbanLane { Id = "lane-b", Title = "Lane B" };
        var card = new FlowTask { Title = "Assigned card", LaneId = firstLane.Id };
        var column = new FlowKanbanColumnData { Title = "Todo" };
        column.Tasks.Add(card);
        var board = new FlowKanbanData
        {
            Title = "Lane board",
            GroupBy = FlowKanbanGroupBy.Lane
        };
        board.Lanes.Add(firstLane);
        board.Lanes.Add(secondLane);
        board.Columns.Add(column);

        var window = CreateWindow(new Border());
        FlowBoardEditorDialog? boardDialog = null;

        try
        {
            var showTask = FlowBoardEditorDialog.ShowAsync(board, window);
            FlushLayout(window);
            boardDialog = window.GetVisualDescendants().OfType<FlowBoardEditorDialog>().Single();

            var deleteName = FloweryLocalization.GetString("Kanban_Lanes_Delete");
            var laneDeleteButtons = boardDialog.GetVisualDescendants()
                .OfType<DaisyButton>()
                .Where(button => string.Equals(
                    AutomationProperties.GetName(button),
                    deleteName,
                    StringComparison.Ordinal))
                .ToList();
            Assert.Equal(2, laneDeleteButtons.Count);
            Invoke(laneDeleteButtons[0]);
            FlushLayout(window);

            var deleteDialog = FindDialog(window, "LaneDeleteDialog");
            AssertDialogContract(
                deleteDialog,
                window,
                FloweryLocalization.GetString("Common_ConfirmDelete"));
            var fallbackSelect = deleteDialog.GetVisualDescendants().OfType<DaisySelect>().Single();
            Assert.Equal(0, fallbackSelect.SelectedIndex);
            var selectPeer = Assert.IsAssignableFrom<AutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(fallbackSelect));
            Assert.Equal(AutomationControlType.ComboBox, selectPeer.GetAutomationControlType());
            Assert.IsAssignableFrom<IExpandCollapseProvider>(selectPeer);

            var deleteButton = FindActionButton(deleteDialog, "Common_Delete");
            AssertMediumActionButton(deleteButton);
            Invoke(deleteButton);
            Dispatcher.UIThread.RunJobs();

            Assert.Equal(2, board.Lanes.Count);
            Assert.Equal(firstLane.Id, card.LaneId);
            Invoke(FindActionButton(boardDialog, "Common_Save"));

            Assert.True(await showTask);
            var remainingLane = Assert.Single(board.Lanes);
            Assert.Equal(secondLane.Id, remainingLane.Id);
            Assert.Equal(secondLane.Id, card.LaneId);
        }
        finally
        {
            CloseDialog(boardDialog);
            CloseOpenDialogs(window);
            window.Close();
        }
    }

    [AvaloniaFact]
    public void When_ReadOnlyDialogsRealize_TheyExposeNamedModalAndInvokeSemantics()
    {
        var previousStorage = StateStorageProvider.Instance;
        StateStorageProvider.Configure(new DialogStateStorage());
        var kanban = new FlowKanban { AutoSaveAfterEdits = false };
        kanban.Board.Title = "Metrics board";
        var window = CreateWindow(kanban);

        try
        {
            kanban.ShowMetricsCommand.Execute(null);
            FlushLayout(window);
            var metricsDialog = FindDialog(window, "FlowKanbanMetricsDialog");
            AssertDialogContract(
                metricsDialog,
                window,
                FloweryLocalization.GetString("Kanban_BoardStatistics"));
            var metricsClose = FindActionButton(metricsDialog, "Common_Close");
            AssertMediumActionButton(metricsClose);
            Invoke(metricsClose);

            kanban.ShowKeyboardHelpCommand.Execute(null);
            for (var attempt = 0; attempt < 3; attempt++)
            {
                FlushLayout(window);
            }

            var helpDialog = FindDialog(window, "FlowKanbanKeyboardHelpDialog");
            AssertDialogContract(
                helpDialog,
                window,
                FloweryLocalization.GetString("Kanban_KeyboardHelp_Title"));
            Assert.NotEmpty(helpDialog.GetVisualDescendants().OfType<DaisyKbd>());
            var helpClose = FindActionButton(helpDialog, "Common_Close");
            AssertMediumActionButton(helpClose);
            Invoke(helpClose);
        }
        finally
        {
            CloseOpenDialogs(window);
            window.Close();
            StateStorageProvider.Configure(previousStorage);
        }
    }

    [AvaloniaFact]
    public async Task When_ProviderAndUserDialogsRun_TheyExposeSecureInputAndSelectionSemantics()
    {
        var window = CreateWindow(new Border());

        try
        {
            const string connectTitle = "Connect GitHub";
            const string connectText = "Connect";
            var tokenTask = GitHubConnectDialog.ShowAsync(
                connectTitle,
                "Enter a token",
                "GitHub token",
                connectText,
                window);
            FlushLayout(window);
            var connectDialog = window.GetVisualDescendants().OfType<GitHubConnectDialog>().Single();
            AssertDialogContract(connectDialog, window, connectTitle);
            Assert.Equal(300, connectDialog.DialogWidth);
            var tokenInput = connectDialog.GetVisualDescendants().OfType<DaisyPasswordBox>().Single();
            var innerTokenInput = tokenInput.GetVisualDescendants().OfType<TextBox>().Single();
            var tokenPeer = Assert.IsAssignableFrom<AutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(innerTokenInput));
            var tokenValueProvider = Assert.IsAssignableFrom<IValueProvider>(tokenPeer);
            Assert.False(tokenValueProvider.IsReadOnly);
            tokenInput.Password = "  secret-token  ";
            Dispatcher.UIThread.RunJobs();
            var connectButton = FindButtonByText(connectDialog, connectText);
            Assert.True(connectButton.IsEnabled);
            AssertMediumActionButton(connectButton);
            Invoke(connectButton);
            Assert.Equal("secret-token", await tokenTask);

            const string errorTitle = "Provider error";
            var errorTask = ProviderErrorDialog.ShowAsync(errorTitle, "Request failed", window);
            FlushLayout(window);
            var errorDialog = window.GetVisualDescendants().OfType<ProviderErrorDialog>().Single();
            AssertDialogContract(errorDialog, window, errorTitle);
            Assert.Equal(360, errorDialog.DialogWidth);
            var closeButton = FindActionButton(errorDialog, "Common_Close");
            AssertMediumActionButton(closeButton);
            Invoke(closeButton);
            await errorTask;

            const string confirmTitle = "Disconnect provider";
            const string confirmText = "Disconnect";
            var cancelledTask = ProviderConfirmDialog.ShowAsync(
                confirmTitle,
                "Disconnect this provider?",
                confirmText,
                window);
            FlushLayout(window);
            var confirmDialog = window.GetVisualDescendants().OfType<ProviderConfirmDialog>().Single();
            AssertDialogContract(confirmDialog, window, confirmTitle);
            Assert.Equal(360, confirmDialog.DialogWidth);
            Invoke(FindActionButton(confirmDialog, "Common_Cancel"));
            Assert.False(await cancelledTask);

            var confirmedTask = ProviderConfirmDialog.ShowAsync(
                confirmTitle,
                "Disconnect this provider?",
                confirmText,
                window);
            FlushLayout(window);
            confirmDialog = window.GetVisualDescendants().OfType<ProviderConfirmDialog>().Single();
            var confirmButton = FindButtonByText(confirmDialog, confirmText);
            AssertMediumActionButton(confirmButton);
            Invoke(confirmButton);
            Assert.True(await confirmedTask);

            var user = new DialogUser("local:user-1", "Local User");
            const string linkText = "Link user";
            var linkTask = LinkLocalUserDialog.ShowAsync(
                "Choose a local user",
                linkText,
                [user],
                window);
            FlushLayout(window);
            var linkDialog = window.GetVisualDescendants().OfType<LinkLocalUserDialog>().Single();
            AssertDialogContract(
                linkDialog,
                window,
                FloweryLocalization.GetString("Kanban_Users_LinkLocal_Button"));
            Assert.Equal(300, linkDialog.DialogWidth);
            var userSelect = linkDialog.GetVisualDescendants().OfType<DaisySelect>().Single();
            var userSelectPeer = Assert.IsAssignableFrom<AutomationPeer>(
                ControlAutomationPeer.CreatePeerForElement(userSelect));
            Assert.IsAssignableFrom<IExpandCollapseProvider>(userSelectPeer);
            userSelect.SelectedIndex = 0;
            Dispatcher.UIThread.RunJobs();
            var linkButton = FindButtonByText(linkDialog, linkText);
            Assert.True(linkButton.IsEnabled);
            AssertMediumActionButton(linkButton);
            Invoke(linkButton);
            Assert.Same(user, await linkTask);
        }
        finally
        {
            CloseOpenDialogs(window);
            window.Close();
        }
    }

    [AvaloniaFact]
    public async Task When_SubtaskInlineEditHandlesEscape_TaskDialogRemainsOpen()
    {
        var task = new FlowTask { Title = "Task with subtask" };
        task.Subtasks.Add(new FlowSubtask { Title = "Editable subtask" });
        var window = CreateWindow(new Border());
        FlowTaskEditorDialog? dialog = null;

        try
        {
            var showTask = FlowTaskEditorDialog.ShowAsync(task, window);
            FlushLayout(window);
            dialog = window.GetVisualDescendants().OfType<FlowTaskEditorDialog>().Single();
            var tabs = dialog.GetVisualDescendants().OfType<DaisyTabs>().Single();
            tabs.SelectedIndex = 2;
            FlushLayout(window);

            var subtaskTitle = dialog.GetVisualDescendants()
                .OfType<TextBlock>()
                .Single(textBlock => string.Equals(
                    textBlock.Text,
                    "Editable subtask",
                    StringComparison.Ordinal));
            var subtaskRow = Assert.IsType<Grid>(subtaskTitle.GetVisualParent());
            var subtaskPanel = Assert.IsType<StackPanel>(subtaskRow.GetVisualParent());
            var editSubtasksField = typeof(FlowTaskEditorDialog).GetField(
                "_editSubtasks",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(editSubtasksField);
            var editSubtasks = Assert.IsType<List<FlowSubtask>>(
                editSubtasksField!.GetValue(dialog));
            var refreshMethod = typeof(FlowTaskEditorDialog).GetMethod(
                "RefreshSubtasksList",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
            Assert.NotNull(refreshMethod);
            refreshMethod!.Invoke(null, [subtaskPanel, editSubtasks, 0]);
            Dispatcher.UIThread.RunJobs();

            var editInput = FindInput(dialog, "Editable subtask");
            editInput.Text = "Discarded subtask edit";
            Assert.True(editInput.Focus());
            window.KeyPress(
                Key.Escape,
                RawInputModifiers.None,
                PhysicalKey.Escape,
                keySymbol: null);
            Dispatcher.UIThread.RunJobs();

            Assert.True(dialog.IsOpen);
            Assert.False(showTask.IsCompleted);
            Assert.Equal("Editable subtask", task.Subtasks.Single().Title);

            Invoke(FindActionButton(dialog, "Common_Cancel"));
            Assert.False(await showTask);
        }
        finally
        {
            CloseDialog(dialog);
            window.Close();
        }
    }

    private static FlowKanbanDialogBase FindDialog(Window window, string typeName)
    {
        return window.GetVisualDescendants()
            .OfType<FlowKanbanDialogBase>()
            .Single(dialog => string.Equals(dialog.GetType().Name, typeName, StringComparison.Ordinal));
    }

    private static void AssertDialogContract(
        FlowKanbanDialogBase dialog,
        Window window,
        string expectedName)
    {
        Assert.True(dialog.IsOpen);
        Assert.Equal(
            KeyboardNavigationMode.Cycle,
            KeyboardNavigation.GetTabNavigation(dialog));
        var peer = Assert.IsAssignableFrom<AutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(dialog));
        Assert.Equal(AutomationControlType.Window, peer.GetAutomationControlType());
        Assert.Equal(expectedName, peer.GetName());
        Assert.True(peer.IsControlElement());
        Assert.True(peer.IsContentElement());
        Assert.InRange(dialog.Bounds.Width, 1, window.Bounds.Width);
        Assert.InRange(dialog.Bounds.Height, 1, window.Bounds.Height);

        var focusedElement = window.FocusManager?.GetFocusedElement() as Visual;
        Assert.NotNull(focusedElement);
        Assert.True(
            ReferenceEquals(dialog, focusedElement)
            || dialog.IsVisualAncestorOf(focusedElement));
    }

    private static void AssertMediumActionButton(DaisyButton button)
    {
        Assert.Equal(DaisySize.Medium, button.Size);
        Assert.Equal(
            LayoutTestAssertions.GetUnoButtonHeight(DaisySize.Medium),
            button.Bounds.Height,
            precision: 3);
        var peer = Assert.IsAssignableFrom<AutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(button));
        Assert.IsAssignableFrom<IInvokeProvider>(peer);
        var contentPanel = button.GetVisualDescendants()
            .OfType<StackPanel>()
            .Single(panel => string.Equals(
                panel.Name,
                "PART_ContentPanel",
                StringComparison.Ordinal));
        LayoutTestAssertions.IsCentered(button, contentPanel);
    }

    private static void CloseOpenDialogs(Window window)
    {
        foreach (var dialog in window.GetVisualDescendants()
                     .OfType<FlowKanbanDialogBase>()
                     .Reverse()
                     .ToArray())
        {
            if (dialog.IsOpen)
            {
                dialog.IsOpen = false;
            }
        }

        Dispatcher.UIThread.RunJobs();
    }

    private static Window CreateWindow(Control content)
    {
        var window = new Window
        {
            Width = 1000,
            Height = 800,
            Content = content
        };
        window.Show();
        FlushLayout(window);
        return window;
    }

    private static void FlushLayout(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static DaisyInput FindInput(Control dialog, string text)
    {
        return dialog.GetVisualDescendants()
            .OfType<DaisyInput>()
            .Single(input => string.Equals(input.Text, text, StringComparison.Ordinal));
    }

    private static DaisyToggle FindLabeledToggle(Control dialog, string label)
    {
        return dialog.GetVisualDescendants()
            .OfType<DaisyToggle>()
            .Single(toggle => AutomationProperties.GetLabeledBy(toggle) is TextBlock textBlock
                              && string.Equals(textBlock.Text, label, StringComparison.Ordinal));
    }

    private static DaisyButton FindActionButton(Control dialog, string resourceKey)
    {
        var text = FloweryLocalization.GetString(resourceKey);
        return dialog.GetVisualDescendants()
            .OfType<DaisyButton>()
            .Single(button => string.Equals(button.Content as string, text, StringComparison.Ordinal));
    }

    private static DaisyButton FindButtonByText(Control dialog, string text)
    {
        return dialog.GetVisualDescendants()
            .OfType<DaisyButton>()
            .Single(button => string.Equals(button.Content as string, text, StringComparison.Ordinal));
    }

    private static void Invoke(DaisyButton button)
    {
        var peer = Assert.IsAssignableFrom<AutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(button));
        var provider = Assert.IsAssignableFrom<IInvokeProvider>(peer);
        provider.Invoke();
        Dispatcher.UIThread.RunJobs();
    }

    private static void AssertActualFooter(Control dialog)
    {
        var saveButton = FindActionButton(dialog, "Common_Save");
        var cancelButton = FindActionButton(dialog, "Common_Cancel");
        var expectedHeight = LayoutTestAssertions.GetUnoButtonHeight(DaisySize.Medium);

        Assert.Equal(DaisySize.Medium, saveButton.Size);
        Assert.Equal(DaisySize.Medium, cancelButton.Size);
        Assert.Equal(expectedHeight, saveButton.Bounds.Height, precision: 3);
        Assert.Equal(expectedHeight, cancelButton.Bounds.Height, precision: 3);

        var savePeer = Assert.IsAssignableFrom<AutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(saveButton));
        var cancelPeer = Assert.IsAssignableFrom<AutomationPeer>(
            ControlAutomationPeer.CreatePeerForElement(cancelButton));
        Assert.IsAssignableFrom<IInvokeProvider>(savePeer);
        Assert.IsAssignableFrom<IInvokeProvider>(cancelPeer);
    }

    private static void CloseDialog(FlowKanbanDialogBase? dialog)
    {
        if (dialog?.IsOpen == true)
        {
            dialog.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
        }
    }

    private sealed class DialogStateStorage : IStateStorage
    {
        private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);

        public IReadOnlyList<string> LoadLines(string key) =>
            _values.TryGetValue(key, out var lines) ? lines : Array.Empty<string>();

        public void SaveLines(string key, IEnumerable<string> lines)
        {
            _values[key] = new List<string>(lines);
        }

        public void Delete(string key)
        {
            _values.Remove(key);
        }

        public void Rename(string sourceKey, string targetKey)
        {
            if (!_values.TryGetValue(sourceKey, out var lines))
            {
                throw new InvalidOperationException($"Storage key '{sourceKey}' does not exist.");
            }

            _values[targetKey] = new List<string>(lines);
            _values.Remove(sourceKey);
        }

        public IEnumerable<string> GetKeys(string prefix) =>
            _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    }

    private sealed class DialogUser : IFlowUser
    {
        public DialogUser(string id, string displayName)
        {
            Id = id;
            DisplayName = displayName;
        }

        public string Id { get; }
        public string ProviderKey => "local";
        public string RawId => Id;
        public string DisplayName { get; }
        public string? Email => null;
        public string? AvatarUrl => null;
        public byte[]? AvatarBytes => null;
        public FlowUserStatus Status => FlowUserStatus.Online;
        public string? Department => null;
        public string? Title => null;
        public IReadOnlyDictionary<string, object>? CustomData => null;
    }
}
