using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Windows.Input;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>A named view supplied by the application.</summary>
    public sealed class DaisyTabView
    {
        public required object Id { get; init; }
        public required string Name { get; init; }
    }

    /// <summary>Identifies the view requested by the user.</summary>
    public sealed class DaisyTabViewEventArgs(object id, string name) : EventArgs
    {
        public object Id { get; } = id;
        public string Name { get; } = name;
    }

    /// <summary>
    /// Provides optional navigation buttons and a view menu for a tab header or toolbar.
    /// The application owns navigation, view selection, and view management.
    /// </summary>
    public class DaisyTabsHeaderButtons : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyTabsHeaderButtons);

        private readonly MenuFlyout _viewMenu;
        private readonly ObservableCollection<Control> _viewMenuItems = [];
        private EventHandler<CancelEventArgs>? _viewMenuOpening;
        private Button? _previousButton;
        private Button? _nextButton;
        private Button? _viewMenuButton;
        private INotifyCollectionChanged? _observedViews;

        public static readonly StyledProperty<bool> ShowNavigationButtonsProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, bool>(nameof(ShowNavigationButtons));

        /// <summary>Gets or sets whether the previous and next buttons are visible.</summary>
        public bool ShowNavigationButtons
        {
            get => GetValue(ShowNavigationButtonsProperty);
            set => SetValue(ShowNavigationButtonsProperty, value);
        }

        public static readonly StyledProperty<bool> ShowViewMenuButtonProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, bool>(nameof(ShowViewMenuButton));

        /// <summary>Gets or sets whether the view menu button is visible.</summary>
        public bool ShowViewMenuButton
        {
            get => GetValue(ShowViewMenuButtonProperty);
            set => SetValue(ShowViewMenuButtonProperty, value);
        }

        public static readonly StyledProperty<IList<DaisyTabView>?> ViewsProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, IList<DaisyTabView>?>(nameof(Views));

        /// <summary>
        /// Gets or sets the views read each time the menu opens.
        /// Observable collections also update the menu button's enabled state immediately.
        /// </summary>
        public IList<DaisyTabView>? Views
        {
            get => GetValue(ViewsProperty);
            set => SetValue(ViewsProperty, value);
        }

        public static readonly StyledProperty<object?> ActiveViewIdProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, object?>(nameof(ActiveViewId));

        /// <summary>Gets or sets the identifier of the checked view.</summary>
        public object? ActiveViewId
        {
            get => GetValue(ActiveViewIdProperty);
            set => SetValue(ActiveViewIdProperty, value);
        }

        public static readonly StyledProperty<bool> ShowManageViewsEntryProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, bool>(nameof(ShowManageViewsEntry), true);

        /// <summary>Gets or sets whether the menu includes the view management request.</summary>
        public bool ShowManageViewsEntry
        {
            get => GetValue(ShowManageViewsEntryProperty);
            set => SetValue(ShowManageViewsEntryProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, DaisySize>(nameof(Size), DaisySize.Small);

        /// <summary>Gets or sets the size of the buttons and icons.</summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public static readonly StyledProperty<ICommand?> PreviousCommandProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, ICommand?>(nameof(PreviousCommand));

        /// <summary>Gets or sets the previous request command. Its parameter is null.</summary>
        public ICommand? PreviousCommand
        {
            get => GetValue(PreviousCommandProperty);
            set => SetValue(PreviousCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand?> NextCommandProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, ICommand?>(nameof(NextCommand));

        /// <summary>Gets or sets the next request command. Its parameter is null.</summary>
        public ICommand? NextCommand
        {
            get => GetValue(NextCommandProperty);
            set => SetValue(NextCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand?> ViewSelectedCommandProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, ICommand?>(nameof(ViewSelectedCommand));

        /// <summary>Gets or sets the view selection command. Its parameter is the view identifier.</summary>
        public ICommand? ViewSelectedCommand
        {
            get => GetValue(ViewSelectedCommandProperty);
            set => SetValue(ViewSelectedCommandProperty, value);
        }

        public static readonly StyledProperty<ICommand?> ManageViewsCommandProperty =
            AvaloniaProperty.Register<DaisyTabsHeaderButtons, ICommand?>(nameof(ManageViewsCommand));

        /// <summary>Gets or sets the view management command. Its parameter is null.</summary>
        public ICommand? ManageViewsCommand
        {
            get => GetValue(ManageViewsCommandProperty);
            set => SetValue(ManageViewsCommandProperty, value);
        }

        public event EventHandler? PreviousRequested;
        public event EventHandler? NextRequested;
        public event EventHandler<DaisyTabViewEventArgs>? ViewSelected;
        public event EventHandler? ManageViewsRequested;

        /// <summary>
        /// Raised before the menu entries are built. The handler can load Views, update permissions,
        /// or cancel opening. A subscribed handler keeps an initially empty menu button available.
        /// </summary>
        public event EventHandler<CancelEventArgs>? ViewMenuOpening
        {
            add
            {
                _viewMenuOpening += value;
                UpdateViewMenuAvailability();
            }
            remove
            {
                _viewMenuOpening -= value;
                UpdateViewMenuAvailability();
            }
        }

        public DaisyTabsHeaderButtons()
        {
            _viewMenu = new ViewMenuFlyout(this) { ItemsSource = _viewMenuItems };
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(14.0, 11.0, scaleFactor);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            _viewMenu.Hide();
            if (_previousButton is not null)
                _previousButton.Click -= OnPreviousClick;
            if (_nextButton is not null)
                _nextButton.Click -= OnNextClick;
            if (_viewMenuButton is not null)
                _viewMenuButton.Flyout = null;

            base.OnApplyTemplate(e);
            _previousButton = e.NameScope.Find<Button>("PART_PreviousButton");
            _nextButton = e.NameScope.Find<Button>("PART_NextButton");
            _viewMenuButton = e.NameScope.Find<Button>("PART_ViewMenuButton");

            if (_previousButton is not null)
                _previousButton.Click += OnPreviousClick;
            if (_nextButton is not null)
                _nextButton.Click += OnNextClick;
            if (_viewMenuButton is not null)
                _viewMenuButton.Flyout = _viewMenu;

            UpdateButtonLabels();
            UpdateViewMenuAvailability();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            FloweryLocalization.CultureChanged += OnCultureChanged;
            LayoutUpdated += OnLayoutUpdated;
            ObserveViews();
            UpdateButtonLabels();
            UpdateViewMenuAvailability();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            FloweryLocalization.CultureChanged -= OnCultureChanged;
            LayoutUpdated -= OnLayoutUpdated;
            StopObservingViews();
            _viewMenu.Hide();
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ViewsProperty)
            {
                ObserveViews();
                UpdateViewMenuAvailability();
            }
            else if (change.Property == ShowManageViewsEntryProperty || change.Property == ShowViewMenuButtonProperty)
            {
                UpdateViewMenuAvailability();
            }
        }

        private void ObserveViews()
        {
            StopObservingViews();
            if (this.IsAttachedToVisualTree() && Views is INotifyCollectionChanged observable)
            {
                _observedViews = observable;
                observable.CollectionChanged += OnViewsChanged;
            }
        }

        private void StopObservingViews()
        {
            if (_observedViews is not null)
                _observedViews.CollectionChanged -= OnViewsChanged;
            _observedViews = null;
        }

        private void OnViewsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateViewMenuAvailability();

        // IList does not require change notifications. Recheck its count after layout as well.
        private void OnLayoutUpdated(object? sender, EventArgs e) => UpdateViewMenuAvailability();

        private void UpdateViewMenuAvailability()
        {
            var hasEntries = Views is { Count: > 0 } || ShowManageViewsEntry || _viewMenuOpening is not null;
            if (_viewMenuButton is not null)
                _viewMenuButton.IsEnabled = hasEntries;
            if (!hasEntries || !ShowViewMenuButton)
                _viewMenu.Hide();
        }

        private void PrepareViewMenu(CancelEventArgs e)
        {
            _viewMenuOpening?.Invoke(this, e);
            if (e.Cancel)
                return;

            _viewMenuItems.Clear();
            if (Views is { } views)
            {
                foreach (var view in views)
                {
                    var item = new MenuItem
                    {
                        Header = view.Name,
                        Tag = view,
                        ToggleType = MenuItemToggleType.CheckBox,
                        IsChecked = Equals(view.Id, ActiveViewId),
                        Command = ViewSelectedCommand,
                        CommandParameter = view.Id
                    };
                    item.AddHandler(MenuItem.ClickEvent, OnViewClick, handledEventsToo: true);
                    _viewMenuItems.Add(item);
                }
            }

            if (ShowManageViewsEntry)
            {
                if (_viewMenuItems.Count > 0)
                    _viewMenuItems.Add(new Separator());
                var manageItem = new MenuItem
                {
                    Header = FloweryLocalization.GetStringInternal("Tabs_ManageViews", "Manage views…"),
                    Command = ManageViewsCommand
                };
                manageItem.AddHandler(MenuItem.ClickEvent, OnManageViewsClick, handledEventsToo: true);
                _viewMenuItems.Add(manageItem);
            }

            if (_viewMenuItems.Count == 0 || !ShowViewMenuButton || !IsEffectivelyEnabled)
                e.Cancel = true;
        }

        private void OnPreviousClick(object? sender, RoutedEventArgs e)
        {
            if (_previousButton?.IsEffectivelyEnabled == true)
                PreviousRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnNextClick(object? sender, RoutedEventArgs e)
        {
            if (_nextButton?.IsEffectivelyEnabled == true)
                NextRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnViewClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { IsEffectivelyEnabled: true, Tag: DaisyTabView view })
                ViewSelected?.Invoke(this, new DaisyTabViewEventArgs(view.Id, view.Name));
        }

        private void OnManageViewsClick(object? sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { IsEffectivelyEnabled: true })
                ManageViewsRequested?.Invoke(this, EventArgs.Empty);
        }

        private void OnCultureChanged(object? sender, CultureInfo culture)
        {
            if (Dispatcher.UIThread.CheckAccess())
                UpdateButtonLabels();
            else
                Dispatcher.UIThread.Post(UpdateButtonLabels);
        }

        private void UpdateButtonLabels()
        {
            SetButtonLabel(_previousButton, "Accessibility_PreviousItem", "Previous item");
            SetButtonLabel(_nextButton, "Accessibility_NextItem", "Next item");
            SetButtonLabel(_viewMenuButton, "Tabs_Views", "Views");
        }

        private static void SetButtonLabel(Button? button, string key, string fallback)
        {
            if (button is null)
                return;
            var label = FloweryLocalization.GetStringInternal(key, fallback);
            AutomationProperties.SetName(button, label);
            ToolTip.SetTip(button, label);
        }

        private sealed class ViewMenuFlyout(DaisyTabsHeaderButtons owner) : MenuFlyout
        {
            protected override void OnOpening(CancelEventArgs args)
            {
                owner.PrepareViewMenu(args);
                base.OnOpening(args);
            }
        }
    }
}
