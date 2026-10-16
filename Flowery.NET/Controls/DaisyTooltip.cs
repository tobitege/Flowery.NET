using System;
using Avalonia;
using Avalonia.Controls;

namespace Flowery.Controls
{
    /// <summary>
    /// A tooltip control styled after DaisyUI's Tooltip component.
    /// Wraps content and shows <see cref="Tip"/> in a themed tooltip on hover.
    /// </summary>
    /// <remarks>
    /// The tooltip uses the Daisy <c>ToolTip</c> styles, so <see cref="Variant"/> maps to the same
    /// classes (<c>primary</c>, <c>secondary</c>, ...) that can be set on a plain <c>ToolTip</c>.
    /// </remarks>
    public class DaisyTooltip : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(ContentControl);

        private readonly ToolTip _toolTip = new();
        private bool _isSyncingIsOpen;

        /// <summary>
        /// Defines the <see cref="Tip"/> property.
        /// </summary>
        public static readonly StyledProperty<string?> TipProperty =
            AvaloniaProperty.Register<DaisyTooltip, string?>(nameof(Tip), string.Empty);

        /// <summary>
        /// Gets or sets the tooltip text. No tooltip is shown while it is empty.
        /// </summary>
        public string? Tip
        {
            get => GetValue(TipProperty);
            set => SetValue(TipProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Variant"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisyColor> VariantProperty =
            AvaloniaProperty.Register<DaisyTooltip, DaisyColor>(nameof(Variant), DaisyColor.Default);

        /// <summary>
        /// Gets or sets the tooltip color. <see cref="DaisyColor.Default"/> uses the neutral tooltip colors.
        /// </summary>
        public DaisyColor Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Placement"/> property.
        /// </summary>
        public static readonly StyledProperty<PlacementMode> PlacementProperty =
            AvaloniaProperty.Register<DaisyTooltip, PlacementMode>(nameof(Placement), PlacementMode.Top);

        /// <summary>
        /// Gets or sets where the tooltip is placed relative to the content.
        /// </summary>
        public PlacementMode Placement
        {
            get => GetValue(PlacementProperty);
            set => SetValue(PlacementProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="IsOpen"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> IsOpenProperty =
            AvaloniaProperty.Register<DaisyTooltip, bool>(nameof(IsOpen));

        /// <summary>
        /// Gets or sets whether the tooltip is shown. Follows hover changes as well.
        /// </summary>
        public bool IsOpen
        {
            get => GetValue(IsOpenProperty);
            set => SetValue(IsOpenProperty, value);
        }

        public DaisyTooltip()
        {
            ApplyTip();
            ApplyVariant();
            ToolTip.SetPlacement(this, Placement);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TipProperty)
            {
                ApplyTip();
            }
            else if (change.Property == VariantProperty)
            {
                ApplyVariant();
            }
            else if (change.Property == PlacementProperty)
            {
                ToolTip.SetPlacement(this, Placement);
            }
            else if (change.Property == IsOpenProperty)
            {
                if (!_isSyncingIsOpen)
                {
                    ToolTip.SetIsOpen(this, IsOpen && HasTip);
                }
            }
            else if (change.Property == ToolTip.IsOpenProperty)
            {
                _isSyncingIsOpen = true;
                try
                {
                    SetCurrentValue(IsOpenProperty, change.GetNewValue<bool>());
                }
                finally
                {
                    _isSyncingIsOpen = false;
                }
            }
        }

        private bool HasTip => !string.IsNullOrEmpty(Tip);

        private void ApplyTip()
        {
            _toolTip.Content = Tip;
            ToolTip.SetTip(this, HasTip ? _toolTip : null);
        }

        private void ApplyVariant()
        {
            _toolTip.Classes.Set("primary", Variant == DaisyColor.Primary);
            _toolTip.Classes.Set("secondary", Variant == DaisyColor.Secondary);
            _toolTip.Classes.Set("accent", Variant == DaisyColor.Accent);
            _toolTip.Classes.Set("info", Variant == DaisyColor.Info);
            _toolTip.Classes.Set("success", Variant == DaisyColor.Success);
            _toolTip.Classes.Set("warning", Variant == DaisyColor.Warning);
            _toolTip.Classes.Set("error", Variant == DaisyColor.Error);
        }
    }
}
