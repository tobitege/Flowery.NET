using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Kanban.Controls;
using Xunit;

namespace Flowery.NET.Tests;

// Two fingers zoom the board through its sizes (FlowKanban.Pinch).
public partial class KanbanAndroidRegressionTests
{
    [AvaloniaFact]
    public void Spreading_Two_Fingers_Zooms_The_Board_In_And_Pinching_Zooms_It_Out_Within_Its_Sizes()
    {
        var (window, card) = CreateBoard(new FlowTask { Title = "Task" });
        try
        {
            var board = (FlowKanban)window.Content!;
            board.BoardSize = DaisySize.Medium;
            // Beside the column, where no card is.
            var middle = card.TranslatePoint(new Point(card.Bounds.Width + 200, card.Bounds.Height), window)!.Value;

            var (first, second) = PinchBegin(window, middle, 100);
            // One size per quarter of the distance, truncated: 130 is one, 160 two, and the sizes end at ExtraLarge.
            PinchMove(window, first, second, middle, 120);
            Assert.Equal(DaisySize.Medium, board.BoardSize);
            PinchMove(window, first, second, middle, 130);
            Assert.Equal(DaisySize.Large, board.BoardSize);
            PinchMove(window, first, second, middle, 160);
            Assert.Equal(DaisySize.ExtraLarge, board.BoardSize);
            PinchMove(window, first, second, middle, 300);
            Assert.Equal(DaisySize.ExtraLarge, board.BoardSize);
            Assert.False(board.ZoomInCommand.CanExecute(null));

            // Back to where the fingers started, and closer: down to ExtraSmall.
            PinchMove(window, first, second, middle, 100);
            Assert.Equal(DaisySize.Medium, board.BoardSize);
            PinchMove(window, first, second, middle, 30);
            Assert.Equal(DaisySize.ExtraSmall, board.BoardSize);
            PinchEnd(window, first, second, middle, 30);

            // The next pinch starts at the size the last one left.
            (first, second) = PinchBegin(window, middle, 100);
            PinchMove(window, first, second, middle, 60);
            Assert.Equal(DaisySize.ExtraSmall, board.BoardSize);
            PinchMove(window, first, second, middle, 130);
            Assert.Equal(DaisySize.Small, board.BoardSize);
            PinchEnd(window, first, second, middle, 130);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Pinch_That_Starts_On_A_Card_Neither_Opens_Selects_Nor_Holds_It()
    {
        var task = new FlowTask { Title = "Task" };
        var edits = new List<FlowTask>();
        var (window, card) = CreateBoard(task);
        try
        {
            var board = (FlowKanban)window.Content!;
            board.EditCardCommand = new EditRecorder(edits);
            board.BoardSize = DaisySize.Medium;
            var onCard = Spot(window, card);

            var first = window.TouchBegin(onCard, RawInputModifiers.None);
            var second = window.TouchBegin(onCard + new Vector(0, 60), RawInputModifiers.None);
            window.TouchMove(second, onCard + new Vector(0, 90));
            Flush(window);
            Assert.Equal(DaisySize.Large, board.BoardSize);

            // Longer than the hold: the first finger selects nothing.
            WaitLongerThanAHold(window);
            Assert.False(task.IsSelected);

            window.TouchEnd(second, onCard + new Vector(0, 90), RawInputModifiers.None);
            window.TouchEnd(first, onCard, RawInputModifiers.None);
            Flush(window);
            Assert.False(task.IsSelected);
            Assert.Empty(edits);

            // One finger works as before.
            TouchTap(window, Card(window, task));
            Assert.Equal([task], edits);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void A_Second_Finger_Ends_The_Drag_Of_A_Held_Card_Without_A_Drop()
    {
        var first = new FlowTask { Title = "First", IsSelected = true };
        var second = new FlowTask { Title = "Second" };
        var todo = new FlowKanbanColumnData { Title = "Todo", Tasks = { first, second } };
        var done = new FlowKanbanColumnData { Title = "Done" };
        var board = new FlowKanban { AutoSaveAfterEdits = false, Board = new FlowKanbanData { Columns = { todo, done } } };
        var window = CreateWindow(board, 700);
        try
        {
            board.CurrentView = FlowKanbanView.Board;
            board.BoardSize = DaisySize.Medium;
            Flush(window);
            UseTouchSelection(window);

            var card = Card(window, first);
            var start = Spot(window, card);
            var held = HoldStart(window, card);
            window.TouchMove(held, start + new Vector(200, 0));
            Flush(window);
            Assert.NotEmpty(Ghosts(board));

            var other = window.TouchBegin(start + new Vector(200, 80), RawInputModifiers.None);
            Flush(window);
            Assert.Empty(Ghosts(board));

            window.TouchMove(other, start + new Vector(200, 120));
            window.TouchMove(held, start + new Vector(300, 0));
            window.TouchEnd(other, start + new Vector(200, 120), RawInputModifiers.None);
            window.TouchEnd(held, start + new Vector(300, 0), RawInputModifiers.None);
            Flush(window);
            Assert.Equal([first, second], todo.Tasks);
            Assert.Empty(done.Tasks);
            Assert.Empty(Ghosts(board));
        }
        finally
        {
            window.Close();
        }
    }

    // A lost capture ends the press and the drag of the finger (FlowTaskCard.Touch); the hold that selects a card and
    // builds it anew must not end them.
    [AvaloniaFact]
    public void Holding_An_Unselected_Card_Selects_It_And_The_Finger_Still_Drags_It()
    {
        var first = new FlowTask { Title = "First" };
        var todo = new FlowKanbanColumnData { Title = "Todo", Tasks = { first, new FlowTask { Title = "Second" } } };
        var done = new FlowKanbanColumnData { Title = "Done", Tasks = { new FlowTask { Title = "Shipped" } } };
        var board = new FlowKanban { AutoSaveAfterEdits = false, Board = new FlowKanbanData { Columns = { todo, done } } };
        var window = CreateWindow(board, 700);
        try
        {
            board.CurrentView = FlowKanbanView.Board;
            Flush(window);

            TouchDrag(window, Card(window, first), BottomOf(Card(window, done.Tasks[0]), window));
            Assert.True(first.IsSelected);
            Assert.Same(first, done.Tasks.Last());
        }
        finally
        {
            window.Close();
        }
    }

    // On a phone, fingers often land in the room the compact layout leaves between the column choice and the column.
    [AvaloniaFact]
    public void Two_Fingers_Between_The_Column_Choice_And_The_Column_Of_The_Compact_Layout_Zoom_The_Board()
    {
        var (window, _) = CreateBoard(new FlowTask { Title = "Task" });
        try
        {
            var board = (FlowKanban)window.Content!;
            board.BoardSize = DaisySize.Medium;
            board.SetCompactLayout(true);
            Flush(window);
            var choice = window.GetVisualDescendants().OfType<ComboBox>().Single(static box => box.Name == "PART_CompactColumnSelect");
            var column = window.GetVisualDescendants().OfType<ContentPresenter>().Single(static presenter => presenter.Name == "PART_CompactColumnPresenter");
            var choiceBottom = choice.TranslatePoint(new Point(0, choice.Bounds.Height), window)!.Value.Y;
            var columnTop = column.TranslatePoint(default, window)!.Value.Y;
            Assert.True(columnTop - choiceBottom >= 4, $"no room between the choice ({choiceBottom}) and the column ({columnTop})");
            var middle = choice.TranslatePoint(new Point(choice.Bounds.Width / 2, 0), window)!.Value.WithY((choiceBottom + columnTop) / 2);

            var first = window.TouchBegin(middle - new Vector(50, 0), RawInputModifiers.None);
            var second = window.TouchBegin(middle + new Vector(50, 0), RawInputModifiers.None);
            window.TouchMove(first, middle - new Vector(80, 0));
            window.TouchMove(second, middle + new Vector(80, 0));
            Flush(window);
            window.TouchEnd(first, middle - new Vector(80, 0), RawInputModifiers.None);
            window.TouchEnd(second, middle + new Vector(80, 0), RawInputModifiers.None);
            Flush(window);

            Assert.Equal(DaisySize.ExtraLarge, board.BoardSize);
        }
        finally
        {
            window.Close();
        }
    }

    [AvaloniaFact]
    public void The_Board_Does_Not_Scroll_While_Two_Fingers_Pinch()
    {
        var data = new FlowKanbanData();
        for (var index = 0; index < 8; index++)
            data.Columns.Add(new FlowKanbanColumnData { Title = $"Column {index}", Tasks = { new FlowTask { Title = $"Task {index}" } } });
        var board = new FlowKanban { AutoSaveAfterEdits = false, Board = data };
        var window = CreateWindow(board, 700);
        try
        {
            board.CurrentView = FlowKanbanView.Board;
            board.BoardSize = DaisySize.Medium;
            Flush(window);
            UseTouchSelection(window);
            var scroll = window.GetVisualDescendants().OfType<FlowKanbanColumnsHost>().First().FindAncestorOfType<ScrollViewer>()!;
            Assert.True(scroll.Extent.Width > scroll.Viewport.Width, "the columns are wider than the board");

            // The second finger swipes to the left, which would scroll the board to the right on its own.
            var host = window.GetVisualDescendants().OfType<Grid>().Single(static grid => grid.Name == "PART_BoardContentHost");
            var fixedPoint = host.TranslatePoint(new Point(host.Bounds.Width - 50, host.Bounds.Height - 100), window)!.Value;
            var moving = fixedPoint - new Vector(100, 0);
            var first = window.TouchBegin(fixedPoint, RawInputModifiers.None);
            var second = window.TouchBegin(moving, RawInputModifiers.None);
            for (var step = 1; step <= 8; step++)
            {
                window.TouchMove(second, moving - new Vector(step * 50, 0));
                Flush(window);
            }
            window.TouchEnd(second, moving - new Vector(400, 0), RawInputModifiers.None);
            window.TouchEnd(first, fixedPoint, RawInputModifiers.None);
            Flush(window);

            Assert.Equal(DaisySize.ExtraLarge, board.BoardSize);
            Assert.Equal(0, scroll.Offset.X);
        }
        finally
        {
            window.Close();
        }
    }

    // As on a phone, where every card takes touch as touch: a finger on a card in mouse mode starts a platform drag.
    private static void UseTouchSelection(Window window)
    {
        foreach (var card in window.GetVisualDescendants().OfType<FlowTaskCard>())
            card.UsesTouchSelection = true;
    }

    // Two fingers above and below the middle, the distance apart.
    private static (IHeadlessTouchPointer First, IHeadlessTouchPointer Second) PinchBegin(Window window, Point middle, double distance)
    {
        var first = window.TouchBegin(middle - new Vector(0, distance / 2), RawInputModifiers.None);
        var second = window.TouchBegin(middle + new Vector(0, distance / 2), RawInputModifiers.None);
        Flush(window);
        return (first, second);
    }

    private static void PinchMove(Window window, IHeadlessTouchPointer first, IHeadlessTouchPointer second, Point middle, double distance)
    {
        window.TouchMove(first, middle - new Vector(0, distance / 2));
        window.TouchMove(second, middle + new Vector(0, distance / 2));
        Flush(window);
    }

    private static void PinchEnd(Window window, IHeadlessTouchPointer first, IHeadlessTouchPointer second, Point middle, double distance)
    {
        window.TouchEnd(first, middle - new Vector(0, distance / 2), RawInputModifiers.None);
        window.TouchEnd(second, middle + new Vector(0, distance / 2), RawInputModifiers.None);
        Flush(window);
    }

    private static void WaitLongerThanAHold(Window window)
    {
        Thread.Sleep(Application.Current!.PlatformSettings!.HoldWaitDuration + TimeSpan.FromMilliseconds(150));
        // The dispatcher looks for due timers only after it ran a job.
        Dispatcher.UIThread.Post(static () => { });
        Flush(window);
        Flush(window);
    }
}
