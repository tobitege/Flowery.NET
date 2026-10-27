using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    /// The pseudo-class <c>:dragging</c> marks the range while the pointer holds its thumb or track, which the glass
    /// theme turns into a lens.
    /// </summary>
    [PseudoClasses(DraggingPseudoClass)]
    public class DaisyRange : Slider, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyRange);

        private const string DraggingPseudoClass = ":dragging";

        private const double BaseTextFontSize = 12.0;
        private RepeatButton? _increaseButton;
        private RepeatButton? _decreaseButton;
        private Thumb? _thumb;

        public DaisyRange()
        {
            // The slider marks its own presses as handled; the pseudo-class follows them anyway.
            AddHandler(PointerPressedEvent, OnAnyPointerPressed, RoutingStrategies.Tunnel, handledEventsToo: true);
            AddHandler(PointerReleasedEvent, OnAnyPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble, handledEventsToo: true);
            AddHandler(PointerCaptureLostEvent, OnAnyPointerCaptureLost, RoutingStrategies.Direct, handledEventsToo: true);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            // A press on the track captures the pointer in one of its buttons; losing that capture ends the drag too.
            foreach (var button in new[] { _increaseButton, _decreaseButton }.OfType<RepeatButton>())
                button.PointerCaptureLost -= OnAnyPointerCaptureLost;
            if (_thumb != null)
                _thumb.DragCompleted -= OnThumbDragCompleted;

            _increaseButton = e.NameScope.Find<RepeatButton>("PART_IncreaseButton");
            _decreaseButton = e.NameScope.Find<RepeatButton>("PART_DecreaseButton");
            _thumb = e.NameScope.Find<Thumb>("PART_Thumb");

            foreach (var button in new[] { _increaseButton, _decreaseButton }.OfType<RepeatButton>())
                button.PointerCaptureLost += OnAnyPointerCaptureLost;
            // Also raised when the thumb loses the pointer without a release.
            if (_thumb != null)
                _thumb.DragCompleted += OnThumbDragCompleted;

            UpdateAutomationParts();
        }

        private void OnAnyPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (IsEnabled && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                PseudoClasses.Set(DraggingPseudoClass, true);
        }

        private void OnAnyPointerReleased(object? sender, PointerReleasedEventArgs e) => PseudoClasses.Set(DraggingPseudoClass, false);

        private void OnAnyPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e) => PseudoClasses.Set(DraggingPseudoClass, false);

        private void OnThumbDragCompleted(object? sender, VectorEventArgs e) => PseudoClasses.Set(DraggingPseudoClass, false);

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

    /// <summary>
    /// Converts the bounds of the thumb into the horizontal position of its middle, where the progress of a template
    /// that draws it outside the track ends.
    /// </summary>
    internal sealed class RangeThumbCenterConverter : IValueConverter
    {
        public static RangeThumbCenterConverter Instance { get; } = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            value is Rect bounds ? bounds.X + bounds.Width / 2 : 0d;

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
            throw new NotSupportedException();
    }
}
