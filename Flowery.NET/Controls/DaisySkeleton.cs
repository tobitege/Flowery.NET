using System;
using Avalonia;
using Avalonia.Controls;

namespace Flowery.Controls
{
    /// <summary>
    /// A Skeleton control styled after DaisyUI's Skeleton component.
    /// Used to show a loading state placeholder.
    /// </summary>
    public class DaisySkeleton : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisySkeleton);

        /// <summary>
        /// Defines the <see cref="IsTextMode"/> property.
        /// When true, animates text color instead of background (skeleton-text).
        /// </summary>
        public static readonly StyledProperty<bool> IsTextModeProperty =
            AvaloniaProperty.Register<DaisySkeleton, bool>(nameof(IsTextMode), false);

        /// <summary>
        /// Gets or sets whether to use text animation mode.
        /// When true, animates the foreground/text color with a gradient effect instead of background opacity.
        /// </summary>
        public bool IsTextMode
        {
            get => GetValue(IsTextModeProperty);
            set => SetValue(IsTextModeProperty, value);
        }

    }
}
