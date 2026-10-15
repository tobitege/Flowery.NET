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
using Flowery.NET.Kanban.Interfaces;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

public class KanbanAutomationRegressionTests
{
    [AvaloniaFact]
    public void ModelSelectionAndRealizedCardSelectionShareBoardProvider()
    {
        using var scope = new Scope();
        var first = new FlowTask { Title = "First" };
        var second = new FlowTask { Title = "Second", IsSelected = true };
        var column = new FlowKanbanColumnData { Title = "Tasks" };
        column.Tasks.Add(first);
        column.Tasks.Add(second);
        var data = new FlowKanbanData();
        data.Columns.Add(column);
        var board = new FlowKanban { Board = data, AutoSaveAfterEdits = false };
        var provider = Assert.IsAssignableFrom<ISelectionProvider>(ControlAutomationPeer.CreatePeerForElement(board));
        var virtualPeer = Assert.Single(provider.GetSelection());
        Assert.Equal("Second", virtualPeer.GetName());
        Assert.True(virtualPeer.IsOffscreen());
        var virtualSelection = Assert.IsAssignableFrom<ISelectionItemProvider>(virtualPeer);
        Assert.Same(provider, virtualSelection.SelectionContainer);
        virtualSelection.RemoveFromSelection();
        Assert.False(second.IsSelected);
        virtualSelection.AddToSelection();
        Assert.True(second.IsSelected);
        scope.Window.Content = board;
        scope.Flush();
        board.CurrentView = FlowKanbanView.Board;
        scope.Flush();
        var card = board.GetVisualDescendants().OfType<FlowTaskCard>().First(control => ReferenceEquals(control.Task, first));
        var cardPeer = ControlAutomationPeer.CreatePeerForElement(card);
        var selection = Assert.IsAssignableFrom<ISelectionItemProvider>(cardPeer);
        Assert.Same(provider, selection.SelectionContainer);
        selection.Select();
        Assert.True(first.IsSelected);
        Assert.False(second.IsSelected);
        Assert.Same(cardPeer, Assert.Single(provider.GetSelection()));
        virtualSelection.AddToSelection();
        Assert.Equal(2, provider.GetSelection().Count);
        Assert.Equal(2, board.SelectedCount);
        card.Focus();
        Press(scope.Window, Key.Down);
        Assert.Same(second, Assert.IsType<FlowTaskCard>(scope.Window.FocusManager!.GetFocusedElement()).Task);
        Press(scope.Window, Key.Up);
        Assert.Same(card, scope.Window.FocusManager!.GetFocusedElement());
        Press(scope.Window, Key.Space);
        Assert.False(first.IsSelected);
        Assert.Equal(1, board.SelectedCount);
        board.IsEnabled = false;
        Assert.Throws<InvalidOperationException>(selection.Select);
        Assert.Throws<InvalidOperationException>(virtualSelection.Select);
    }

    [AvaloniaFact]
    public async Task EditorPaletteAndSubtaskRenameCanBeChangedThroughKeyboardAndSaved()
    {
        using var scope = new Scope();
        var task = new FlowTask { Title = "Editor", Palette = DaisyColor.Default };
        task.Subtasks.Add(new FlowSubtask { Title = "Rename me" });
        var showTask = FlowTaskEditorDialog.ShowAsync(task, scope.Window);
        scope.Flush();
        var dialog = scope.Window.GetVisualDescendants().OfType<FlowTaskEditorDialog>().Single();
        try
        {
            var tabs = dialog.GetVisualDescendants().OfType<DaisyTabs>().Single();
            tabs.SelectedIndex = 1;
            scope.Flush();
            var color = dialog.GetVisualDescendants().OfType<RadioButton>().First(button => button.Tag is DaisyColor selected && selected != task.Palette);
            var selectedColor = Assert.IsType<DaisyColor>(color.Tag);
            color.Focus();
            Press(scope.Window, Key.Space);
            Assert.True(color.IsChecked);
            Assert.Equal(DaisyColor.Default, task.Palette);
            tabs.SelectedIndex = 2;
            scope.Flush();
            var rename = dialog.GetVisualDescendants().OfType<DaisyButton>().Single(button => AutomationProperties.GetName(button) == "Rename me");
            Assert.True(rename.Focus());
            Press(scope.Window, Key.Enter);
            scope.Flush();
            var editor = dialog.GetVisualDescendants().OfType<DaisyInput>().Single(input => input.Text == "Rename me");
            editor.Text = "Discard me";
            Press(scope.Window, Key.Escape);
            scope.Flush();
            rename = dialog.GetVisualDescendants().OfType<DaisyButton>().Single(button => AutomationProperties.GetName(button) == "Rename me");
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(rename)).Invoke();
            scope.Flush();
            editor = dialog.GetVisualDescendants().OfType<DaisyInput>().Single(input => input.Text == "Rename me");
            editor.Text = "Renamed";
            Press(scope.Window, Key.Enter);
            scope.Flush();
            Assert.Equal("Rename me", task.Subtasks[0].Title);
            var save = dialog.GetVisualDescendants().OfType<DaisyButton>().Single(button => Equals(button.Content, FloweryLocalization.GetString("Common_Save")));
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(save)).Invoke();
            Dispatcher.UIThread.RunJobs();
            Assert.True(await showTask);
            Assert.Equal(selectedColor, task.Palette);
            Assert.Equal("Renamed", task.Subtasks[0].Title);
        }
        finally { dialog.IsOpen = false; }
    }

    [AvaloniaTheory]
    [InlineData("newer")]
    [InlineData("home")]
    [InlineData("detach")]
    [InlineData("store")]
    public async Task PendingBoardLoadCannotOverrideNewerIntent(string intent)
    {
        using var scope = new Scope();
        using var store = new GatedStore();
        var board = new FlowKanban { BoardStore = store, AutoSaveAfterEdits = false, Board = new FlowKanbanData { Id = "initial" } };
        scope.Window.Content = board;
        scope.Flush();
        board.CurrentView = FlowKanbanView.Home;
        var original = board.Board;
        var pending = board.TryLoadBoardAsync("old");
        await store.Entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            switch (intent)
            {
                case "newer": Assert.True((await board.TryLoadBoardAsync("new")).loaded); break;
                case "home": board.ShowHomeCommand.Execute(null); break;
                case "detach": scope.Window.Content = null; scope.Flush(); break;
                case "store":
                    board.BoardStore = new FlowKanbanBoardStore(scope);
                    board.BoardStore = store;
                    break;
            }
        }
        finally { store.Release.Set(); }
        Assert.False((await pending.WaitAsync(TimeSpan.FromSeconds(5))).loaded);
        Assert.Same(intent == "newer" ? store.NewBoard : original, board.Board);
        Assert.Equal(FlowKanbanView.Home, board.CurrentView);
    }

    private sealed class GatedStore : IBoardStore, IDisposable
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public ManualResetEventSlim Release { get; } = new();
        public FlowKanbanData NewBoard { get; } = new() { Id = "new", Title = "New" };
        public IReadOnlyList<FlowBoardMetadata> ListBoards() => [];
        public bool TryLoadBoard(string boardId, out FlowKanbanData? board, out Exception? error)
        {
            if (boardId == "old")
            {
                Entered.TrySetResult();
                if (!Release.Wait(TimeSpan.FromSeconds(5))) throw new TimeoutException("The test did not release the pending load.");
            }
            board = boardId == "new" ? NewBoard : new FlowKanbanData { Id = "old", Title = "Old" };
            error = null;
            return true;
        }
        public bool TrySaveBoard(FlowKanbanData board, out Exception? error) { error = null; return true; }
        public bool TryDeleteBoard(string boardId, out Exception? error) { error = null; return true; }
        public bool TryExportBoard(string boardId, out string? json, out Exception? error) { json = null; error = null; return false; }
        public void Dispose() { Release.Set(); Release.Dispose(); }
    }

    private static void Press(Window window, Key key)
    {
        window.KeyPress(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        window.KeyRelease(key, RawInputModifiers.None, PhysicalKey.None, keySymbol: null);
        Dispatcher.UIThread.RunJobs();
    }

    private sealed class Scope : IDisposable, IStateStorage
    {
        private readonly IStateStorage _previous = StateStorageProvider.Instance;
        private readonly Dictionary<string, List<string>> _values = new(StringComparer.Ordinal);
        public Window Window { get; } = new() { Width = 1200, Height = 900, Content = new Border() };
        public Scope() { StateStorageProvider.Configure(this); Window.Show(); Flush(); }
        public void Flush() { Dispatcher.UIThread.RunJobs(); Window.UpdateLayout(); Dispatcher.UIThread.RunJobs(); }
        public void Dispose() { Window.Close(); StateStorageProvider.Configure(_previous); }
        public IReadOnlyList<string> LoadLines(string key) => _values.TryGetValue(key, out var value) ? value : [];
        public void SaveLines(string key, IEnumerable<string> lines) => _values[key] = [.. lines];
        public void Delete(string key) => _values.Remove(key);
        public void Rename(string sourceKey, string targetKey) { _values[targetKey] = _values[sourceKey]; _values.Remove(sourceKey); }
        public IEnumerable<string> GetKeys(string prefix) => _values.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
    }
}
