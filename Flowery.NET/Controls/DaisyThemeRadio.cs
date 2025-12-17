using System;
using Avalonia;
using Avalonia.Controls;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum ThemeRadioMode
    {
        Radio,
        Button
    }

    /// <summary>
    /// A RadioButton that applies a DaisyUI theme when selected.
    /// Use within a group to allow multi-theme selection.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyThemeRadio : RadioButton, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyThemeRadio);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        private bool _isSyncing;

        public static readonly StyledProperty<string> ThemeNameProperty =
            AvaloniaProperty.Register<DaisyThemeRadio, string>(nameof(ThemeName), string.Empty);

        /// <summary>
        /// The theme name to apply when this radio is checked (e.g., "Synthwave", "Retro").
        /// </summary>
        public string ThemeName
        {
            get => GetValue(ThemeNameProperty);
            set => SetValue(ThemeNameProperty, value);
        }

        public static readonly StyledProperty<ThemeRadioMode> ModeProperty =
            AvaloniaProperty.Register<DaisyThemeRadio, ThemeRadioMode>(nameof(Mode), ThemeRadioMode.Radio);

        /// <summary>
        /// Display mode: Radio (standard radio button) or Button (styled as a button).
        /// </summary>
        public ThemeRadioMode Mode
        {
            get => GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyThemeRadio, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        static DaisyThemeRadio()
        {
            IsCheckedProperty.Changed.AddClassHandler<DaisyThemeRadio>((x, e) => x.OnIsCheckedChanged(e));
        }

        private void OnIsCheckedChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_isSyncing) return;

            var newValue = e.NewValue as bool?;
            if (newValue == true && !string.IsNullOrEmpty(ThemeName))
            {
                DaisyThemeManager.ApplyTheme(ThemeName);
            }
        }

        protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
            SyncWithCurrentTheme();
        }

        protected override void OnDetachedFromVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
        }

        private void OnThemeChanged(object? sender, string themeName)
        {
            SyncWithCurrentTheme();
        }

        private void SyncWithCurrentTheme()
        {
            _isSyncing = true;
            try
            {
                var currentTheme = DaisyThemeManager.CurrentThemeName;
                var isThisTheme = string.Equals(currentTheme, ThemeName, StringComparison.OrdinalIgnoreCase);
                SetCurrentValue(IsCheckedProperty, isThisTheme);
            }
            finally
            {
                _isSyncing = false;
            }
        }
    }
}
