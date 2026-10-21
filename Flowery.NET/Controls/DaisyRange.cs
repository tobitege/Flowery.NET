using System;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Media;
using Flowery.Localization;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisyRangeVariant
    {
        Default,
        Primary,
        Secondary,
        Accent,
        Success,
        Warning,
        Info,
        Error
    }

    /// <summary>
    /// A Slider control styled after DaisyUI's Range component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyRange : Slider, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyRange);

        private const double BaseTextFontSize = 12.0;
        private RepeatButton? _increaseButton;
        private RepeatButton? _decreaseButton;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _increaseButton = e.NameScope.Find<RepeatButton>("PART_IncreaseButton");
            _decreaseButton = e.NameScope.Find<RepeatButton>("PART_DecreaseButton");
            UpdateAutomationParts();
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            FloweryLocalization.CultureChanged += OnCultureChanged;
            UpdateAutomationParts();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            FloweryLocalization.CultureChanged -= OnCultureChanged;
            base.OnDetachedFromVisualTree(e);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == AutomationProperties.NameProperty || change.Property == AutomationProperties.AutomationIdProperty)
                UpdateAutomationParts();
        }

        private void OnCultureChanged(object? sender, System.Globalization.CultureInfo culture)
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess()) UpdateAutomationParts();
            else Avalonia.Threading.Dispatcher.UIThread.Post(UpdateAutomationParts);
        }

        private void UpdateAutomationParts()
        {
            var name = AutomationProperties.GetName(this) ?? string.Empty;
            var id = AutomationProperties.GetAutomationId(this) ?? nameof(DaisyRange);
            SetPart(_increaseButton, "Accessibility_IncreaseValue", name, id + ".Increase");
            SetPart(_decreaseButton, "Accessibility_DecreaseValue", name, id + ".Decrease");
        }

        private static void SetPart(RepeatButton? button, string key, string name, string id)
        {
            if (button == null) return;
            AutomationProperties.SetName(button, string.Format(FloweryLocalization.CurrentCulture,
                FloweryLocalization.GetString(key), name).Trim());
            AutomationProperties.SetAutomationId(button, id);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<DaisyRangeVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyRange, DaisyRangeVariant>(nameof(Variant), DaisyRangeVariant.Default);

        public DaisyRangeVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyRange, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the range slider thumb (maps to --range-thumb).
        /// </summary>
        public static readonly StyledProperty<IBrush?> ThumbBrushProperty =
            AvaloniaProperty.Register<DaisyRange, IBrush?>(nameof(ThumbBrush));

        public IBrush? ThumbBrush
        {
            get => GetValue(ThumbBrushProperty);
            set => SetValue(ThumbBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets the size of the range slider thumb (maps to --range-thumb-size).
        /// </summary>
        public static readonly StyledProperty<double> ThumbSizeProperty =
            AvaloniaProperty.Register<DaisyRange, double>(nameof(ThumbSize), 24.0);

        public double ThumbSize
        {
            get => GetValue(ThumbSizeProperty);
            set => SetValue(ThumbSizeProperty, value);
        }

        /// <summary>
        /// Gets or sets the color of the range slider progress (maps to --range-progress).
        /// </summary>
        public static readonly StyledProperty<IBrush?> ProgressBrushProperty =
            AvaloniaProperty.Register<DaisyRange, IBrush?>(nameof(ProgressBrush));

        public IBrush? ProgressBrush
        {
            get => GetValue(ProgressBrushProperty);
            set => SetValue(ProgressBrushProperty, value);
        }

        /// <summary>
        /// Gets or sets whether to show the progress fill on the track (maps to --range-fill).
        /// </summary>
        public static readonly StyledProperty<bool> ShowProgressProperty =
            AvaloniaProperty.Register<DaisyRange, bool>(nameof(ShowProgress), false);

        public bool ShowProgress
        {
            get => GetValue(ShowProgressProperty);
            set => SetValue(ShowProgressProperty, value);
        }
    }
}
