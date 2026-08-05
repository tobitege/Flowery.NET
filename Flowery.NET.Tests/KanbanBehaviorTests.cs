using System.Text.Json;
using System.Text.Json.Nodes;
using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Layout;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Kanban.Controls;
using Flowery.NET.Kanban.Controls.Users;
using Flowery.NET.Kanban.Interfaces;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests
{
    public class KanbanBehaviorTests
    {
        [AvaloniaFact]
        public void When_Constructing_Kanban_Defaults_AreSet()
        {
            var kanban = new FlowKanban();

            Assert.NotNull(kanban.Board);
            Assert.Equal(DaisySize.Medium, kanban.BoardSize);
            Assert.Equal(string.Empty, kanban.SearchText);
            Assert.True(kanban.IsStatusBarVisible);
            Assert.True(kanban.ConfirmColumnRemovals);
            Assert.True(kanban.ConfirmCardRemovals);
            Assert.True(kanban.AutoSaveAfterEdits);
            Assert.True(kanban.AutoExpandCardDetails);
            Assert.False(kanban.EnableUndoRedo);
            Assert.Equal(FlowKanbanView.Board, kanban.CurrentView);
        }

        [Fact]
        public void When_InspectingKanbanContainerBase_ButtonContractIsAbsent()
        {
            var controlType = typeof(FlowKanbanContentControl);

            Assert.Null(controlType.GetProperty("Command"));
            Assert.Null(controlType.GetProperty("CommandParameter"));
            Assert.Null(controlType.GetEvent("Click"));
            Assert.Null(controlType.GetField("CommandProperty"));
            Assert.Null(controlType.GetField("CommandParameterProperty"));
        }

        [AvaloniaFact]
        public void When_Constructing_MultipleKanbans_DefaultBoardsAreIsolated()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());

            try
            {
                var first = new FlowKanban { AutoSaveAfterEdits = false };
                var second = new FlowKanban { AutoSaveAfterEdits = false };
                var firstBoardEdits = 0;
                var secondBoardEdits = 0;
                var firstColumnChanges = 0;
                var secondColumnChanges = 0;
                first.BoardEdited += (_, _) => firstBoardEdits++;
                second.BoardEdited += (_, _) => secondBoardEdits++;
                first.Board.Columns.CollectionChanged += (_, _) => firstColumnChanges++;
                second.Board.Columns.CollectionChanged += (_, _) => secondColumnChanges++;

                Assert.Throws<ArgumentNullException>(() => first.Board = null!);
                first.Board.Columns.Add(CreateColumn("Only first"));
                first.Board.Title = "First board";

                Assert.NotSame(first.Board, second.Board);
                Assert.NotSame(first.Board.Columns, second.Board.Columns);
                Assert.Single(first.Board.Columns);
                Assert.Empty(second.Board.Columns);
                Assert.Equal(1, firstColumnChanges);
                Assert.Equal(0, secondColumnChanges);
                Assert.Equal(1, firstBoardEdits);
                Assert.Equal(0, secondBoardEdits);
            }
            finally
            {
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public void When_ColumnCollapses_ColumnsHostReleasesItsExpandedWidth()
        {
            var columns = new[]
            {
                CreateColumn("Todo"),
                CreateColumn("In Progress")
            };
            var host = new FlowKanbanColumnsHost
            {
                ColumnSpacing = 8,
                ItemsSource = columns,
                Template = new FuncControlTemplate<FlowKanbanColumnsHost>(
                    (control, _) => new ItemsPresenter { ItemsPanel = control.ItemsPanel }),
                ItemTemplate = new FuncDataTemplate<FlowKanbanColumnData>(
                    (column, _) => new FlowKanbanColumn { ColumnData = column },
                    supportsRecycling: true)
            };
            var window = new Window
            {
                Width = 800,
                Height = 400,
                Content = host
            };
            window.Show();
            host.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            try
            {
                var itemsPanel = host.GetVisualDescendants()
                    .OfType<StackPanel>()
                    .Single(panel => panel.Children.Count == columns.Length
                                     && panel.Children.All(child => child is ContentPresenter));
                var containers = itemsPanel.Children.ToList();
                var realizedColumns = host.GetVisualDescendants()
                    .OfType<FlowKanbanColumn>()
                    .ToList();

                Assert.Equal(2, containers.Count);
                Assert.Equal(2, realizedColumns.Count);
                var expandedSecondColumnX = GetPositionX(containers[1], host);

                columns[0].IsCollapsed = true;
                Dispatcher.UIThread.RunJobs();
                host.UpdateLayout();

                var collapsedSecondColumnX = GetPositionX(containers[1], host);
                Assert.Equal(realizedColumns[0].Bounds.Width, containers[0].Bounds.Width, precision: 3);
                Assert.Equal(
                    containers[0].Bounds.Width + host.ColumnSpacing,
                    collapsedSecondColumnX,
                    precision: 3);
                Assert.True(collapsedSecondColumnX < expandedSecondColumnX);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void When_SearchText_Changes_TaskMatchesUpdate()
        {
            var matchingTask = new FlowTask
            {
                Title = "Fix bug",
                Description = "urgent issue",
                Tags = "urgent"
            };
            var otherTask = new FlowTask
            {
                Title = "Write docs",
                Description = "guides",
                Tags = "documentation"
            };
            var kanban = CreateKanban(CreateColumn("Backlog", matchingTask, otherTask));
            var window = ShowKanban(kanban);

            try
            {
                kanban.SearchText = "bug";
                Dispatcher.UIThread.RunJobs();

                Assert.True(matchingTask.IsSearchMatch);
                Assert.False(otherTask.IsSearchMatch);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public async Task When_ReplacingColumns_OnlyNewCollectionUpdatesUiSearchAndAutoSave()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var oldColumns = new ObservableCollection<FlowKanbanColumnData>
            {
                CreateColumn("Old")
            };
            var board = new FlowKanbanData { Columns = oldColumns };
            var store = new CountingBoardStore();
            var kanban = new FlowKanban
            {
                Board = board,
                BoardStore = store,
                SearchText = "needle"
            };
            var window = ShowKanban(kanban);

            try
            {
                await kanban.LoadUserSettingsAsync(forceReload: true);
                kanban.CurrentView = FlowKanbanView.Board;
                kanban.SetCompactLayout(false);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                var newColumns = new ObservableCollection<FlowKanbanColumnData>();
                board.Columns = newColumns;
                await WaitForAutoSaveAsync();
                store.Reset();

                var detachedTask = CreateTask("detached");
                oldColumns.Add(CreateColumn("Detached", detachedTask));
                await WaitForAutoSaveAsync();

                Assert.Equal(0, store.SaveCount);
                Assert.True(detachedTask.IsSearchMatch);
                Assert.Same(newColumns, kanban.StandardColumnsSource);
                Assert.Empty(newColumns);

                var currentTask = CreateTask("current");
                var currentColumn = CreateColumn("Current", currentTask);
                newColumns.Add(currentColumn);
                await WaitForAutoSaveAsync();

                Assert.Equal(1, store.SaveCount);
                Assert.False(currentTask.IsSearchMatch);
                Assert.Contains(currentColumn, kanban.StandardColumnsSource!);
            }
            finally
            {
                window.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_ReplacingTasks_OnlyNewCollectionUpdatesSearchMetricsWipAndAutoSave()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var column = CreateColumn("Todo");
            var oldTasks = column.Tasks;
            var store = new CountingBoardStore();
            var kanban = new FlowKanban
            {
                Board = CreateBoard(column),
                BoardStore = store,
                SearchText = "needle"
            };
            var window = ShowKanban(kanban);

            try
            {
                var newTasks = new ObservableCollection<FlowTask>();
                column.Tasks = newTasks;
                await WaitForAutoSaveAsync();
                store.Reset();

                var detachedTask = new FlowTask { Title = "detached", IsSelected = true };
                oldTasks.Add(detachedTask);
                await WaitForAutoSaveAsync();

                Assert.Equal(0, store.SaveCount);
                Assert.True(detachedTask.IsSearchMatch);
                Assert.Equal(0, kanban.SelectedCount);
                Assert.Equal("0", column.WipDisplay);

                var currentTask = new FlowTask { Title = "current", IsSelected = true };
                newTasks.Add(currentTask);
                await WaitForAutoSaveAsync();

                Assert.Equal(1, store.SaveCount);
                Assert.False(currentTask.IsSearchMatch);
                Assert.Equal(1, kanban.SelectedCount);
                Assert.Equal("1", column.WipDisplay);
            }
            finally
            {
                window.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public void When_HomeSearchFiltersBoards_ContainerCleanupDoesNotRequireNameScope()
        {
            var home = new FlowKanbanHome();
            home.Boards.Add(new FlowBoardMetadata
            {
                Id = "board-1",
                Title = "Existing Board",
                LastModified = DateTime.UtcNow
            });
            var window = new Window
            {
                Width = 800,
                Height = 600,
                Content = home
            };
            window.Show();
            home.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            try
            {
                var exception = Record.Exception(() =>
                {
                    home.SearchText = "no matching board";
                    Dispatcher.UIThread.RunJobs();
                });

                Assert.Null(exception);
                Assert.Empty(home.FilteredBoards);
                Assert.True(home.IsEmptyStateVisible);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void When_KanbanTemplateRealizes_ToolButtonsHaveNamesAndStableIds()
        {
            var kanban = CreateKanban(CreateColumn("Todo", CreateTask("Accessible card")));
            var window = ShowKanban(kanban);

            try
            {
                var expectedIds = new[]
                {
                    "Kanban_Home",
                    "Kanban_Users",
                    "Kanban_Settings",
                    "Kanban_Undo",
                    "Kanban_Redo",
                    "Kanban_ZoomIn",
                    "Kanban_ZoomOut",
                    "Kanban_KeyboardHelp",
                    "Kanban_Statistics",
                    "Kanban_ToggleStatusBar",
                    "Kanban_BoardMenu"
                };

                foreach (var automationId in expectedIds)
                {
                    var control = GetAutomationControl(kanban, automationId);
                    var name = AutomationProperties.GetName(control);

                    Assert.False(string.IsNullOrWhiteSpace(name));
                    Assert.False(name?.StartsWith("Kanban_", StringComparison.Ordinal) ?? true);
                }
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void When_BoardActionMenuRealizes_ItemsExposeNamesAndInvokeCommands()
        {
            var kanban = CreateKanban(CreateColumn("Todo", CreateTask("Accessible card")));
            var window = ShowKanban(kanban);

            try
            {
                var boardMenu = Assert.IsType<DaisyButton>(
                    GetAutomationControl(kanban, "Kanban_BoardMenu"));
                var boardMenuPeer = Assert.IsAssignableFrom<AutomationPeer>(
                    ControlAutomationPeer.CreatePeerForElement(boardMenu));
                Assert.IsAssignableFrom<IInvokeProvider>(boardMenuPeer).Invoke();
                Dispatcher.UIThread.RunJobs();

                var popover = boardMenu.GetVisualAncestors()
                    .OfType<DaisyPopover>()
                    .Single();
                var menu = Assert.IsType<DaisyMenu>(popover.PopoverContent);
                var menuItems = menu.Items.OfType<FlowKanbanActionMenuItem>().ToList();

                Assert.Equal(4, menuItems.Count);
                foreach (var menuItem in menuItems)
                {
                    var name = AutomationProperties.GetName(menuItem);
                    var peer = Assert.IsAssignableFrom<AutomationPeer>(
                        ControlAutomationPeer.CreatePeerForElement(menuItem));

                    Assert.False(string.IsNullOrWhiteSpace(name));
                    Assert.Equal(AutomationControlType.MenuItem, peer.GetAutomationControlType());
                    Assert.IsAssignableFrom<IInvokeProvider>(peer);
                }

                var commandInvoked = false;
                var firstItem = menuItems[0];
                firstItem.Command = new RelayCommand(() => commandInvoked = true);
                var firstItemPeer = Assert.IsAssignableFrom<AutomationPeer>(
                    ControlAutomationPeer.CreatePeerForElement(firstItem));

                Assert.IsAssignableFrom<IInvokeProvider>(firstItemPeer).Invoke();
                Assert.True(commandInvoked);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void When_KanbanChromeRealizes_ToolButtonsShareUnoMetricsAndCenterIcons()
        {
            var kanban = CreateKanban(CreateColumn("Todo", CreateTask("Measured card")));
            var window = ShowKanban(kanban);

            try
            {
                kanban.EnableUndoRedo = true;
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                var requiredSidebarIds = new[]
                {
                    "Kanban_Home",
                    "Kanban_Settings",
                    "Kanban_Undo",
                    "Kanban_Redo",
                    "Kanban_ZoomIn",
                    "Kanban_ZoomOut",
                    "Kanban_Statistics",
                    "Kanban_ToggleStatusBar"
                };
                var requiredSidebarButtons = requiredSidebarIds
                    .Select(automationId => Assert.IsType<DaisyButton>(
                        GetAutomationControl(kanban, automationId)))
                    .ToList();
                var optionalSidebarButtons = new[] { "Kanban_Users", "Kanban_KeyboardHelp" }
                    .Select(automationId => Assert.IsType<DaisyButton>(
                        GetAutomationControl(kanban, automationId)))
                    .Where(button => button.IsEffectivelyVisible)
                    .ToList();
                var sidebarButtons = requiredSidebarButtons
                    .Concat(optionalSidebarButtons)
                    .ToList();
                var expectedSidebarSide = LayoutTestAssertions.GetUnoButtonHeight(DaisySize.Small);
                var expectedSidebarX = LayoutTestAssertions.GetPosition(sidebarButtons[0], kanban).X;

                Assert.All(requiredSidebarButtons, button => Assert.True(button.IsEffectivelyVisible));
                foreach (var button in sidebarButtons)
                {
                    LayoutTestAssertions.HasSize(button, expectedSidebarSide, expectedSidebarSide);
                    Assert.Equal(
                        expectedSidebarX,
                        LayoutTestAssertions.GetPosition(button, kanban).X,
                        precision: 3);
                    var icon = button.GetVisualDescendants()
                        .OfType<Viewbox>()
                        .Single(control => string.Equals(
                            control.Name,
                            "PART_IconViewbox",
                            StringComparison.Ordinal));
                    LayoutTestAssertions.IsCentered(button, icon);
                }

                var boardMenu = Assert.IsType<DaisyButton>(GetAutomationControl(kanban, "Kanban_BoardMenu"));
                var expectedMenuSide = LayoutTestAssertions.GetUnoButtonHeight(DaisySize.Medium);
                LayoutTestAssertions.HasSize(boardMenu, expectedMenuSide, expectedMenuSide);
                LayoutTestAssertions.IsCentered(
                    boardMenu,
                    boardMenu.GetVisualDescendants()
                        .OfType<Viewbox>()
                        .Single(control => string.Equals(
                            control.Name,
                            "PART_IconViewbox",
                            StringComparison.Ordinal)));

                var archiveToggle = kanban.GetVisualDescendants()
                    .OfType<DaisyButton>()
                    .Single(button => button.IsEffectivelyVisible
                                      && ReferenceEquals(
                                          button.Command,
                                          kanban.ToggleArchiveColumnVisibilityCommand));
                var archiveContent = Assert.IsType<DaisyIconText>(archiveToggle.Content);
                AssertIconTextVisibleContentVerticallyCentered(archiveToggle, archiveContent);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public async Task When_SharedColumnSurfaceRealizes_AllLayoutsKeepMetricsAndGeometry()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            Window? window = null;

            try
            {
                var lane = new FlowKanbanLane { Id = "lane-1", Title = "Lane" };
                var todo = CreateColumn(
                    "Todo",
                    new FlowTask { Title = "A", LaneId = lane.Id });
                var doing = CreateColumn(
                    "Doing",
                    new FlowTask { Title = "B", LaneId = lane.Id },
                    new FlowTask { Title = "C", LaneId = lane.Id });
                doing.WipLimit = 1;
                var board = CreateBoard(todo, doing);
                board.Lanes.Add(lane);
                var kanban = new FlowKanban
                {
                    Board = board,
                    ColumnWidth = 320,
                    AutoSaveAfterEdits = false
                };
                kanban.SetCompactLayout(false);
                window = ShowKanban(kanban);
                await kanban.LoadUserSettingsAsync(forceReload: true);
                kanban.CurrentView = FlowKanbanView.Board;
                kanban.SetCompactLayout(false);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                Assert.True(kanban.IsBoardViewActive);
                Assert.True(kanban.IsStandardLayoutEnabled);
                Assert.True(kanban.IsStandardLayoutVisible);
                Assert.Same(board.Columns, kanban.StandardColumnsSource);
                Assert.All(board.Columns, column => Assert.True(column.IsArchiveColumnVisible));

                var standardColumns = GetVisibleColumns(kanban)
                    .Where(column => column.ShowColumnHeader && column.ShowTasks)
                    .OrderBy(column => LayoutTestAssertions.GetPosition(column, kanban).X)
                    .ToList();
                Assert.Equal(2, standardColumns.Count);
                AssertColumnHeaderButtonMetrics(standardColumns[0], expectCollapseButton: true);
                Assert.All(standardColumns, column =>
                    Assert.Equal(kanban.ColumnWidth, column.Bounds.Width, precision: 3));
                Assert.All(standardColumns, column =>
                    AssertLaneWipIndicatorVisibility(column, expectedVisibility: false));

                var standardHost = kanban.GetVisualDescendants()
                    .OfType<FlowKanbanColumnsHost>()
                    .Single(host => string.Equals(host.Name, "PART_ColumnsItemsControl", StringComparison.Ordinal));
                var standardItemsPanel = Assert.IsType<StackPanel>(standardHost.ItemsPanelRoot);
                Assert.Equal(8, standardItemsPanel.Spacing);

                standardHost.ColumnSpacing = 12;
                for (var iteration = 0; iteration < 5; iteration++)
                {
                    Dispatcher.UIThread.RunJobs();
                    kanban.UpdateLayout();
                }

                Assert.Same(standardItemsPanel, standardHost.ItemsPanelRoot);
                Assert.Equal(12, standardItemsPanel.Spacing);
                var expandedDistance = LayoutTestAssertions.GetPosition(standardColumns[1], standardHost).X
                                       - LayoutTestAssertions.GetPosition(standardColumns[0], standardHost).X;
                Assert.Equal(
                    standardColumns[0].Bounds.Width
                    + standardColumns[0].Margin.Left
                    + standardColumns[0].Margin.Right
                    + standardHost.ColumnSpacing,
                    expandedDistance,
                    precision: 3);

                board.Columns[0].IsCollapsed = true;
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                var collapsedDistance = LayoutTestAssertions.GetPosition(standardColumns[1], standardHost).X
                                        - LayoutTestAssertions.GetPosition(standardColumns[0], standardHost).X;
                Assert.Equal(
                    standardColumns[0].Bounds.Width
                    + standardColumns[0].Margin.Left
                    + standardColumns[0].Margin.Right
                    + standardHost.ColumnSpacing,
                    collapsedDistance,
                    precision: 3);
                Assert.True(collapsedDistance < expandedDistance);
                board.Columns[0].IsCollapsed = false;

                kanban.SetCompactLayout(true);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                var compactColumn = GetVisibleColumns(kanban)
                    .Single(column => column.ShowColumnHeader && column.ShowTasks);
                AssertColumnHeaderButtonMetrics(compactColumn, expectCollapseButton: false);
                AssertLaneWipIndicatorVisibility(compactColumn, expectedVisibility: false);

                board.GroupBy = FlowKanbanGroupBy.Lane;
                kanban.SetCompactLayout(false);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                var swimlaneHeader = GetVisibleColumns(kanban)
                    .First(column => column.ShowColumnHeader && !column.ShowTasks);
                AssertColumnHeaderButtonMetrics(swimlaneHeader, expectCollapseButton: true);
                AssertLaneWipIndicatorVisibility(swimlaneHeader, expectedVisibility: false);
                var swimlaneCells = GetVisibleColumns(kanban)
                    .Where(column => !column.ShowColumnHeader && column.ShowTasks)
                    .ToList();
                var swimlaneCell = swimlaneCells.First();
                AssertLaneWipIndicatorVisibility(
                    swimlaneCells.Single(column => ReferenceEquals(column.ColumnData, todo)),
                    expectedVisibility: false);
                AssertLaneWipIndicatorVisibility(
                    swimlaneCells.Single(column => ReferenceEquals(column.ColumnData, doing)),
                    expectedVisibility: true);
                Assert.Equal(kanban.SwimlaneCellMaxHeight, swimlaneCell.TaskListMaxHeight, precision: 3);
                Assert.Single(
                    swimlaneCell.GetVisualDescendants().OfType<ListBox>(),
                    list => string.Equals(list.Name, "PART_TasksItemsControl", StringComparison.Ordinal));
            }
            finally
            {
                window?.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_CollapsedColumnIsResized_ByPointerAndKeyboard_GeometryRemainsConsistent()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            Window? window = null;

            try
            {
                var board = CreateBoard(
                    CreateColumn("Todo", CreateTask("A")),
                    CreateColumn("Doing", CreateTask("B")),
                    CreateColumn("Done", CreateTask("C")));
                var kanban = new FlowKanban
                {
                    Board = board,
                    ColumnWidth = 240,
                    IsColumnResizeEnabled = true,
                    AutoSaveAfterEdits = false
                };
                kanban.SetCompactLayout(false);
                window = ShowKanban(kanban);
                await kanban.LoadUserSettingsAsync(forceReload: true);
                kanban.CurrentView = FlowKanbanView.Board;
                kanban.SetCompactLayout(false);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                var host = kanban.GetVisualDescendants()
                    .OfType<FlowKanbanColumnsHost>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_ColumnsItemsControl",
                        StringComparison.Ordinal));
                var columns = GetOrderedBoardColumns(kanban, host);
                Assert.Equal(3, columns.Count);
                var itemsPanel = Assert.IsType<StackPanel>(host.ItemsPanelRoot);
                var containers = itemsPanel.Children.OfType<Control>().ToList();
                Assert.Equal(3, containers.Count);
                Assert.True(host.IsGripEnabled);
                board.Columns[0].IsCollapsed = true;
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                Assert.True(columns[0].Bounds.Width < kanban.ColumnWidth);
                Assert.Equal(kanban.ColumnWidth, columns[1].Bounds.Width, precision: 3);
                Assert.Equal(kanban.ColumnWidth, columns[2].Bounds.Width, precision: 3);
                var initialWidth = kanban.ColumnWidth;
                var initialOccupiedWidth = GetOccupiedWidth(containers, host);
                var initialFirstGap = GetGapCenter(containers, 0, host);
                var initialSecondGap = GetGapCenter(containers, 1, host);
                var hostPosition = LayoutTestAssertions.GetPosition(host, window);
                var firstContainerPosition = LayoutTestAssertions.GetPosition(containers[0], host);
                var dragStart = new Point(
                    hostPosition.X + initialFirstGap - host.ColumnSpacing / 2 - 8,
                    hostPosition.Y + firstContainerPosition.Y
                        + Math.Min(50, containers[0].Bounds.Height / 2));
                var dragEnd = new Point(dragStart.X + 40, dragStart.Y);

                window.MouseMove(dragStart, RawInputModifiers.None);
                window.MouseDown(dragStart, MouseButton.Left, RawInputModifiers.None);
                window.MouseMove(dragEnd, RawInputModifiers.LeftMouseButton);
                window.MouseUp(dragEnd, MouseButton.Left, RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                Assert.Equal(initialWidth + 40, kanban.ColumnWidth, precision: 3);
                Assert.Equal(kanban.ColumnWidth, columns[1].Bounds.Width, precision: 3);
                Assert.Equal(kanban.ColumnWidth, columns[2].Bounds.Width, precision: 3);
                Assert.Equal(
                    initialOccupiedWidth + 80,
                    GetOccupiedWidth(containers, host),
                    precision: 3);
                Assert.Equal(initialFirstGap, GetGapCenter(containers, 0, host), precision: 3);
                Assert.Equal(initialSecondGap + 40, GetGapCenter(containers, 1, host), precision: 3);

                Assert.True(host.Focus());
                window.KeyPress(
                    Key.Right,
                    RawInputModifiers.None,
                    PhysicalKey.ArrowRight,
                    keySymbol: null);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                Assert.Equal(initialWidth + 48, kanban.ColumnWidth, precision: 3);
                Assert.Equal(
                    initialOccupiedWidth + 96,
                    GetOccupiedWidth(containers, host),
                    precision: 3);
                Assert.Equal(initialFirstGap, GetGapCenter(containers, 0, host), precision: 3);
                Assert.Equal(initialSecondGap + 48, GetGapCenter(containers, 1, host), precision: 3);
            }
            finally
            {
                window?.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_ColumnsHostAutomationPeerIsQueried_RangeSemanticsAreExposed()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            Window? window = null;

            try
            {
                var kanban = new FlowKanban
                {
                    Board = CreateBoard(
                        CreateColumn("Todo", new FlowTask { Title = "A" }),
                        CreateColumn("Doing", new FlowTask { Title = "B" })),
                    ColumnWidth = 320,
                    MinColumnWidth = 180,
                    MaxColumnWidth = 640,
                    IsColumnResizeEnabled = true,
                    AutoSaveAfterEdits = false
                };
                kanban.SetCompactLayout(false);
                window = ShowKanban(kanban);
                await kanban.LoadUserSettingsAsync(forceReload: true);
                kanban.CurrentView = FlowKanbanView.Board;
                kanban.SetCompactLayout(false);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                var host = kanban.GetVisualDescendants()
                    .OfType<FlowKanbanColumnsHost>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_ColumnsItemsControl",
                        StringComparison.Ordinal));
                var peer = ControlAutomationPeer.CreatePeerForElement(host);
                var rangeProvider = Assert.IsAssignableFrom<IRangeValueProvider>(peer);

                Assert.Equal(AutomationControlType.Slider, peer.GetAutomationControlType());
                Assert.True(peer.IsControlElement());
                Assert.Equal(kanban.Localization["Kanban_Column_ResizeHelp"], peer.GetHelpText());
                Assert.Equal(180, rangeProvider.Minimum);
                Assert.Equal(640, rangeProvider.Maximum);
                Assert.Equal(320, rangeProvider.Value);
                Assert.Equal(8, rangeProvider.SmallChange);
                Assert.Equal(32, rangeProvider.LargeChange);
                Assert.False(rangeProvider.IsReadOnly);

                rangeProvider.SetValue(400);
                Dispatcher.UIThread.RunJobs();
                Assert.Equal(400, rangeProvider.Value);
                Assert.Equal(400, kanban.ColumnWidth);

                host.IsGripEnabled = false;
                Assert.True(rangeProvider.IsReadOnly);
                Assert.Throws<InvalidOperationException>(() => rangeProvider.SetValue(420));

                kanban.SetCompactLayout(true);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                Assert.False(peer.IsControlElement());
                Assert.DoesNotContain(
                    kanban.GetVisualDescendants().OfType<FlowKanbanColumnsHost>(),
                    control => control.IsEffectivelyVisible);
            }
            finally
            {
                window?.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_CardAndColumnAutomationPeersAreQueried_ActionsAndVisibilityAreSemantic()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            Window? window = null;

            try
            {
                var task = new FlowTask { Title = "A" };
                var board = CreateBoard(CreateColumn("Todo", task));
                var kanban = new FlowKanban
                {
                    Board = board,
                    AutoSaveAfterEdits = false
                };
                kanban.SetCompactLayout(false);
                window = ShowKanban(kanban);
                await kanban.LoadUserSettingsAsync(forceReload: true);
                kanban.CurrentView = FlowKanbanView.Board;
                kanban.SetCompactLayout(false);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                var standardColumn = GetVisibleColumns(kanban)
                    .Single(column => column.ShowColumnHeader && column.ShowTasks);
                var standardCard = standardColumn.GetVisualDescendants()
                    .OfType<FlowTaskCard>()
                    .Single();
                var columnPeer = ControlAutomationPeer.CreatePeerForElement(standardColumn);
                var expandCollapseProvider = Assert.IsAssignableFrom<IExpandCollapseProvider>(columnPeer);
                var cardPeer = ControlAutomationPeer.CreatePeerForElement(standardCard);
                var invokeProvider = Assert.IsAssignableFrom<IInvokeProvider>(cardPeer);
                FlowTask? invokedTask = null;
                standardCard.SetCurrentValue(
                    FlowTaskCard.EditCommandProperty,
                    new RelayCommand<FlowTask>(editedTask => invokedTask = editedTask));

                Assert.Equal(AutomationControlType.Group, columnPeer.GetAutomationControlType());
                Assert.True(columnPeer.IsControlElement());
                Assert.Equal(ExpandCollapseState.Expanded, expandCollapseProvider.ExpandCollapseState);
                Assert.False(expandCollapseProvider.ShowsMenu);
                Assert.Equal(AutomationControlType.ListItem, cardPeer.GetAutomationControlType());
                Assert.True(cardPeer.IsControlElement());

                invokeProvider.Invoke();
                Assert.Same(task, invokedTask);

                expandCollapseProvider.Collapse();
                Dispatcher.UIThread.RunJobs();
                Assert.True(board.Columns[0].IsCollapsed);
                Assert.Equal(ExpandCollapseState.Collapsed, expandCollapseProvider.ExpandCollapseState);

                expandCollapseProvider.Expand();
                Dispatcher.UIThread.RunJobs();
                Assert.False(board.Columns[0].IsCollapsed);
                Assert.Equal(ExpandCollapseState.Expanded, expandCollapseProvider.ExpandCollapseState);

                kanban.SetCompactLayout(true);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                Assert.False(columnPeer.IsControlElement());
                Assert.False(cardPeer.IsControlElement());

                var compactColumn = GetVisibleColumns(kanban)
                    .Single(column => column.ShowColumnHeader && column.ShowTasks);
                var compactPeer = ControlAutomationPeer.CreatePeerForElement(compactColumn);
                Assert.True(compactPeer.IsControlElement());
                Assert.False(compactPeer is IExpandCollapseProvider);
            }
            finally
            {
                window?.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public void When_KanbanHomeRealizes_TextButtonsMatchUnoSmallMetrics()
        {
            var home = new FlowKanbanHome();
            var window = new Window
            {
                Width = 800,
                Height = 600,
                Content = home
            };
            window.Show();
            home.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            try
            {
                var textButtons = home.GetVisualDescendants()
                    .OfType<DaisyButton>()
                    .Where(button =>
                        button.Bounds.Width > 0 &&
                        button.Size == DaisySize.Small &&
                        button.Shape == DaisyButtonShape.Default &&
                        button.Content is string)
                    .ToList();

                Assert.Equal(2, textButtons.Count);
                foreach (var button in textButtons)
                {
                    Assert.Equal(
                        LayoutTestAssertions.GetUnoButtonHeight(DaisySize.Small),
                        button.Bounds.Height,
                        precision: 3);
                    Assert.Equal(LayoutTestAssertions.GetUnoButtonFontSize(DaisySize.Small), button.FontSize);
                    Assert.Equal(LayoutTestAssertions.GetUnoButtonPadding(DaisySize.Small), button.Padding);
                    Assert.True(button.Bounds.Width > button.Bounds.Height);
                    LayoutTestAssertions.HasHorizontalPadding(button);
                }
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void When_StandardDialogFooterRealizes_ButtonsMatchSharedMetrics()
        {
            var footer = FlowKanbanDialogBase.CreateStandardButtonFooter(
                out var saveButton,
                out var cancelButton,
                "Save",
                "Cancel");
            var dialogContent = FlowKanbanDialogBase.CreateDialogContent(
                topLevel: null,
                headerContent: null,
                new Border(),
                footer);
            var window = new Window
            {
                Width = 1000,
                Height = 800,
                Content = dialogContent
            };
            window.Show();
            Dispatcher.UIThread.RunJobs();
            dialogContent.UpdateLayout();

            try
            {
                var expectedHeight = LayoutTestAssertions.GetUnoButtonHeight(DaisySize.Medium);
                Assert.Equal(DaisySize.Medium, saveButton.Size);
                Assert.Equal(DaisySize.Medium, cancelButton.Size);
                Assert.Equal(80, saveButton.MinWidth);
                Assert.Equal(80, cancelButton.MinWidth);
                Assert.Equal(expectedHeight, saveButton.Bounds.Height, precision: 3);
                Assert.Equal(expectedHeight, cancelButton.Bounds.Height, precision: 3);
                Assert.True(saveButton.Bounds.Width >= saveButton.MinWidth);
                Assert.True(cancelButton.Bounds.Width >= cancelButton.MinWidth);
                Assert.Equal(12, footer.Spacing);
                Assert.Equal(HorizontalAlignment.Right, footer.HorizontalAlignment);
                Assert.Equal(new Thickness(0, 12, 0, 0), footer.Margin);
                Assert.Equal(2, Grid.GetRow(footer));

                var savePosition = LayoutTestAssertions.GetPosition(saveButton, footer);
                var cancelPosition = LayoutTestAssertions.GetPosition(cancelButton, footer);
                Assert.Equal(savePosition.Y, cancelPosition.Y, precision: 3);
                Assert.Equal(
                    savePosition.X + saveButton.Bounds.Width + footer.Spacing,
                    cancelPosition.X,
                    precision: 3);
                var saveContent = saveButton.GetVisualDescendants()
                    .OfType<StackPanel>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_ContentPanel",
                        StringComparison.Ordinal));
                var cancelContent = cancelButton.GetVisualDescendants()
                    .OfType<StackPanel>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_ContentPanel",
                        StringComparison.Ordinal));
                LayoutTestAssertions.IsCentered(saveButton, saveContent);
                LayoutTestAssertions.IsCentered(cancelButton, cancelContent);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void When_ColumnAndCardBindData_AutomationMetadataTracksTheirIdentity()
        {
            var task = CreateTask("Accessible card");
            var columnData = CreateColumn("Todo", task);
            var column = new FlowKanbanColumn { ColumnData = columnData };
            var card = new FlowTaskCard { Task = task };
            var panel = new StackPanel();
            panel.Children.Add(column);
            panel.Children.Add(card);
            var window = new Window { Content = panel };
            window.Show();
            panel.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            try
            {
                Assert.Equal("Todo", AutomationProperties.GetName(column));
                Assert.Equal($"kanban-column-{columnData.Id}", AutomationProperties.GetAutomationId(column));
                Assert.Equal("Accessible card", AutomationProperties.GetName(card));
                Assert.Equal($"kanban-card-{task.Id}", AutomationProperties.GetAutomationId(card));

                columnData.Title = "Doing";
                task.Title = "Updated card";

                Assert.Equal("Doing", AutomationProperties.GetName(column));
                Assert.Equal("Updated card", AutomationProperties.GetName(card));
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void When_AddCardTemplateRealizes_ButtonOwnsInvokeAndInputHasContextualAutomationMetadata()
        {
            var column = CreateColumn("Todo");
            column.Id = "todo";
            var addCard = new FlowKanbanAddCard
            {
                ColumnData = column,
                AddCardText = "Add card",
                AddCardPlaceholderText = "Card title",
                InsertAtTop = true
            };
            var window = new Window { Content = addCard };
            window.Show();
            addCard.UpdateLayout();
            Dispatcher.UIThread.RunJobs();

            try
            {
                var addButton = addCard.GetVisualDescendants()
                    .OfType<DaisyButton>()
                    .Single(control => string.Equals(control.Name, "PART_AddButton", StringComparison.Ordinal));
                var titleInput = addCard.GetVisualDescendants()
                    .OfType<DaisyInput>()
                    .Single(control => string.Equals(control.Name, "PART_TitleInput", StringComparison.Ordinal));
                var addButtonPeer = ControlAutomationPeer.CreatePeerForElement(addButton);
                var invokeProvider = Assert.IsAssignableFrom<IInvokeProvider>(addButtonPeer);

                Assert.Equal("Add card", AutomationProperties.GetName(addButton));
                Assert.Equal("kanban-add-card-todo-all-top-button", AutomationProperties.GetAutomationId(addButton));
                Assert.Equal("Card title", AutomationProperties.GetName(titleInput));
                Assert.Equal("kanban-add-card-todo-all-top-title", AutomationProperties.GetAutomationId(titleInput));

                invokeProvider.Invoke();
                Dispatcher.UIThread.RunJobs();
                Assert.True(addCard.IsEditing);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void When_Grouping_ByLane_UpdatesLaneRows()
        {
            var lane = new FlowKanbanLane { Id = "lane-1", Title = "Lane 1" };
            var board = CreateBoard(CreateColumn(
                "Column",
                new FlowTask { Title = "Assigned", LaneId = lane.Id },
                new FlowTask { Title = "Unassigned" }));
            board.GroupBy = FlowKanbanGroupBy.Lane;
            board.Lanes.Add(lane);

            var kanban = new FlowKanban { Board = board };
            var window = ShowKanban(kanban);

            try
            {
                Assert.True(kanban.IsSwimlaneLayoutEnabled);
                Assert.False(kanban.IsStandardLayoutEnabled);
                Assert.Equal(2, kanban.LaneRows.Count);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void When_BoardSize_Changes_EventFires()
        {
            var kanban = new FlowKanban();
            var invocationCount = 0;
            DaisySize? lastSize = null;

            kanban.BoardSizeChanged += (_, size) =>
            {
                invocationCount++;
                lastSize = size;
            };

            kanban.BoardSize = DaisySize.Small;
            kanban.BoardSize = DaisySize.Large;

            Assert.Equal(2, invocationCount);
            Assert.Equal(DaisySize.Large, lastSize);
        }

        [AvaloniaFact]
        public void When_WipLimit_Exceeded_MoveResultReflectsPolicy()
        {
            var source = CreateColumn("Todo", CreateTask("Task A"));
            var target = CreateColumn("Doing", CreateTask("Task B"));
            target.WipLimit = 1;
            var kanban = CreateKanban(source, target);
            var manager = new FlowKanbanManager(kanban, autoAttach: false);
            var task = source.Tasks[0];

            var blocked = manager.TryMoveTaskWithWipEnforcement(task, target, enforceHard: true);
            Assert.Equal(MoveResult.BlockedByWip, blocked);
            Assert.Single(target.Tasks);

            var warning = manager.TryMoveTaskWithWipEnforcement(task, target, enforceHard: false);
            Assert.Equal(MoveResult.AllowedWithWipWarning, warning);
            Assert.Equal(2, target.Tasks.Count);
        }

        [AvaloniaFact]
        public void When_Configuring_Column_Defaults_RespectSettings()
        {
            var column = new FlowKanbanColumn();

            Assert.True(column.ShowColumnHeader);
            Assert.True(column.ShowTasks);
            Assert.True(column.ShowAddCard);
            Assert.False(column.IsCollapsed);
            Assert.True(column.IsDropEnabled);
            Assert.Equal(DaisySize.Medium, column.ColumnSize);
            Assert.Null(column.LaneFilterId);

            column.IsDropEnabled = false;
            Assert.False(DragDrop.GetAllowDrop(column));
        }

        [Theory]
        [InlineData((int)FlowKanbanRuntimePlatform.Desktop, false, true, true)]
        [InlineData((int)FlowKanbanRuntimePlatform.Browser, false, false, false)]
        [InlineData((int)FlowKanbanRuntimePlatform.Android, true, false, false)]
        [InlineData((int)FlowKanbanRuntimePlatform.IOS, true, true, true)]
        public void When_ResolvingPlatformDefaults_UsesRuntimeCapabilities(
            int platformValue,
            bool staggeredRendering,
            bool keyboardHelp,
            bool columnTooltips)
        {
            var platform = (FlowKanbanRuntimePlatform)platformValue;
            var defaults = FlowKanbanPlatformDefaults.For(platform);

            Assert.Equal(staggeredRendering, defaults.EnableStaggeredTaskRendering);
            Assert.Equal(keyboardHelp, defaults.IsKeyboardHelpVisible);
            Assert.Equal(columnTooltips, defaults.IsColumnTooltipsEnabled);
        }

        [AvaloniaFact]
        public void When_TaskCard_BindsTask_SubtaskSummaryUpdates()
        {
            var task = new FlowTask
            {
                Title = "Card",
                PlannedEndDate = DateTime.Today.AddDays(1),
                Subtasks =
                {
                    new FlowSubtask { Title = "A", IsCompleted = true },
                    new FlowSubtask { Title = "B" },
                    new FlowSubtask { Title = "C" }
                }
            };

            var card = new FlowTaskCard { Task = task };

            Assert.True(card.HasSubtaskSummary);
            Assert.False(string.IsNullOrWhiteSpace(card.SubtaskSummaryText));
            Assert.True(card.HasDueDate);
            Assert.False(string.IsNullOrWhiteSpace(card.DueDateText));
        }

        [AvaloniaFact]
        public async Task When_ReplacingSubtasks_OnlyNewCollectionUpdatesCardAndAutoSave()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var task = CreateTask("Card");
            task.Subtasks.Add(new FlowSubtask { Title = "Original" });
            var oldSubtasks = task.Subtasks;
            var store = new CountingBoardStore();
            var kanban = new FlowKanban
            {
                Board = CreateBoard(CreateColumn("Todo", task)),
                BoardStore = store
            };
            var card = new FlowTaskCard { Task = task };
            var window = new Window { Content = card };
            window.Show();
            Dispatcher.UIThread.RunJobs();

            try
            {
                var newSubtasks = new ObservableCollection<FlowSubtask>();
                task.Subtasks = newSubtasks;
                await WaitForAutoSaveAsync();
                store.Reset();

                oldSubtasks.Add(new FlowSubtask { Title = "Detached" });
                await WaitForAutoSaveAsync();

                Assert.Equal(0, store.SaveCount);
                Assert.False(card.HasSubtaskSummary);

                newSubtasks.Add(new FlowSubtask { Title = "Current", IsCompleted = true });
                await WaitForAutoSaveAsync();

                Assert.Equal(1, store.SaveCount);
                Assert.True(card.HasSubtaskSummary);
                Assert.False(string.IsNullOrWhiteSpace(card.SubtaskSummaryText));
            }
            finally
            {
                window.Close();
                StateStorageProvider.Configure(previousStorage);
                GC.KeepAlive(kanban);
            }
        }

        [AvaloniaFact]
        public void When_Statistics_Handle_DuplicateColumnTitles()
        {
            var kanban = CreateKanban(
                CreateColumn("Todo", CreateTask("A")),
                CreateColumn("Todo", CreateTask("B"), CreateTask("C")));
            var manager = new FlowKanbanManager(kanban, autoAttach: false);

            var stats = manager.GetStatistics();

            Assert.Equal(2, stats.ColumnCount);
            Assert.Equal(3, stats.TasksPerColumn["Todo"]);
        }

        [AvaloniaFact]
        public void When_UndoRedo_MoveTask_RestoresState()
        {
            var task = CreateTask("Task A");
            var source = CreateColumn("Todo", task);
            var target = CreateColumn("Doing");
            var kanban = CreateKanban(source, target);
            kanban.EnableUndoRedo = true;
            var manager = new FlowKanbanManager(kanban, autoAttach: false);

            var result = manager.TryMoveTaskWithWipEnforcement(task, target, 0, enforceHard: false);
            Assert.Equal(MoveResult.Success, result);
            Assert.DoesNotContain(task, source.Tasks);
            Assert.Contains(task, target.Tasks);

            Assert.True(kanban.UndoCommand.CanExecute(null));
            kanban.UndoCommand.Execute(null);
            Assert.Contains(task, source.Tasks);
            Assert.DoesNotContain(task, target.Tasks);

            Assert.True(kanban.RedoCommand.CanExecute(null));
            kanban.RedoCommand.Execute(null);
            Assert.DoesNotContain(task, source.Tasks);
            Assert.Contains(task, target.Tasks);
        }

        [AvaloniaFact]
        public void When_ArchiveAndUnarchive_Task_ReturnsToSource()
        {
            var task = CreateTask("Archived Task");
            var source = CreateColumn("Doing", task);
            var kanban = CreateKanban(source);
            var manager = new FlowKanbanManager(kanban, autoAttach: false);

            manager.ArchiveTask(task);

            var archiveColumn = manager.GetArchiveColumn();
            Assert.NotNull(archiveColumn);
            Assert.True(task.IsArchived);
            Assert.Contains(task, archiveColumn!.Tasks);

            manager.UnarchiveTask(task);
            Assert.False(task.IsArchived);
            Assert.Contains(task, source.Tasks);
        }

        [AvaloniaFact]
        public void When_MovingTask_WithTargetLane_LaneIdUpdates()
        {
            var lane = new FlowKanbanLane { Id = "lane-1", Title = "Lane 1" };
            var task = CreateTask("Task A");
            var source = CreateColumn("Todo", task);
            var target = CreateColumn("Doing");
            var board = CreateBoard(source, target);
            board.GroupBy = FlowKanbanGroupBy.Lane;
            board.Lanes.Add(lane);
            var kanban = new FlowKanban { Board = board };
            var manager = new FlowKanbanManager(kanban, autoAttach: false);

            var result = manager.TryMoveTaskWithWipEnforcement(task, target, 0, lane.Id, enforceHard: false);

            Assert.Equal(MoveResult.Success, result);
            Assert.Equal(lane.Id, task.LaneId);
            Assert.Contains(task, target.Tasks);

            var back = manager.TryMoveTaskWithWipEnforcement(
                task,
                source,
                0,
                FlowKanban.UnassignedLaneId,
                enforceHard: false);
            Assert.Equal(MoveResult.Success, back);
            Assert.Null(task.LaneId);
            Assert.Contains(task, source.Tasks);
        }

        [Fact]
        public void When_CalculatingDropIndex_PositionsMapToIndices()
        {
            var realizedContainers = new List<(int Index, double Top, double Height)>
            {
                (2, 200, 80),
                (0, 0, 80),
                (1, 100, 80)
            };

            Assert.Equal(
                0,
                FlowKanbanColumn.CalculateInsertIndexFromLayout(0, 3, realizedContainers, out var indicatorY));
            Assert.Equal(0, indicatorY);

            Assert.Equal(
                1,
                FlowKanbanColumn.CalculateInsertIndexFromLayout(40, 3, realizedContainers, out indicatorY));
            Assert.Equal(100, indicatorY);

            Assert.Equal(
                2,
                FlowKanbanColumn.CalculateInsertIndexFromLayout(140, 3, realizedContainers, out indicatorY));
            Assert.Equal(200, indicatorY);

            Assert.Equal(
                3,
                FlowKanbanColumn.CalculateInsertIndexFromLayout(240, 3, realizedContainers, out indicatorY));
            Assert.Equal(280, indicatorY);

            Assert.Equal(
                3,
                FlowKanbanColumn.CalculateInsertIndexFromLayout(100000, 3, realizedContainers, out _));
        }

        [AvaloniaFact]
        public async Task When_DroppingOnRealizedTaskContainer_TaskMovesToMeasuredIndex()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            Window? window = null;

            try
            {
                var sourceTask = new FlowTask { Id = "source-task", Title = "Source" };
                var targetTasks = new[]
                {
                    new FlowTask { Id = "target-a", Title = "A" },
                    new FlowTask { Id = "target-b", Title = "B" },
                    new FlowTask { Id = "target-c", Title = "C" }
                };
                var sourceColumn = CreateColumn("Source", sourceTask);
                var targetColumn = CreateColumn("Target", targetTasks);
                var kanban = new FlowKanban
                {
                    Board = CreateBoard(sourceColumn, targetColumn),
                    EnableStaggeredTaskRendering = false,
                    AutoSaveAfterEdits = false
                };
                window = ShowKanban(kanban);
                await kanban.LoadUserSettingsAsync(forceReload: true);
                kanban.CurrentView = FlowKanbanView.Board;
                kanban.SetCompactLayout(false);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                var targetControl = GetVisibleColumns(kanban)
                    .Single(column => ReferenceEquals(column.ColumnData, targetColumn));
                var taskList = targetControl.GetVisualDescendants()
                    .OfType<ListBox>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_TasksItemsControl",
                        StringComparison.Ordinal));
                var tasksPanel = Assert.IsAssignableFrom<Panel>(taskList.ItemsPanelRoot);
                var realizedContainers = tasksPanel.Children
                    .OfType<Control>()
                    .Select(control => (Control: control, Index: taskList.IndexFromContainer(control)))
                    .Where(item => item.Index >= 0)
                    .OrderBy(item => item.Index)
                    .ToList();
                Assert.Equal(new[] { 0, 1, 2 }, realizedContainers.Select(item => item.Index));
                var secondContainer = realizedContainers[1].Control;
                var secondPositionInList = LayoutTestAssertions.GetPosition(secondContainer, taskList);
                var dropY = secondPositionInList.Y + 1;

                Assert.Equal(1, targetControl.CalculateInsertIndex(dropY, out var indicatorY));
                Assert.Equal(secondPositionInList.Y, indicatorY, precision: 3);

                var item = DataTransferItem.CreateText(sourceTask.Id);
                item.Set(FlowKanban.TaskDragFormat, sourceTask.Id);
                var data = new DataTransfer();
                data.Add(item);
                var secondPositionInWindow = LayoutTestAssertions.GetPosition(secondContainer, window);
                var dropPoint = new Point(
                    secondPositionInWindow.X + Math.Min(20, secondContainer.Bounds.Width / 2),
                    secondPositionInWindow.Y + 1);

                window.DragDrop(
                    dropPoint,
                    RawDragEventType.DragOver,
                    data,
                    DragDropEffects.Move,
                    RawInputModifiers.None);
                window.DragDrop(
                    dropPoint,
                    RawDragEventType.Drop,
                    data,
                    DragDropEffects.Move,
                    RawInputModifiers.None);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                Assert.Empty(sourceColumn.Tasks);
                Assert.Equal(new[] { "A", "Source", "B", "C" },
                    targetColumn.Tasks.Select(task => task.Title));
                Assert.Same(sourceTask, targetColumn.Tasks[1]);
                var postDropIndices = tasksPanel.Children
                    .OfType<Control>()
                    .Select(taskList.IndexFromContainer)
                    .Where(index => index >= 0)
                    .OrderBy(index => index)
                    .ToArray();
                Assert.Equal(new[] { 0, 1, 2, 3 }, postDropIndices);
            }
            finally
            {
                window?.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_GeneratedDemoBoardIsStressed_AllOperationsRemainBoundedAndPersistent()
        {
            var previousStorage = StateStorageProvider.Instance;
            var storage = new InMemoryStateStorage();
            StateStorageProvider.Configure(storage);
            Window? window = null;
            var totalDuration = System.Diagnostics.Stopwatch.StartNew();

            try
            {
                var store = new FlowKanbanBoardStore(storage);
                var generator = new FlowKanban
                {
                    BoardStore = store,
                    AutoSaveAfterEdits = false
                };
                var generationDuration = System.Diagnostics.Stopwatch.StartNew();
                Assert.True(generator.CreateDemoBoardCommand.CanExecute(null));
                generator.CreateDemoBoardCommand.Execute(null);
                generationDuration.Stop();

                var generatedBoard = generator.Board;
                Assert.Equal(4, generatedBoard.Columns.Count);
                Assert.Equal(200, generatedBoard.Columns.Sum(column => column.Tasks.Count));
                Assert.True(storage.SaveCount >= 1);
                Assert.True(
                    generationDuration.Elapsed < TimeSpan.FromSeconds(10),
                    $"Demo board generation took {generationDuration.Elapsed}.");

                var loadDuration = System.Diagnostics.Stopwatch.StartNew();
                Assert.True(store.TryLoadBoard(generatedBoard.Id, out var loadedBoard, out var loadError));
                loadDuration.Stop();
                Assert.Null(loadError);
                Assert.NotNull(loadedBoard);
                Assert.Equal(200, loadedBoard!.Columns.Sum(column => column.Tasks.Count));
                Assert.True(
                    loadDuration.Elapsed < TimeSpan.FromSeconds(10),
                    $"Generated board reload took {loadDuration.Elapsed}.");

                var kanban = new FlowKanban
                {
                    Board = loadedBoard,
                    BoardStore = store,
                    ColumnWidth = 240,
                    IsColumnResizeEnabled = true,
                    EnableStaggeredTaskRendering = false,
                    AutoSaveAfterEdits = true
                };
                var layoutDuration = System.Diagnostics.Stopwatch.StartNew();
                window = ShowKanban(kanban);
                await kanban.LoadUserSettingsAsync(forceReload: true);
                kanban.CurrentView = FlowKanbanView.Board;
                kanban.SetCompactLayout(false);
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                layoutDuration.Stop();
                Assert.True(
                    layoutDuration.Elapsed < TimeSpan.FromSeconds(20),
                    $"Initial 200-card layout took {layoutDuration.Elapsed}.");

                var host = kanban.GetVisualDescendants()
                    .OfType<FlowKanbanColumnsHost>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_ColumnsItemsControl",
                        StringComparison.Ordinal));
                var columnControls = GetOrderedBoardColumns(kanban, host);
                Assert.Equal(4, columnControls.Count);
                var initialRealizedCardCount = columnControls
                    .Sum(column => column.GetRealizedTaskCards().Count());
                Assert.InRange(initialRealizedCardCount, 1, 200);

                var searchDuration = System.Diagnostics.Stopwatch.StartNew();
                kanban.SearchText = "accessibility";
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                var matchingTaskCount = loadedBoard.Columns
                    .SelectMany(column => column.Tasks)
                    .Count(task => task.IsSearchMatch);
                searchDuration.Stop();
                Assert.InRange(matchingTaskCount, 1, 199);
                Assert.True(
                    searchDuration.Elapsed < TimeSpan.FromSeconds(10),
                    $"Search filtering took {searchDuration.Elapsed}.");
                kanban.SearchText = string.Empty;
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                Assert.All(
                    loadedBoard.Columns.SelectMany(column => column.Tasks),
                    task => Assert.True(task.IsSearchMatch));

                var scrollDuration = System.Diagnostics.Stopwatch.StartNew();
                var sourceColumn = loadedBoard.Columns[0];
                var sourceControl = columnControls.Single(column =>
                    ReferenceEquals(column.ColumnData, sourceColumn));
                var lastSourceTask = sourceColumn.Tasks[^1];
                Assert.True(sourceControl.ScrollTaskIntoView(lastSourceTask));
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                Assert.NotNull(sourceControl.TryGetTaskCard(lastSourceTask));
                var realizedAfterScroll = sourceControl.GetRealizedTaskCards().Count();
                Assert.InRange(realizedAfterScroll, 1, sourceColumn.Tasks.Count);
                scrollDuration.Stop();
                Assert.True(
                    scrollDuration.Elapsed < TimeSpan.FromSeconds(10),
                    $"Task scrolling and realization took {scrollDuration.Elapsed}.");

                var itemsPanel = Assert.IsType<StackPanel>(host.ItemsPanelRoot);
                var columnContainers = itemsPanel.Children.OfType<Control>().ToList();
                Assert.Equal(4, columnContainers.Count);
                var expandedOccupiedWidth = GetOccupiedWidth(columnContainers, host);
                sourceColumn.IsCollapsed = true;
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                var collapsedOccupiedWidth = GetOccupiedWidth(columnContainers, host);
                Assert.True(collapsedOccupiedWidth < expandedOccupiedWidth);
                sourceColumn.IsCollapsed = false;
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();

                var geometryDuration = System.Diagnostics.Stopwatch.StartNew();
                for (var iteration = 0; iteration < 20; iteration++)
                {
                    sourceColumn.IsCollapsed = iteration % 2 == 0;
                    host.ColumnWidth = 240 + iteration % 5 * 8;
                    Dispatcher.UIThread.RunJobs();
                    kanban.UpdateLayout();
                }
                sourceColumn.IsCollapsed = false;
                Dispatcher.UIThread.RunJobs();
                kanban.UpdateLayout();
                geometryDuration.Stop();
                Assert.Same(itemsPanel, host.ItemsPanelRoot);
                Assert.True(
                    geometryDuration.Elapsed < TimeSpan.FromSeconds(15),
                    $"Repeated collapse and resize took {geometryDuration.Elapsed}.");

                var draggedTask = sourceColumn.Tasks[0];
                var targetColumn = loadedBoard.Columns[1];
                var targetControl = columnControls.Single(column =>
                    ReferenceEquals(column.ColumnData, targetColumn));
                var targetList = targetControl.GetVisualDescendants()
                    .OfType<ListBox>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_TasksItemsControl",
                        StringComparison.Ordinal));
                var targetPanel = Assert.IsAssignableFrom<Panel>(targetList.ItemsPanelRoot);
                var firstTargetContainer = targetPanel.Children
                    .OfType<Control>()
                    .Single(control => targetList.IndexFromContainer(control) == 0);
                var firstTargetPosition = LayoutTestAssertions.GetPosition(firstTargetContainer, targetList);
                Assert.Equal(
                    0,
                    targetControl.CalculateInsertIndex(firstTargetPosition.Y + 1, out _));
                var transferItem = DataTransferItem.CreateText(draggedTask.Id);
                transferItem.Set(FlowKanban.TaskDragFormat, draggedTask.Id);
                var transfer = new DataTransfer();
                transfer.Add(transferItem);
                var targetPositionInWindow = LayoutTestAssertions.GetPosition(firstTargetContainer, window);
                var dropPoint = new Point(
                    targetPositionInWindow.X + Math.Min(20, firstTargetContainer.Bounds.Width / 2),
                    targetPositionInWindow.Y + 1);
                var sourceCountBeforeDrop = sourceColumn.Tasks.Count;
                var targetCountBeforeDrop = targetColumn.Tasks.Count;
                storage.ResetSaveCount();
                var dropDuration = System.Diagnostics.Stopwatch.StartNew();

                window.DragDrop(
                    dropPoint,
                    RawDragEventType.DragOver,
                    transfer,
                    DragDropEffects.Move,
                    RawInputModifiers.None);
                window.DragDrop(
                    dropPoint,
                    RawDragEventType.Drop,
                    transfer,
                    DragDropEffects.Move,
                    RawInputModifiers.None);
                await WaitForAutoSaveAsync();
                dropDuration.Stop();

                Assert.Equal(sourceCountBeforeDrop - 1, sourceColumn.Tasks.Count);
                Assert.Equal(targetCountBeforeDrop + 1, targetColumn.Tasks.Count);
                Assert.Same(draggedTask, targetColumn.Tasks[0]);
                Assert.True(storage.SaveCount >= 1);
                Assert.True(
                    dropDuration.Elapsed < TimeSpan.FromSeconds(10),
                    $"Drag/drop and autosave took {dropDuration.Elapsed}.");

                Assert.True(store.TryLoadBoard(loadedBoard.Id, out var reloadedBoard, out var reloadError));
                Assert.Null(reloadError);
                Assert.NotNull(reloadedBoard);
                Assert.Equal(200, reloadedBoard!.Columns.Sum(column => column.Tasks.Count));
                var reloadedTarget = reloadedBoard.Columns.Single(column => column.Id == targetColumn.Id);
                Assert.Equal(draggedTask.Id, reloadedTarget.Tasks[0].Id);
                totalDuration.Stop();
                Assert.True(
                    totalDuration.Elapsed < TimeSpan.FromSeconds(45),
                    $"Complete generated-board stress scenario took {totalDuration.Elapsed}.");
            }
            finally
            {
                window?.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_UserSettingsLoadsCompleteOutOfOrder_OldProviderCannotOverwriteNewSettings()
        {
            var previousStorage = StateStorageProvider.Instance;
            var storage = new InMemoryStateStorage();
            StateStorageProvider.Configure(storage);
            SaveUserSettings(storage, "old-user", 310);
            SaveUserSettings(storage, "new-user", 340);
            var kanban = new FlowKanban();
            var oldProvider = new ControlledUserProvider("old");
            var newProvider = new ControlledUserProvider("new");

            try
            {
                kanban.UserProvider = oldProvider;
                var oldLoad = kanban.LoadUserSettingsAsync(forceReload: true);
                kanban.UserProvider = newProvider;
                var newLoad = kanban.LoadUserSettingsAsync(forceReload: true);

                newProvider.Complete("new-user");
                await newLoad;
                Assert.Equal(340, kanban.ColumnWidth);

                oldProvider.Complete("old-user");
                await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await oldLoad);
                Assert.Equal(340, kanban.ColumnWidth);
            }
            finally
            {
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_UserSettingsProviderThrows_ErrorIsObservableAndVisible()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var expected = new InvalidOperationException("User lookup failed.");
            var provider = new ControlledUserProvider("throwing");
            var kanban = new FlowKanban { UserProvider = provider };
            FlowKanbanPersistenceFailedEventArgs? failure = null;
            kanban.PersistenceFailed += (_, args) => failure = args;

            try
            {
                provider.Fail(expected);

                var thrown = await Assert.ThrowsAsync<InvalidOperationException>(
                    () => kanban.LoadUserSettingsAsync(forceReload: true));

                Assert.Same(expected, thrown);
                Assert.NotNull(failure);
                Assert.Same(expected, failure!.Exception);
                Assert.Equal(FlowKanbanPersistenceOperation.LoadSettings, failure.Operation);
                Assert.Equal(expected.Message, kanban.StatusMessage);
                Assert.True(kanban.HasStatusMessage);
            }
            finally
            {
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_UserSettingsResolveAfterUnload_ControlStateIsNotChanged()
        {
            var previousStorage = StateStorageProvider.Instance;
            var storage = new InMemoryStateStorage();
            StateStorageProvider.Configure(storage);
            SaveUserSettings(storage, "late-user", 360);
            var provider = new ControlledUserProvider("late");
            var kanban = new FlowKanban { UserProvider = provider };
            var initialWidth = kanban.ColumnWidth;
            var window = ShowKanban(kanban);

            try
            {
                Assert.True(provider.CurrentUserRequestCount > 0);
                window.Close();

                provider.Complete("late-user");
                await Task.Delay(50);
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(initialWidth, kanban.ColumnWidth);
            }
            finally
            {
                if (window.IsVisible)
                {
                    window.Close();
                }

                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public void When_PersistingBoard_RoundTrips()
        {
            var storage = new InMemoryStateStorage();
            var store = new FlowKanbanBoardStore(storage);
            var board = CreateBoard(CreateColumn("Todo", CreateTask("Saved Task")));
            board.Id = "board-1";
            board.Title = "Round Trip";

            Assert.True(store.TrySaveBoard(board, out var saveError));
            Assert.Null(saveError);
            Assert.True(store.TryLoadBoard(board.Id, out var loaded, out var loadError));
            Assert.Null(loadError);
            Assert.NotNull(loaded);
            Assert.Equal(board.Id, loaded!.Id);
            Assert.Equal(board.Title, loaded.Title);
            Assert.Single(loaded.Columns);
            Assert.Equal("Todo", loaded.Columns[0].Title);
            Assert.Single(loaded.Columns[0].Tasks);
        }

        [AvaloniaFact]
        public void When_OlderBoardIsSavedAgain_RecentlyModifiedSortMovesItFirst()
        {
            var storage = new InMemoryStateStorage();
            var timeProvider = new TestTimeProvider(new DateTimeOffset(2026, 8, 2, 8, 0, 0, TimeSpan.Zero));
            var store = new FlowKanbanBoardStore(storage, timeProvider);
            var older = CreateBoard();
            older.Id = "11111111-1111-1111-1111-111111111111";
            older.Title = "Older";
            older.CreatedAt = new DateTime(2025, 1, 1);
            var newer = CreateBoard();
            newer.Id = "22222222-2222-2222-2222-222222222222";
            newer.Title = "Newer";
            newer.CreatedAt = new DateTime(2026, 1, 1);

            Assert.True(store.TrySaveBoard(older, out var olderSaveError));
            Assert.Null(olderSaveError);
            timeProvider.Advance(TimeSpan.FromHours(1));
            Assert.True(store.TrySaveBoard(newer, out var newerSaveError));
            Assert.Null(newerSaveError);
            timeProvider.Advance(TimeSpan.FromHours(1));
            older.Title = "Older, edited";
            Assert.True(store.TrySaveBoard(older, out var editedSaveError));
            Assert.Null(editedSaveError);

            var home = new FlowKanbanHome
            {
                Boards = new ObservableCollection<FlowBoardMetadata>(store.ListBoards())
            };

            Assert.Equal(older.Id, home.FilteredBoards[0].Id);
            Assert.True(home.FilteredBoards[0].LastModified > home.FilteredBoards[1].LastModified);
            Assert.True(store.TryLoadBoard(older.Id, out var loaded, out var loadError));
            Assert.Null(loadError);
            Assert.Equal(older.UpdatedAt, loaded!.UpdatedAt);
        }

        [Fact]
        public void When_BoardHasNoUpdatedAt_MetadataFallsBackToCreatedAt()
        {
            var createdAt = new DateTime(2024, 6, 1, 12, 30, 0, DateTimeKind.Utc);
            var board = CreateBoard();
            board.Id = "33333333-3333-3333-3333-333333333333";
            board.CreatedAt = createdAt;
            board.UpdatedAt = null;
            var json = JsonSerializer.Serialize(board, FlowKanbanJsonContext.Default.FlowKanbanData);

            Assert.True(FlowKanbanBoardSanitizer.TryBuildBoardMetadata(json, board.Id, out var metadata));
            Assert.Equal(createdAt, metadata.LastModified);
        }

        [Fact]
        public void When_PersistingBoard_SaveFails_ReturnsConcreteError()
        {
            var expected = new IOException("Save failed.");
            var storage = new FailingStateStorage(StorageFailure.Save, expected);
            var store = new FlowKanbanBoardStore(storage);
            var board = CreateBoard();
            board.Id = "save-failure";
            var previousUpdatedAt = new DateTime(2026, 1, 1);
            board.UpdatedAt = previousUpdatedAt;

            Assert.False(store.TrySaveBoard(board, out var error));
            Assert.Same(expected, error);
            Assert.Equal(previousUpdatedAt, board.UpdatedAt);
        }

        [Fact]
        public void When_PersistingBoard_RenameFails_ReturnsConcreteError()
        {
            var expected = new IOException("Rename failed.");
            var storage = new FailingStateStorage(StorageFailure.Rename, expected);
            var store = new FlowKanbanBoardStore(storage);
            var board = CreateBoard();
            board.Id = "rename-failure";

            Assert.False(store.TrySaveBoard(board, out var error));
            Assert.Same(expected, error);
        }

        [Fact]
        public void When_DeletingBoard_DeleteFails_ReturnsConcreteError()
        {
            var expected = new IOException("Delete failed.");
            var storage = new FailingStateStorage(StorageFailure.Delete, expected);
            var store = new FlowKanbanBoardStore(storage);

            Assert.False(store.TryDeleteBoard("22222222-2222-2222-2222-222222222222", out var error));
            Assert.Same(expected, error);
        }

        [Fact]
        public void When_FileStateStorageTargetsExistingFile_MutationsReturnRealIoErrors()
        {
            var processPath = Assert.IsType<string>(Environment.ProcessPath);
            Assert.True(Path.IsPathRooted(processPath));
            Assert.True(File.Exists(processPath));
            var storage = new FileStateStorage(processPath);
            var store = new FlowKanbanBoardStore(storage);
            var board = CreateBoard();
            board.Id = "file-io-failure";

            Assert.False(store.TrySaveBoard(board, out var saveError));
            Assert.IsAssignableFrom<IOException>(saveError);
            Assert.ThrowsAny<IOException>(() => storage.Rename("source", "target"));
            Assert.False(store.TryDeleteBoard(board.Id, out var deleteError));
            Assert.True(deleteError is IOException or UnauthorizedAccessException);
        }

        [AvaloniaFact]
        public void When_PersistenceFails_ManagerRaisesEventAndShowsStatus()
        {
            var expected = new IOException("Storage unavailable.");
            var kanban = new FlowKanban
            {
                BoardStore = new FlowKanbanBoardStore(
                    new FailingStateStorage(StorageFailure.Save, expected))
            };
            var manager = new FlowKanbanManager(kanban, autoAttach: false);
            FlowKanbanPersistenceFailedEventArgs? failure = null;
            manager.PersistenceFailed += (_, args) => failure = args;

            Assert.False(manager.SaveBoard());
            Assert.NotNull(failure);
            Assert.Same(expected, failure!.Exception);
            Assert.Equal(FlowKanbanPersistenceOperation.SaveBoard, failure.Operation);
            Assert.Equal(expected.Message, kanban.StatusMessage);
            Assert.True(kanban.HasStatusMessage);
            Assert.True(kanban.IsBoardStatusBarVisible);
        }

        [AvaloniaFact]
        public void When_Migrating_V0Json_LoadsBoard()
        {
            var board = CreateBoard(CreateColumn("Backlog", CreateTask("Legacy Task")));
            board.Id = "legacy-board";
            board.Title = "Legacy";

            var json = JsonSerializer.Serialize(board, FlowKanbanJsonContext.Default.FlowKanbanData);
            var root = JsonNode.Parse(json) as JsonObject;
            root?.Remove("schemaVersion");

            var migrated = FlowKanbanMigration.MigrateFromJson(root?.ToJsonString() ?? json);

            Assert.NotNull(migrated);
            Assert.Equal(board.Id, migrated!.Id);
            Assert.Equal(board.Title, migrated.Title);
            Assert.Single(migrated.Columns);
            Assert.Equal("Backlog", migrated.Columns[0].Title);
            Assert.Single(migrated.Columns[0].Tasks);
        }

        private static FlowTask CreateTask(string title) => new() { Title = title };

        private static async Task WaitForAutoSaveAsync()
        {
            await Task.Delay(900);
            Dispatcher.UIThread.RunJobs();
        }

        private static void SaveUserSettings(
            InMemoryStateStorage storage,
            string userId,
            double columnWidth)
        {
            var state = new FlowKanban.FlowKanbanUserSettingsState
            {
                ColumnWidth = columnWidth
            };
            var json = JsonSerializer.Serialize(
                state,
                FlowKanbanJsonContext.Default.FlowKanbanUserSettingsState);
            var storageKey = StateStorageKeyHelper.BuildScopedKey("kanban.user.settings", userId);
            storage.SaveLines(storageKey, new[] { json });
        }

        private static double GetPositionX(Visual control, Visual relativeTo)
        {
            var transform = control.TransformToVisual(relativeTo);
            Assert.NotNull(transform);
            return transform.Value.Transform(default).X;
        }

        private static IReadOnlyList<FlowKanbanColumn> GetVisibleColumns(FlowKanban kanban)
        {
            return kanban.GetVisualDescendants()
                .OfType<FlowKanbanColumn>()
                .Where(column => column.IsEffectivelyVisible)
                .ToList();
        }

        private static IReadOnlyList<FlowKanbanColumn> GetOrderedBoardColumns(
            FlowKanban kanban,
            Visual relativeTo)
        {
            return GetVisibleColumns(kanban)
                .Where(column => column.ShowColumnHeader && column.ShowTasks)
                .OrderBy(column => LayoutTestAssertions.GetPosition(column, relativeTo).X)
                .ToList();
        }

        private static double GetOccupiedWidth(
            IReadOnlyList<Control> controls,
            Visual relativeTo)
        {
            var firstX = LayoutTestAssertions.GetPosition(controls[0], relativeTo).X;
            var last = controls[^1];
            var lastRight = LayoutTestAssertions.GetPosition(last, relativeTo).X + last.Bounds.Width;
            return lastRight - firstX;
        }

        private static double GetGapCenter(
            IReadOnlyList<Control> controls,
            int gapIndex,
            Visual relativeTo)
        {
            var left = controls[gapIndex];
            var leftRight = LayoutTestAssertions.GetPosition(left, relativeTo).X + left.Bounds.Width;
            var rightX = LayoutTestAssertions.GetPosition(controls[gapIndex + 1], relativeTo).X;
            return (leftRight + rightX) / 2;
        }

        private static void AssertIconTextVisibleContentVerticallyCentered(
            Control container,
            DaisyIconText content)
        {
            var icon = content.GetVisualDescendants()
                .OfType<Viewbox>()
                .Single(control => string.Equals(
                    control.Name,
                    "PART_IconViewbox",
                    StringComparison.Ordinal));
            var text = content.GetVisualDescendants()
                .OfType<Control>()
                .Single(control => string.Equals(
                    control.Name,
                    "PART_TextBlock",
                    StringComparison.Ordinal));
            var iconPosition = LayoutTestAssertions.GetPosition(icon, container);
            var textPosition = LayoutTestAssertions.GetPosition(text, container);
            var top = Math.Min(iconPosition.Y, textPosition.Y);
            var bottom = Math.Max(
                iconPosition.Y + icon.Bounds.Height,
                textPosition.Y + text.Bounds.Height);
            var verticalOffset = Math.Abs(container.Bounds.Height / 2 - (top + bottom) / 2);

            Assert.InRange(verticalOffset, 0, 0.5);
        }

        private static void AssertLaneWipIndicatorVisibility(
            FlowKanbanColumn column,
            bool expectedVisibility)
        {
            var indicator = column.GetVisualDescendants()
                .OfType<StackPanel>()
                .Single(control => string.Equals(
                    control.Name,
                    "PART_LaneWipIndicator",
                    StringComparison.Ordinal));

            Assert.Equal(expectedVisibility, indicator.IsEffectivelyVisible);
        }

        private static void AssertColumnHeaderButtonMetrics(
            FlowKanbanColumn column,
            bool expectCollapseButton)
        {
            var buttons = column.GetVisualDescendants().OfType<DaisyButton>().ToList();
            var renameButton = buttons.Single(button =>
                string.Equals(button.Name, "PART_RenameBtn", StringComparison.Ordinal));
            var expectedSide = LayoutTestAssertions.GetUnoButtonHeight(DaisySize.ExtraSmall);
            LayoutTestAssertions.HasSize(renameButton, expectedSide, expectedSide);
            LayoutTestAssertions.IsCentered(
                renameButton,
                renameButton.GetVisualDescendants()
                    .OfType<Viewbox>()
                    .Single(control => string.Equals(
                        control.Name,
                        "PART_IconViewbox",
                        StringComparison.Ordinal)));

            var collapseButton = buttons.Single(button =>
                string.Equals(button.Name, "PART_CollapseBtn", StringComparison.Ordinal));
            Assert.Equal(expectCollapseButton, collapseButton.IsEffectivelyVisible);
            if (expectCollapseButton)
            {
                LayoutTestAssertions.HasSize(collapseButton, expectedSide, expectedSide);
                LayoutTestAssertions.IsCentered(
                    collapseButton,
                    collapseButton.GetVisualDescendants()
                        .OfType<Viewbox>()
                        .Single(control => string.Equals(
                            control.Name,
                            "PART_IconViewbox",
                            StringComparison.Ordinal)));
            }
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

        private static FlowKanbanData CreateBoard(params FlowKanbanColumnData[] columns)
        {
            var board = new FlowKanbanData();
            foreach (var column in columns)
            {
                board.Columns.Add(column);
            }

            return board;
        }

        private static FlowKanban CreateKanban(params FlowKanbanColumnData[] columns) =>
            new() { Board = CreateBoard(columns) };

        private static Window ShowKanban(FlowKanban kanban)
        {
            var window = new Window
            {
                Width = 1200,
                Height = 800,
                Content = kanban
            };
            window.Show();
            kanban.CurrentView = FlowKanbanView.Board;
            Dispatcher.UIThread.RunJobs();
            kanban.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            return window;
        }

        private static Control GetAutomationControl(Control root, string automationId)
        {
            return root.GetVisualDescendants()
                .OfType<Control>()
                .Single(control => string.Equals(
                    AutomationProperties.GetAutomationId(control),
                    automationId,
                    StringComparison.Ordinal));
        }

        private sealed class TestTimeProvider : TimeProvider
        {
            private DateTimeOffset _utcNow;

            public TestTimeProvider(DateTimeOffset utcNow)
            {
                _utcNow = utcNow;
            }

            public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

            public override DateTimeOffset GetUtcNow() => _utcNow;

            public void Advance(TimeSpan value)
            {
                _utcNow = _utcNow.Add(value);
            }
        }

        private sealed class ControlledUserProvider : IUserProvider
        {
            private readonly TaskCompletionSource<IFlowUser?> _currentUser =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public ControlledUserProvider(string providerKey)
            {
                ProviderKey = providerKey;
            }

            public int CurrentUserRequestCount { get; private set; }
            public string ProviderKey { get; }
            public string DisplayName => ProviderKey;
            public string ImplementationVersion => "test";
            public bool SupportsAvatars => false;
            public bool SupportsPresence => false;
            public bool SupportsRealtime => false;

            public event Action? UsersChanged
            {
                add { }
                remove { }
            }

            public Task<IEnumerable<IFlowUser>> GetAllUsersAsync(CancellationToken cancellation = default) =>
                Task.FromResult<IEnumerable<IFlowUser>>(Array.Empty<IFlowUser>());

            public Task<IFlowUser?> GetUserByIdAsync(string rawId, CancellationToken cancellation = default) =>
                Task.FromResult<IFlowUser?>(null);

            public Task<IEnumerable<IFlowUser>> SearchUsersAsync(
                string query,
                int maxResults = 20,
                CancellationToken cancellation = default) =>
                Task.FromResult<IEnumerable<IFlowUser>>(Array.Empty<IFlowUser>());

            public Task<IFlowUser?> GetCurrentUserAsync(CancellationToken cancellation = default)
            {
                CurrentUserRequestCount++;
                return _currentUser.Task;
            }

            public Task RefreshAsync(CancellationToken cancellation = default) => Task.CompletedTask;

            public void Complete(string userId)
            {
                _currentUser.TrySetResult(new FlowUser
                {
                    Id = userId,
                    RawId = userId,
                    ProviderKey = ProviderKey,
                    DisplayName = userId
                });
            }

            public void Fail(Exception error)
            {
                _currentUser.TrySetException(error);
            }
        }

        private sealed class CountingBoardStore : IBoardStore
        {
            public int SaveCount { get; private set; }

            public IReadOnlyList<FlowBoardMetadata> ListBoards() => Array.Empty<FlowBoardMetadata>();

            public bool TryLoadBoard(string boardId, out FlowKanbanData? board, out Exception? error)
            {
                board = null;
                error = null;
                return false;
            }

            public bool TrySaveBoard(FlowKanbanData board, out Exception? error)
            {
                SaveCount++;
                error = null;
                return true;
            }

            public bool TryDeleteBoard(string boardId, out Exception? error)
            {
                error = null;
                return true;
            }

            public bool TryExportBoard(string boardId, out string? json, out Exception? error)
            {
                json = null;
                error = null;
                return false;
            }

            public void Reset()
            {
                SaveCount = 0;
            }
        }

        private sealed class InMemoryStateStorage : IStateStorage
        {
            private readonly Dictionary<string, List<string>> _store = new(StringComparer.Ordinal);

            public int SaveCount { get; private set; }

            public IReadOnlyList<string> LoadLines(string key) =>
                _store.TryGetValue(key, out var lines) ? lines : Array.Empty<string>();

            public void SaveLines(string key, IEnumerable<string> lines)
            {
                SaveCount++;
                _store[key] = new List<string>(lines);
            }

            public void ResetSaveCount()
            {
                SaveCount = 0;
            }

            public void Delete(string key)
            {
                _store.Remove(key);
            }

            public void Rename(string sourceKey, string targetKey)
            {
                if (!_store.TryGetValue(sourceKey, out var lines))
                {
                    throw new InvalidOperationException($"Storage key '{sourceKey}' does not exist.");
                }

                _store[targetKey] = new List<string>(lines);
                _store.Remove(sourceKey);
            }

            public IEnumerable<string> GetKeys(string prefix) =>
                _store.Keys.Where(key => key.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
        }

        private enum StorageFailure
        {
            Save,
            Rename,
            Delete
        }

        private sealed class FailingStateStorage : IStateStorage
        {
            private readonly InMemoryStateStorage _inner = new();
            private readonly StorageFailure _failure;
            private readonly Exception _exception;

            public FailingStateStorage(StorageFailure failure, Exception exception)
            {
                _failure = failure;
                _exception = exception;
            }

            public IReadOnlyList<string> LoadLines(string key) => _inner.LoadLines(key);

            public void SaveLines(string key, IEnumerable<string> lines)
            {
                if (_failure == StorageFailure.Save)
                    throw _exception;

                _inner.SaveLines(key, lines);
            }

            public void Delete(string key)
            {
                if (_failure == StorageFailure.Delete)
                    throw _exception;

                _inner.Delete(key);
            }

            public void Rename(string sourceKey, string targetKey)
            {
                if (_failure == StorageFailure.Rename)
                    throw _exception;

                _inner.Rename(sourceKey, targetKey);
            }

            public IEnumerable<string> GetKeys(string prefix) => _inner.GetKeys(prefix);
        }
    }
}
