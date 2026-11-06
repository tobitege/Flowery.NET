using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Controls.Templates;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Helpers;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// A section with a clickable header and a chevron that shows or hides its content, with its own header and content
    /// padding, header colors and chevron size. The header is a button: Tab reaches it, Enter or Space toggles the
    /// section, and screen readers hear whether it is expanded.
    /// </summary>
    public partial class DaisyExpander : ContentControl
    {
        private Button? _headerButton;
        private Border? _headerBorder;
        private ContentPresenter? _headerContent;
        private Path? _chevron;
        // The chevron follows the header text while contrast colors apply.
        private IDisposable? _chevronFillBinding;
        private Border? _contentBorder;
        private ContentPresenter? _contentPresenter;

        #region Dependency Properties

        public static readonly StyledProperty<object?> HeaderProperty =
            AvaloniaProperty.Register<DaisyExpander, object?>(
                nameof(Header));

        public object? Header
        {
            get => GetValue(HeaderProperty);
            set => SetValue(HeaderProperty, value);
        }

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<DaisyExpander, bool>(
                nameof(IsExpanded),
                true);

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public static readonly StyledProperty<IBrush?> HeaderBackgroundProperty =
            AvaloniaProperty.Register<DaisyExpander, IBrush?>(
                nameof(HeaderBackground));

        public IBrush? HeaderBackground
        {
            get => GetValue(HeaderBackgroundProperty);
            set => SetValue(HeaderBackgroundProperty, value);
        }

        public static readonly StyledProperty<IBrush?> HeaderForegroundProperty =
            AvaloniaProperty.Register<DaisyExpander, IBrush?>(
                nameof(HeaderForeground));

        public IBrush? HeaderForeground
        {
            get => GetValue(HeaderForegroundProperty);
            set => SetValue(HeaderForegroundProperty, value);
        }

        public static readonly StyledProperty<Thickness> HeaderPaddingProperty =
            AvaloniaProperty.Register<DaisyExpander, Thickness>(
                nameof(HeaderPadding),
                new Thickness(8, 6, 8, 6));

        public Thickness HeaderPadding
        {
            get => GetValue(HeaderPaddingProperty);
            set => SetValue(HeaderPaddingProperty, value);
        }

        public static readonly StyledProperty<Thickness> ContentPaddingProperty =
            AvaloniaProperty.Register<DaisyExpander, Thickness>(
                nameof(ContentPadding),
                new Thickness(0));

        public Thickness ContentPadding
        {
            get => GetValue(ContentPaddingProperty);
            set => SetValue(ContentPaddingProperty, value);
        }

        public static readonly StyledProperty<double> ChevronSizeProperty =
            AvaloniaProperty.Register<DaisyExpander, double>(
                nameof(ChevronSize),
                12d);

        public double ChevronSize
        {
            get => GetValue(ChevronSizeProperty);
            set => SetValue(ChevronSizeProperty, value);
        }

        public static readonly StyledProperty<bool> ShowChevronProperty =
            AvaloniaProperty.Register<DaisyExpander, bool>(
                nameof(ShowChevron),
                true);

        public bool ShowChevron
        {
            get => GetValue(ShowChevronProperty);
            set => SetValue(ShowChevronProperty, value);
        }

        #endregion

        public DaisyExpander()
        {
            IsTabStop = false;

            // The layout is the template: Content is the expandable content, shown by PART_ContentPresenter,
            // and only the parts built here draw the border, background and padding.
            Template = new FuncControlTemplate<DaisyExpander>(static (expander, scope) => expander.BuildLayout(scope));
        }

        private Control BuildLayout(INameScope scope)
        {
            var rootPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // A button for the header: it takes the focus and the keyboard, and tells screen readers the state.
            _headerButton = new HeaderButton(this)
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                HorizontalContentAlignment = HorizontalAlignment.Stretch,
                Background = new SolidColorBrush(Colors.Transparent),
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0)
            };

            var headerGrid = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _headerContent = new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                VerticalAlignment = VerticalAlignment.Center,
                Content = Header
            };
            Grid.SetColumn(_headerContent, 0);
            headerGrid.Children.Add(_headerContent);

            // Chevron icon
            var chevronViewbox = new Viewbox
            {
                Width = ChevronSize,
                Height = ChevronSize,
                // The template can be built after the settings, so it starts from them.
                IsVisible = ShowChevron,
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(8, 0, 4, 0)
            };

            _chevron = new Path
            {
                Stretch = Stretch.Uniform
            };
            chevronViewbox.Child = _chevron;
            Grid.SetColumn(chevronViewbox, 1);
            headerGrid.Children.Add(chevronViewbox);

            _headerButton.Content = headerGrid;
            _headerButton.Click += OnHeaderClick;

            // Wrap header in a border to support BorderBrush/BorderThickness on header row
            _headerBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch,
                Child = _headerButton
            };

            // Content area
            _contentBorder = new Border
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // The content host of the ContentControl: the expander's Content is its logical child.
            _contentPresenter = new ContentPresenter
            {
                Name = "PART_ContentPresenter",
                HorizontalAlignment = HorizontalAlignment.Stretch,
                [!ContentPresenter.ContentProperty] = this[!ContentProperty],
                [!ContentPresenter.ContentTemplateProperty] = this[!ContentTemplateProperty]
            }.RegisterInNameScope(scope);
            _contentBorder.Child = _contentPresenter;

            rootPanel.Children.Add(_headerBorder);
            rootPanel.Children.Add(_contentBorder);

            UpdateChevron();
            UpdateContentVisibility();
            UpdateHeaderPadding();
            UpdateContentPadding();
            UpdateBorder();
            UpdateColors();
            return rootPanel;
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            // Contrast changes replace the palette without a theme change in Flowery.NET.
            DaisyAccessibility.PreferencesChanged += OnPreferencesChanged;
            OnThemeChanged(this, DaisyThemeManager.CurrentThemeName ?? string.Empty);
        }

        protected override void OnUnloaded(RoutedEventArgs e)
        {
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
            DaisyAccessibility.PreferencesChanged -= OnPreferencesChanged;
            base.OnUnloaded(e);
        }

        private void OnThemeChanged(object? sender, string themeName)
        {
            UpdateColors();
            UpdateBorder();
        }

        private void OnPreferencesChanged(object? sender, EventArgs e) => OnThemeChanged(sender, DaisyThemeManager.CurrentThemeName ?? string.Empty);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == HeaderProperty)
            {
                if (_headerContent != null)
                    _headerContent.Content = Header;
            }
            else if (change.Property == IsExpandedProperty)
            {
                UpdateChevron();
                UpdateContentVisibility();
                // Only a peer that exists has a screen reader listening.
                if (_headerButton != null && ControlAutomationPeer.FromElement(_headerButton) is DaisyExpanderHeaderAutomationPeer peer)
                    peer.RaiseExpandCollapseChanged(change.GetOldValue<bool>(), change.GetNewValue<bool>());
            }
            else if (change.Property == HeaderBackgroundProperty) UpdateColors();
            else if (change.Property == HeaderForegroundProperty) UpdateHeaderForeground();
            else if (change.Property == HeaderPaddingProperty) UpdateHeaderPadding();
            else if (change.Property == ContentPaddingProperty) UpdateContentPadding();
            else if (change.Property == ChevronSizeProperty || change.Property == ShowChevronProperty) UpdateChevronBox();
            // A disabled ancestor changes only the effective state of the expander.
            else if (change.Property == IsEffectivelyEnabledProperty) UpdateColors();
            // Register for BorderBrush/BorderThickness changes (inherited from Control)
            else if (change.Property == BorderBrushProperty || change.Property == BorderThicknessProperty) UpdateBorder();
        }

        // A user change, like the selection of DaisyButtonGroup: SetCurrentValue, so a binding on IsExpanded stays.
        private void OnHeaderClick(object? sender, RoutedEventArgs e) => SetCurrentValue(IsExpandedProperty, !IsExpanded);

        private void UpdateChevronBox()
        {
            if (_chevron?.Parent is not Viewbox viewbox)
                return;
            viewbox.Width = ChevronSize;
            viewbox.Height = ChevronSize;
            viewbox.IsVisible = ShowChevron;
        }

        private void UpdateChevron()
        {
            if (_chevron == null)
                return;

            try
            {
                var pathData = IsExpanded ? FloweryPathHelpers.ChevronUpPath : FloweryPathHelpers.ChevronDownPath;
                _chevron.Data = FloweryPathHelpers.ParseGeometry(pathData);
            }
            catch
            {
                // Fallback if path parsing fails
            }

            UpdateHeaderForeground();
        }

        private void UpdateHeaderForeground()
        {
            // In contrast mode and while disabled, the header text keeps the colors of the button, and the chevron
            // follows it.
            if (_headerContent != null)
            {
                if (HeaderForeground is { } headerForeground && !DaisyAccessibility.IsHighContrast && IsEffectivelyEnabled)
                    _headerContent.Foreground = headerForeground;
                else
                    _headerContent.ClearValue(ContentPresenter.ForegroundProperty);
            }
            if (_chevron == null)
                return;

            var foreground = HeaderForeground ?? DaisyResourceLookup.GetBrush("DaisyBaseContentBrush") ?? new SolidColorBrush(Colors.White);
            // A local value does not replace a binding in Avalonia, so the contrast binding is removed explicitly.
            _chevronFillBinding?.Dispose();
            _chevronFillBinding = null;
            _chevron.Fill = foreground;
            if ((DaisyAccessibility.IsHighContrast || !IsEffectivelyEnabled) && _headerContent != null)
                _chevronFillBinding = _chevron.Bind(Shape.FillProperty, _headerContent.GetObservable(ContentPresenter.ForegroundProperty));
        }

        private void UpdateContentVisibility()
        {
            if (_contentBorder != null)
            {
                _contentBorder.IsVisible = IsExpanded;
            }
        }

        private void UpdateHeaderPadding()
        {
            if (_headerButton != null)
            {
                _headerButton.Padding = HeaderPadding;
            }
        }

        private void UpdateContentPadding()
        {
            if (_contentBorder != null)
            {
                _contentBorder.Padding = ContentPadding;
            }
        }

        private void UpdateColors()
        {
            UpdateHeaderForeground();

            if (_headerButton != null)
            {
                _headerButton.Background = HeaderBackground ?? new SolidColorBrush(Colors.Transparent);
                if (DaisyAccessibility.IsHighContrast)
                    _headerButton.Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush");
            }
            if (_contentPresenter != null)
            {
                if (DaisyAccessibility.IsHighContrast)
                    _contentPresenter.Foreground = DaisyResourceLookup.GetBrush(IsEffectivelyEnabled ? "DaisyBaseContentBrush" : "DaisyBaseContentDisabledBrush");
                else
                    _contentPresenter.ClearValue(ContentPresenter.ForegroundProperty);
            }
        }

        private void UpdateBorder()
        {
            if (_headerBorder != null)
            {
                _headerBorder.BorderBrush = DaisyAccessibility.IsHighContrast ? DaisyResourceLookup.GetBrush("DaisyBaseContentBrush") : BorderBrush;
                _headerBorder.BorderThickness = DaisyAccessibility.IsHighContrast ? new Thickness(1) : BorderThickness;
            }
        }

        /// <summary>
        /// Sets the actual expandable content. In Avalonia the layout is the template, so this is the same as
        /// <see cref="ContentControl.Content"/>, which XAML child content sets and which can be bound or styled.
        /// </summary>
        public object? ExpanderContent
        {
            get => Content;
            set => Content = value;
        }

        // The header button with the expand and collapse pattern; it looks like any button.
        private sealed class HeaderButton(DaisyExpander owner) : Button
        {
            internal DaisyExpander Expander => owner;

            protected override Type StyleKeyOverride => typeof(Button);

            protected override AutomationPeer OnCreateAutomationPeer() => new DaisyExpanderHeaderAutomationPeer(this);
        }

        /// <summary>
        /// Exposes the header of a <see cref="DaisyExpander"/> to screen readers as a button that expands and collapses
        /// the section and tells whether it is expanded, named after the header text.
        /// </summary>
        private sealed class DaisyExpanderHeaderAutomationPeer(HeaderButton owner) : ButtonAutomationPeer(owner), IExpandCollapseProvider
        {
            private DaisyExpander Expander => owner.Expander;

            public ExpandCollapseState ExpandCollapseState =>
                Expander.IsExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

            public bool ShowsMenu => false;

            public void Expand() => Expander.SetCurrentValue(IsExpandedProperty, true);

            public void Collapse() => Expander.SetCurrentValue(IsExpandedProperty, false);

            internal void RaiseExpandCollapseChanged(bool wasExpanded, bool isExpanded) =>
                RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
                    wasExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
                    isExpanded ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

            // The content of the button is the layout grid, so the name comes from the header.
            protected override string? GetNameCore() =>
                AutomationProperties.GetName(Owner) is { Length: > 0 } name
                    ? name
                    : Expander.Header switch
                    {
                        string text => text,
                        Control control when AutomationProperties.GetName(control) is { Length: > 0 } controlName => controlName,
                        TextBlock block => block.Text,
                        _ => base.GetNameCore()
                    };
        }
    }
}
