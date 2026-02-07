using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Presenters;
using Avalonia.Layout;
using Avalonia.Threading;
using Flowery.Enums;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// An indicator control styled after DaisyUI's Indicator component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyIndicator : ContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyIndicator);

        private const double BaseTextFontSize = 12.0;
        private const double InsideInset = 3.0;

        private ContentPresenter? _mainPresenter;
        private ContentPresenter? _markerPresenter;
        private bool _pendingPositionUpdate;

        public DaisyIndicator()
        {
            AttachedToVisualTree += OnAttachedToVisualTree;
            DetachedFromVisualTree += OnDetachedFromVisualTree;
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<object?> MarkerProperty =
            AvaloniaProperty.Register<DaisyIndicator, object?>(nameof(Marker));

        /// <summary>
        /// Gets or sets the marker/overlay content to display at the specified position.
        /// Can be any control: DaisyBadge, TextBlock, Image, Border, etc.
        /// </summary>
        public object? Marker
        {
            get => GetValue(MarkerProperty);
            set => SetValue(MarkerProperty, value);
        }

        public static readonly StyledProperty<BadgePosition> MarkerPositionProperty =
            AvaloniaProperty.Register<DaisyIndicator, BadgePosition>(nameof(MarkerPosition), BadgePosition.TopRight);

        /// <summary>
        /// Gets or sets where the marker appears: TopLeft, TopRight, BottomLeft, BottomRight, etc.
        /// Default is TopRight.
        /// </summary>
        public BadgePosition MarkerPosition
        {
            get => GetValue(MarkerPositionProperty);
            set => SetValue(MarkerPositionProperty, value);
        }

        public static readonly StyledProperty<BadgeAlignment> MarkerAlignmentProperty =
            AvaloniaProperty.Register<DaisyIndicator, BadgeAlignment>(nameof(MarkerAlignment), BadgeAlignment.Inside);

        /// <summary>
        /// Gets or sets how the marker aligns to the corner:
        /// Inside (fully inside), Edge (straddles corner), Outside (mostly outside).
        /// Default is Inside.
        /// </summary>
        public BadgeAlignment MarkerAlignment
        {
            get => GetValue(MarkerAlignmentProperty);
            set => SetValue(MarkerAlignmentProperty, value);
        }

        public static readonly StyledProperty<object?> BadgeProperty =
            AvaloniaProperty.Register<DaisyIndicator, object?>(nameof(Badge));

        /// <summary>
        /// Legacy alias for Marker. Prefer Marker.
        /// </summary>
        public object? Badge
        {
            get => GetValue(BadgeProperty);
            set => SetValue(BadgeProperty, value);
        }

        public static readonly StyledProperty<HorizontalAlignment> BadgeHorizontalAlignmentProperty =
            AvaloniaProperty.Register<DaisyIndicator, HorizontalAlignment>(nameof(BadgeHorizontalAlignment), HorizontalAlignment.Right);

        /// <summary>
        /// Legacy alignment for the badge. Prefer MarkerPosition.
        /// </summary>
        public HorizontalAlignment BadgeHorizontalAlignment
        {
            get => GetValue(BadgeHorizontalAlignmentProperty);
            set => SetValue(BadgeHorizontalAlignmentProperty, value);
        }

        public static readonly StyledProperty<VerticalAlignment> BadgeVerticalAlignmentProperty =
            AvaloniaProperty.Register<DaisyIndicator, VerticalAlignment>(nameof(BadgeVerticalAlignment), VerticalAlignment.Top);

        /// <summary>
        /// Legacy alignment for the badge. Prefer MarkerPosition.
        /// </summary>
        public VerticalAlignment BadgeVerticalAlignment
        {
            get => GetValue(BadgeVerticalAlignmentProperty);
            set => SetValue(BadgeVerticalAlignmentProperty, value);
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);

            DetachPresenterHandlers();

            _mainPresenter = e.NameScope.Find<ContentPresenter>("PART_MainContent");
            _markerPresenter = e.NameScope.Find<ContentPresenter>("PART_MarkerPresenter");

            AttachPresenterHandlers();
            UpdateMarkerContent();
            QueueMarkerPositionUpdate();
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == MarkerProperty ||
                change.Property == BadgeProperty)
            {
                UpdateMarkerContent();
                QueueMarkerPositionUpdate();
            }
            else if (change.Property == MarkerPositionProperty ||
                     change.Property == MarkerAlignmentProperty ||
                     change.Property == BadgeHorizontalAlignmentProperty ||
                     change.Property == BadgeVerticalAlignmentProperty)
            {
                QueueMarkerPositionUpdate();
            }
        }

        private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            QueueMarkerPositionUpdate();
        }

        private void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            _pendingPositionUpdate = false;
        }

        private void AttachPresenterHandlers()
        {
            if (_mainPresenter != null)
            {
                _mainPresenter.SizeChanged += OnPresenterSizeChanged;
            }

            if (_markerPresenter != null)
            {
                _markerPresenter.SizeChanged += OnPresenterSizeChanged;
            }
        }

        private void DetachPresenterHandlers()
        {
            if (_mainPresenter != null)
            {
                _mainPresenter.SizeChanged -= OnPresenterSizeChanged;
            }

            if (_markerPresenter != null)
            {
                _markerPresenter.SizeChanged -= OnPresenterSizeChanged;
            }
        }

        private void OnPresenterSizeChanged(object? sender, SizeChangedEventArgs e)
        {
            QueueMarkerPositionUpdate();
        }

        private void UpdateMarkerContent()
        {
            if (_markerPresenter == null)
            {
                return;
            }

            var content = GetMarkerContent();
            _markerPresenter.Content = content;
            _markerPresenter.IsVisible = content != null;
        }

        private object? GetMarkerContent()
        {
            if (Marker != null || IsSet(MarkerProperty))
            {
                return Marker;
            }

            return Badge;
        }

        private void QueueMarkerPositionUpdate()
        {
            if (_pendingPositionUpdate)
            {
                return;
            }

            _pendingPositionUpdate = true;
            Dispatcher.UIThread.Post(() =>
            {
                _pendingPositionUpdate = false;
                UpdateMarkerPosition();
            }, DispatcherPriority.Render);
        }

        private void UpdateMarkerPosition()
        {
            if (_markerPresenter == null || _mainPresenter == null)
            {
                return;
            }

            var markerContent = GetMarkerContent();
            if (markerContent == null)
            {
                _markerPresenter.IsVisible = false;
                return;
            }

            _markerPresenter.IsVisible = true;

            var contentWidth = _mainPresenter.Bounds.Width;
            var contentHeight = _mainPresenter.Bounds.Height;
            var markerWidth = _markerPresenter.Bounds.Width;
            var markerHeight = _markerPresenter.Bounds.Height;

            if (contentWidth <= 0 || contentHeight <= 0 || markerWidth <= 0 || markerHeight <= 0)
            {
                return;
            }

            var position = GetEffectiveMarkerPosition();
            var alignment = GetEffectiveMarkerAlignment();
            var (left, top) = CalculateMarkerPosition(
                contentWidth,
                contentHeight,
                markerWidth,
                markerHeight,
                position,
                alignment);

            Canvas.SetLeft(_markerPresenter, left);
            Canvas.SetTop(_markerPresenter, top);
        }

        private BadgePosition GetEffectiveMarkerPosition()
        {
            if (!UseLegacyAlignment())
            {
                return MarkerPosition;
            }

            var horizontal = NormalizeHorizontalAlignment(BadgeHorizontalAlignment);
            var vertical = NormalizeVerticalAlignment(BadgeVerticalAlignment);

            return (vertical, horizontal) switch
            {
                (VerticalAlignment.Top, HorizontalAlignment.Left) => BadgePosition.TopLeft,
                (VerticalAlignment.Top, HorizontalAlignment.Center) => BadgePosition.TopCenter,
                (VerticalAlignment.Top, HorizontalAlignment.Right) => BadgePosition.TopRight,
                (VerticalAlignment.Center, HorizontalAlignment.Left) => BadgePosition.CenterLeft,
                (VerticalAlignment.Center, HorizontalAlignment.Center) => BadgePosition.Center,
                (VerticalAlignment.Center, HorizontalAlignment.Right) => BadgePosition.CenterRight,
                (VerticalAlignment.Bottom, HorizontalAlignment.Left) => BadgePosition.BottomLeft,
                (VerticalAlignment.Bottom, HorizontalAlignment.Center) => BadgePosition.BottomCenter,
                _ => BadgePosition.BottomRight
            };
        }

        private BadgeAlignment GetEffectiveMarkerAlignment()
        {
            return UseLegacyAlignment() ? BadgeAlignment.Edge : MarkerAlignment;
        }

        private bool UseLegacyAlignment()
        {
            if (IsMarkerPositionSet() || IsMarkerAlignmentSet())
            {
                return false;
            }

            return Badge != null ||
                   IsSet(BadgeProperty) ||
                   IsSet(BadgeHorizontalAlignmentProperty) ||
                   IsSet(BadgeVerticalAlignmentProperty);
        }

        private bool IsMarkerPositionSet()
        {
            return MarkerPosition != BadgePosition.TopRight || IsSet(MarkerPositionProperty);
        }

        private bool IsMarkerAlignmentSet()
        {
            return MarkerAlignment != BadgeAlignment.Inside || IsSet(MarkerAlignmentProperty);
        }

        private static HorizontalAlignment NormalizeHorizontalAlignment(HorizontalAlignment alignment)
        {
            return alignment switch
            {
                HorizontalAlignment.Center => HorizontalAlignment.Center,
                HorizontalAlignment.Right => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left
            };
        }

        private static VerticalAlignment NormalizeVerticalAlignment(VerticalAlignment alignment)
        {
            return alignment switch
            {
                VerticalAlignment.Center => VerticalAlignment.Center,
                VerticalAlignment.Bottom => VerticalAlignment.Bottom,
                _ => VerticalAlignment.Top
            };
        }

        /// <summary>
        /// Calculates the marker position based on semantic position and alignment.
        /// This logic can be reused by other controls.
        /// </summary>
        public static (double Left, double Top) CalculateMarkerPosition(
            double contentWidth, double contentHeight,
            double markerWidth, double markerHeight,
            BadgePosition position, BadgeAlignment alignment)
        {
            double left = 0;
            double top = 0;

            double factor = alignment switch
            {
                BadgeAlignment.Inside => 0.0,
                BadgeAlignment.Edge => 0.5,
                BadgeAlignment.Outside => 1.0,
                _ => 0.0
            };

            switch (position)
            {
                case BadgePosition.TopLeft:
                case BadgePosition.CenterLeft:
                case BadgePosition.BottomLeft:
                    left = -markerWidth * factor;
                    if (alignment == BadgeAlignment.Inside)
                    {
                        left += InsideInset;
                    }
                    break;

                case BadgePosition.TopCenter:
                case BadgePosition.Center:
                case BadgePosition.BottomCenter:
                    left = (contentWidth - markerWidth) / 2;
                    break;

                case BadgePosition.TopRight:
                case BadgePosition.CenterRight:
                case BadgePosition.BottomRight:
                    left = contentWidth - markerWidth + (markerWidth * factor);
                    if (alignment == BadgeAlignment.Inside)
                    {
                        left -= InsideInset;
                    }
                    break;
            }

            switch (position)
            {
                case BadgePosition.TopLeft:
                case BadgePosition.TopCenter:
                case BadgePosition.TopRight:
                    top = -markerHeight * factor;
                    if (alignment == BadgeAlignment.Inside)
                    {
                        top += InsideInset;
                    }
                    break;

                case BadgePosition.CenterLeft:
                case BadgePosition.Center:
                case BadgePosition.CenterRight:
                    top = (contentHeight - markerHeight) / 2;
                    break;

                case BadgePosition.BottomLeft:
                case BadgePosition.BottomCenter:
                case BadgePosition.BottomRight:
                    top = contentHeight - markerHeight + (markerHeight * factor);
                    if (alignment == BadgeAlignment.Inside)
                    {
                        top -= InsideInset;
                    }
                    break;
            }

            return (left, top);
        }
    }
}
