using System.Windows.Input;
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
        [AvaloniaTheory]
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
            Assert.IsType<MenuItem>(host.Menu.Items[2]).PointerClick();
            Assert.Equal(3, Assert.Single(selected).Id);
            Assert.Equal("Archive", selected[0].Name);
            Assert.Equal(2, control.ActiveViewId);
            Assert.Equal(3, control.Views!.Count);
            host.Menu.Hide();
            host.OpenMenu();
            Assert.IsType<MenuItem>(host.Menu.Items[4]).PointerClick();
            Assert.Equal(1, manageRequests);
            if (useCommands)
            {
                Assert.Equal(3, Assert.Single(viewCommand.Parameters));
                Assert.Null(Assert.Single(manageCommand.Parameters));
            }
        }

        [AvaloniaTheory]
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
            // The old button is no longer shown, so only a raised event can reach its handler.
            oldPrevious.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            host.Click(host.Previous);
            Assert.Equal(2, requests);
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
