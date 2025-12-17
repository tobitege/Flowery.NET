using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// An image diff/comparison control styled after DaisyUI's Diff component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyDiff : TemplatedControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyDiff);

        private const double BaseTextFontSize = 12.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<object?> Image1Property =
            AvaloniaProperty.Register<DaisyDiff, object?>(nameof(Image1));

        public object? Image1
        {
            get => GetValue(Image1Property);
            set => SetValue(Image1Property, value);
        }

        public static readonly StyledProperty<object?> Image2Property =
            AvaloniaProperty.Register<DaisyDiff, object?>(nameof(Image2));

        public object? Image2
        {
            get => GetValue(Image2Property);
            set => SetValue(Image2Property, value);
        }

        public static readonly StyledProperty<double> OffsetProperty =
            AvaloniaProperty.Register<DaisyDiff, double>(nameof(Offset), 50.0, coerce: CoerceOffset);

        private static double CoerceOffset(AvaloniaObject obj, double value)
        {
            return Math.Max(0, Math.Min(100, value));
        }

        public double Offset
        {
            get => GetValue(OffsetProperty);
            set => SetValue(OffsetProperty, value);
        }

        private Control? _topImageContainer;
        private Control? _image1Presenter;
        private Control? _grip;
        private bool _isDragging;

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            if (_grip != null)
            {
                _grip.PointerPressed -= OnGripPointerPressed;
                _grip.PointerMoved -= OnGripPointerMoved;
                _grip.PointerReleased -= OnGripPointerReleased;
                _grip.PointerCaptureLost -= OnGripPointerCaptureLost;
            }

            _topImageContainer = e.NameScope.Find<Control>("PART_TopImageContainer");
            _image1Presenter = e.NameScope.Find<Control>("PART_Image1Presenter");
            _grip = e.NameScope.Find<Control>("PART_Grip");

            if (_grip != null)
            {
                _grip.PointerPressed += OnGripPointerPressed;
                _grip.PointerMoved += OnGripPointerMoved;
                _grip.PointerReleased += OnGripPointerReleased;
                _grip.PointerCaptureLost += OnGripPointerCaptureLost;
            }

            UpdateDiffLayout();
        }

        private void OnGripPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_grip == null) return;

            if (e.GetCurrentPoint(_grip).Properties.IsLeftButtonPressed)
            {
                _isDragging = true;
                e.Pointer.Capture(_grip);
                e.Handled = true;
            }
        }

        private void OnGripPointerMoved(object? sender, PointerEventArgs e)
        {
            if (_isDragging)
            {
                var pos = e.GetPosition(this);
                var width = Bounds.Width;
                if (width > 0)
                {
                    Offset = (pos.X / width) * 100.0;
                }
                e.Handled = true;
            }
        }

        private void OnGripPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDragging)
            {
                _isDragging = false;
                e.Pointer.Capture(null);
                e.Handled = true;
            }
        }

        private void OnGripPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            _isDragging = false;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == OffsetProperty || change.Property == BoundsProperty)
            {
                UpdateDiffLayout();
            }
        }

        private void UpdateDiffLayout()
        {
            var w = Bounds.Width;
            var h = Bounds.Height;
            if (w <= 0 || h <= 0) return;

            var clipWidth = w * (Offset / 100.0);

            if (_topImageContainer != null)
            {
                _topImageContainer.Width = clipWidth;
            }

            if (_image1Presenter != null)
            {
                _image1Presenter.Width = w;
            }

            if (_grip != null)
            {
                _grip.Height = h;
                Canvas.SetLeft(_grip, clipWidth);
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            var res = base.ArrangeOverride(finalSize);
            UpdateDiffLayout();
            return res;
        }
    }
}
