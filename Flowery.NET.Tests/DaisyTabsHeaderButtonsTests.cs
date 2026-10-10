using System.Collections.ObjectModel;
using System.Windows.Input;
using System.ComponentModel;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyTabsHeaderButtonsTests
    {
        [AvaloniaFact]
        public void Defaults_HideButtons_AndKeepManageEntryAvailable()
        {
            using var host = new TestHost(new DaisyTabsHeaderButtons());
            Assert.False(host.Previous.IsVisible);
            Assert.False(host.Next.IsVisible);
            Assert.False(host.ViewMenuButton.IsVisible);
            Assert.True(host.Control.ShowManageViewsEntry);
            Assert.Equal(FlowerySizeManager.CurrentSize, host.Control.Size);
            Assert.All(host.Control.GetVisualDescendants().OfType<DaisyButton>(), button =>
            {
                Assert.Equal(DaisyButtonVariant.Default, button.Variant);
                Assert.Equal(host.Control.Size, button.Size);
                Assert.Equal(DaisyButtonShape.Square, button.Shape);
            });
            Assert.Null(host.Control.Views);
            Assert.Null(host.Control.ActiveViewId);
        }

        [AvaloniaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void ViewMenu_HasExpectedEntriesAndCheckMark(bool showManage)
        {
            using var host = new TestHost(CreateControl());
            host.Control.ShowManageViewsEntry = showManage;
            host.OpenMenu();
            var items = host.Menu.Items.ToArray();
            Assert.Equal(showManage ? 5 : 3, items.Length);
            var views = items.Take(3).Cast<MenuItem>().ToArray();
            Assert.Equal(new[] { "Overview", "Details", "Archive" }, views.Select(item => item.Header));
            Assert.Equal(new[] { false, true, false }, views.Select(item => item.IsChecked));
            Assert.All(views, item => Assert.Equal(MenuItemToggleType.CheckBox, item.ToggleType));
            if (showManage)
            {
                Assert.IsType<Separator>(items[3]);
                Assert.IsType<MenuItem>(items[4]);
            }
            else
            {
                Assert.DoesNotContain(items, item => item is Separator);
            }
        }

        [AvaloniaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void ViewSelectionAndManagement_RaiseEventsAndExecuteCommands(bool useCommands)
        {
            var selected = new List<DaisyTabViewEventArgs>();
            var manageRequests = 0;
            var viewCommand = new RecordingCommand();
            var manageCommand = new RecordingCommand();
            var control = CreateControl();
            control.ViewSelected += (_, args) => selected.Add(args);
            control.ManageViewsRequested += (_, _) => manageRequests++;
            if (useCommands)
            {
                control.ViewSelectedCommand = viewCommand;
                control.ManageViewsCommand = manageCommand;
            }
            using var host = new TestHost(control);
            host.OpenMenu();
            Assert.IsType<MenuItem>(host.Menu.Items[2]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(3, Assert.Single(selected).Id);
            Assert.Equal("Archive", selected[0].Name);
            Assert.Equal(2, control.ActiveViewId);
            Assert.Equal(3, control.Views!.Count);
            host.Menu.Hide();
            host.OpenMenu();
            Assert.IsType<MenuItem>(host.Menu.Items[4]).RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));
            Assert.Equal(1, manageRequests);
            if (useCommands)
            {
                Assert.Equal(3, Assert.Single(viewCommand.Parameters));
                Assert.Null(Assert.Single(manageCommand.Parameters));
            }
        }

        [AvaloniaFact]
        public void ViewMenu_RebuildsFromMutableListAndCurrentPropertiesOnEachOpening()
        {
            var control = CreateControl();
            using var host = new TestHost(control);
            host.OpenMenu();
            var firstItem = host.Menu.Items[0];
            host.Menu.Hide();
            control.Views!.RemoveAt(0);
            control.Views.Add(new DaisyTabView { Id = 4, Name = "Recent" });
            control.ActiveViewId = 4;
            control.ShowManageViewsEntry = false;
            host.OpenMenu();
            var items = host.Menu.Items.Cast<MenuItem>().ToArray();
            Assert.Equal(3, items.Length);
            Assert.DoesNotContain(firstItem, host.Menu.Items);
            Assert.Equal(new[] { "Details", "Archive", "Recent" }, items.Select(item => item.Header));
            Assert.Equal(new[] { false, false, true }, items.Select(item => item.IsChecked));
        }

        [AvaloniaFact]
        public void EmptyMenu_DisablesButton_AndManageOnlyMenuHasNoSeparator()
        {
            var views = new ObservableCollection<DaisyTabView>();
            using var host = new TestHost(new DaisyTabsHeaderButtons
            {
                ShowViewMenuButton = true, ShowManageViewsEntry = false, Views = views
            });
            Assert.False(host.ViewMenuButton.IsEffectivelyEnabled);
            views.Add(new DaisyTabView { Id = "one", Name = "One" });
            Assert.True(host.ViewMenuButton.IsEffectivelyEnabled);
            host.OpenMenu();
            views.Clear();
            Assert.False(host.Menu.IsOpen);
            Assert.False(host.ViewMenuButton.IsEffectivelyEnabled);
            host.Control.ShowManageViewsEntry = true;
            host.OpenMenu();
            Assert.IsType<MenuItem>(Assert.Single(host.Menu.Items));
        }

        [AvaloniaFact]
        public void ViewCollectionReplacementAndReattachment_UpdateAvailability()
        {
            var oldViews = new ObservableCollection<DaisyTabView>();
            var replacement = new ObservableCollection<DaisyTabView>();
            var control = new DaisyTabsHeaderButtons
            {
                ShowViewMenuButton = true, ShowManageViewsEntry = false, Views = oldViews
            };
            using var host = new TestHost(control);
            control.Views = replacement;
            oldViews.Add(new DaisyTabView { Id = 1, Name = "Old" });
            Assert.False(host.ViewMenuButton.IsEnabled);
            replacement.Add(new DaisyTabView { Id = 2, Name = "New" });
            Assert.True(host.ViewMenuButton.IsEnabled);
            host.Window.Content = null;
            replacement.Clear();
            Assert.True(host.ViewMenuButton.IsEnabled);
            host.Window.Content = control;
            host.Settle();
            Assert.False(host.ViewMenuButton.IsEnabled);

            var plainViews = new List<DaisyTabView>();
            control.Views = plainViews;
            plainViews.Add(new DaisyTabView { Id = 3, Name = "Plain list" });
            host.Window.Width += 1;
            host.Settle();
            Assert.True(host.ViewMenuButton.IsEnabled);
        }

        [AvaloniaTheory]
        [InlineData(false)]
        [InlineData(true)]
        public void NavigationClicks_RaiseEventsAndExecuteCommands(bool useCommands)
        {
            var previousRequests = 0;
            var nextRequests = 0;
            var previousCommand = new RecordingCommand();
            var nextCommand = new RecordingCommand();
            using var host = new TestHost(new DaisyTabsHeaderButtons());
            host.Control.PreviousRequested += (_, _) => previousRequests++;
            host.Control.NextRequested += (_, _) => nextRequests++;
            host.Control.ShowNavigationButtons = true;
            if (useCommands)
            {
                host.Control.PreviousCommand = previousCommand;
                host.Control.NextCommand = nextCommand;
            }
            host.Settle();
            host.Click(host.Previous);
            host.Click(host.Next);
            Assert.Equal(1, previousRequests);
            Assert.Equal(1, nextRequests);
            if (useCommands)
            {
                Assert.Null(Assert.Single(previousCommand.Parameters));
                Assert.Null(Assert.Single(nextCommand.Parameters));
            }
        }

        [AvaloniaFact]
        public void Commands_RespectCanExecuteAndReplacement()
        {
            var blocked = new RecordingCommand { Allowed = false };
            var allowed = new RecordingCommand();
            var control = CreateControl();
            control.ShowNavigationButtons = true;
            control.PreviousCommand = blocked;
            control.NextCommand = blocked;
            control.ViewSelectedCommand = blocked;
            control.ManageViewsCommand = blocked;
            using var host = new TestHost(control);
            Assert.False(host.Previous.IsEffectivelyEnabled);
            Assert.False(host.Next.IsEffectivelyEnabled);
            host.Click(host.Previous);
            Assert.Empty(blocked.Parameters);
            host.OpenMenu();
            Assert.All(host.Menu.Items.OfType<MenuItem>(), item => Assert.False(item.IsEffectivelyEnabled));
            blocked.Allowed = true;
            blocked.NotifyCanExecuteChanged();
            Assert.True(host.Previous.IsEffectivelyEnabled);
            Assert.All(host.Menu.Items.OfType<MenuItem>(), item => Assert.True(item.IsEffectivelyEnabled));
            host.Menu.Hide();
            control.PreviousCommand = allowed;
            host.Click(host.Previous);
            Assert.Single(allowed.Parameters);
            Assert.Empty(blocked.Parameters);
        }

        [AvaloniaFact]
        public void TemplateReapplication_DetachesOldButtonsAndClosesMenu()
        {
            var requests = 0;
            var control = CreateControl();
            control.ShowNavigationButtons = true;
            control.PreviousRequested += (_, _) => requests++;
            using var host = new TestHost(control);
            var oldPrevious = host.Previous;
            host.Click(oldPrevious);
            host.OpenMenu();
            var template = control.Template;
            control.Template = null;
            control.ApplyTemplate();
            control.Template = template;
            host.Settle();
            Assert.False(host.Menu.IsOpen);
            Assert.NotSame(oldPrevious, host.Previous);
            oldPrevious.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            host.Click(host.Previous);
            Assert.Equal(2, requests);
        }

        [AvaloniaFact]
        public void SizeAndScale_ReachButtonsAndIcons_AndLabelsAreAccessible()
        {
            using var host = new TestHost(new DaisyTabsHeaderButtons { ShowNavigationButtons = true, ShowViewMenuButton = true });
            foreach (var size in Enum.GetValues<DaisySize>())
            {
                host.Control.Size = size;
                host.Settle();
                Assert.All(host.Control.GetVisualDescendants().OfType<DaisyButton>(), button => Assert.Equal(size, button.Size));
                Assert.All(host.Control.GetVisualDescendants().OfType<DaisyIconText>(), icon => Assert.Equal(size, icon.Size));
                Assert.Equal(LayoutTestAssertions.GetUnoButtonFontSize(size), host.Control.FontSize);
                Assert.All(host.Control.GetVisualDescendants().OfType<DaisyIconText>(), icon => Assert.Equal(host.Control.FontSize, icon.IconSize));
            }
            host.Control.ApplyScaleFactor(0.8);
            host.Settle();
            Assert.Equal(11.2, host.Control.FontSize, 3);
            Assert.Equal(host.Control.FontSize, host.Previous.FontSize);
            Assert.All(host.Control.GetVisualDescendants().OfType<DaisyIconText>(), icon => Assert.Equal(11.2, icon.IconSize, 3));
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(host.Previous)));
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(host.Next)));
            Assert.False(string.IsNullOrEmpty(AutomationProperties.GetName(host.ViewMenuButton)));
        }

        [AvaloniaFact]
        public void MenuAndButtonLabels_UseCurrentCulture()
        {
            var originalCulture = FloweryLocalization.CurrentCulture;
            try
            {
                using var host = new TestHost(CreateControl());
                FloweryLocalization.SetCulture("de");
                host.OpenMenu();
                Assert.Equal("Ansichten", AutomationProperties.GetName(host.ViewMenuButton));
                Assert.Equal("Bearbeitung...", Assert.IsType<MenuItem>(host.Menu.Items[4]).Header);
            }
            finally
            {
                FloweryLocalization.SetCulture(originalCulture.Name);
            }
        }

        [AvaloniaFact]
        public void ViewMenuOpening_LoadsViewsAndPermissionsBeforeBuildingEntries()
        {
            var control = new DaisyTabsHeaderButtons { ShowViewMenuButton = true, ShowManageViewsEntry = false };
            using var host = new TestHost(control);
            var openingCount = 0;
            EventHandler<CancelEventArgs> loadViews = (_, _) =>
            {
                openingCount++;
                Assert.Empty(host.Menu.Items);
                control.Views = new List<DaisyTabView> { new() { Id = 42, Name = "Loaded view" } };
                control.ActiveViewId = 42;
                control.ShowManageViewsEntry = true;
            };
            control.ViewMenuOpening += loadViews;
            Assert.True(host.ViewMenuButton.IsEnabled);
            host.OpenMenu();
            Assert.Equal(1, openingCount);
            Assert.Equal(3, host.Menu.Items.Count);
            var item = Assert.IsType<MenuItem>(host.Menu.Items[0]);
            Assert.Equal("Loaded view", item.Header);
            Assert.True(item.IsChecked);
            Assert.IsType<Separator>(host.Menu.Items[1]);
            control.ViewMenuOpening -= loadViews;
        }

        [AvaloniaFact]
        public void ViewMenuOpening_CancelPreventsMenuBuildAndDisplay()
        {
            var control = CreateControl();
            using var host = new TestHost(control);
            host.OpenMenu();
            var originalItems = host.Menu.Items.ToArray();
            host.Menu.Hide();
            var openingCount = 0;
            EventHandler<CancelEventArgs> cancel = (_, args) =>
            {
                openingCount++;
                control.Views = null;
                args.Cancel = true;
            };
            control.ViewMenuOpening += cancel;
            host.Click(host.ViewMenuButton);
            Assert.Equal(1, openingCount);
            Assert.False(host.Menu.IsOpen);
            Assert.Equal(originalItems, host.Menu.Items.ToArray());
            control.ViewMenuOpening -= cancel;
            control.ShowManageViewsEntry = false;
            Assert.False(host.ViewMenuButton.IsEnabled);
        }

        [AvaloniaFact]
        public void ViewMenuOpening_WithNoLoadedEntries_DoesNotShowEmptyPopup()
        {
            using var host = new TestHost(new DaisyTabsHeaderButtons { ShowViewMenuButton = true, ShowManageViewsEntry = false });
            var openingCount = 0;
            host.Control.ViewMenuOpening += (_, _) => openingCount++;
            host.Click(host.ViewMenuButton);
            Assert.Equal(1, openingCount);
            Assert.False(host.Menu.IsOpen);
            Assert.Empty(host.Menu.Items);
        }

        private static DaisyTabsHeaderButtons CreateControl() => new()
        {
            ShowViewMenuButton = true,
            ActiveViewId = 2,
            Views = new List<DaisyTabView>
            {
                new() { Id = 1, Name = "Overview" },
                new() { Id = 2, Name = "Details" },
                new() { Id = 3, Name = "Archive" }
            }
        };

        private sealed class TestHost : IDisposable
        {
            public DaisyTabsHeaderButtons Control { get; }
            public Window Window { get; }
            public Button Previous => FindButton("PART_PreviousButton");
            public Button Next => FindButton("PART_NextButton");
            public Button ViewMenuButton => FindButton("PART_ViewMenuButton");
            public MenuFlyout Menu => Assert.IsAssignableFrom<MenuFlyout>(ViewMenuButton.Flyout);

            public TestHost(DaisyTabsHeaderButtons control)
            {
                Control = control;
                Window = new Window { Width = 400, Height = 200, Content = control };
                Window.Show();
                Settle();
            }

            public void OpenMenu()
            {
                Click(ViewMenuButton);
                Assert.True(Menu.IsOpen);
                TopLevel.GetTopLevel(Menu.Popup.Child!)?.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                var presenter = Assert.IsType<MenuFlyoutPresenter>(Menu.Popup.Child);
                Assert.Equal(Menu.Items.Count, presenter.ItemCount);
                Assert.Equal(Menu.Items.Count, presenter.GetVisualDescendants().Count(control => control is MenuItem or Separator));
            }

            public void Click(Button button)
            {
                Settle();
                var point = button.TranslatePoint(new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), Window)!.Value;
                Window.MouseMove(point);
                Window.MouseDown(point, MouseButton.Left);
                Window.MouseUp(point, MouseButton.Left);
                Settle();
            }

            public void Settle()
            {
                Window.UpdateLayout();
                Dispatcher.UIThread.RunJobs();
                Window.UpdateLayout();
            }

            public void Dispose() => Window.Close();

            private Button FindButton(string name) => Control.GetVisualDescendants().OfType<Button>().Single(button => button.Name == name);
        }

        private sealed class RecordingCommand : ICommand
        {
            public bool Allowed { get; set; } = true;
            public List<object?> Parameters { get; } = [];
            public event EventHandler? CanExecuteChanged;
            public bool CanExecute(object? parameter) => Allowed;
            public void Execute(object? parameter) => Parameters.Add(parameter);
            public void NotifyCanExecuteChanged() => CanExecuteChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
