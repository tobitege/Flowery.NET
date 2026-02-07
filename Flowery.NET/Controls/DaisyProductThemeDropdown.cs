using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Flowery.Helpers;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// Preview information for a product theme.
    /// </summary>
    public class ProductThemePreviewInfo
    {
        public ProductPalette Palette { get; }

        public string Name => Palette.Name;
        public string DisplayName => Palette.Name;

        public IBrush Primary { get; }
        public IBrush Secondary { get; }
        public IBrush Accent { get; }
        public IBrush Base100 { get; }
        public IBrush BaseContent { get; }

        public ProductThemePreviewInfo(ProductPalette palette)
        {
            Palette = palette;
            Primary = ParseBrush(palette.Primary);
            Secondary = ParseBrush(palette.Secondary);
            Accent = ParseBrush(palette.Accent);
            Base100 = ParseBrush(palette.Background);
            BaseContent = ParseBrush(palette.Text);
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

        private static List<ProductThemePreviewInfo>? _cachedThemes;
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
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);

            if (ItemCount == 0)
            {
                ItemsSource = GetThemeInfos();
            }

            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            SyncWithCurrentTheme();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedItemProperty && change.NewValue is ProductThemePreviewInfo info)
            {
                if (!_isSyncing)
                {
                    ApplyProductTheme(info);
                }
                SelectedTheme = info.Name;
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

            EnsureThemeRegistered(info.Name, info.Palette);
            DaisyThemeManager.ApplyTheme(info.Name);
        }

        private void OnThemeChanged(object? sender, string themeName)
        {
            SyncWithCurrentTheme();
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
                        SelectedItem = match;
                        SelectedTheme = match.Name;
                    }
                    else
                    {
                        // Deselect if current theme is not in product list (e.g. system theme)
                        SelectedItem = null;
                        SelectedTheme = themeName; // Keep property in sync even if not in dropdown
                    }
                }
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private static List<ProductThemePreviewInfo> GetThemeInfos()
        {
            if (_cachedThemes != null)
                return _cachedThemes;

            var palettes = ProductPaletteFactory.GetAll();
            _cachedThemes = palettes.Select(p => new ProductThemePreviewInfo(p)).ToList();
            return _cachedThemes;
        }

        /// <summary>
        /// Clears cached preview entries so they are rebuilt on next attach.
        /// </summary>
        public static void InvalidateThemeCache()
        {
            _cachedThemes = null;
        }

        private static void EnsureThemeRegistered(string themeName, ProductPalette fallbackPalette)
        {
            if (DaisyThemeManager.GetThemeInfo(themeName) != null)
                return;

            // Prefer precompiled palette data when available.
            var precompiled = ProductPalettes.Get(themeName);
            if (precompiled != null)
            {
                var isDark = FloweryColorHelpers.IsDark(precompiled.Base100);
                var info = new DaisyThemeInfo(themeName, isDark);
                DaisyThemeManager.RegisterTheme(info, () => DaisyPaletteFactory.Create(precompiled));
                return;
            }

            var fallbackIsDark = FloweryColorHelpers.IsDark(fallbackPalette.Background);
            var fallbackInfo = new DaisyThemeInfo(themeName, fallbackIsDark);
            DaisyThemeManager.RegisterTheme(fallbackInfo, () => ProductPaletteFactory.CreateResourceDictionary(fallbackPalette));
        }
    }
}
