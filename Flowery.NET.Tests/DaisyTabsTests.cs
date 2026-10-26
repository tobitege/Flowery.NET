using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Xunit;

namespace Flowery.NET.Tests
{
    public class DaisyTabsTests
    {
        [AvaloniaFact]
        public void NoneVariant_SelectedHeader_FitsSemiBoldTitle()
        {
            var selected = new TabItem { Header = "Einstellungen" };
            var tabs = new DaisyTabs { Variant = DaisyTabVariant.None };
            tabs.Items.Add(selected);
            tabs.Items.Add(new TabItem { Header = "Andere" });
            tabs.SelectedItem = selected;

            using var window = new TestWindow(tabs, 600, 200);
            Dispatcher.UIThread.RunJobs();
            window.Window.UpdateLayout();

            var headerText = selected.GetVisualDescendants()
                .OfType<TextBlock>()
                .First();

            Assert.True(
                headerText.DesiredSize.Width <= headerText.Bounds.Width + 0.5,
                $"Selected header clips: desired {headerText.DesiredSize.Width}, arranged {headerText.Bounds.Width}.");
            Assert.Equal(FontWeight.SemiBold, selected.FontWeight);
        }

        public static IEnumerable<object[]> HeaderLayouts()
        {
            foreach (var variant in Enum.GetValues<DaisyTabVariant>())
            foreach (var size in Enum.GetValues<DaisySize>())
            foreach (var mode in Enum.GetValues<DaisyTabWidthMode>())
                yield return [variant, size, mode];
        }

        [AvaloniaTheory]
        [MemberData(nameof(HeaderLayouts))]
        public void TrailingSlot_ReservesSpace_AcrossWidthsVariantsAndSizes(
            DaisyTabVariant variant, DaisySize size, DaisyTabWidthMode mode)
        {
            var slot = new Border { Width = 80, Height = 24 };
            var tabs = CreateTabs(variant, size, mode);
            tabs.HeaderTrailingContent = slot;
            var window = new Window { Width = 800, Height = 300, Content = tabs };
            try
            {
                window.Show();
                foreach (var width in new[] { 800d, 300d, 600d })
                {
                    window.Width = width;
                    Settle(window);
                    var presenter = FindPart<ItemsPresenter>(tabs, "PART_ItemsPresenter");
                    var trailing = FindPart<ContentPresenter>(tabs, "PART_HeaderTrailingContent");
                    var header = FindPart<Border>(tabs, "PART_HeaderContainer");
                    var slotLeft = slot.TranslatePoint(default, tabs)!.Value.X;
                    Assert.True(trailing.IsVisible);
                    Assert.True(slot.Bounds.Width > 0);
                    Assert.True(presenter.TranslatePoint(default, tabs)!.Value.X + presenter.Bounds.Width <= slotLeft);
                    foreach (var tab in tabs.Items.OfType<TabItem>())
                    {
                        var tabRight = tab.TranslatePoint(default, tabs)!.Value.X + tab.Bounds.Width;
                        Assert.True(tabRight <= slotLeft, $"Tab right {tabRight} overlaps slot at {slotLeft} ({width}).");
                    }

                    if (variant == DaisyTabVariant.Boxed && width == 800)
                        Assert.True(header.Bounds.Width < tabs.Bounds.Width - 100, "Boxed header stretched to fill its parent.");
                }
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaTheory]
        [MemberData(nameof(HeaderLayouts))]
        public void EmptyTrailingSlot_PreservesOriginalPresenterBounds(
            DaisyTabVariant variant, DaisySize size, DaisyTabWidthMode mode)
        {
            var tabs = CreateTabs(variant, size, mode);
            var window = new Window { Width = 600, Height = 250, Content = tabs };
            try
            {
                window.Show();
                Settle(window);
                var presenter = FindPart<ItemsPresenter>(tabs, "PART_ItemsPresenter");
                var bounds = presenter.Bounds;
                var position = presenter.TranslatePoint(default, tabs);
                var headerBounds = FindPart<Border>(tabs, "PART_HeaderContainer").Bounds;
                Assert.False(FindPart<ContentPresenter>(tabs, "PART_HeaderTrailingContent").IsVisible);

                tabs.HeaderTrailingContent = new Border { Width = 80, Height = 24 };
                Settle(window);
                tabs.HeaderTrailingContent = null;
                Settle(window);
                Assert.Equal(bounds, presenter.Bounds);
                Assert.False(FindPart<ContentPresenter>(tabs, "PART_HeaderTrailingContent").IsVisible);

                // Reproduce the original template, before the trailing slot was introduced.
                tabs.Template = new FuncControlTemplate<DaisyTabs>((owner, scope) =>
                {
                    var items = new ItemsPresenter { Name = "PART_ItemsPresenter" };
                    items.Bind(ItemsPresenter.ItemsPanelProperty, owner.GetObservable(ItemsControl.ItemsPanelProperty));
                    scope.Register(items.Name, items);
                    var header = new Border { Name = "PART_HeaderContainer", Child = items };
                    scope.Register(header.Name, header);
                    DockPanel.SetDock(header, Dock.Top);
                    var content = new ContentPresenter { Name = "PART_SelectedContentHost" };
                    scope.Register(content.Name, content);
                    content.Bind(ContentPresenter.ContentProperty, owner.GetObservable(TabControl.SelectedContentProperty));
                    return new DockPanel { Children = { header, content } };
                });
                Settle(window);
                var originalPresenter = FindPart<ItemsPresenter>(tabs, "PART_ItemsPresenter");
                // The Grid changes the local origin. Compare size and position in DaisyTabs coordinates.
                Assert.Equal(bounds.Size, originalPresenter.Bounds.Size);
                Assert.Equal(position, originalPresenter.TranslatePoint(default, tabs));
                Assert.Equal(headerBounds, FindPart<Border>(tabs, "PART_HeaderContainer").Bounds);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void TrailingContentTemplate_ReceivesDataAndPreservesConsumerHeaderOverrides()
        {
            var tabs = CreateTabs(DaisyTabVariant.Boxed, DaisySize.Small, DaisyTabWidthMode.Auto);
            tabs.HeaderTrailingContent = "Actions";
            tabs.HeaderTrailingContentTemplate = new FuncDataTemplate<string>((text, _) => new TextBlock { Text = text });
            var window = new Window { Width = 500, Height = 250, Content = tabs };
            try
            {
                window.Show();
                Settle(window);
                var presenter = FindPart<ContentPresenter>(tabs, "PART_HeaderTrailingContent");
                Assert.Equal("Actions", Assert.IsType<TextBlock>(presenter.Child).Text);
                var header = FindPart<Border>(tabs, "PART_HeaderContainer");
                header.Padding = new Thickness(3, 4, 35, 6);
                Settle(window);
                Assert.Equal(new Thickness(3, 4, 35, 6), header.Padding);
                header.IsVisible = false;
                Settle(window);
                Assert.False(presenter.IsEffectivelyVisible);
            }
            finally
            {
                window.Close();
            }
        }

        [AvaloniaFact]
        public void ShowHeaderButtons_PreservesInstanceCustomContentAndBinding()
        {
            var source = new ContentControl();
            var tabs = new DaisyTabs();
            using var binding = tabs.Bind(DaisyTabs.HeaderTrailingContentProperty,
                new Binding(nameof(ContentControl.Content)) { Source = source });
            Assert.Null(tabs.HeaderButtons);
            tabs.ShowHeaderButtons = true;
            var buttons = Assert.IsType<DaisyTabsHeaderButtons>(tabs.HeaderButtons);
            Assert.Same(buttons, tabs.HeaderTrailingContent);
            buttons.ShowNavigationButtons = true;
            tabs.ShowHeaderButtons = false;
            Assert.Null(tabs.HeaderTrailingContent);
            tabs.ShowHeaderButtons = true;
            Assert.Same(buttons, tabs.HeaderTrailingContent);
            Assert.True(buttons.ShowNavigationButtons);

            var custom = new TextBlock { Text = "Custom actions" };
            source.Content = custom;
            Assert.Same(custom, tabs.HeaderTrailingContent);
            tabs.ShowHeaderButtons = false;
            Assert.Same(custom, tabs.HeaderTrailingContent);
            tabs.ShowHeaderButtons = true;
            Assert.Same(custom, tabs.HeaderTrailingContent);
            source.Content = null;
            Assert.Same(buttons, tabs.HeaderTrailingContent);

            var customFirst = new DaisyTabs { HeaderTrailingContent = custom, ShowHeaderButtons = true };
            Assert.Null(customFirst.HeaderButtons);
            Assert.Same(custom, customFirst.HeaderTrailingContent);
        }

        [AvaloniaFact]
        public void BuiltInHeaderButtons_AreVisibleBesideTabs_AndRestoreEmptyLayoutWhenDisabled()
        {
            var tabs = CreateTabs(DaisyTabVariant.Boxed, DaisySize.Small, DaisyTabWidthMode.Equal);
            var window = new Window { Width = 600, Height = 250, Content = tabs };
            try
            {
                window.Show();
                Settle(window);
                var header = FindPart<Border>(tabs, "PART_HeaderContainer");
                var emptyBounds = header.Bounds;
                tabs.ShowHeaderButtons = true;
                var buttons = Assert.IsType<DaisyTabsHeaderButtons>(tabs.HeaderButtons);
                buttons.ShowNavigationButtons = true;
                buttons.ShowViewMenuButton = true;
                Settle(window);
                Assert.True(buttons.IsEffectivelyVisible);
                Assert.True(buttons.Bounds.Width > 0);
                Assert.True(header.Bounds.Width < tabs.Bounds.Width);
                var slotLeft = buttons.TranslatePoint(default, tabs)!.Value.X;
                foreach (var tab in tabs.Items.OfType<TabItem>())
                    Assert.True(tab.TranslatePoint(default, tabs)!.Value.X + tab.Bounds.Width <= slotLeft);
                tabs.ShowHeaderButtons = false;
                Settle(window);
                Assert.Equal(emptyBounds, header.Bounds);
                Assert.False(FindPart<ContentPresenter>(tabs, "PART_HeaderTrailingContent").IsVisible);
            }
            finally
            {
                window.Close();
            }
        }

        private static DaisyTabs CreateTabs(DaisyTabVariant variant, DaisySize size, DaisyTabWidthMode mode)
        {
            var tabs = new DaisyTabs
            {
                Variant = variant, Size = size, TabWidthMode = mode,
                TabWidth = 100, TabMaxWidth = 110, TabMinWidth = 35
            };
            tabs.Items.Add(new TabItem { Header = "One" });
            tabs.Items.Add(new TabItem { Header = "Longer title" });
            tabs.Items.Add(new TabItem { Header = "Three" });
            return tabs;
        }

        private static T FindPart<T>(Control control, string name) where T : Control =>
            control.GetVisualDescendants().OfType<T>().Single(part => part.Name == name);

        private static void Settle(Window window)
        {
            window.UpdateLayout();
            Dispatcher.UIThread.RunJobs();
            window.UpdateLayout();
        }
    }
}
