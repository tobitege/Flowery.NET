using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Services;

namespace Flowery.Controls
{
    public enum DaisySelectVariant
    {
        Bordered,
        Ghost,
        Primary,
        Secondary,
        Accent,
        Info,
        Success,
        Warning,
        Error
    }

    /// <summary>
    /// A ComboBox control styled after DaisyUI's Select component.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisySelect : ComboBox, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisySelect);

        // Base font size for scaling
        private const double BaseTextFontSize = 14.0;

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            FontSize = FloweryScaleManager.ApplyScale(BaseTextFontSize, 11.0, scaleFactor);
        }

        public static readonly StyledProperty<DaisySelectVariant> VariantProperty =
            AvaloniaProperty.Register<DaisySelect, DaisySelectVariant>(nameof(Variant), DaisySelectVariant.Bordered);

        public DaisySelectVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisySelect, DaisySize>(nameof(Size), DaisySize.Medium);

        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        // Browser/WASM root cause:
        // Opening a pre-selected ComboBox focuses the selected item, which triggers a BringIntoView request.
        // That request bubbles to the page ScrollViewer (ScrollContentPresenter) and scrolls the page, AFTER the popup is positioned,
        // resulting in the popup showing up at a wrong Y offset.
        // Fix strategy:
        // - Suppress BringIntoView for ComboBoxItem only during the "opening" window (prevents the induced scroll).
        // - Keep a scroll-restore fallback (in case something still scrolls).
        // - Re-focus the selected item after open to preserve keyboard behavior.
        private ScrollViewer? _parentScrollViewer;
        private double _scrollOffsetBeforeOpen;
        private bool _suppressBringIntoViewOnOpen;

        static DaisySelect()
        {
            // Disable auto-scroll to selected item (partial mitigation)
            AutoScrollToSelectedItemProperty.OverrideDefaultValue<DaisySelect>(false);
            // Root fix: prevent ComboBoxItem BringIntoView from bubbling to the parent ScrollViewer during dropdown open.
            RequestBringIntoViewEvent.AddClassHandler<DaisySelect>((x, e) => x.OnRequestBringIntoView(e));
            // Hook dropdown state changes
            IsDropDownOpenProperty.Changed.AddClassHandler<DaisySelect>((x, e) => x.OnDropDownOpenChanged(e));
        }

        public DaisySelect()
        {
            DropDownOpened += OnDropDownOpened;
        }

        private void OnDropDownOpenChanged(AvaloniaPropertyChangedEventArgs e)
        {
            if (!e.GetNewValue<bool>())
            {
                _suppressBringIntoViewOnOpen = false;
                _parentScrollViewer = null;
                return;
            }

            _suppressBringIntoViewOnOpen = true;
            _parentScrollViewer = FindParentScrollViewer();
            _scrollOffsetBeforeOpen = _parentScrollViewer?.Offset.Y ?? 0;
        }

        private ScrollViewer? FindParentScrollViewer()
        {
            for (var parent = this.GetVisualParent(); parent != null; parent = parent.GetVisualParent())
            {
                if (parent is ScrollViewer sv)
                {
                    return sv;
                }
            }

            return null;
        }

        private void OnRequestBringIntoView(RequestBringIntoViewEventArgs e)
        {
            // Suppress ONLY the dropdown item BringIntoView while opening to prevent the page ScrollViewer from jumping.
            if (_suppressBringIntoViewOnOpen && e.TargetObject is ComboBoxItem)
                e.Handled = true;
        }

        private void OnDropDownOpened(object? sender, EventArgs e)
        {
            var capturedScrollViewer = _parentScrollViewer;
            var capturedScrollOffsetBeforeOpen = _scrollOffsetBeforeOpen;

            Dispatcher.UIThread.Post(() =>
            {
                _suppressBringIntoViewOnOpen = false;

                if (capturedScrollViewer is { } sv)
                {
                    // Fallback: if something still scrolled, restore the scroll so popup position remains correct.
                    var scrollDelta = sv.Offset.Y - capturedScrollOffsetBeforeOpen;

                    if (Math.Abs(scrollDelta) > 1)
                    {
                        sv.SetCurrentValue(ScrollViewer.OffsetProperty,
                            new Vector(sv.Offset.X, capturedScrollOffsetBeforeOpen));
                    }
                }

                var index = SelectedIndex;

                if (IsDropDownOpen && index >= 0)
                {
                    // Ensure the selected item is realized + focused (we suppressed BringIntoView during open).
                    var container = ContainerFromIndex(index);

                    if (container == null)
                    {
                        ScrollIntoView(index);
                        container = ContainerFromIndex(index);
                    }

                    container?.Focus();
                }
            }, DispatcherPriority.Loaded);
        }
    }
}
