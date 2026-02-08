using System;
using Avalonia;

namespace Flowery.Controls
{
    public class DaisyThemeSwap : DaisySwap
    {
        protected override Type StyleKeyOverride => typeof(DaisyThemeSwap);

        public bool IsCurrentThemeDark => DaisyThemeManager.IsCurrentThemeDark;

        public static readonly StyledProperty<string> LightThemeProperty =
            AvaloniaProperty.Register<DaisyThemeSwap, string>(nameof(LightTheme), "Light");

        public static readonly StyledProperty<string> DarkThemeProperty =
            AvaloniaProperty.Register<DaisyThemeSwap, string>(nameof(DarkTheme), "Dark");

        public string LightTheme
        {
            get => GetValue(LightThemeProperty);
            set => SetValue(LightThemeProperty, value);
        }

        public string DarkTheme
        {
            get => GetValue(DarkThemeProperty);
            set => SetValue(DarkThemeProperty, value);
        }

        public DaisyThemeSwap()
        {
            TransitionEffect = SwapEffect.Rotate;
            DaisyThemeManager.ThemeChanged += OnThemeChanged;
        }

        protected override void OnAttachedToVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            SyncState();
        }

        protected override void OnClick()
        {
            base.OnClick();
            ToggleTheme();
        }

        private void ToggleTheme()
        {
            var currentTheme = DaisyThemeManager.CurrentThemeName;
            var isDark = currentTheme != null && DaisyThemeManager.IsDarkTheme(currentTheme);

            if (isDark)
            {
                DaisyThemeManager.ApplyTheme(LightTheme);
            }
            else
            {
                DaisyThemeManager.ApplyTheme(DarkTheme);
            }
        }

        private void OnThemeChanged(object? sender, string themeName)
        {
            SyncState();
        }

        private void SyncState()
        {
            IsChecked = IsCurrentThemeDark;
        }

        protected override void OnDetachedFromVisualTree(global::Avalonia.VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            DaisyThemeManager.ThemeChanged -= OnThemeChanged;
        }
    }
}
