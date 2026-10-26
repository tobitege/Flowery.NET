using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Flowery.Helpers;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// Preview information for a product theme.
    /// </summary>
    public class ProductThemePreviewInfo : IThemeListItem
    {
        /// <summary>Product palette, or null for a registered standard theme entry.</summary>
        public ProductPalette? Palette { get; }

        public string Name { get; }
        public string DisplayName { get; }

        /// <summary>Whether the popup draws a group separator before this theme.</summary>
        public bool HasSeparatorBefore { get; private set; }

        IThemeListItem IThemeListItem.WithSeparator(bool show)
        {
            if (HasSeparatorBefore == show) return this;
            var copy = (ProductThemePreviewInfo)MemberwiseClone();
            copy.HasSeparatorBefore = show;
            return copy;
        }

        public IBrush Primary { get; }
        public IBrush Secondary { get; }
        public IBrush Accent { get; }
        public IBrush Base100 { get; }
        public IBrush BaseContent { get; }

        public ProductThemePreviewInfo(ProductPalette palette)
        {
            Palette = palette;
            Name = palette.Name;
            DisplayName = palette.Name;
            Primary = ParseBrush(palette.Primary);
            Secondary = ParseBrush(palette.Secondary);
            Accent = ParseBrush(palette.Accent);
            Base100 = ParseBrush(palette.Background);
            BaseContent = ParseBrush(palette.Text);
        }

        internal ProductThemePreviewInfo(DaisyAddedTheme entry)
        {
            var preview = ThemeListResolver.CreatePreview(entry);
            Palette = entry.ProductPalette;
            Name = preview.Name;
            DisplayName = preview.DisplayName;
            Primary = preview.Primary;
            Secondary = preview.Secondary;
            Accent = preview.Accent;
            Base100 = preview.Base100;
            BaseContent = preview.BaseContent;
        }

        private static IBrush ParseBrush(string hex)
        {
            if (FloweryColorHelpers.TryParseColor(hex, out var color))
                return new SolidColorBrush(color);
            return Brushes.Transparent;
        }
    }

    /// <summary>
    /// A dropdown for selecting product themes (palettes).
    /// Registers and applies the selected product palette via DaisyThemeManager.
    /// </summary>
    public class DaisyProductThemeDropdown : ComboBox
    {
        protected override Type StyleKeyOverride => typeof(DaisyProductThemeDropdown);

        private readonly ThemeDropdownSource<ProductThemePreviewInfo> _themeSource;
        private bool _isSyncing;

        public static readonly StyledProperty<string> SelectedThemeProperty =
            AvaloniaProperty.Register<DaisyProductThemeDropdown, string>(nameof(SelectedTheme), "SaaS");

        /// <summary>
        /// Defines whether selecting an item applies it immediately.
        /// </summary>
        public static readonly StyledProperty<bool> ApplyOnSelectionProperty =
            AvaloniaProperty.Register<DaisyProductThemeDropdown, bool>(nameof(ApplyOnSelection), true);

        public string SelectedTheme
        {
            get => GetValue(SelectedThemeProperty);
            set => SetValue(SelectedThemeProperty, value);
        }

        /// <summary>
        /// When true (default), selecting a product theme immediately applies it globally.
        /// Set to false to update selection state only.
        /// </summary>
        public bool ApplyOnSelection
        {
            get => GetValue(ApplyOnSelectionProperty);
            set => SetValue(ApplyOnSelectionProperty, value);
        }

        /// <summary>
        /// Raised when a product theme is selected from the dropdown.
        /// </summary>
        public event EventHandler<string>? ProductThemeSelected;

        public DaisyProductThemeDropdown()
        {
            MinWidth = 200;
            _themeSource = new ThemeDropdownSource<ProductThemePreviewInfo>(this,
                ThemeListResolver.ProductThemes, static entry => new ProductThemePreviewInfo(entry), RefreshThemes);
        }

        /// <summary>Adds or updates a local entry while retaining the product list and global additions.</summary>
        /// <param name="originalName">Original registered or product theme name.</param>
        /// <param name="displayName">Text shown only in this dropdown.</param>
        /// <param name="preferred">Whether to place the entry in the preferred group, sorted by display name.</param>
        public void AddTheme(string originalName, string displayName, bool preferred) =>
            _themeSource.Add(originalName, displayName, preferred);

        /// <summary>Removes a local entry that <see cref="AddTheme"/> added and restores the global or product list entry, if available.</summary>
        public bool RemoveAddedTheme(string originalName) => _themeSource.Remove(originalName);

        /// <inheritdoc cref="RemoveAddedTheme"/>
        [Obsolete("Use RemoveAddedTheme, the counterpart of AddTheme.")]
        public bool RemoveThemeOverride(string originalName) => RemoveAddedTheme(originalName);

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            _themeSource.Attach();

            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            SyncWithCurrentTheme();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
            _themeSource.Detach();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedItemProperty && change.NewValue is ProductThemePreviewInfo info)
            {
                SetCurrentValue(SelectedThemeProperty, info.Name);
                if (!_isSyncing)
                {
                    ApplyProductTheme(info);
                }
            }
            else if (change.Property == SelectedThemeProperty && change.NewValue is string name)
            {
                SyncToTheme(name);
            }
        }

        private void ApplyProductTheme(ProductThemePreviewInfo info)
        {
            ProductThemeSelected?.Invoke(this, info.Name);

            if (!ApplyOnSelection)
                return;

            if (info.Palette is { } palette)
                DaisyThemeManager.EnsureProductThemeRegistered(palette);
            DaisyThemeManager.ApplyTheme(info.Name);
        }

        private void OnThemeChanged(object? sender, string themeName)
        {
            SyncWithCurrentTheme();
        }

        private void RefreshThemes()
        {
            var themes = _themeSource.Resolve();
            var themeName = SelectedTheme;
            var wasSyncing = _isSyncing;
            _isSyncing = true;
            try
            {
                _themeSource.SetView(themes);
            }
            finally
            {
                _isSyncing = wasSyncing;
            }
            SyncToTheme(themeName);
        }

        private void SyncWithCurrentTheme()
        {
            var currentTheme = DaisyThemeManager.CurrentThemeName;
            if (string.IsNullOrEmpty(currentTheme)) return;

            SyncToTheme(currentTheme!);
        }

        private void SyncToTheme(string themeName)
        {
            if (_isSyncing) return;
            _isSyncing = true;

            try
            {
                if (ItemsSource is IEnumerable<ProductThemePreviewInfo> items)
                {
                    var match = items.FirstOrDefault(i => string.Equals(i.Name, themeName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        SetCurrentValue(SelectedItemProperty, match);
                        SetCurrentValue(SelectedThemeProperty, match.Name);
                    }
                    else
                    {
                        // Deselect if current theme is not in product list (e.g. system theme)
                        SetCurrentValue(SelectedItemProperty, null);
                        SetCurrentValue(SelectedThemeProperty, themeName);
                    }
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }


        /// <summary>
        /// Clears cached preview entries so they are rebuilt on next attach.
        /// </summary>
        public static void InvalidateThemeCache()
        {
            ThemeListResolver.InvalidateProductThemes();
        }

    }
}
