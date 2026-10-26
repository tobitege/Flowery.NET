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
    public void With_Touch_Selection_A_Tap_Selects_A_Card_A_Double_Tap_Edits_It_And_Swipes_Do_Neither()
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

            // As in DaisyScheduler: a tap selects, the second tap of a double tap edits and keeps the selection.
            TouchTap(window, Card(window, task));
            Assert.True(task.IsSelected);
            Assert.Empty(edits);
            TouchTap(window, Card(window, task));
            Assert.Equal([task], edits);
            Assert.True(task.IsSelected);
            // A third tap starts a new pair.
            TouchTap(window, Card(window, task));
            Assert.Single(edits);

            // A tap on another card selects that one alone.
            TouchTap(window, Card(window, other));
            Assert.True(other.IsSelected);
            Assert.False(task.IsSelected);

            // Swipes over a card neither select nor edit it, and a tap after a swipe is no second tap.
            TouchSwipe(window, Card(window, task));
            TouchSwipe(window, Card(window, task));
            Assert.False(task.IsSelected);
            TouchSwipe(window, Card(window, task));
            TouchTap(window, Card(window, task));
            Assert.True(task.IsSelected);
            Assert.Single(edits);
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

            // An unselected card scrolls instead of moving.
            TouchDrag(window, Card(window, second), BottomOf(Card(window, first), window));
            Assert.Equal([second], todo.Tasks);

            // A touch the system cancels (no release, the capture is lost) ends the drag without a move.
            second.IsSelected = true;
            var card = Card(window, second);
            card.UsesTouchSelection = true;
            IPointer? pointer = null;
            window.AddHandler(InputElement.PointerPressedEvent, (_, e) => pointer = e.Pointer, RoutingStrategies.Tunnel, true);
            var start = Spot(window, card);
            var touch = window.TouchBegin(start, RawInputModifiers.None);
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

    private static FlowTaskCard Card(Window window, FlowTask task) =>
        window.GetVisualDescendants().OfType<FlowTaskCard>().Single(card => ReferenceEquals(card.Task, task));

    // A point just above the bottom edge of a card, in the window.
    private static Point BottomOf(FlowTaskCard card, Window window) =>
        card.TranslatePoint(new Point(card.Bounds.Width / 2, card.Bounds.Height - 4), window)!.Value;

    // The column builds its cards anew after a move, so the touch mode is set on the card that is dragged.
    private static void TouchDrag(Window window, FlowTaskCard card, Point target)
    {
        card.UsesTouchSelection = true;
        var start = Spot(window, card);
        var touch = window.TouchBegin(start, RawInputModifiers.None);
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
        var card = window.GetVisualDescendants().OfType<FlowTaskCard>().Single();
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
