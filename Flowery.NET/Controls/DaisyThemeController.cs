using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum ThemeControllerMode
    {
        Toggle,
        Checkbox,
        Swap,
        ToggleWithText,
        ToggleWithIcons
    }

    /// <summary>
    /// A toggle control for switching between light/dark themes.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyThemeController : ToggleButton, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyThemeController);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        private bool _isSyncing;

        public bool IsCurrentThemeDark => DaisyThemeManager.IsCurrentThemeDark;

        public static readonly StyledProperty<ThemeControllerMode> ModeProperty =
            AvaloniaProperty.Register<DaisyThemeController, ThemeControllerMode>(nameof(Mode), ThemeControllerMode.Toggle);

        public ThemeControllerMode Mode
        {
            get => GetValue(ModeProperty);
            set => SetValue(ModeProperty, value);
        }

        public static readonly StyledProperty<string> UncheckedLabelProperty =
            AvaloniaProperty.Register<DaisyThemeController, string>(nameof(UncheckedLabel), "Light");

        public string UncheckedLabel
        {
            get => GetValue(UncheckedLabelProperty);
            set => SetValue(UncheckedLabelProperty, value);
        }

        public static readonly StyledProperty<string> CheckedLabelProperty =
            AvaloniaProperty.Register<DaisyThemeController, string>(nameof(CheckedLabel), "Dark");

        public string CheckedLabel
        {
            get => GetValue(CheckedLabelProperty);
            set => SetValue(CheckedLabelProperty, value);
        }

        public static readonly StyledProperty<string> UncheckedThemeProperty =
            AvaloniaProperty.Register<DaisyThemeController, string>(nameof(UncheckedTheme), "Light");

        public string UncheckedTheme
        {
            get => GetValue(UncheckedThemeProperty);
            set => SetValue(UncheckedThemeProperty, value);
        }

        public static readonly StyledProperty<string> CheckedThemeProperty =
            AvaloniaProperty.Register<DaisyThemeController, string>(nameof(CheckedTheme), "Dark");

        public string CheckedTheme
        {
            get => GetValue(CheckedThemeProperty);
            set => SetValue(CheckedThemeProperty, value);
        }

        static DaisyThemeController()
        {
            IsCheckedProperty.Changed.AddClassHandler<DaisyThemeController>((x, e) => x.OnIsCheckedChanged(e));
        }

        private void OnIsCheckedChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (_isSyncing) return;

            var newValue = e.NewValue as bool?;
            var targetTheme = newValue == true ? CheckedTheme : UncheckedTheme;
            DaisyThemeManager.ApplyTheme(targetTheme);
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
            UpdateAlternateTheme(themeName);
        }

        private void SyncWithCurrentTheme()
        {
            _isSyncing = true;
            try
            {
                var currentTheme = DaisyThemeManager.CurrentThemeName;
                var isBaseTheme = string.Equals(currentTheme, UncheckedTheme, StringComparison.OrdinalIgnoreCase);
                SetCurrentValue(IsCheckedProperty, !isBaseTheme);
            }
            finally
            {
                _isSyncing = false;
            }
        }

        private void UpdateAlternateTheme(string themeName)
        {
            // If the new theme is not the base/unchecked theme, update the checked theme
            if (!string.Equals(themeName, UncheckedTheme, StringComparison.OrdinalIgnoreCase))
            {
                SetCurrentValue(CheckedThemeProperty, themeName);
                SetCurrentValue(CheckedLabelProperty, themeName);
            }
        }
    }
}
