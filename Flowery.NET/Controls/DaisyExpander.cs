using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Flowery.Helpers;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// A custom expander control that works reliably on all platforms including Skia/WASM.
    /// Unlike the built-in Expander which has known issues on non-Windows platforms,
    /// this control uses simple visibility toggling with a clickable header.
    /// </summary>
    public partial class DaisyExpander : ContentControl
    {
        // The layout is built in code; the control uses the plain ContentControl template.
        protected override Type StyleKeyOverride => typeof(ContentControl);

        private Button? _headerButton;
        private Border? _headerBorder;
        private ContentPresenter? _headerContent;
        private Path? _chevron;
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

            BuildLayout();
        }

        private void BuildLayout()
        {
            var rootPanel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                HorizontalAlignment = HorizontalAlignment.Stretch
            };

            // Use a Button for the header to ensure click events work on all platforms
            // Button.Click is more reliably supported than Border.Tapped on Skia/WASM
            _headerButton = new Button
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
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_headerContent, 0);
            headerGrid.Children.Add(_headerContent);

            // Chevron icon
            var chevronViewbox = new Viewbox
            {
                Width = ChevronSize,
                Height = ChevronSize,
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

            _contentPresenter = new ContentPresenter
            {
                HorizontalAlignment = HorizontalAlignment.Stretch
            };
            _contentBorder.Child = _contentPresenter;

            rootPanel.Children.Add(_headerBorder);
            rootPanel.Children.Add(_contentBorder);

            Content = rootPanel;

            UpdateChevron();
            UpdateContentVisibility();
            UpdateHeaderPadding();
            UpdateContentPadding();
            UpdateBorder();
        }

        protected override void OnLoaded(RoutedEventArgs e)
        {
            base.OnLoaded(e);
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            // Contrast changes replace the palette without a theme change in Flowery.NET.
            DaisyAccessibility.PreferencesChanged += OnPreferencesChanged;
            // Ensure header content is synced after control is fully loaded
            // This is needed because the property might be set before _headerContent is ready
            if (_headerContent != null && Header != null)
            {
                _headerContent.Content = Header;
            }

            // Ensure content is synced
            if (_contentPresenter != null && ExpanderContent != null)
            {
                _contentPresenter.Content = ExpanderContent;
            }

            UpdateChevron();
            UpdateContentVisibility();
            UpdateColors();
            // LogContentState("Loaded");
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

            if (change.Property == HeaderProperty) OnHeaderChanged(this, change);
            else if (change.Property == IsExpandedProperty) OnIsExpandedChanged(this, change);
            else if (change.Property == HeaderBackgroundProperty) OnHeaderBackgroundChanged(this, change);
            else if (change.Property == HeaderForegroundProperty) OnHeaderForegroundChanged(this, change);
            else if (change.Property == HeaderPaddingProperty) OnHeaderPaddingChanged(this, change);
            else if (change.Property == ContentPaddingProperty) OnContentPaddingChanged(this, change);
            else if (change.Property == ChevronSizeProperty) OnChevronSizeChanged(this, change);
            else if (change.Property == ShowChevronProperty) OnShowChevronChanged(this, change);
            else if (change.Property == IsEnabledProperty) UpdateColors();
            // Register for BorderBrush/BorderThickness changes (inherited from Control)
            else if (change.Property == BorderBrushProperty || change.Property == BorderThicknessProperty) UpdateBorder();
            else if (change.Property == ContentProperty) OnContentChanged(change.OldValue, change.NewValue);
        }

        private void OnHeaderClick(object? sender, RoutedEventArgs e)
        {
            ToggleExpanded();
        }

        private void ToggleExpanded()
        {
            IsExpanded = !IsExpanded;
        }

        private static void OnHeaderChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is DaisyExpander expander && expander._headerContent != null)
            {
                expander._headerContent.Content = e.NewValue;
            }
        }

        private static void OnIsExpandedChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is DaisyExpander expander)
            {
                expander.UpdateChevron();
                expander.UpdateContentVisibility();
            }
        }

        private static void OnHeaderBackgroundChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is DaisyExpander expander && expander._headerButton != null)
            {
                expander.UpdateColors();
            }
        }

        private static void OnHeaderForegroundChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is DaisyExpander expander)
            {
                expander.UpdateChevronColor();
            }
        }

        private static void OnHeaderPaddingChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is DaisyExpander expander)
            {
                expander.UpdateHeaderPadding();
            }
        }

        private static void OnContentPaddingChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is DaisyExpander expander)
            {
                expander.UpdateContentPadding();
            }
        }

        private static void OnChevronSizeChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is DaisyExpander expander && expander._chevron?.Parent is Viewbox viewbox)
            {
                var size = (double)e.NewValue!;
                viewbox.Width = size;
                viewbox.Height = size;
            }
        }

        private static void OnShowChevronChanged(AvaloniaObject d, AvaloniaPropertyChangedEventArgs e)
        {
            if (d is DaisyExpander expander && expander._chevron?.Parent is Viewbox viewbox)
            {
                viewbox.IsVisible = (bool)e.NewValue!;
            }
        }

        private void UpdateChevron()
        {
            if (_chevron == null)
                return;

            try
            {
                var pathData = IsExpanded ? FloweryPathHelpers.ChevronUpPath : FloweryPathHelpers.ChevronDownPath;
                _chevron.Data = Geometry.Parse(pathData);
            }
            catch
            {
                // Fallback if path parsing fails
            }

            UpdateChevronColor();
        }

        private void UpdateChevronColor()
        {
            if (_chevron == null)
                return;

            var foreground = HeaderForeground ?? DaisyResourceLookup.GetBrush("DaisyBaseContentBrush") ?? new SolidColorBrush(Colors.White);
            _chevron.Fill = foreground;
            if (DaisyAccessibility.IsHighContrast && _headerContent != null)
                _chevron.Bind(Shape.FillProperty, _headerContent.GetObservable(ContentPresenter.ForegroundProperty));
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
            UpdateChevronColor();

            if (_headerButton != null)
            {
                _headerButton.Background = HeaderBackground ?? new SolidColorBrush(Colors.Transparent);
                if (DaisyAccessibility.IsHighContrast)
                    _headerButton.Background = DaisyResourceLookup.GetBrush("DaisyBase100Brush");
            }
            if (_contentPresenter != null)
            {
                if (DaisyAccessibility.IsHighContrast)
                    _contentPresenter.Foreground = DaisyResourceLookup.GetBrush(IsEnabled ? "DaisyBaseContentBrush" : "DaisyBaseContentDisabledBrush");
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

        // private void LogContentState(string context)
        // {
        //     if (_loggedContent)
        //     {
        //         return;
        //     }

        //     _loggedContent = true;
        //     var content = ExpanderContent;
        //     var typeName = content?.GetType().FullName ?? "<null>";
        //     var countInfo = "<n/a>";

        //     if (content is Panel panel)
        //     {
        //         countInfo = panel.Children.Count.ToString();
        //     }
        //     else if (content is ItemsControl itemsControl)
        //     {
        //         countInfo = itemsControl.Items?.Count.ToString() ?? "0";
        //     }
        //     else if (content is System.Collections.ICollection collection)
        //     {
        //         countInfo = collection.Count.ToString();
        //     }

        //     FloweryDiagnostics.Log($"{DateTimeOffset.Now:O} [DaisyExpander] {context}: contentType={typeName}, count={countInfo}");
        // }

        private void OnContentChanged(object? oldContent, object? newContent)
        {
            // The base.Content is our layout panel, actual content goes to _contentPresenter
            if (_contentPresenter != null && newContent != Content)
            {
                _contentPresenter.Content = newContent;
                // FloweryDiagnostics.Log($"[DaisyExpander] Content changed: type={newContent?.GetType().FullName ?? "<null>"}");
            }
        }

        /// <summary>
        /// Sets the actual expandable content (not the layout panel).
        /// Use this instead of Content property.
        /// </summary>
        public object? ExpanderContent
        {
            get => _contentPresenter?.Content;
            set
            {
                if (_contentPresenter != null)
                {
                    _contentPresenter.Content = value;
                }
            }
        }

    }
}
