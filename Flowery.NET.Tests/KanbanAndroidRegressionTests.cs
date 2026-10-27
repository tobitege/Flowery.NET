using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Kanban.Controls;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests;

// The Kanban board on a phone or tablet: the on-screen keyboard, touch taps and a low board.
public class KanbanAndroidRegressionTests
{
    [AvaloniaFact]
    public async Task A_Dialog_Moves_Above_The_On_Screen_Keyboard_And_Back()
    {
        var window = CreateWindow(new Button(), 800);
        try
        {
            var showTask = FlowTaskEditorDialog.ShowAsync(new FlowTask { Title = "Task" }, window);
            Flush(window);
            var dialog = window.GetVisualDescendants().OfType<FlowTaskEditorDialog>().Single();
            var save = dialog.GetVisualDescendants().OfType<DaisyButton>()
                .Single(button => Equals(button.Content, FloweryLocalization.GetString("Common_Save")));
            var height = dialog.Bounds.Height;

            // The keyboard covers the lower 350 of 800 and leaves the overlay as it is.
            dialog.KeepClearOfInputPane(new Rect(0, 450, 1000, 350));
            Flush(window);
            Assert.True(Bottom(save, window) <= 450, $"Save ends at {Bottom(save, window)}, under the keyboard");
            Assert.True(Bottom(dialog, window) <= 450);

            dialog.KeepClearOfInputPane(default);
            Flush(window);
            Assert.Equal(height, dialog.Bounds.Height, 1);

            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, keySymbol: null);
            Dispatcher.UIThread.RunJobs();
            Assert.False(await showTask);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void On_Touch_A_Tap_Edits_A_Card_A_Hold_Selects_It_And_Taps_Then_Toggle_The_Selection()
    {
        var task = new FlowTask { Title = "Task" };
        var other = new FlowTask { Title = "Other" };
        var column = new FlowKanbanColumnData { Title = "Todo", Tasks = { task, other } };
        var edits = new List<FlowTask>();
        var board = new FlowKanban
        {
            AutoSaveAfterEdits = false,
            Board = new FlowKanbanData { Columns = { column } },
            EditCardCommand = new EditRecorder(edits)
        };
        var window = CreateWindow(board, 700);
        try
        {
            board.CurrentView = FlowKanbanView.Board;
            Flush(window);

            // As in the apps of the platforms: a tap opens the card.
            TouchTap(window, Card(window, task));
            Assert.Equal([task], edits);
            Assert.False(task.IsSelected);

            // Holding it selects it, and its release is no tap.
            TouchHold(window, Card(window, task));
            Assert.True(task.IsSelected);
            Assert.Single(edits);

            // While cards are selected, a tap adds a card to the selection or takes it out.
            TouchTap(window, Card(window, other));
            Assert.True(other.IsSelected);
            Assert.True(task.IsSelected);
            TouchTap(window, Card(window, task));
            Assert.False(task.IsSelected);
            TouchTap(window, Card(window, other));
            Assert.False(other.IsSelected);
            Assert.Single(edits);

            // Without a selection a tap opens again, and swipes neither open nor select.
            TouchTap(window, Card(window, task));
            Assert.Equal(2, edits.Count);
            TouchSwipe(window, Card(window, task));
            TouchSwipe(window, Card(window, task));
            Assert.Equal(2, edits.Count);
            Assert.False(task.IsSelected);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void On_Touch_A_Card_Tapped_Before_It_Knows_Its_Board_Joins_The_Selection()
    {
        var selected = new FlowTask { Title = "Selected", IsSelected = true };
        var task = new FlowTask { Title = "Task" };
        var column = new FlowKanbanColumnData { Title = "Todo", Tasks = { selected } };
        var edits = new List<FlowTask>();
        var board = new FlowKanban
        {
            AutoSaveAfterEdits = false,
            Board = new FlowKanbanData { Columns = { column } },
            EditCardCommand = new EditRecorder(edits)
        };
        var window = CreateWindow(board, 700);
        try
        {
            board.CurrentView = FlowKanbanView.Board;
            Flush(window);

            // The layout realizes the new card, and the dispatcher raises its Loaded later. On a phone the platform
            // delivers a touch right away, so it can reach the card in between; the headless input helpers would run the
            // dispatcher first, so the touch is raised on the card itself.
            column.Tasks.Add(task);
            window.UpdateLayout();
            var card = window.GetVisualDescendants().OfType<FlowTaskCard>().Single(found => ReferenceEquals(found.Task, task));
            Assert.False(card.IsLoaded);
            card.UsesTouchSelection = true;
            var pointer = new Pointer(Pointer.GetNextFreeId(), PointerType.Touch, isPrimary: true);
            var point = Spot(window, card);
            card.RaiseEvent(new PointerPressedEventArgs(card, pointer, window, point, 0,
                new PointerPointProperties(RawInputModifiers.LeftMouseButton, PointerUpdateKind.LeftButtonPressed), KeyModifiers.None));
            card.RaiseEvent(new PointerReleasedEventArgs(card, pointer, window, point, 1,
                new PointerPointProperties(RawInputModifiers.None, PointerUpdateKind.LeftButtonReleased), KeyModifiers.None, MouseButton.Left));
            Assert.False(card.IsLoaded);

            Assert.True(task.IsSelected);
            Assert.True(selected.IsSelected);
            Assert.Empty(edits);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Tap_Or_Click_On_A_Free_Place_Clears_The_Selection_And_A_Swipe_There_Does_Not()
    {
        var task = new FlowTask { Title = "Task", IsSelected = true };
        var (window, card) = CreateBoard(task);
        try
        {
            // Below the card, inside its column.
            var free = card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height + 40), window)!.Value;

            // A vertical swipe beside the column, where nothing scrolls that way, so the release reaches the board.
            var beside = card.TranslatePoint(new Point(card.Bounds.Width + 150, card.Bounds.Height / 2), window)!.Value;
            var touch = window.TouchBegin(beside, RawInputModifiers.None);
            window.TouchMove(touch, beside + new Vector(0, 40));
            window.TouchEnd(touch, beside + new Vector(0, 40), RawInputModifiers.None);
            Flush(window);
            Assert.True(task.IsSelected, "a swipe clears nothing");

            // A tap on the selected card unselects it.
            TouchTap(window, Card(window, task));
            Assert.False(task.IsSelected);

            task.IsSelected = true;
            Flush(window);

            touch = window.TouchBegin(free, RawInputModifiers.None);
            window.TouchEnd(touch, free, RawInputModifiers.None);
            Flush(window);
            Assert.False(task.IsSelected);

            // Beside the column as well.
            task.IsSelected = true;
            Flush(window);
            touch = window.TouchBegin(beside, RawInputModifiers.None);
            window.TouchEnd(touch, beside, RawInputModifiers.None);
            Flush(window);
            Assert.False(task.IsSelected);

            // A mouse drag over a free place clears nothing; the scroll gesture leaves the mouse alone.
            task.IsSelected = true;
            Flush(window);
            window.MouseDown(beside, MouseButton.Left);
            window.MouseMove(beside + new Vector(0, 40));
            window.MouseUp(beside + new Vector(0, 40), MouseButton.Left);
            Flush(window);
            Assert.True(task.IsSelected, "a mouse drag clears nothing");

            window.MouseDown(free, MouseButton.Left);
            window.MouseUp(free, MouseButton.Left);
            Flush(window);
            Assert.False(task.IsSelected);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Tap_On_A_Board_Tile_Opens_The_Board_And_Its_Menu_Names_Its_Entries()
    {
        var previousStorage = StateStorageProvider.Instance;
        var storage = new InMemoryStateStorage();
        StateStorageProvider.Configure(storage);
        var store = new FlowKanbanBoardStore(storage);
        var saved = new FlowKanbanData { Title = "Saved", Columns = { new FlowKanbanColumnData { Title = "Todo" } } };
        Assert.True(store.TrySaveBoard(saved, out var saveError), saveError?.Message);
        var board = new FlowKanban { AutoSaveAfterEdits = false, BoardStore = store };
        var window = CreateWindow(board, 700);
        try
        {
            board.CurrentView = FlowKanbanView.Home;
            Flush(window);

            var tile = window.GetVisualDescendants().OfType<Border>().Single(static border => border.Name == "PART_BoardOpenSurface");
            var menu = window.GetVisualDescendants().OfType<DaisyButton>().Single(static button => button.Name == "PART_BoardMenuButton");
            Assert.All(Assert.IsType<MenuFlyout>(menu.Flyout).Items.OfType<MenuItem>(), static item =>
            {
                Assert.False(string.IsNullOrEmpty(item.Header as string), $"{item.Tag} has no text");
                Assert.NotNull(item.Command);
            });

            var point = tile.TranslatePoint(new Point(24, 24), window)!.Value;
            var touch = window.TouchBegin(point, RawInputModifiers.None);
            window.TouchEnd(touch, point, RawInputModifiers.None);
            // The board loads on a worker thread and comes back through the dispatcher.
            for (var attempt = 0; attempt < 50 && board.CurrentView != FlowKanbanView.Board; attempt++)
            {
                Thread.Sleep(20);
                Flush(window);
            }

            Assert.Equal(FlowKanbanView.Board, board.CurrentView);
            Assert.Equal(saved.Id, board.Board.Id);
        }
        finally
        {
            window.Close();
            StateStorageProvider.Configure(previousStorage);
        }
    }

    [AvaloniaFact]
    public void A_Board_Tile_Still_Opens_After_The_Home_View_Was_Taken_Out_And_Put_Back()
    {
        var board = new FlowBoardMetadata { Id = "board", Title = "Board" };
        var opened = new List<object?>();
        var home = new FlowKanbanHome
        {
            Boards = new System.Collections.ObjectModel.ObservableCollection<FlowBoardMetadata> { board },
            OpenBoardCommand = new CommandRecorder(opened)
        };
        var host = new ContentControl { Content = home };
        var window = CreateWindow(host, 700);
        try
        {
            // As a tab switch or a cached page does; the presenters build their tiles anew.
            host.Content = null;
            Flush(window);
            host.Content = home;
            Flush(window);

            var menu = window.GetVisualDescendants().OfType<DaisyButton>().Single(static button => button.Name == "PART_BoardMenuButton");
            Assert.All(Assert.IsType<MenuFlyout>(menu.Flyout).Items.OfType<MenuItem>(), static item =>
                Assert.False(string.IsNullOrEmpty(item.Header as string), $"{item.Tag} has no text"));

            var tile = window.GetVisualDescendants().OfType<Border>().Single(static border => border.Name == "PART_BoardOpenSurface");
            var point = tile.TranslatePoint(new Point(24, 24), window)!.Value;
            var touch = window.TouchBegin(point, RawInputModifiers.None);
            window.TouchEnd(touch, point, RawInputModifiers.None);
            Flush(window);

            Assert.Same(board, Assert.Single(opened));
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Tap_On_The_Tile_Of_The_Open_Board_Shows_Its_Waiting_Edit()
    {
        var previousStorage = StateStorageProvider.Instance;
        var storage = new InMemoryStateStorage();
        StateStorageProvider.Configure(storage);
        var store = new FlowKanbanBoardStore(storage);
        var column = new FlowKanbanColumnData { Title = "Todo" };
        var data = new FlowKanbanData { Title = "Saved", Columns = { column } };
        Assert.True(store.TrySaveBoard(data, out var saveError), saveError?.Message);
        var board = new FlowKanban { Board = data, BoardStore = store };
        var window = CreateWindow(board, 700);
        DispatcherTimer? timer = null;
        try
        {
            board.CurrentView = FlowKanbanView.Board;
            Flush(window);

            var collapsed = !column.IsCollapsed;
            column.IsCollapsed = collapsed;
            timer = (DispatcherTimer?)typeof(FlowKanban).GetField("_autoSaveTimer",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(board);
            Assert.True(timer?.IsEnabled, "the edit did not start an auto-save");

            // Home and back to the same board before the auto-save came; the tile loads the board on a worker thread.
            board.CurrentView = FlowKanbanView.Home;
            Flush(window);
            var tile = window.GetVisualDescendants().OfType<Border>().Single(static border => border.Name == "PART_BoardOpenSurface");
            var point = tile.TranslatePoint(new Point(24, 24), window)!.Value;
            var touch = window.TouchBegin(point, RawInputModifiers.None);
            window.TouchEnd(touch, point, RawInputModifiers.None);
            for (var attempt = 0; attempt < 50 && board.CurrentView != FlowKanbanView.Board; attempt++)
            {
                Thread.Sleep(20);
                Flush(window);
            }

            Assert.Equal(FlowKanbanView.Board, board.CurrentView);
            Assert.NotSame(data, board.Board);
            Assert.Equal(collapsed, Assert.Single(board.Board.Columns).IsCollapsed);
        }
        finally
        {
            timer?.Stop();
            window.Close();
            StateStorageProvider.Configure(previousStorage);
        }
    }

    [AvaloniaFact]
    public void A_Tap_On_The_Column_Choice_Of_The_Compact_Layout_Keeps_The_Selection()
    {
        var task = new FlowTask { Title = "Task", IsSelected = true };
        var (window, _) = CreateBoard(task);
        try
        {
            var board = (FlowKanban)window.Content!;
            board.SetCompactLayout(true);
            Flush(window);
            var choice = window.GetVisualDescendants().OfType<ComboBox>().Single(static box => box.Name == "PART_CompactColumnSelect");
            var point = choice.TranslatePoint(new Point(choice.Bounds.Width / 2, choice.Bounds.Height / 2), window)!.Value;
            var touch = window.TouchBegin(point, RawInputModifiers.None);
            window.TouchEnd(touch, point, RawInputModifiers.None);
            Flush(window);
            Assert.True(task.IsSelected);
        }
        finally
        {
            window.Close();
        }
    }

    private sealed class EditRecorder(List<FlowTask> edits) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => edits.Add((FlowTask)parameter!);
    }

    private sealed class CommandRecorder(List<object?> parameters) : System.Windows.Input.ICommand
    {
        public event EventHandler? CanExecuteChanged { add { } remove { } }
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => parameters.Add(parameter);
    }

    [AvaloniaFact]
    public void A_Board_Lower_Than_Its_Sidebar_Scrolls_The_Sidebar_Instead_Of_Overlapping_Its_Groups()
    {
        var (window, _) = CreateBoard(new FlowTask { Title = "Task" }, height: 160);
        try
        {
            var sidebar = window.GetVisualDescendants().OfType<Border>().Single(border => border.Name == "PART_SidebarBorder");
            // The actions above, the statistics below.
            var groups = sidebar.GetVisualDescendants().OfType<Grid>()
                .Select(static grid => grid.Children.OfType<StackPanel>().ToArray())
                .Single(static panels => panels.Length == 2);
            // A row lower than its panel squeezes the panel, but its buttons keep their height.
            var lastAction = groups[0].GetVisualDescendants().OfType<DaisyButton>().Last(static button => button.IsEffectivelyVisible);
            var actionsBottom = Bottom(lastAction, sidebar);
            Assert.True(actionsBottom - groups[0].TranslatePoint(default, sidebar)!.Value.Y + groups[1].DesiredSize.Height
                > sidebar.Bounds.Height, "the board is lower than its sidebar");
            var statisticsTop = groups[1].TranslatePoint(default, sidebar)!.Value.Y;
            Assert.True(statisticsTop >= actionsBottom, $"statistics at {statisticsTop}, actions end at {actionsBottom}");

            // A tall board keeps the statistics at the bottom.
            window.Height = 900;
            Flush(window);
            var bottom = groups[1].TranslatePoint(new Point(0, groups[1].Bounds.Height), sidebar)!.Value.Y;
            Assert.Equal(sidebar.Bounds.Height - sidebar.Padding.Bottom, bottom, 1);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void With_Touch_Selection_A_Finger_Drags_A_Selected_Card_To_Another_Column_And_Within_Its_Own()
    {
        var first = new FlowTask { Title = "First", IsSelected = true };
        var second = new FlowTask { Title = "Second" };
        var todo = new FlowKanbanColumnData { Title = "Todo", Tasks = { first, second } };
        var done = new FlowKanbanColumnData { Title = "Done", Tasks = { new FlowTask { Title = "Shipped" } } };
        var window = CreateWindow(new FlowKanban { AutoSaveAfterEdits = false, Board = new FlowKanbanData { Columns = { todo, done } } }, 700);
        try
        {
            var board = (FlowKanban)window.Content!;
            board.CurrentView = FlowKanbanView.Board;
            Flush(window);

            // Within its column: below the second card.
            var secondCard = Card(window, second);
            TouchDrag(window, Card(window, first), BottomOf(secondCard, window));
            Assert.Equal([second, first], todo.Tasks);

            // To the other column, below its card; no copy of the card stays in the overlay.
            TouchDrag(window, Card(window, first), BottomOf(Card(window, done.Tasks[0]), window));
            Assert.Equal([second], todo.Tasks);
            Assert.Same(first, done.Tasks.Last());
            Assert.Empty(Ghosts(board));

            // A swipe without a hold scrolls instead of moving the card.
            TouchSwipe(window, Card(window, second));
            Assert.Equal([second], todo.Tasks);

            // A touch the system cancels (no release, the capture is lost) ends the drag without a move.
            var card = Card(window, second);
            IPointer? pointer = null;
            window.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, true);
            var start = Spot(window, card);
            var touch = HoldStart(window, card);
            window.TouchMove(touch, start + new Vector(200, 0));
            Flush(window);
            Assert.Same(card, pointer!.Captured);
            Assert.NotEmpty(Ghosts(board));
            pointer.Capture(null);
            Flush(window);
            Assert.Empty(Ghosts(board));
            Assert.Equal([second], todo.Tasks);
        }
        finally
        {
            window.Close();
        }
    }

    // The copies of dragged cards in the overlay.
    private static IEnumerable<Border> Ghosts(Visual board) =>
        OverlayLayer.GetOverlayLayer(board)!.Children.OfType<Border>().Where(static border => border.Background is VisualBrush);

    // The column builds its cards anew after a selection or a move. A tap does not need a loaded card, but the other
    // handlers of a card that is only in the tree have not run yet, so a test taps a card only once it is loaded.
    private static FlowTaskCard Card(Window window, FlowTask task)
    {
        var card = Find(window, task);
        for (var attempt = 0; attempt < 10 && !card.IsLoaded; attempt++)
        {
            Flush(window);
            card = Find(window, task);
        }
        Assert.True(card.IsLoaded, $"the card of {task.Title} did not load");
        return card;

        static FlowTaskCard Find(Window window, FlowTask task) =>
            window.GetVisualDescendants().OfType<FlowTaskCard>().Single(found => ReferenceEquals(found.Task, task));
    }

    // A point just above the bottom edge of a card, in the window.
    private static Point BottomOf(FlowTaskCard card, Window window) =>
        card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height - 4), window)!.Value;

    // Holds a card and drags it to the target.
    private static void TouchDrag(Window window, FlowTaskCard card, Point target)
    {
        var start = Spot(window, card);
        var touch = HoldStart(window, card);
        for (var step = 1; step <= 10; step++)
        {
            window.TouchMove(touch, start + (target - start) * (step / 10.0));
            Flush(window);
        }
        window.TouchEnd(touch, target, RawInputModifiers.None);
        Flush(window);
    }

    private static (Window Window, FlowTaskCard Card) CreateBoard(FlowTask task, double height = 700)
    {
        var column = new FlowKanbanColumnData { Title = "Todo" };
        column.Tasks.Add(task);
        var board = new FlowKanban { AutoSaveAfterEdits = false, Board = new FlowKanbanData { Columns = { column } } };
        var window = CreateWindow(board, height);
        // Without a stored view the board opens on its home view, as on a fresh machine.
        board.CurrentView = FlowKanbanView.Board;
        Flush(window);
        var card = Card(window, task);
        card.UsesTouchSelection = true;
        return (window, card);
    }

    private static Window CreateWindow(Control content, double height)
    {
        var window = new Window { Width = 1000, Height = height, Content = content };
        window.Show();
        Flush(window);
        return window;
    }

    private static void Flush(Window window)
    {
        Dispatcher.UIThread.RunJobs();
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();
    }

    private static double Bottom(Visual visual, Visual root) =>
        visual.TranslatePoint(new Point(0, visual.Bounds.Height), root)!.Value.Y;

    // A point of the card away from its buttons.
    private static Point Spot(Window window, FlowTaskCard card) =>
        card.TranslatePoint(new Point(12, card.Bounds.Height / 2), window)!.Value;

    // Puts a finger on the card and keeps it there longer than the hold of the platform. The column builds its cards anew
    // after a selection or a move, so the touch mode is set on the card the finger is on.
    private static IHeadlessTouchPointer HoldStart(Window window, FlowTaskCard card)
    {
        card.UsesTouchSelection = true;
        var touch = window.TouchBegin(Spot(window, card), RawInputModifiers.None);
        var hold = Application.Current!.PlatformSettings!.HoldWaitDuration;
        Thread.Sleep(hold + TimeSpan.FromMilliseconds(150));
        // The dispatcher looks for due timers only after it ran a job.
        Dispatcher.UIThread.Post(static () => { });
        Flush(window);
        Flush(window);
        return touch;
    }

    private static void TouchHold(Window window, FlowTaskCard card)
    {
        var point = Spot(window, card);
        var touch = HoldStart(window, card);
        window.TouchEnd(touch, point, RawInputModifiers.None);
        Flush(window);
    }

    private static void TouchTap(Window window, FlowTaskCard card)
    {
        card.UsesTouchSelection = true;
        var point = Spot(window, card);
        var touch = window.TouchBegin(point, RawInputModifiers.None);
        window.TouchEnd(touch, point, RawInputModifiers.None);
        Flush(window);
    }

    private static void TouchSwipe(Window window, FlowTaskCard card)
    {
        card.UsesTouchSelection = true;
        var point = Spot(window, card);
        var touch = window.TouchBegin(point, RawInputModifiers.None);
        for (var step = 1; step <= 4; step++)
            window.TouchMove(touch, point + new Vector(0, step * 10));
        window.TouchEnd(touch, point + new Vector(0, 40), RawInputModifiers.None);
        Flush(window);
    }
}
