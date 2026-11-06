using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Kanban.Controls;
using Flowery.NET.Kanban.Interfaces;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Tests
{
    public class KanbanBehaviorTests
    {
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

        [AvaloniaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_BoardUnloadsWithAPendingAutoSave_TheEditIsSavedAtOnceAndNotLater(bool onHomeView)
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var column = CreateColumn("Todo", CreateTask("Task"));
            var store = new CountingBoardStore();
            var kanban = new FlowKanban { Board = CreateBoard(column), BoardStore = store };
            Window? window = ShowKanban(kanban);

            try
            {
                // The board opens on its home view once it is shown.
                kanban.CurrentView = FlowKanbanView.Board;
                Dispatcher.UIThread.RunJobs();
                store.Reset();

                column.IsCollapsed = !column.IsCollapsed;
                var timer = kanban.AutoSaveTimer;
                // The user leaves for the home view before the auto-save came, then closes the window.
                if (onHomeView)
                {
                    kanban.CurrentView = FlowKanbanView.Home;
                    Dispatcher.UIThread.RunJobs();
                }
                Assert.True(timer?.IsEnabled, "the edit did not start an auto-save");
                Assert.Equal(0, store.SaveCount);

                // Before, the timer stayed on and saved later, into the storage of whatever ran then.
                window.Close();
                window = null;
                Dispatcher.UIThread.RunJobs();

                Assert.Equal(1, store.SaveCount);
                Assert.False(timer!.IsEnabled);
                // The save comes before the home view clears the last board, so the next start opens the home view.
                if (onHomeView)
                    Assert.Null(kanban.LastBoardId);
                else
                    Assert.NotNull(kanban.LastBoardId);
            }
            finally
            {
                window?.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void When_BoardOrStoreChangesWithAPendingAutoSave_TheEditGoesToItsBoardAndStore(bool replaceStore)
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var column = CreateColumn("Todo", CreateTask("Task"));
            var edited = CreateBoard(column);
            var store = new CountingBoardStore();
            var kanban = new FlowKanban { Board = edited, BoardStore = store };
            var window = ShowKanban(kanban);

            try
            {
                kanban.CurrentView = FlowKanbanView.Board;
                Dispatcher.UIThread.RunJobs();
                store.Reset();
                var lastBoardId = kanban.LastBoardId;

                column.IsCollapsed = !column.IsCollapsed;
                var timer = kanban.AutoSaveTimer;
                Assert.True(timer?.IsEnabled, "the edit did not start an auto-save");

                // Before, the timer saved later: the board open then, into the store set then.
                var otherStore = new CountingBoardStore();
                if (replaceStore)
                    kanban.BoardStore = otherStore;
                else
                    kanban.Board = CreateBoard(CreateColumn("Other"));

                Assert.Same(edited, Assert.Single(store.SavedBoards));
                Assert.Empty(otherStore.SavedBoards);
                Assert.False(timer!.IsEnabled);
                // A replaced board is no longer open, so it does not become the last board; with a new store it stays open.
                Assert.Equal(replaceStore ? edited.Id : lastBoardId, kanban.LastBoardId);
            }
            finally
            {
                window.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public void When_AppGoesToTheBackgroundWithAPendingAutoSave_TheEditIsSavedAtOnce()
        {
            var previousStorage = StateStorageProvider.Instance;
            var previousLifetimeSource = FlowKanban.ActivatableLifetimeSource;
            var column = CreateColumn("Todo", CreateTask("Task"));
            var edited = CreateBoard(column);
            var store = new CountingBoardStore();
            Window? window = null;
            DispatcherTimer? timer = null;

            try
            {
                StateStorageProvider.Configure(new InMemoryStateStorage());
                var lifetime = TestActivatableLifetime.Create();
                FlowKanban.ActivatableLifetimeSource = () => lifetime;
                var kanban = new FlowKanban { Board = edited, BoardStore = store };
                window = ShowKanban(kanban);
                kanban.CurrentView = FlowKanbanView.Board;
                Dispatcher.UIThread.RunJobs();
                store.Reset();

                column.IsCollapsed = !column.IsCollapsed;
                timer = kanban.AutoSaveTimer;
                Assert.True(timer?.IsEnabled, "the edit did not start an auto-save");

                // On a phone the board stays loaded in the background, where the system may end the app at any time.
                TestActivatableLifetime.EnterBackground(lifetime);

                Assert.Same(edited, Assert.Single(store.SavedBoards));
                Assert.False(timer!.IsEnabled);

                // An unloaded board no longer listens.
                window.Close();
                window = null;
                store.Reset();
                column.IsCollapsed = !column.IsCollapsed;
                Assert.True(timer.IsEnabled, "the edit did not start an auto-save");
                TestActivatableLifetime.EnterBackground(lifetime);
                Assert.Empty(store.SavedBoards);
            }
            finally
            {
                timer?.Stop();
                window?.Close();
                FlowKanban.ActivatableLifetimeSource = previousLifetimeSource;
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_AppGoesToTheBackgroundOnAnotherThread_TheEditIsSavedBeforeTheEventReturns()
        {
            var previousStorage = StateStorageProvider.Instance;
            var previousLifetimeSource = FlowKanban.ActivatableLifetimeSource;
            var column = CreateColumn("Todo", CreateTask("Task"));
            var edited = CreateBoard(column);
            var store = new CountingBoardStore();
            Window? window = null;
            try
            {
                StateStorageProvider.Configure(new InMemoryStateStorage());
                var lifetime = TestActivatableLifetime.Create();
                FlowKanban.ActivatableLifetimeSource = () => lifetime;
                var kanban = new FlowKanban { Board = edited, BoardStore = store };
                window = ShowKanban(kanban);
                store.Reset();
                column.IsCollapsed = !column.IsCollapsed;
                Assert.True(kanban.AutoSaveTimer?.IsEnabled, "the edit did not start an auto-save");

                // The system raises it on a thread of its own and may end the app as soon as it returns, so the save is done
                // by then. Which dispatcher the board takes cannot be seen here: both are the same in this test session.
                var savedBeforeReturn = await Task.Run(() =>
                {
                    TestActivatableLifetime.EnterBackground(lifetime);
                    return store.SaveCount;
                });

                Assert.Equal(1, savedBeforeReturn);
                Assert.Same(edited, Assert.Single(store.SavedBoards));
                Assert.False(kanban.AutoSaveTimer!.IsEnabled);
            }
            finally
            {
                window?.Close();
                FlowKanban.ActivatableLifetimeSource = previousLifetimeSource;
                StateStorageProvider.Configure(previousStorage);
            }
        }

        // Avalonia lets no user code implement IActivatableLifetime, so the test builds one at run time.
        public class TestActivatableLifetime : System.Reflection.DispatchProxy
        {
            private EventHandler<ActivatedEventArgs>? _deactivated;

            public static IActivatableLifetime Create() => Create<IActivatableLifetime, TestActivatableLifetime>();

            public static void EnterBackground(IActivatableLifetime lifetime) =>
                ((TestActivatableLifetime)(object)lifetime)._deactivated?.Invoke(lifetime, new ActivatedEventArgs(ActivationKind.Background));

            protected override object? Invoke(System.Reflection.MethodInfo? targetMethod, object?[]? args)
            {
                switch (targetMethod?.Name)
                {
                    case "add_Deactivated":
                        _deactivated += (EventHandler<ActivatedEventArgs>)args![0]!;
                        return null;
                    case "remove_Deactivated":
                        _deactivated -= (EventHandler<ActivatedEventArgs>)args![0]!;
                        return null;
                    default:
                        return targetMethod?.ReturnType == typeof(bool) ? false : null;
                }
            }
        }

        [AvaloniaFact]
        public void When_OpenBoardIsDeletedWithAPendingAutoSave_ItIsNotSavedBack()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var column = CreateColumn("Todo", CreateTask("Task"));
            var edited = CreateBoard(column);
            var store = new CountingBoardStore();
            var kanban = new FlowKanban { Board = edited, BoardStore = store };
            var window = ShowKanban(kanban);

            try
            {
                kanban.CurrentView = FlowKanbanView.Board;
                Dispatcher.UIThread.RunJobs();
                store.Reset();

                column.IsCollapsed = !column.IsCollapsed;
                var timer = kanban.AutoSaveTimer;
                Assert.True(timer?.IsEnabled, "the edit did not start an auto-save");

                Assert.True(kanban.TryDeleteBoard(edited.Id, out var error));
                Assert.Null(error);

                Assert.NotSame(edited, kanban.Board);
                Assert.Empty(store.SavedBoards);
                Assert.False(timer!.IsEnabled);
            }
            finally
            {
                window.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public void When_OpenBoardIsReopenedWithAPendingAutoSave_TheReopenedBoardHasTheEdit()
        {
            var previousStorage = StateStorageProvider.Instance;
            var storage = new InMemoryStateStorage();
            StateStorageProvider.Configure(storage);
            var column = CreateColumn("Todo", CreateTask("Task"));
            var edited = CreateBoard(column);
            var kanban = new FlowKanban { Board = edited, BoardStore = new FlowKanbanBoardStore(storage) };
            var window = ShowKanban(kanban);

            try
            {
                kanban.CurrentView = FlowKanbanView.Board;
                Dispatcher.UIThread.RunJobs();
                Assert.True(kanban.TrySaveBoard(out var saveError), saveError?.Message);

                var collapsed = !column.IsCollapsed;
                column.IsCollapsed = collapsed;
                Assert.True(kanban.AutoSaveTimer?.IsEnabled, "the edit did not start an auto-save");

                // Before, the load read the stored copy without the edit, and the edit was saved only afterwards.
                Assert.True(kanban.TryLoadBoard(edited.Id, out var loadError), loadError?.Message);

                Assert.NotSame(edited, kanban.Board);
                Assert.Equal(collapsed, Assert.Single(kanban.Board.Columns).IsCollapsed);
            }
            finally
            {
                window.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public void When_ReplacedBoardCannotBeSaved_ThePersistenceFailureIsReported()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var column = CreateColumn("Todo", CreateTask("Task"));
            var expected = new IOException("disk full");
            var kanban = new FlowKanban { Board = CreateBoard(column), BoardStore = new CountingBoardStore(expected) };
            var window = ShowKanban(kanban);
            FlowKanbanPersistenceFailedEventArgs? failure = null;
            kanban.PersistenceFailed += (_, args) => failure = args;

            try
            {
                kanban.CurrentView = FlowKanbanView.Board;
                Dispatcher.UIThread.RunJobs();
                failure = null;

                column.IsCollapsed = !column.IsCollapsed;
                Assert.True(kanban.AutoSaveTimer?.IsEnabled, "the edit did not start an auto-save");

                kanban.Board = CreateBoard(CreateColumn("Other"));

                Assert.NotNull(failure);
                Assert.Equal(FlowKanbanPersistenceOperation.SaveBoard, failure!.Operation);
                Assert.Same(expected, failure.Exception);
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
                await WaitForAutoSaveAsync(kanban);
                store.Reset();

                var detachedTask = new FlowTask { Title = "detached", IsSelected = true };
                oldTasks.Add(detachedTask);
                await WaitForAutoSaveAsync(kanban);

                Assert.Equal(0, store.SaveCount);
                Assert.True(detachedTask.IsSearchMatch);
                Assert.Equal(0, kanban.SelectedCount);
                Assert.Equal("0", column.WipDisplay);

                var currentTask = new FlowTask { Title = "current", IsSelected = true };
                newTasks.Add(currentTask);
                await WaitForAutoSaveAsync(kanban);

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
            // Without saved boards, loading switches to Home and hides the board buttons;
            // in-memory storage keeps the result independent of the machine's saved boards.
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var kanban = CreateKanban(CreateColumn("Todo", CreateTask("Measured card")));
            var window = ShowKanban(kanban);

            try
            {
                kanban.CurrentView = FlowKanbanView.Board;
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
                var optionalSidebarButtons = new[] { "Kanban_KeyboardHelp" }
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

                var archiveGroup = Assert.IsType<StackPanel>(archiveToggle.GetVisualParent());
                var statusRow = Assert.IsType<StackPanel>(archiveGroup.GetVisualParent());
                var archiveGroupIndex = statusRow.Children.IndexOf(archiveGroup);
                Assert.True(archiveGroupIndex >= 2);
                var zoomGroup = Assert.IsType<StackPanel>(statusRow.Children[archiveGroupIndex - 2]);
                var zoomContent = zoomGroup.Children
                    .OfType<Control>()
                    .Where(control => control.IsEffectivelyVisible)
                    .ToList();
                Assert.NotEmpty(zoomContent);
                var zoomTop = zoomContent.Min(control =>
                    LayoutTestAssertions.GetPosition(control, statusRow).Y);
                var zoomBottom = zoomContent.Max(control =>
                    LayoutTestAssertions.GetPosition(control, statusRow).Y + control.Bounds.Height);
                var archivePosition = LayoutTestAssertions.GetPosition(archiveContent, statusRow);
                var archiveCenter = archivePosition.Y + archiveContent.Bounds.Height / 2;
                var zoomCenter = (zoomTop + zoomBottom) / 2;

                Assert.InRange(Math.Abs(archiveCenter - zoomCenter), 0, 0.5);
            }
            finally
            {
                window.Close();
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
                LoadSettings(kanban);
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
                LoadSettings(kanban);
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
                    Assert.Equal(LayoutTestAssertions.GetButtonFontSize(DaisySize.Small), button.FontSize);
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
        public void When_DropIsDisabled_OnALoadedColumn_ColumnAndTaskListRefuseDrops()
        {
            var kanban = CreateKanban(CreateColumn("Todo", CreateTask("Task")));
            var window = ShowKanban(kanban);

            try
            {
                ShowBoardView(kanban);
                var column = GetVisibleColumns(kanban).Single();
                var taskList = column.GetVisualDescendants().OfType<ListBox>().Single(list => list.Name == "PART_TasksItemsControl");
                Assert.True(DragDrop.GetAllowDrop(column));
                Assert.True(DragDrop.GetAllowDrop(taskList));

                column.IsDropEnabled = false;

                Assert.False(DragDrop.GetAllowDrop(column));
                Assert.False(DragDrop.GetAllowDrop(taskList));
            }
            finally
            {
                window.Close();
            }
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
                LoadSettings(kanban);
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
        public async Task When_AssigneeAdapterCannotResolveId_AssignmentIsPreserved()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var task = new FlowTask
            {
                Title = "Assigned task",
                Assignee = "External User",
                AssigneeId = "external:user-42"
            };
            var column = CreateColumn("Todo");
            column.Tasks.Add(task);
            var kanban = new FlowKanban { AutoSaveAfterEdits = false };
            kanban.Board.Columns.Add(column);

            try
            {
                kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter();
                await kanban.RefreshAssigneesAsync(TestContext.Current.CancellationToken);

                Assert.Equal("external:user-42", task.AssigneeId);
                Assert.Equal("External User", task.Assignee);
                var unresolved = Assert.Single(kanban.AssigneeFilterOptions);
                Assert.Equal(task.AssigneeId, unresolved.Id);
                Assert.False(unresolved.IsResolved);
                Assert.Contains("External User", unresolved.DisplayName, StringComparison.Ordinal);

                kanban.AssigneeAdapter = null;
                await kanban.RefreshAssigneesAsync(TestContext.Current.CancellationToken);

                Assert.Equal("external:user-42", task.AssigneeId);
                Assert.Equal("External User", task.Assignee);
                unresolved = Assert.Single(kanban.AssigneeFilterOptions);
                Assert.False(unresolved.IsResolved);
            }
            finally
            {
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_AssigneeAdapterChangesOffUiThread_BoardRefreshesOnDispatcher()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            Window? window = null;

            try
            {
                var adapter = new FlowKanbanAssigneeAdapter(
                [
                    new FlowKanbanAssignee("external:first", "First User")
                ]);
                var kanban = new FlowKanban
                {
                    AutoSaveAfterEdits = false,
                    AssigneeAdapter = adapter
                };
                window = ShowKanban(kanban);
                await kanban.RefreshAssigneesAsync(TestContext.Current.CancellationToken);

                await Task.Run(() => adapter.SetAssignees(
                [
                    new FlowKanbanAssignee("external:second", "Second User")
                ]), TestContext.Current.CancellationToken);
                for (var attempt = 0; attempt < 20; attempt++)
                {
                    Dispatcher.UIThread.RunJobs();
                    if (kanban.AssigneeFilterOptions.SingleOrDefault()?.Id == "external:second")
                        break;
                    await Task.Delay(10, TestContext.Current.CancellationToken);
                }

                var assignee = Assert.Single(kanban.AssigneeFilterOptions);
                Assert.Equal("external:second", assignee.Id);
            }
            finally
            {
                window?.Close();
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [Fact]
        public async Task When_CallbackAdapterLoadsCompleteOutOfOrder_ResolveUsesNewestSnapshot()
        {
            var loader = new ControlledAssigneeLoader();
            var adapter = new FlowKanbanAssigneeAdapter(loader.LoadAsync);

            var olderLoad = adapter.GetAssigneesAsync(TestContext.Current.CancellationToken);
            var newerLoad = adapter.GetAssigneesAsync(TestContext.Current.CancellationToken);

            loader.Second.SetResult(
            [
                new FlowKanbanAssignee("person-1", "New User", roles: ["New Role"])
            ]);
            var newerSnapshot = await newerLoad;
            Assert.Equal("New User", Assert.Single(newerSnapshot).DisplayName);

            loader.First.SetResult(
            [
                new FlowKanbanAssignee("person-1", "Old User", roles: ["Old Role"])
            ]);
            await olderLoad;

            var resolved = await adapter.ResolveAssigneeAsync(
                "person-1",
                TestContext.Current.CancellationToken);
            Assert.NotNull(resolved);
            Assert.Equal("New User", resolved.DisplayName);
            Assert.Equal(["New Role"], resolved.Roles);
        }

        [AvaloniaFact]
        public async Task When_AssigneeResolverThrows_FailureIsReportedAndAssignmentIsPreserved()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var task = new FlowTask
            {
                Title = "Assigned task",
                Assignee = "External User",
                AssigneeId = "external:subject-1"
            };
            var column = CreateColumn("Todo");
            column.Tasks.Add(task);
            var kanban = new FlowKanban { AutoSaveAfterEdits = false };
            kanban.Board.Columns.Add(column);
            FlowKanbanAssigneeAdapterFailedEventArgs? failure = null;
            kanban.AssigneeAdapterFailed += (_, args) => failure = args;

            try
            {
                kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(
                    static (_, _) => Task.FromException<FlowKanbanAssignee?>(
                        new IOException("Assignee lookup unavailable.")));
                await kanban.RefreshAssigneesAsync(TestContext.Current.CancellationToken);

                Assert.Equal("external:subject-1", task.AssigneeId);
                Assert.Equal("External User", task.Assignee);
                var reported = Assert.IsType<FlowKanbanAssigneeAdapterFailedEventArgs>(failure);
                Assert.Equal(FlowKanbanAssigneeAdapterOperation.ResolveAssignee, reported.Operation);
                Assert.Equal(task.AssigneeId, reported.AssigneeId);
                Assert.IsType<IOException>(reported.Exception);
                var option = Assert.Single(kanban.AssigneeFilterOptions);
                Assert.Equal(task.AssigneeId, option.Id);
                Assert.False(option.IsResolved);
            }
            finally
            {
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_AssigneeAdapterLoadsCompleteOutOfOrder_OldResultCannotOverwriteNewMetadata()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var task = new FlowTask { Title = "Assigned task", AssigneeId = "person-1" };
            var column = CreateColumn("Todo");
            column.Tasks.Add(task);
            var kanban = new FlowKanban { AutoSaveAfterEdits = false };
            kanban.Board.Columns.Add(column);
            var oldCompletion = new TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var newCompletion = new TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>>(
                TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(
                    _ => oldCompletion.Task);
                var oldLoad = kanban.RefreshAssigneesAsync(TestContext.Current.CancellationToken);
                kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(
                    _ => newCompletion.Task);
                var newLoad = kanban.RefreshAssigneesAsync(TestContext.Current.CancellationToken);

                newCompletion.SetResult(
                [
                    new FlowKanbanAssignee("person-1", "New User", roles: ["New Role"])
                ]);
                await newLoad;
                Assert.Equal("New User", task.Assignee);
                Assert.Equal(["New Role"], task.AssigneeRoles);

                oldCompletion.SetResult(
                [
                    new FlowKanbanAssignee("person-1", "Old User", roles: ["Old Role"])
                ]);
                await oldLoad;
                Assert.Equal("New User", task.Assignee);
                Assert.Equal(["New Role"], task.AssigneeRoles);
            }
            finally
            {
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_AssigneeAdapterLoadThrows_FailureIsReportedAndMetadataIsRetained()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var task = new FlowTask { Title = "Assigned task", AssigneeId = "person-1" };
            var column = CreateColumn("Todo");
            column.Tasks.Add(task);
            var kanban = new FlowKanban
            {
                AutoSaveAfterEdits = false,
                AssigneeAdapter = new FlowKanbanAssigneeAdapter(
                [
                    new FlowKanbanAssignee("person-1", "Known User", roles: ["Reviewer"])
                ])
            };
            kanban.Board.Columns.Add(column);
            FlowKanbanAssigneeAdapterFailedEventArgs? failure = null;
            kanban.AssigneeAdapterFailed += (_, args) => failure = args;

            try
            {
                await kanban.RefreshAssigneesAsync(TestContext.Current.CancellationToken);
                kanban.AssigneeAdapter = new FlowKanbanAssigneeAdapter(
                    static _ => Task.FromException<IReadOnlyList<FlowKanbanAssignee>>(
                        new InvalidOperationException("Assignee load failed.")));
                await kanban.RefreshAssigneesAsync(TestContext.Current.CancellationToken);

                var reported = Assert.IsType<FlowKanbanAssigneeAdapterFailedEventArgs>(failure);
                Assert.Equal(FlowKanbanAssigneeAdapterOperation.LoadAssignees, reported.Operation);
                Assert.IsType<InvalidOperationException>(reported.Exception);
                Assert.Equal("Known User", task.Assignee);
                Assert.Equal(["Reviewer"], task.AssigneeRoles);
            }
            finally
            {
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public async Task When_AssigneeAdapterCompletesAfterUnload_ControlMetadataIsNotChanged()
        {
            var previousStorage = StateStorageProvider.Instance;
            StateStorageProvider.Configure(new InMemoryStateStorage());
            var completion = new TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var task = new FlowTask
            {
                Title = "Assigned task",
                Assignee = "Original User",
                AssigneeId = "person-1"
            };
            var column = CreateColumn("Todo");
            column.Tasks.Add(task);
            var kanban = new FlowKanban
            {
                AutoSaveAfterEdits = false,
                AssigneeAdapter = new FlowKanbanAssigneeAdapter(_ => completion.Task)
            };
            kanban.Board.Columns.Add(column);
            var window = ShowKanban(kanban);

            try
            {
                var refresh = kanban.RefreshAssigneesAsync(TestContext.Current.CancellationToken);
                window.Close();

                completion.SetResult(
                [
                    new FlowKanbanAssignee("person-1", "Late User", roles: ["Late Role"])
                ]);
                await refresh;
                Dispatcher.UIThread.RunJobs();

                Assert.Equal("Original User", task.Assignee);
                Assert.Empty(task.AssigneeRoles);
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
        public void When_LoadingSettings_StorageThrows_ReturnsConcreteError()
        {
            var previousStorage = StateStorageProvider.Instance;
            var expected = new IOException("Settings load failed.");
            StateStorageProvider.Configure(new FailingStateStorage(StorageFailure.Load, expected));

            try
            {
                var kanban = new FlowKanban();

                Assert.False(kanban.TryLoadSettings(forceReload: true, out var error));
                Assert.Same(expected, error);
            }
            finally
            {
                StateStorageProvider.Configure(previousStorage);
            }
        }

        [AvaloniaFact]
        public void When_SavingSettings_StorageThrows_ReturnsConcreteError()
        {
            var previousStorage = StateStorageProvider.Instance;
            var expected = new IOException("Settings save failed.");
            StateStorageProvider.Configure(new FailingStateStorage(StorageFailure.Save, expected));

            try
            {
                var kanban = new FlowKanban();

                Assert.False(kanban.TrySaveSettings(out var error));
                Assert.Same(expected, error);
            }
            finally
            {
                StateStorageProvider.Configure(previousStorage);
            }
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
            Assert.NotNull(root);
            // Without the removal, a renamed property would turn this into a normal load.
            Assert.True(root.Remove("schemaVersion"), "the board JSON has no schemaVersion to remove");

            var migrated = FlowKanbanMigration.MigrateFromJson(root.ToJsonString());

            Assert.NotNull(migrated);
            Assert.Equal(board.Id, migrated!.Id);
            Assert.Equal(board.Title, migrated.Title);
            Assert.Single(migrated.Columns);
            Assert.Equal("Backlog", migrated.Columns[0].Title);
            Assert.Single(migrated.Columns[0].Tasks);
        }

        private static FlowTask CreateTask(string title) => new() { Title = title };

        private static async Task WaitForAutoSaveAsync(FlowKanban kanban)
        {
            Dispatcher.UIThread.RunJobs();
            var timer = kanban.AutoSaveTimer;
            if (timer?.IsEnabled != true) return;
            var completed = new TaskCompletionSource();
            EventHandler tick = (_, _) => completed.TrySetResult();
            timer.Tick += tick;
            try { await completed.Task.WaitAsync(TimeSpan.FromSeconds(5)); }
            finally { timer.Tick -= tick; }
            Dispatcher.UIThread.RunJobs();
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

        private static void LoadSettings(FlowKanban kanban)
        {
            Assert.True(
                kanban.TryLoadSettings(forceReload: true, out var error),
                error?.ToString());
        }

        // Without auto-save: a board that never enters a window keeps an edit waiting for the timer, which fires in a
        // later test and writes this board's settings into the storage that test set up.
        private static FlowKanban CreateKanban(params FlowKanbanColumnData[] columns) =>
            new() { AutoSaveAfterEdits = false, Board = CreateBoard(columns) };

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

        // The board opens on its home view once it is shown, so a test of its columns switches to the board again.
        private static void ShowBoardView(FlowKanban kanban)
        {
            kanban.CurrentView = FlowKanbanView.Board;
            Dispatcher.UIThread.RunJobs();
            kanban.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
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
        }

        private sealed class ControlledAssigneeLoader
        {
            private int _requestCount;

            public TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>> First { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public TaskCompletionSource<IReadOnlyList<FlowKanbanAssignee>> Second { get; } =
                new(TaskCreationOptions.RunContinuationsAsynchronously);

            public Task<IReadOnlyList<FlowKanbanAssignee>> LoadAsync(
                CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Interlocked.Increment(ref _requestCount) switch
                {
                    1 => First.Task,
                    2 => Second.Task,
                    _ => Task.FromException<IReadOnlyList<FlowKanbanAssignee>>(
                        new InvalidOperationException("Unexpected assignee load."))
                };
            }
        }

        // Counts the boards it is asked to save; with an error, every save fails with it.
        private sealed class CountingBoardStore(Exception? saveError = null) : IBoardStore
        {
            public int SaveCount => SavedBoards.Count;

            public List<FlowKanbanData> SavedBoards { get; } = new();

            public IReadOnlyList<FlowBoardMetadata> ListBoards() => Array.Empty<FlowBoardMetadata>();

            public bool TryLoadBoard(string boardId, out FlowKanbanData? board, out Exception? error)
            {
                board = null;
                error = null;
                return false;
            }

            public bool TrySaveBoard(FlowKanbanData board, out Exception? error)
            {
                SavedBoards.Add(board);
                error = saveError;
                return saveError is null;
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
                SavedBoards.Clear();
            }
        }

        private enum StorageFailure
        {
            Load,
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

            public IReadOnlyList<string> LoadLines(string key)
            {
                if (_failure == StorageFailure.Load)
                    throw _exception;

                return _inner.LoadLines(key);
            }

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
