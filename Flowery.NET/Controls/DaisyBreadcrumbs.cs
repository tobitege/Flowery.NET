using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// Breadcrumbs component that helps users navigate through the website.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyBreadcrumbs : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyBreadcrumbs);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        /// <summary>
        /// Gets or sets the separator character/content between breadcrumb items.
        /// Default is "/" but can be changed to ">" or custom content.
        /// </summary>
        public static readonly StyledProperty<string> SeparatorProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbs, string>(nameof(Separator), "/");

        /// <summary>
        /// Gets or sets the opacity of the separator.
        /// </summary>
        public static readonly StyledProperty<double> SeparatorOpacityProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbs, double>(nameof(SeparatorOpacity), 0.5);

        public string Separator
        {
            get => GetValue(SeparatorProperty);
            set => SetValue(SeparatorProperty, value);
        }

        public double SeparatorOpacity
        {
            get => GetValue(SeparatorOpacityProperty);
            set => SetValue(SeparatorOpacityProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemCountProperty)
            {
                UpdateItemStates();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            UpdateItemStates();
        }

        private void UpdateItemStates()
        {
            int count = ItemCount;
            for (int i = 0; i < count; i++)
            {
                var container = ContainerFromIndex(i);
                if (container is DaisyBreadcrumbItem item)
                {
                    item.SetCurrentValue(DaisyBreadcrumbItem.IsFirstProperty, i == 0);
                    item.SetCurrentValue(DaisyBreadcrumbItem.IsLastProperty, i == count - 1);
                    item.SetCurrentValue(DaisyBreadcrumbItem.IndexProperty, i);
                    item.SetCurrentValue(DaisyBreadcrumbItem.SeparatorProperty, Separator);
                    item.SetCurrentValue(DaisyBreadcrumbItem.SeparatorOpacityProperty, SeparatorOpacity);
                }
            }
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            return new DaisyBreadcrumbItem();
        }

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            recycleKey = null;
            return item is not DaisyBreadcrumbItem;
        }

        protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
        {
            base.PrepareContainerForItemOverride(container, item, index);

            if (container is DaisyBreadcrumbItem breadcrumbItem)
            {
                int count = ItemCount;
                breadcrumbItem.SetCurrentValue(DaisyBreadcrumbItem.IsFirstProperty, index == 0);
                breadcrumbItem.SetCurrentValue(DaisyBreadcrumbItem.IsLastProperty, index == count - 1);
                breadcrumbItem.SetCurrentValue(DaisyBreadcrumbItem.IndexProperty, index);
                breadcrumbItem.SetCurrentValue(DaisyBreadcrumbItem.SeparatorProperty, Separator);
                breadcrumbItem.SetCurrentValue(DaisyBreadcrumbItem.SeparatorOpacityProperty, SeparatorOpacity);
            }
        }
    }

    /// <summary>
    /// Individual breadcrumb item that can display content with an optional icon.
    /// </summary>
    public class DaisyBreadcrumbItem : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyBreadcrumbItem);

        /// <summary>
        /// Gets or sets whether this is the first item (hides separator).
        /// </summary>
        public static readonly StyledProperty<bool> IsFirstProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbItem, bool>(nameof(IsFirst));

        /// <summary>
        /// Gets or sets whether this is the last item (typically not clickable).
        /// </summary>
        public static readonly StyledProperty<bool> IsLastProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbItem, bool>(nameof(IsLast));

        /// <summary>
        /// Gets or sets the index of this item in the breadcrumbs.
        /// </summary>
        public static readonly StyledProperty<int> IndexProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbItem, int>(nameof(Index));

        /// <summary>
        /// Gets or sets the icon geometry to display before the content.
        /// </summary>
        public static readonly StyledProperty<Geometry?> IconProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbItem, Geometry?>(nameof(Icon));

        /// <summary>
        /// Gets or sets the separator text displayed before this item.
        /// </summary>
        public static readonly StyledProperty<string> SeparatorProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbItem, string>(nameof(Separator), "/");

        /// <summary>
        /// Gets or sets the opacity of the separator.
        /// </summary>
        public static readonly StyledProperty<double> SeparatorOpacityProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbItem, double>(nameof(SeparatorOpacity), 0.5);

        /// <summary>
        /// Gets or sets the command to execute when the breadcrumb item is clicked.
        /// </summary>
        public static readonly StyledProperty<ICommand?> CommandProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbItem, ICommand?>(nameof(Command));

        /// <summary>
        /// Gets or sets the parameter to pass to the Command.
        /// </summary>
        public static readonly StyledProperty<object?> CommandParameterProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbItem, object?>(nameof(CommandParameter));

        /// <summary>
        /// Gets or sets whether the item is clickable/interactive.
        /// Default is true for all items except the last one.
        /// </summary>
        public static readonly StyledProperty<bool> IsClickableProperty =
            AvaloniaProperty.Register<DaisyBreadcrumbItem, bool>(nameof(IsClickable), true);

        public bool IsFirst
        {
            get => GetValue(IsFirstProperty);
            set => SetValue(IsFirstProperty, value);
        }

        public bool IsLast
        {
            get => GetValue(IsLastProperty);
            set => SetValue(IsLastProperty, value);
        }

        public int Index
        {
            get => GetValue(IndexProperty);
            set => SetValue(IndexProperty, value);
        }

        public Geometry? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string Separator
        {
            get => GetValue(SeparatorProperty);
            set => SetValue(SeparatorProperty, value);
        }

        public double SeparatorOpacity
        {
            get => GetValue(SeparatorOpacityProperty);
            set => SetValue(SeparatorOpacityProperty, value);
        }

        public ICommand? Command
        {
            get => GetValue(CommandProperty);
            set => SetValue(CommandProperty, value);
        }

        public object? CommandParameter
        {
            get => GetValue(CommandParameterProperty);
            set => SetValue(CommandParameterProperty, value);
        }

        public bool IsClickable
        {
            get => GetValue(IsClickableProperty);
            set => SetValue(IsClickableProperty, value);
        }

        protected override void OnPointerPressed(Avalonia.Input.PointerPressedEventArgs e)
        {
            base.OnPointerPressed(e);

            if (IsClickable && !IsLast && Command != null)
            {
                if (Command.CanExecute(CommandParameter))
                {
                    Command.Execute(CommandParameter);
                }
            }
        }
    }
}
