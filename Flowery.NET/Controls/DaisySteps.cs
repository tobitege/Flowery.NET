using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Layout;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyStepColor
    {
        Default,
        Neutral,
        Primary,
        Secondary,
        Accent,
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// A Steps control styled after DaisyUI's Steps component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisySteps : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisySteps);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisySteps, Orientation>(nameof(Orientation), Orientation.Horizontal);

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisySteps, DaisySize>(nameof(Size), DaisySize.Medium);

        public static readonly StyledProperty<int> SelectedIndexProperty =
            AvaloniaProperty.Register<DaisySteps, int>(nameof(SelectedIndex), -1);

        public static readonly StyledProperty<string?> JsonStepsProperty =
            AvaloniaProperty.Register<DaisySteps, string?>(nameof(JsonSteps));

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        public int SelectedIndex
        {
            get => GetValue(SelectedIndexProperty);
            set => SetValue(SelectedIndexProperty, value);
        }

        public string? JsonSteps
        {
            get => GetValue(JsonStepsProperty);
            set => SetValue(JsonStepsProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ItemCountProperty ||
                change.Property == OrientationProperty ||
                change.Property == SelectedIndexProperty ||
                change.Property == SizeProperty)
            {
                UpdateItemStates();
            }

            if (change.Property == JsonStepsProperty)
            {
                UpdateItemsSourceFromJson(change.GetNewValue<string?>());
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
                if (container is DaisyStepItem item)
                {
                    item.SetCurrentValue(DaisyStepItem.IsFirstProperty, i == 0);
                    item.SetCurrentValue(DaisyStepItem.IsLastProperty, i == count - 1);
                    item.SetCurrentValue(DaisyStepItem.IndexProperty, i);
                    item.SetCurrentValue(DaisyStepItem.OrientationProperty, Orientation);
                    item.SetCurrentValue(DaisyStepItem.SizeProperty, Size);

                    // Update active state based on SelectedIndex if set
                    if (SelectedIndex >= 0)
                    {
                        bool isActive = i <= SelectedIndex;
                        item.SetCurrentValue(DaisyStepItem.IsActiveProperty, isActive);
                    }
                }
            }
        }

        protected override Control CreateContainerForItemOverride(object? item, int index, object? recycleKey)
        {
            return new DaisyStepItem();
        }

        protected override bool NeedsContainerOverride(object? item, int index, out object? recycleKey)
        {
            recycleKey = null;
            return item is not DaisyStepItem;
        }

        protected override void PrepareContainerForItemOverride(Control container, object? item, int index)
        {
            base.PrepareContainerForItemOverride(container, item, index);

            if (container is DaisyStepItem stepItem)
            {
                int count = ItemCount;
                stepItem.SetCurrentValue(DaisyStepItem.IsFirstProperty, index == 0);
                stepItem.SetCurrentValue(DaisyStepItem.IsLastProperty, index == count - 1);
                stepItem.SetCurrentValue(DaisyStepItem.IndexProperty, index);
                stepItem.SetCurrentValue(DaisyStepItem.OrientationProperty, Orientation);
                stepItem.SetCurrentValue(DaisyStepItem.SizeProperty, Size);

                // Update active state based on SelectedIndex if set
                if (SelectedIndex >= 0)
                {
                    bool isActive = index <= SelectedIndex;
                    stepItem.SetCurrentValue(DaisyStepItem.IsActiveProperty, isActive);
                }

                if (item is DaisyStepModel model)
                {
                    stepItem.Content = model.Content;
                    if (!string.IsNullOrEmpty(model.Color) && Enum.TryParse<DaisyStepColor>(model.Color, true, out var color))
                    {
                        stepItem.Color = color;
                    }

                    // Only apply model's IsActive if SelectedIndex is not controlling it
                    if (SelectedIndex < 0)
                    {
                        stepItem.SetCurrentValue(DaisyStepItem.IsActiveProperty, model.IsActive);
                    }
                }
            }
        }

        private void UpdateItemsSourceFromJson(string? json)
        {
            var jsonValue = json?.Trim();
            if (jsonValue is null || jsonValue.Length == 0)
            {
                ItemsSource = null;
                return;
            }

            try
            {
                var options = new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };
                var data = System.Text.Json.JsonSerializer.Deserialize<System.Collections.Generic.List<DaisyStepModel>>(jsonValue, options);
                ItemsSource = data;
            }
            catch (System.Text.Json.JsonException)
            {
                ItemsSource = null;
            }
        }
    }

    public class DaisyStepItem : ContentControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyStepItem);

        public static readonly StyledProperty<DaisyStepColor> ColorProperty =
            AvaloniaProperty.Register<DaisyStepItem, DaisyStepColor>(nameof(Color), DaisyStepColor.Default);

        public static readonly StyledProperty<object?> IconProperty =
            AvaloniaProperty.Register<DaisyStepItem, object?>(nameof(Icon));

        public static readonly StyledProperty<string?> DataContentProperty =
            AvaloniaProperty.Register<DaisyStepItem, string?>(nameof(DataContent));

        public static readonly StyledProperty<bool> IsActiveProperty =
            AvaloniaProperty.Register<DaisyStepItem, bool>(nameof(IsActive));

        public static readonly StyledProperty<bool> IsFirstProperty =
            AvaloniaProperty.Register<DaisyStepItem, bool>(nameof(IsFirst));

        public static readonly StyledProperty<bool> IsLastProperty =
            AvaloniaProperty.Register<DaisyStepItem, bool>(nameof(IsLast));

        public static readonly StyledProperty<int> IndexProperty =
            AvaloniaProperty.Register<DaisyStepItem, int>(nameof(Index));

        public static readonly StyledProperty<Orientation> OrientationProperty =
            AvaloniaProperty.Register<DaisyStepItem, Orientation>(nameof(Orientation), Orientation.Horizontal);

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyStepItem, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisyStepColor Color
        {
            get => GetValue(ColorProperty);
            set => SetValue(ColorProperty, value);
        }

        public object? Icon
        {
            get => GetValue(IconProperty);
            set => SetValue(IconProperty, value);
        }

        public string? DataContent
        {
            get => GetValue(DataContentProperty);
            set => SetValue(DataContentProperty, value);
        }

        public bool IsActive
        {
            get => GetValue(IsActiveProperty);
            set => SetValue(IsActiveProperty, value);
        }

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

        public Orientation Orientation
        {
            get => GetValue(OrientationProperty);
            set => SetValue(OrientationProperty, value);
        }

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsActiveProperty ||
                change.Property == ColorProperty)
            {
                UpdatePseudoClasses();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            UpdatePseudoClasses();
        }

        private void UpdatePseudoClasses()
        {
            PseudoClasses.Set(":active", IsActive);
            PseudoClasses.Set(":neutral", Color == DaisyStepColor.Neutral);
            PseudoClasses.Set(":primary", Color == DaisyStepColor.Primary);
            PseudoClasses.Set(":secondary", Color == DaisyStepColor.Secondary);
            PseudoClasses.Set(":accent", Color == DaisyStepColor.Accent);
            PseudoClasses.Set(":info", Color == DaisyStepColor.Info);
            PseudoClasses.Set(":success", Color == DaisyStepColor.Success);
            PseudoClasses.Set(":warning", Color == DaisyStepColor.Warning);
            PseudoClasses.Set(":error", Color == DaisyStepColor.Error);
        }

    }

    /// <summary>
    /// Model for JSON deserialization of steps.
    /// </summary>
    public class DaisyStepModel
    {
        public string? Content { get; set; }
        public string? Color { get; set; }
        public bool IsActive { get; set; }
    }
}
