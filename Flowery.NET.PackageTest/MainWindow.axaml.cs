using System.Reflection;
using Avalonia.Controls;
using Flowery.Controls;
using Flowery.NET.Kanban.Controls;
using Flowery.Skia;

namespace Flowery.NET.PackageTest;

public partial class MainWindow : Window
{
    /// <summary>
    /// The title of the card on the board of the Kanban.
    /// </summary>
    internal const string TaskTitle = "Check the Kanban package";

    /// <summary>
    /// The error of saving the board of the Kanban, or null when the board was saved.
    /// </summary>
    internal Exception? BoardSaveError { get; }

    public MainWindow()
    {
        InitializeComponent();
        Packages.Text = string.Join('\n',
            Describe(typeof(DaisyGlass).Assembly),
            Describe(typeof(FlowerySkia).Assembly),
            Describe(typeof(FlowKanban).Assembly));

        // A Kanban without saved boards opens on its home screen, so the board is saved before the Kanban loads.
        var board = CreateBoard();
        if (!Kanban.BoardStore.TrySaveBoard(board, out var error))
        {
            BoardSaveError = error;
            Packages.Text += $"\nThe Kanban could not save its board: {error?.Message}";
        }
        Kanban.Board = board;
    }

    /// <summary>
    /// Gets the name of an assembly with its package version and the commit it was built from.
    /// </summary>
    internal static string Describe(Assembly assembly)
    {
        var version = assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        return $"{assembly.GetName().Name} {version}";
    }

    private static FlowKanbanData CreateBoard()
    {
        var todo = new FlowKanbanColumnData { Title = "To Do" };
        todo.Tasks.Add(new FlowTask { Title = TaskTitle });
        var board = new FlowKanbanData { Title = "Package test" };
        board.Columns.Add(todo);
        board.Columns.Add(new FlowKanbanColumnData { Title = "Done" });
        return board;
    }
}
