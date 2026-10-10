using System;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyStatVariant
    {
        Default,
        Primary,
        Secondary,
        Accent,
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// A Stat display control styled after DaisyUI's Stat component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyStat : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyStat);

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, Size), 10.0, scaleFactor);
            LabelFontSize = FloweryScaleManager.ApplyScale(FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Secondary, Size), 9.0, scaleFactor);
            ValueFontSize = FloweryScaleManager.ApplyScale(GetValueFontSize(Size), 14.0, scaleFactor);
        }

        /// <summary>
        /// Code-side counterpart of the DaisyStat*ValueFontSize tokens.
        /// </summary>
        private static double GetValueFontSize(DaisySize size) => size switch
        {
            DaisySize.ExtraSmall => 18,
            DaisySize.Small => 20,
            DaisySize.Medium => 24,
            DaisySize.Large => 28,
            DaisySize.ExtraLarge => 32,
            _ => 24
        };

        /// <summary>
        /// Defines the <see cref="Size"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyStat, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size tier. Drives <see cref="ValueFontSize"/>, <see cref="LabelFontSize"/>
        /// and the control font through the theme.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ValueFontSize"/> property.
        /// </summary>
        public static readonly StyledProperty<double> ValueFontSizeProperty =
            AvaloniaProperty.Register<DaisyStat, double>(nameof(ValueFontSize), 24.0);

        /// <summary>
        /// Gets or sets the font size of the value text. Set by the theme per <see cref="Size"/>.
        /// </summary>
        public double ValueFontSize
        {
            get => GetValue(ValueFontSizeProperty);
            set => SetValue(ValueFontSizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="LabelFontSize"/> property.
        /// </summary>
        public static readonly StyledProperty<double> LabelFontSizeProperty =
            AvaloniaProperty.Register<DaisyStat, double>(nameof(LabelFontSize), 12.0);

        /// <summary>
        /// Gets or sets the font size of the title and description. Set by the theme per <see cref="Size"/>.
        /// </summary>
        public double LabelFontSize
        {
            get => GetValue(LabelFontSizeProperty);
            set => SetValue(LabelFontSizeProperty, value);
        }

        public static readonly StyledProperty<string> TitleProperty =
            AvaloniaProperty.Register<DaisyStat, string>(nameof(Title));

        public string Title
        {
            get => GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly StyledProperty<string> ValueProperty =
            AvaloniaProperty.Register<DaisyStat, string>(nameof(Value));

        public string Value
        {
            get => GetValue(ValueProperty);
            set => SetValue(ValueProperty, value);
        }

        public static readonly StyledProperty<string> DescriptionProperty =
            AvaloniaProperty.Register<DaisyStat, string>(nameof(Description));

        public string Description
        {
            get => GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly StyledProperty<object?> FigureProperty =
            AvaloniaProperty.Register<DaisyStat, object?>(nameof(Figure));

        public object? Figure
        {
            get => GetValue(FigureProperty);
            set => SetValue(FigureProperty, value);
        }

        public static readonly StyledProperty<object?> ActionsProperty =
            AvaloniaProperty.Register<DaisyStat, object?>(nameof(Actions));

        public object? Actions
        {
            get => GetValue(ActionsProperty);
            set => SetValue(ActionsProperty, value);
        }

        public static readonly StyledProperty<bool> IsCenteredProperty =
            AvaloniaProperty.Register<DaisyStat, bool>(nameof(IsCentered), false);

        public bool IsCentered
        {
            get => GetValue(IsCenteredProperty);
            set => SetValue(IsCenteredProperty, value);
        }

        public static readonly StyledProperty<DaisyStatVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyStat, DaisyStatVariant>(nameof(Variant), DaisyStatVariant.Default);

        public DaisyStatVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisyStatVariant> DescriptionVariantProperty =
            AvaloniaProperty.Register<DaisyStat, DaisyStatVariant>(nameof(DescriptionVariant), DaisyStatVariant.Default);

        public DaisyStatVariant DescriptionVariant
        {
            get => GetValue(DescriptionVariantProperty);
            set => SetValue(DescriptionVariantProperty, value);
        }
    }

    public class DaisyStats : ItemsControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyStats);

        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyStats, Orientation>(nameof(Orientation), Orientation.Horizontal);

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        static DaisyStats()
        {
            OrientationProperty.Changed.AddClassHandler<DaisyStats>((x, _) => x.UpdateChildBorders());
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            UpdateChildBorders();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ItemCountProperty)
            {
                UpdateChildBorders();
            }
        }

        private void UpdateChildBorders()
        {
            var items = Items;
            if (items == null) return;

            var dividerBrush = this.FindResource("DaisyBase300Brush") as IBrush ?? Brushes.Gray;
            var isHorizontal = Orientation == Orientation.Horizontal;
            var index = 0;

            foreach (var item in items)
            {
                if (item is DaisyStat stat)
                {
                    if (index == 0)
                    {
                        stat.BorderThickness = new Thickness(0);
                    }
                    else
                    {
                        stat.BorderThickness = isHorizontal ? new Thickness(1, 0, 0, 0) : new Thickness(0, 1, 0, 0);
                        stat.BorderBrush = dividerBrush;
                    }
                }
                index++;
            }
        }
    }
}
