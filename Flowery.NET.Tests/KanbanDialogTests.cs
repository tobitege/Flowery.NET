using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Kanban.Controls;
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
    public void When_InputColumnAndConfirmDialogsRun_OnlyConfirmedCommandsMutateBoard()
    {
        var previousStorage = StateStorageProvider.Instance;
        StateStorageProvider.Configure(new InMemoryStateStorage());
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
            var renameButton = subtaskTitle.FindAncestorOfType<DaisyButton>();
            Assert.NotNull(renameButton);
            Invoke(renameButton);
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

    private static DaisyButton FindActionButton(Control dialog, string resourceKey)
    {
        var text = FloweryLocalization.GetString(resourceKey);
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

    private static void CloseDialog(FlowKanbanDialogBase? dialog)
    {
        if (dialog?.IsOpen == true)
        {
            dialog.IsOpen = false;
            Dispatcher.UIThread.RunJobs();
        }
    }
}
