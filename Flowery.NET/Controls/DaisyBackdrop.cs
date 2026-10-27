using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Flowery.Controls
{
    /// <summary>
    /// The lowest layer of a window: draws the backdrop of the theme, the brush <c>DaisyBackdropBrush</c> and the animated
    /// effect <c>DaisyBackdropEffect</c>, and shows its content over them. The backdrop switches with the theme and with
    /// <see cref="DaisyThemeManager.BackdropOverride"/>. With reduced motion the effect shows a still frame; with high
    /// contrast it is hidden, and the brush is the plain background of the contrast colors.
    /// </summary>
    public class DaisyBackdrop : ContentControl
    {
        private Panel? _effectHost;
        private DaisyGlassBackground? _shownEffect;
        private bool _attached;

        /// <summary>
        /// Defines the <see cref="BackdropEffect"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyGlassBackground?> BackdropEffectProperty =
            AvaloniaProperty.Register<DaisyBackdrop, DaisyGlassBackground?>(nameof(BackdropEffect));

        /// <summary>
        /// Gets or sets the animated effect drawn over <see cref="TemplatedControl.Background"/>. The theme sets it to the
        /// resource <c>DaisyBackdropEffect</c>, which most themes leave out.
        /// </summary>
        public DaisyGlassBackground? BackdropEffect
        {
            get => GetValue(BackdropEffectProperty);
            set => SetValue(BackdropEffectProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _effectHost?.Children.Clear();
            _effectHost = e.NameScope.Find<Panel>("PART_EffectHost");
            UpdateEffect();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == BackdropEffectProperty)
                UpdateEffect();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _attached = true;
            DaisyAccessibility.PreferencesChanged += OnPreferencesChanged;
            UpdateEffect();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            _attached = false;
            DaisyAccessibility.PreferencesChanged -= OnPreferencesChanged;
            base.OnDetachedFromVisualTree(e);
        }

        private void OnPreferencesChanged(object? sender, EventArgs e) => UpdateEffect();

        // The layer of the effect is made for this backdrop alone, so the same effect object can serve every window.
        private void UpdateEffect()
        {
            if (_effectHost == null)
                return;
            var effect = _attached && !DaisyAccessibility.IsHighContrast ? BackdropEffect : null;
            if (_effectHost.Children.Count > 0 && ReferenceEquals(_shownEffect, effect))
                return;
            _effectHost.Children.Clear();
            _shownEffect = effect;
            if (effect != null)
                _effectHost.Children.Add(effect.CreateLayer());
        }
    }
}
