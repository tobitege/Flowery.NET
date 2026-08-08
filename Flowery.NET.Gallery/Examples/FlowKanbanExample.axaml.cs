using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Flowery.Controls;
using Flowery.NET.Kanban.Controls;
using Flowery.Services;

namespace Flowery.NET.Gallery.Examples;

public partial class FlowKanbanExample : UserControl
{
    private readonly FlowKanbanAssigneeAdapter _assigneeAdapter;
    private FlowKanbanManager? _kanbanManager;

    public FlowKanbanExample()
    {
        InitializeComponent();

        _assigneeAdapter = new FlowKanbanAssigneeAdapter(CreateAssignees());

        DemoKanban.AssigneeAdapter = _assigneeAdapter;
        DemoKanban.EditCardCommand = new SimpleCommand(OnEditCardRequested);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_kanbanManager is not null)
        {
            return;
        }

        _kanbanManager = new FlowKanbanManager(DemoKanban, autoAttach: false);
        _kanbanManager.PersistenceFailed += OnPersistenceFailed;
        if (!_kanbanManager.Initialize())
        {
            DemoKanban.Board = CreateSampleBoard();
        }

        Dispatcher.UIThread.Post(() =>
        {
            if (_kanbanManager is not null && DemoKanban.IsLoaded)
            {
                DemoKanban.CurrentView = FlowKanbanView.Board;
            }
        }, DispatcherPriority.Loaded);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        if (_kanbanManager is null)
        {
            return;
        }

        _kanbanManager.PersistenceFailed -= OnPersistenceFailed;
        _kanbanManager.Shutdown();
        _kanbanManager = null;
    }

    private static void OnPersistenceFailed(object? sender, FlowKanbanPersistenceFailedEventArgs e)
    {
        System.Diagnostics.Debug.WriteLine(
            $"Kanban persistence {e.Operation} failed: {e.Exception.Message}");
    }

    private async void OnEditCardRequested(object? parameter)
    {
        if (parameter is not FlowTask task || TopLevel.GetTopLevel(DemoKanban) is not { } topLevel)
        {
            return;
        }

        var assignees = await GetAssigneeOptionsAsync(_assigneeAdapter);
        await FlowTaskEditorDialog.ShowAsyncWithAssignees(
            task,
            topLevel,
            DemoKanban.Board.Tags,
            assignees,
            DemoKanban.AutoExpandCardDetails);
    }

    private static async Task<IReadOnlyList<FlowTaskAssigneeOption>> GetAssigneeOptionsAsync(
        IFlowKanbanAssigneeAdapter adapter)
    {
        var assignees = await adapter.GetAssigneesAsync();
        return assignees
            .Select(assignee => new FlowTaskAssigneeOption(assignee.Id, assignee.DisplayName))
            .OrderBy(option => option.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<FlowKanbanAssignee> CreateAssignees()
    {
        string[] names = ["Sam", "Dario", "Max", "Demis", "Adam", "Lucy", "Anita", "Sue", "Eric", "Forrest"];
        return names.Select((name, index) =>
        {
            string[] roles = index switch
            {
                0 => ["Administrator"],
                1 => ["Product owner"],
                _ => ["Contributor"]
            };
            return new FlowKanbanAssignee(
                $"demo-{name.ToLowerInvariant()}",
                name,
                roles: roles);
        }).ToArray();
    }

    private static FlowKanbanData CreateSampleBoard()
    {
        var interactiveTask = CreateTask(
            "Make content sections more interactive",
            "Add extra visual elements to remove big walls of text\n\nhigh priority",
            DaisyColor.Primary);
        interactiveTask.Subtasks.Add(new FlowSubtask
        {
            Title = "Add cards explaining Kanban concept",
            IsCompleted = true
        });
        interactiveTask.Subtasks.Add(new FlowSubtask { Title = "Add visual roadmap" });
        interactiveTask.Subtasks.Add(new FlowSubtask { Title = "Add images" });

        var coffeeTask = CreateTask("Buy more coffee", "Completed: 1/3");
        coffeeTask.Subtasks.Add(new FlowSubtask { Title = "Order beans online", IsCompleted = true });
        coffeeTask.Subtasks.Add(new FlowSubtask { Title = "Pick up from store" });
        coffeeTask.Subtasks.Add(new FlowSubtask { Title = "Try new roast" });

        return new FlowKanbanData
        {
            Columns =
            {
                CreateColumn(
                    "Todo",
                    interactiveTask,
                    CreateTask("Some other todo", "Additional task details here"),
                    CreateTask("Another todo", "Important pending item", DaisyColor.Secondary),
                    CreateTask("One more", "Final todo item")),
                CreateColumn(
                    "In Progress",
                    CreateTask(
                        "Make content sections more interactive",
                        "Subtask: 1/3 complete",
                        DaisyColor.Error)),
                CreateColumn(
                    "Done",
                    coffeeTask,
                    CreateTask("Published repo", "Successfully published", DaisyColor.Success))
            }
        };
    }

    private static FlowKanbanColumnData CreateColumn(string title, params FlowTask[] tasks)
    {
        var column = new FlowKanbanColumnData { Title = title };
        foreach (var task in tasks)
        {
            column.Tasks.Add(task);
        }

        return column;
    }

    private static FlowTask CreateTask(
        string title,
        string description,
        DaisyColor palette = DaisyColor.Default) =>
        new()
        {
            Title = title,
            Description = description,
            Palette = palette
        };
}
