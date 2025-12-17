using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// A hover-activated image gallery control.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyHoverGallery : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyHoverGallery);

        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        private Panel? _dividersPanel;

        public static readonly StyledProperty<int> VisibleIndexProperty =
            AvaloniaProperty.Register<DaisyHoverGallery, int>(nameof(VisibleIndex), 0);

        public static readonly StyledProperty<IBrush?> DividerBrushProperty =
            AvaloniaProperty.Register<DaisyHoverGallery, IBrush?>(nameof(DividerBrush));

        public static readonly StyledProperty<double> DividerThicknessProperty =
            AvaloniaProperty.Register<DaisyHoverGallery, double>(nameof(DividerThickness), 1.0);

        public static readonly StyledProperty<bool> ShowDividersProperty =
            AvaloniaProperty.Register<DaisyHoverGallery, bool>(nameof(ShowDividers), true);

        public int VisibleIndex
        {
            get => GetValue(VisibleIndexProperty);
            set => SetValue(VisibleIndexProperty, value);
        }

        public IBrush? DividerBrush
        {
            get => GetValue(DividerBrushProperty);
            set => SetValue(DividerBrushProperty, value);
        }

        public double DividerThickness
        {
            get => GetValue(DividerThicknessProperty);
            set => SetValue(DividerThicknessProperty, value);
        }

        public bool ShowDividers
        {
            get => GetValue(ShowDividersProperty);
            set => SetValue(ShowDividersProperty, value);
        }

        static DaisyHoverGallery()
        {
            VisibleIndexProperty.Changed.AddClassHandler<DaisyHoverGallery>((x, _) => x.UpdateItemVisibility());
            ShowDividersProperty.Changed.AddClassHandler<DaisyHoverGallery>((x, _) => x.UpdateDividers());
            DividerBrushProperty.Changed.AddClassHandler<DaisyHoverGallery>((x, _) => x.UpdateDividers());
            DividerThicknessProperty.Changed.AddClassHandler<DaisyHoverGallery>((x, _) => x.UpdateDividers());
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            _dividersPanel = e.NameScope.Find<Panel>("PART_DividersPanel");
            Dispatcher.UIThread.Post(() =>
            {
                UpdateItemVisibility();
                UpdateDividers();
            }, DispatcherPriority.Loaded);
        }

        protected override void OnPointerMoved(PointerEventArgs e)
        {
            base.OnPointerMoved(e);
            UpdateVisibleIndex(e.GetPosition(this).X);
        }

        protected override void OnPointerExited(PointerEventArgs e)
        {
            base.OnPointerExited(e);
            VisibleIndex = 0;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);
            if (change.Property == ItemCountProperty)
            {
                Dispatcher.UIThread.Post(() =>
                {
                    UpdateItemVisibility();
                    UpdateDividers();
                }, DispatcherPriority.Loaded);
            }
            else if (change.Property == BoundsProperty)
            {
                UpdateDividers();
            }
        }

        private void UpdateVisibleIndex(double pointerX)
        {
            var count = ItemCount;
            if (count <= 1)
            {
                VisibleIndex = 0;
                return;
            }

            var width = Bounds.Width;
            if (width <= 0)
            {
                VisibleIndex = 0;
                return;
            }

            var columnCount = count - 1;
            var columnWidth = width / columnCount;
            var columnIndex = (int)(pointerX / columnWidth);
            columnIndex = Math.Max(0, Math.Min(columnIndex, columnCount - 1));
            VisibleIndex = columnIndex + 1;
        }

        private void UpdateItemVisibility()
        {
            var presenter = this.FindDescendantOfType<ItemsPresenter>();
            if (presenter == null) return;

            var panel = presenter.FindDescendantOfType<Panel>();
            if (panel == null) return;

            for (int i = 0; i < panel.Children.Count; i++)
            {
                panel.Children[i].IsVisible = (i == VisibleIndex);
            }
        }

        private void UpdateDividers()
        {
            if (_dividersPanel == null) return;
            _dividersPanel.Children.Clear();

            if (!ShowDividers) return;

            var count = ItemCount;
            if (count <= 2) return;

            var width = Bounds.Width;
            if (width <= 0) return;

            var columnCount = count - 1;
            var columnWidth = width / columnCount;
            var brush = DividerBrush ?? new SolidColorBrush(Color.FromArgb(80, 255, 255, 255));
            var thickness = DividerThickness;

            for (int i = 1; i < columnCount; i++)
            {
                var line = new Rectangle
                {
                    Width = thickness,
                    Fill = brush,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Stretch,
                    Margin = new Thickness(columnWidth * i - thickness / 2, 0, 0, 0),
                    IsHitTestVisible = false
                };
                _dividersPanel.Children.Add(line);
            }
        }
    }
}
