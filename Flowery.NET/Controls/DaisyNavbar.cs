using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A top navigation bar styled after DaisyUI's Navbar component.
    /// Provides Start, Center, and End content areas for flexible layout.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyNavbar : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyNavbar);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        /// <summary>
        /// Gets or sets the content for the start (left) section of the navbar.
        /// This area fills 50% of the width and aligns content to the start.
        /// </summary>
        public static readonly StyledProperty<object?> NavbarStartProperty =
            AvaloniaProperty.Register<DaisyNavbar, object?>(nameof(NavbarStart));

        public object? NavbarStart
        {
            get => GetValue(NavbarStartProperty);
            set => SetValue(NavbarStartProperty, value);
        }

        /// <summary>
        /// Gets or sets the content for the center section of the navbar.
        /// This area is centered horizontally in the navbar.
        /// </summary>
        public static readonly StyledProperty<object?> NavbarCenterProperty =
            AvaloniaProperty.Register<DaisyNavbar, object?>(nameof(NavbarCenter));

        public object? NavbarCenter
        {
            get => GetValue(NavbarCenterProperty);
            set => SetValue(NavbarCenterProperty, value);
        }

        /// <summary>
        /// Gets or sets the content for the end (right) section of the navbar.
        /// This area fills the remaining 50% of the width and aligns content to the end.
        /// </summary>
        public static readonly StyledProperty<object?> NavbarEndProperty =
            AvaloniaProperty.Register<DaisyNavbar, object?>(nameof(NavbarEnd));

        public object? NavbarEnd
        {
            get => GetValue(NavbarEndProperty);
            set => SetValue(NavbarEndProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the navbar uses full-width styling (no corner radius).
        /// </summary>
        public static readonly StyledProperty<bool> IsFullWidthProperty =
            AvaloniaProperty.Register<DaisyNavbar, bool>(nameof(IsFullWidth), false);

        public bool IsFullWidth
        {
            get => GetValue(IsFullWidthProperty);
            set => SetValue(IsFullWidthProperty, value);
        }

        /// <summary>
        /// Gets or sets whether the navbar has a shadow effect.
        /// </summary>
        public static readonly StyledProperty<bool> HasShadowProperty =
            AvaloniaProperty.Register<DaisyNavbar, bool>(nameof(HasShadow), true);

        public bool HasShadow
        {
            get => GetValue(HasShadowProperty);
            set => SetValue(HasShadowProperty, value);
        }

        /// <summary>
        /// Gets or sets whether any of the section properties (Start, Center, End) are set.
        /// Used internally for template switching.
        /// </summary>
        public static readonly StyledProperty<bool> HasSectionContentProperty =
            AvaloniaProperty.Register<DaisyNavbar, bool>(nameof(HasSectionContent), false);

        public bool HasSectionContent
        {
            get => GetValue(HasSectionContentProperty);
            private set => SetValue(HasSectionContentProperty, value);
        }

        private void UpdateHasSectionContent()
        {
            HasSectionContent = NavbarStart != null || NavbarCenter != null || NavbarEnd != null;
        }

        static DaisyNavbar()
        {
            NavbarStartProperty.Changed.AddClassHandler<DaisyNavbar>((x, _) => x.UpdateHasSectionContent());
            NavbarCenterProperty.Changed.AddClassHandler<DaisyNavbar>((x, _) => x.UpdateHasSectionContent());
            NavbarEndProperty.Changed.AddClassHandler<DaisyNavbar>((x, _) => x.UpdateHasSectionContent());
        }
    }
}
