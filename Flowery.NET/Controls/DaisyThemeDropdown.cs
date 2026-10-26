using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Media;
using Flowery.Localization;
using Flowery.Services;
using Flowery.Theming;

namespace Flowery.Controls
{
    /// <summary>
    /// Contains preview information for a theme including colors and localized display name.
    /// </summary>
    public class ThemePreviewInfo : IThemeListItem
    {
        /// <summary>
        /// Internal theme name (e.g., "Synthwave"). Used as key for theme application.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Localized display name for the theme (e.g., "Synth Wave" in German).
        /// Falls back to Name if no localization is available.
        /// </summary>
        public string DisplayName => DisplayNameOverride ?? FloweryLocalization.GetThemeDisplayName(Name);

        internal string? DisplayNameOverride { get; init; }

        /// <summary>Whether the popup draws a group separator before this theme.</summary>
        public bool HasSeparatorBefore { get; private set; }

        IThemeListItem IThemeListItem.WithSeparator(bool show)
        {
            if (HasSeparatorBefore == show) return this;
            var copy = (ThemePreviewInfo)MemberwiseClone();
            copy.HasSeparatorBefore = show;
            return copy;
        }

        public bool IsDark { get; set; }
        public IBrush Base100 { get; set; } = Brushes.Gray;
        public IBrush BaseContent { get; set; } = Brushes.Gray;
        public IBrush Primary { get; set; } = Brushes.Gray;
        public IBrush Secondary { get; set; } = Brushes.Gray;
        public IBrush Accent { get; set; } = Brushes.Gray;
    }

    /// <summary>
    /// A dropdown for selecting themes with visual theme previews.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyThemeDropdown : ComboBox, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyThemeDropdown);

        private const double BaseTextFontSize = 13.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<string> SelectedThemeProperty =
            AvaloniaProperty.Register<DaisyThemeDropdown, string>(nameof(SelectedTheme), "Light");

        public string SelectedTheme
        {
            get => GetValue(SelectedThemeProperty);
            set => SetValue(SelectedThemeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property for the dropdown's appearance.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyThemeDropdown, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of this dropdown control.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        private readonly ThemeDropdownSource<ThemePreviewInfo> _themeSource;
        private bool _isSyncing;

        public bool IsCurrentThemeDark => DaisyThemeManager.IsCurrentThemeDark;


        /// <summary>
        /// Clears cached preview entries so they are rebuilt on the next read.
        /// </summary>
        public static void InvalidateThemeCache()
        {
            ThemeListResolver.InvalidateStandardThemes();
        }

        public DaisyThemeDropdown()
        {
            // Enable keyboard navigation by DisplayName (e.g., press 'S' to jump to "Synthwave")
            TextSearch.SetTextBinding(this, new Binding(nameof(ThemePreviewInfo.DisplayName)));

            _themeSource = new ThemeDropdownSource<ThemePreviewInfo>(this,
                ThemeListResolver.StandardThemes, ThemeListResolver.CreatePreview, EnsureItemsSourceCurrent);
            var themes = _themeSource.Resolve();
            _themeSource.SetView(themes);

            // Sync to current theme if one is already set by the app
            var currentTheme = DaisyThemeManager.CurrentThemeName;
            if (!string.IsNullOrEmpty(currentTheme))
            {
                SyncToTheme(currentTheme!, themes);
            }
            else
            {
                // No theme set yet - use default without triggering ApplyTheme
                _isSyncing = true;
                try
                {
                    SelectedIndex = themes.FindIndex(t => t.Name == "Dark");
                }
                finally
                {
                    _isSyncing = false;
                }
            }
        }

        private void SyncToTheme(string themeName, IEnumerable<ThemePreviewInfo>? themes = null)
        {
            if (_isSyncing) return;
            themes ??= ItemsSource as IEnumerable<ThemePreviewInfo> ?? _themeSource.Resolve();
            var match = themes.FirstOrDefault(t => string.Equals(t.Name, themeName, StringComparison.OrdinalIgnoreCase));
            _isSyncing = true;
            try
            {
                SetCurrentValue(SelectedItemProperty, match);
                SetCurrentValue(SelectedThemeProperty, match?.Name ?? themeName);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        /// <summary>
        /// Adds a standard, registered, or product theme to this dropdown without replacing
        /// the standard list. Repeated original names update the entry without duplicating it.
        /// Call on the UI thread. This does not select, apply, or register a theme.
        /// </summary>
        /// <param name="originalName">Original theme name, matched case-insensitively.</param>
        /// <param name="displayName">Text shown only in this dropdown.</param>
        /// <param name="preferred">Whether to place the entry in the preferred group at the top.</param>
        /// <exception cref="ArgumentException">A name is empty or the original theme is unknown.</exception>
        public void AddTheme(string originalName, string displayName, bool preferred) =>
            _themeSource.Add(originalName, displayName, preferred);

        /// <summary>
        /// Removes a local entry that <see cref="AddTheme"/> added and restores the manager's list entry, if available.
        /// </summary>
        public bool RemoveAddedTheme(string originalName) => _themeSource.Remove(originalName);

        /// <inheritdoc cref="RemoveAddedTheme"/>
        [Obsolete("Use RemoveAddedTheme, the counterpart of AddTheme.")]
        public bool RemoveThemeOverride(string originalName) => RemoveAddedTheme(originalName);

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedThemeProperty && !_isSyncing && _themeSource != null)
            {
                var name = SelectedTheme;
                SyncToTheme(name);
                if (SelectedItem is ThemePreviewInfo selected) ApplyTheme(selected);
            }
            else if (change.Property == SelectedItemProperty && change.NewValue is ThemePreviewInfo themeInfo)
            {
                if (!string.Equals(SelectedTheme, themeInfo.Name, StringComparison.OrdinalIgnoreCase))
                    SyncToTheme(themeInfo.Name);
                if (!_isSyncing)
                {
                    ApplyTheme(themeInfo);
                }
            }
        }

        private void ApplyTheme(ThemePreviewInfo themeInfo)
        {
            if (DaisyThemeManager.GetThemeInfo(themeInfo.Name) == null &&
                ProductPaletteFactory.FindByName(themeInfo.Name) is { } product)
            {
                DaisyThemeManager.EnsureProductThemeRegistered(product);
            }
            DaisyThemeManager.ApplyTheme(themeInfo.Name);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            _themeSource.Attach();
            SyncWithCurrentTheme();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
            _themeSource.Detach();
        }


        private void OnThemeChanged(object? sender, string themeName)
        {
            EnsureItemsSourceCurrent();
            SyncWithCurrentTheme();
        }

        private void EnsureItemsSourceCurrent()
        {
            var themes = _themeSource.Resolve();
            var themeName = DaisyThemeManager.CurrentThemeName ?? SelectedTheme;
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
            SyncToTheme(themeName, themes);
        }

        private void SyncWithCurrentTheme()
        {
            var currentTheme = DaisyThemeManager.CurrentThemeName;
            if (string.IsNullOrEmpty(currentTheme)) return;

            SyncToTheme(currentTheme!);
        }
    }
}
