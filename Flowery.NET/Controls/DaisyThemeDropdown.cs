using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Contains preview information for a theme including colors and localized display name.
    /// </summary>
    public class ThemePreviewInfo
    {
        /// <summary>
        /// Internal theme name (e.g., "Synthwave"). Used as key for theme application.
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Localized display name for the theme (e.g., "Synth Wave" in German).
        /// Falls back to Name if no localization is available.
        /// </summary>
        public string DisplayName => FloweryLocalization.GetThemeDisplayName(Name);

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

        private static List<ThemePreviewInfo>? _cachedThemes;
        private bool _isSyncing;

        public bool IsCurrentThemeDark => DaisyThemeManager.IsCurrentThemeDark;

        public DaisyThemeDropdown()
        {
            // Enable keyboard navigation by DisplayName (e.g., press 'S' to jump to "Synthwave")
            TextSearch.SetTextBinding(this, new Binding(nameof(ThemePreviewInfo.DisplayName)));

            var themes = GetThemeInfos();
            ItemsSource = themes;

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

        private void SyncToTheme(string themeName, List<ThemePreviewInfo>? themes = null)
        {
            themes ??= GetThemeInfos();
            var match = themes.FirstOrDefault(t => string.Equals(t.Name, themeName, StringComparison.OrdinalIgnoreCase));
            if (match != null && SelectedItem != match)
            {
                _isSyncing = true;
                try
                {
                    SelectedItem = match;
                    SelectedTheme = match.Name;
                }
                finally
                {
                    _isSyncing = false;
                }
            }
        }

        private static List<ThemePreviewInfo> GetThemeInfos()
        {
            if (_cachedThemes != null) return _cachedThemes;

            _cachedThemes = new List<ThemePreviewInfo>();

            foreach (var themeInfo in DaisyThemeManager.AvailableThemes)
            {
                var preview = new ThemePreviewInfo { Name = themeInfo.Name, IsDark = themeInfo.IsDark };

                try
                {
                    var paletteUri = new Uri($"avares://Flowery.NET/Themes/Palettes/Daisy{themeInfo.Name}.axaml");
                    var palette = (ResourceDictionary)AvaloniaXamlLoader.Load(paletteUri);

                    if (palette.TryGetResource("DaisyBase100Brush", null, out var base100) && base100 is IBrush b100)
                        preview.Base100 = b100;
                    if (palette.TryGetResource("DaisyBaseContentBrush", null, out var baseContent) && baseContent is IBrush bcb)
                        preview.BaseContent = bcb;
                    if (palette.TryGetResource("DaisyPrimaryBrush", null, out var primary) && primary is IBrush pb)
                        preview.Primary = pb;
                    if (palette.TryGetResource("DaisySecondaryBrush", null, out var secondary) && secondary is IBrush sb)
                        preview.Secondary = sb;
                    if (palette.TryGetResource("DaisyAccentBrush", null, out var accent) && accent is IBrush ab)
                        preview.Accent = ab;
                }
                catch
                {
                    // Use defaults
                }

                _cachedThemes.Add(preview);
            }

            return _cachedThemes;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedItemProperty && change.NewValue is ThemePreviewInfo themeInfo)
            {
                SelectedTheme = themeInfo.Name;
                if (!_isSyncing)
                {
                    ApplyTheme(themeInfo);
                }
            }
        }

        private void ApplyTheme(ThemePreviewInfo themeInfo)
        {
            DaisyThemeManager.ApplyTheme(themeInfo.Name);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            FloweryLocalization.CultureChanged += OnCultureChanged;
            SyncWithCurrentTheme();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
            FloweryLocalization.CultureChanged -= OnCultureChanged;
        }

        private void OnCultureChanged(object? sender, CultureInfo culture)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnCultureChanged(sender, culture));
                return;
            }

            // Force UI refresh when culture changes (DisplayName property will return new value)
            InvalidateVisual();
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
    }
}
