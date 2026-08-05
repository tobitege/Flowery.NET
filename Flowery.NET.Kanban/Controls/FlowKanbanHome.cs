using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.Theming;

namespace Flowery.NET.Kanban.Controls
{
    /// <summary>
    /// Home screen view for listing and managing available Kanban boards.
    /// </summary>
    public partial class FlowKanbanHome : FlowKanbanContentControl
    {
        static FlowKanbanHome()
        {
            LocalizationProperty.Changed.AddClassHandler<FlowKanbanHome>(OnLocalizationChanged);
            ShowWelcomeMessageProperty.Changed.AddClassHandler<FlowKanbanHome>(OnWelcomeMessageChanged);
            WelcomeMessageTitleProperty.Changed.AddClassHandler<FlowKanbanHome>(OnWelcomeMessageChanged);
            WelcomeMessageSubtitleProperty.Changed.AddClassHandler<FlowKanbanHome>(OnWelcomeMessageChanged);
            BoardsProperty.Changed.AddClassHandler<FlowKanbanHome>(OnBoardsChanged);
            SearchTextProperty.Changed.AddClassHandler<FlowKanbanHome>(OnSearchTextChanged);
            SortModeProperty.Changed.AddClassHandler<FlowKanbanHome>(OnSortModeChanged);
            SortModeIndexProperty.Changed.AddClassHandler<FlowKanbanHome>(OnSortModeIndexChanged);
        }

        private readonly HashSet<FlowBoardMetadata> _trackedBoards = new();
        private ItemsControl? _boardList;
        private bool _isSyncingSortMode;
        private bool _isLocalizationSubscribed;
        private static readonly FlowKanbanHomeSortMode[] SortModeOrder =
        [
            FlowKanbanHomeSortMode.RecentlyModified,
            FlowKanbanHomeSortMode.RecentlyModifiedAscending,
            FlowKanbanHomeSortMode.NameAscending,
            FlowKanbanHomeSortMode.NameDescending
        ];

        public FlowKanbanHome()
        {
            EnsureFilteredBoards();
            UpdateWelcomeText();
            Loaded += OnHomeLoaded;
            Unloaded += OnHomeUnloaded;
        }

        protected override void OnThemeChanged(string themeName)
        {
            base.OnThemeChanged(themeName);
            Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush");
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_boardList != null)
            {
                _boardList.ContainerPrepared -= OnBoardContainerPrepared;
                _boardList.ContainerClearing -= OnBoardContainerClearing;
            }

            _boardList = e.NameScope.Find<ItemsControl>("PART_BoardGrid");
            if (_boardList != null)
            {
                _boardList.ContainerPrepared += OnBoardContainerPrepared;
                _boardList.ContainerClearing += OnBoardContainerClearing;
            }
        }

        #region Localization
        public static readonly StyledProperty<FloweryLocalization> LocalizationProperty =
            AvaloniaProperty.Register<FlowKanbanHome, FloweryLocalization>(
                                nameof(Localization),
                                FloweryLocalization.Instance);

        public FloweryLocalization Localization
        {
            get => (FloweryLocalization)GetValue(LocalizationProperty);
            set => SetValue(LocalizationProperty, value);
        }
        #endregion

        #region WelcomeMessage
        public static readonly StyledProperty<bool> ShowWelcomeMessageProperty =
            AvaloniaProperty.Register<FlowKanbanHome, bool>(
                                nameof(ShowWelcomeMessage),
                                true);

        public bool ShowWelcomeMessage
        {
            get => (bool)GetValue(ShowWelcomeMessageProperty);
            set => SetValue(ShowWelcomeMessageProperty, value);
        }

        public static readonly StyledProperty<string> WelcomeMessageTitleProperty =
            AvaloniaProperty.Register<FlowKanbanHome, string>(
                                nameof(WelcomeMessageTitle),
                                string.Empty);

        public string WelcomeMessageTitle
        {
            get => (string)GetValue(WelcomeMessageTitleProperty);
            set => SetValue(WelcomeMessageTitleProperty, value ?? string.Empty);
        }

        public static readonly StyledProperty<string> WelcomeMessageSubtitleProperty =
            AvaloniaProperty.Register<FlowKanbanHome, string>(
                                nameof(WelcomeMessageSubtitle),
                                string.Empty);

        public string WelcomeMessageSubtitle
        {
            get => (string)GetValue(WelcomeMessageSubtitleProperty);
            set => SetValue(WelcomeMessageSubtitleProperty, value ?? string.Empty);
        }

        public static readonly StyledProperty<bool> IsWelcomeMessageVisibleProperty =
            AvaloniaProperty.Register<FlowKanbanHome, bool>(
                nameof(IsWelcomeMessageVisible),
                false);

        public bool IsWelcomeMessageVisible
        {
            get => (bool)GetValue(IsWelcomeMessageVisibleProperty);
            private set => SetValue(IsWelcomeMessageVisibleProperty, value);
        }

        public static readonly StyledProperty<string> WelcomeTitleDisplayProperty =
            AvaloniaProperty.Register<FlowKanbanHome, string>(
                nameof(WelcomeTitleDisplay),
                string.Empty);

        public string WelcomeTitleDisplay
        {
            get => (string)GetValue(WelcomeTitleDisplayProperty);
            private set => SetValue(WelcomeTitleDisplayProperty, value ?? string.Empty);
        }

        public static readonly StyledProperty<string> WelcomeSubtitleDisplayProperty =
            AvaloniaProperty.Register<FlowKanbanHome, string>(
                nameof(WelcomeSubtitleDisplay),
                string.Empty);

        public string WelcomeSubtitleDisplay
        {
            get => (string)GetValue(WelcomeSubtitleDisplayProperty);
            private set => SetValue(WelcomeSubtitleDisplayProperty, value ?? string.Empty);
        }

        private static void OnWelcomeMessageChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanHome home)
            {
                home.UpdateWelcomeText();
            }
        }

        private static void OnLocalizationChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanHome home)
            {
                home.UpdateWelcomeText();
            }
        }
        #endregion

        #region Boards
        public static readonly StyledProperty<ObservableCollection<FlowBoardMetadata>> BoardsProperty =
            AvaloniaProperty.Register<FlowKanbanHome, ObservableCollection<FlowBoardMetadata>>(
                                nameof(Boards),
                                default!);

        public ObservableCollection<FlowBoardMetadata> Boards
        {
            get
            {
                if (GetValue(BoardsProperty) is not ObservableCollection<FlowBoardMetadata> boards)
                {
                    boards = new ObservableCollection<FlowBoardMetadata>();
                    SetValue(BoardsProperty, boards);
                }

                return boards;
            }
            set => SetValue(BoardsProperty, value);
        }

        private static void OnBoardsChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanHome home)
            {
                home.AttachBoards(e.OldValue as ObservableCollection<FlowBoardMetadata>,
                    e.NewValue as ObservableCollection<FlowBoardMetadata>);
            }
        }
        #endregion

        #region FilteredBoards
        public static readonly StyledProperty<ObservableCollection<FlowBoardMetadata>> FilteredBoardsProperty =
            AvaloniaProperty.Register<FlowKanbanHome, ObservableCollection<FlowBoardMetadata>>(
                nameof(FilteredBoards),
                default!);

        public ObservableCollection<FlowBoardMetadata> FilteredBoards
        {
            get => EnsureFilteredBoards();
            private set => SetValue(FilteredBoardsProperty, value);
        }
        #endregion

        #region SearchText
        public static readonly StyledProperty<string> SearchTextProperty =
            AvaloniaProperty.Register<FlowKanbanHome, string>(
                                nameof(SearchText),
                                string.Empty);

        public string SearchText
        {
            get => (string)GetValue(SearchTextProperty);
            set => SetValue(SearchTextProperty, value);
        }

        private static void OnSearchTextChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanHome home)
            {
                home.UpdateFilteredBoards();
            }
        }
        #endregion

        #region SortMode
        public static readonly StyledProperty<FlowKanbanHomeSortMode> SortModeProperty =
            AvaloniaProperty.Register<FlowKanbanHome, FlowKanbanHomeSortMode>(
                                nameof(SortMode),
                                FlowKanbanHomeSortMode.RecentlyModified);

        public FlowKanbanHomeSortMode SortMode
        {
            get => (FlowKanbanHomeSortMode)GetValue(SortModeProperty);
            set => SetValue(SortModeProperty, value);
        }

        private static void OnSortModeChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanHome home)
            {
                home.SyncSortModeIndex(e.GetNewValue<FlowKanbanHomeSortMode>());
                home.UpdateFilteredBoards();
            }
        }
        #endregion

        #region SortModeIndex
        public static readonly StyledProperty<int> SortModeIndexProperty =
            AvaloniaProperty.Register<FlowKanbanHome, int>(
                                nameof(SortModeIndex),
                                0);

        public int SortModeIndex
        {
            get => (int)GetValue(SortModeIndexProperty);
            set => SetValue(SortModeIndexProperty, value);
        }

        private static void OnSortModeIndexChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is FlowKanbanHome home)
            {
                home.ApplySortModeFromIndex(e.GetNewValue<int>());
            }
        }
        #endregion

        #region HasBoards
        public static readonly StyledProperty<bool> HasBoardsProperty =
            AvaloniaProperty.Register<FlowKanbanHome, bool>(
                nameof(HasBoards),
                false);

        public bool HasBoards
        {
            get => (bool)GetValue(HasBoardsProperty);
            private set => SetValue(HasBoardsProperty, value);
        }
        #endregion

        #region HasAnyBoards
        public static readonly StyledProperty<bool> HasAnyBoardsProperty =
            AvaloniaProperty.Register<FlowKanbanHome, bool>(
                nameof(HasAnyBoards),
                false);

        public bool HasAnyBoards
        {
            get => (bool)GetValue(HasAnyBoardsProperty);
            private set => SetValue(HasAnyBoardsProperty, value);
        }
        #endregion

        #region IsEmptyStateVisible
        public static readonly StyledProperty<bool> IsEmptyStateVisibleProperty =
            AvaloniaProperty.Register<FlowKanbanHome, bool>(
                nameof(IsEmptyStateVisible),
                true);

        public bool IsEmptyStateVisible
        {
            get => (bool)GetValue(IsEmptyStateVisibleProperty);
            private set => SetValue(IsEmptyStateVisibleProperty, value);
        }
        #endregion

        #region Commands
        public static readonly StyledProperty<ICommand?> OpenBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanbanHome, ICommand?>(
                nameof(OpenBoardCommand),
                default!);

        public ICommand? OpenBoardCommand
        {
            get => (ICommand?)GetValue(OpenBoardCommandProperty);
            set => SetValue(OpenBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand?> CreateBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanbanHome, ICommand?>(
                nameof(CreateBoardCommand),
                default!);

        public ICommand? CreateBoardCommand
        {
            get => (ICommand?)GetValue(CreateBoardCommandProperty);
            set => SetValue(CreateBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand?> CreateDemoBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanbanHome, ICommand?>(
                nameof(CreateDemoBoardCommand),
                default!);

        public ICommand? CreateDemoBoardCommand
        {
            get => (ICommand?)GetValue(CreateDemoBoardCommandProperty);
            set => SetValue(CreateDemoBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand?> RenameBoardHomeCommandProperty =
            AvaloniaProperty.Register<FlowKanbanHome, ICommand?>(
                nameof(RenameBoardHomeCommand),
                default!);

        public ICommand? RenameBoardHomeCommand
        {
            get => (ICommand?)GetValue(RenameBoardHomeCommandProperty);
            set => SetValue(RenameBoardHomeCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand?> DeleteBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanbanHome, ICommand?>(
                nameof(DeleteBoardCommand),
                default!);

        public ICommand? DeleteBoardCommand
        {
            get => (ICommand?)GetValue(DeleteBoardCommandProperty);
            set => SetValue(DeleteBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand?> DuplicateBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanbanHome, ICommand?>(
                nameof(DuplicateBoardCommand),
                default!);

        public ICommand? DuplicateBoardCommand
        {
            get => (ICommand?)GetValue(DuplicateBoardCommandProperty);
            set => SetValue(DuplicateBoardCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand?> ExportBoardCommandProperty =
            AvaloniaProperty.Register<FlowKanbanHome, ICommand?>(
                nameof(ExportBoardCommand),
                default!);

        public ICommand? ExportBoardCommand
        {
            get => (ICommand?)GetValue(ExportBoardCommandProperty);
            set => SetValue(ExportBoardCommandProperty, value);
        }
        #endregion

        private ObservableCollection<FlowBoardMetadata> EnsureFilteredBoards()
        {
            if (GetValue(FilteredBoardsProperty) is not ObservableCollection<FlowBoardMetadata> boards)
            {
                boards = new ObservableCollection<FlowBoardMetadata>();
                boards.CollectionChanged += OnFilteredBoardsCollectionChanged;
                SetValue(FilteredBoardsProperty, boards);
            }

            return boards;
        }

        private void AttachBoards(ObservableCollection<FlowBoardMetadata>? oldBoards, ObservableCollection<FlowBoardMetadata>? newBoards)
        {
            if (oldBoards != null)
            {
                oldBoards.CollectionChanged -= OnBoardsCollectionChanged;
                DetachBoardItems(oldBoards);
            }

            if (newBoards != null)
            {
                newBoards.CollectionChanged += OnBoardsCollectionChanged;
                AttachBoardItems(newBoards);
            }

            UpdateFilteredBoards();
        }

        private void OnBoardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.OldItems != null)
            {
                foreach (FlowBoardMetadata board in e.OldItems)
                {
                    DetachBoardItem(board);
                }
            }

            if (e.NewItems != null)
            {
                foreach (FlowBoardMetadata board in e.NewItems)
                {
                    AttachBoardItem(board);
                }
            }

            UpdateFilteredBoards();
        }

        private void AttachBoardItems(IEnumerable<FlowBoardMetadata> boards)
        {
            foreach (var board in boards)
            {
                AttachBoardItem(board);
            }
        }

        private void DetachBoardItems(IEnumerable<FlowBoardMetadata> boards)
        {
            foreach (var board in boards)
            {
                DetachBoardItem(board);
            }
        }

        private void AttachBoardItem(FlowBoardMetadata board)
        {
            if (_trackedBoards.Add(board))
            {
                board.PropertyChanged += OnBoardPropertyChanged;
            }
        }

        private void DetachBoardItem(FlowBoardMetadata board)
        {
            if (_trackedBoards.Remove(board))
            {
                board.PropertyChanged -= OnBoardPropertyChanged;
            }
        }

        private void OnBoardPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (string.Equals(e.PropertyName, nameof(FlowBoardMetadata.Title), StringComparison.Ordinal) ||
                string.Equals(e.PropertyName, nameof(FlowBoardMetadata.LastModified), StringComparison.Ordinal))
            {
                UpdateFilteredBoards();
            }
        }

        private void OnFilteredBoardsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            UpdateBoardVisibilityState(FilteredBoards.Count);
        }

        private void UpdateFilteredBoards()
        {
            var filtered = EnsureFilteredBoards();
            filtered.CollectionChanged -= OnFilteredBoardsCollectionChanged;

            try
            {
                filtered.Clear();

                var boards = Boards ?? new ObservableCollection<FlowBoardMetadata>();
                var filteredItems = ApplyFilteringAndSorting(boards);
                foreach (var board in filteredItems)
                {
                    filtered.Add(board);
                }
            }
            finally
            {
                filtered.CollectionChanged += OnFilteredBoardsCollectionChanged;
            }

            UpdateBoardVisibilityState(filtered.Count);
        }

        private IReadOnlyList<FlowBoardMetadata> ApplyFilteringAndSorting(IEnumerable<FlowBoardMetadata> boards)
        {
            var query = boards ?? Enumerable.Empty<FlowBoardMetadata>();

            if (!string.IsNullOrWhiteSpace(SearchText))
            {
                var filter = SearchText.Trim();
                query = query.Where(board => board.Title.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }

            query = SortMode switch
            {
                FlowKanbanHomeSortMode.NameAscending => query.OrderBy(b => b.Title, StringComparer.OrdinalIgnoreCase),
                FlowKanbanHomeSortMode.NameDescending => query.OrderByDescending(b => b.Title, StringComparer.OrdinalIgnoreCase),
                FlowKanbanHomeSortMode.RecentlyModifiedAscending => query.OrderBy(b => b.LastModified),
                _ => query.OrderByDescending(b => b.LastModified)
            };

            return query.ToList();
        }

        private void UpdateBoardVisibilityState(int count)
        {
            var hasBoards = count > 0;
            HasAnyBoards = (Boards?.Count ?? 0) > 0;
            HasBoards = hasBoards;
            IsEmptyStateVisible = !hasBoards;
        }

        private void ApplySortModeFromIndex(int index)
        {
            if (_isSyncingSortMode)
                return;

            var mode = GetSortModeFromIndex(index);

            if (SortMode != mode)
            {
                SortMode = mode;
            }
        }

        private void SyncSortModeIndex(FlowKanbanHomeSortMode mode)
        {
            if (_isSyncingSortMode)
                return;

            _isSyncingSortMode = true;
            try
            {
                var targetIndex = GetIndexFromSortMode(mode);
                if (SortModeIndex != targetIndex)
                {
                    SortModeIndex = targetIndex;
                }
            }
            finally
            {
                _isSyncingSortMode = false;
            }
        }

        private static FlowKanbanHomeSortMode GetSortModeFromIndex(int index)
        {
            return index >= 0 && index < SortModeOrder.Length
                ? SortModeOrder[index]
                : FlowKanbanHomeSortMode.RecentlyModified;
        }

        private static int GetIndexFromSortMode(FlowKanbanHomeSortMode mode)
        {
            var index = Array.IndexOf(SortModeOrder, mode);
            return index >= 0 ? index : 0;
        }

        private void OnBoardContainerPrepared(object? sender, ContainerPreparedEventArgs args)
        {
            if (args.Container is not Control element || element.DataContext is not FlowBoardMetadata board)
                return;

            if (FlowKanbanVisualTree.FindNamedDescendant<Control>(element, "PART_BoardOpenSurface") is { } openSurface)
            {
                openSurface.Tapped -= OnBoardSurfaceTapped;
                openSurface.Tapped += OnBoardSurfaceTapped;
            }

            if (FlowKanbanVisualTree.FindNamedDescendant<DaisyButton>(element, "PART_BoardMenuButton") is { } menuButton)
            {
                if (menuButton.Flyout is MenuFlyout flyout)
                {
                    foreach (var item in flyout.Items)
                    {
                        if (item is MenuItem menuItem)
                        {
                            if (menuItem.Tag is string localizationKey)
                            {
                                menuItem.Header = Localization[localizationKey];
                            }

                            menuItem.CommandParameter = board;
                            menuItem.Command = ResolveMenuCommand(menuItem);
                        }
                    }
                }
            }
        }

        private void OnBoardContainerClearing(object? sender, ContainerClearingEventArgs args)
        {
            if (args.Container is not Control element)
                return;

            if (FlowKanbanVisualTree.FindNamedDescendant<Control>(element, "PART_BoardOpenSurface") is { } openSurface)
            {
                openSurface.Tapped -= OnBoardSurfaceTapped;
            }

            if (FlowKanbanVisualTree.FindNamedDescendant<DaisyButton>(element, "PART_BoardMenuButton") is { } menuButton &&
                menuButton.Flyout is MenuFlyout flyout)
            {
                foreach (var item in flyout.Items)
                {
                    if (item is MenuItem menuItem)
                    {
                        menuItem.Command = null;
                        menuItem.CommandParameter = null;
                    }
                }
            }
        }

        private ICommand? ResolveMenuCommand(MenuItem menuItem)
        {
            return (menuItem.Tag as string) switch
            {
                "Kanban_Home_Open" => OpenBoardCommand,
                "Kanban_Home_Rename" => RenameBoardHomeCommand,
                "Kanban_Home_Duplicate" => DuplicateBoardCommand,
                "Kanban_Home_Export" => ExportBoardCommand,
                "Kanban_Home_Delete" => DeleteBoardCommand,
                _ => null
            };
        }

        private void OnBoardSurfaceTapped(object? sender, TappedEventArgs e)
        {
            if (IsMenuButtonSource(e.Source))
                return;

            if (sender is Control element &&
                element.DataContext is FlowBoardMetadata board &&
                OpenBoardCommand?.CanExecute(board) == true)
            {
                OpenBoardCommand.Execute(board);
            }
        }

        private static bool IsMenuButtonSource(object? source)
        {
            var current = source as Visual;
            while (current is not null)
            {
                if (current is Control element &&
                    string.Equals(element.Name, "PART_BoardMenuButton", StringComparison.Ordinal))
                {
                    return true;
                }

                current = current.GetVisualParent();
            }

            return false;
        }

        private void OnHomeLoaded(object? sender, RoutedEventArgs e)
        {
            if (!_isLocalizationSubscribed)
            {
                FloweryLocalization.CultureChanged += OnLocalizationCultureChanged;
                _isLocalizationSubscribed = true;
            }

            UpdateWelcomeText();
        }

        private void OnHomeUnloaded(object? sender, RoutedEventArgs e)
        {
            if (_isLocalizationSubscribed)
            {
                FloweryLocalization.CultureChanged -= OnLocalizationCultureChanged;
                _isLocalizationSubscribed = false;
            }
        }

        private void OnLocalizationCultureChanged(object? sender, CultureInfo culture)
        {
            UpdateWelcomeText();
        }

        private void UpdateWelcomeText()
        {
            var title = WelcomeMessageTitle;
            var subtitle = WelcomeMessageSubtitle;
            WelcomeTitleDisplay = string.IsNullOrWhiteSpace(title) ? string.Empty : title;
            WelcomeSubtitleDisplay = string.IsNullOrWhiteSpace(subtitle) ? string.Empty : subtitle;
            IsWelcomeMessageVisible = ShowWelcomeMessage &&
                                      (!string.IsNullOrWhiteSpace(WelcomeTitleDisplay) ||
                                       !string.IsNullOrWhiteSpace(WelcomeSubtitleDisplay));
        }

    }

    public enum FlowKanbanHomeSortMode
    {
        RecentlyModified = 0,
        NameAscending = 1,
        NameDescending = 2,
        RecentlyModifiedAscending = 3
    }
}
