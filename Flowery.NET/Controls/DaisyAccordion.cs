using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.LogicalTree;
using Avalonia.VisualTree;
using Flowery.Services;

namespace Flowery.Controls
{
    /// <summary>
    /// An accordion control that displays multiple collapsible sections.
    /// Supports automatic font scaling when contained within a FloweryScaleManager.EnableScaling="True" container.
    /// </summary>
    public class DaisyAccordion : ItemsControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyAccordion);

        private readonly Dictionary<DaisyAccordionItem, IDisposable> _itemSizeBindings = new();

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            var baseFontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, Size);
            FontSize = FloweryScaleManager.ApplyScale(baseFontSize, 10.0, scaleFactor);
        }

        public static readonly StyledProperty<DaisyCollapseVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyAccordion, DaisyCollapseVariant>(nameof(Variant), DaisyCollapseVariant.Arrow);

        /// <summary>
        /// Defines the <see cref="Size"/> property. The size is forwarded to every item.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyAccordion, DaisySize>(nameof(Size), DaisySize.Medium);

        public static readonly StyledProperty<int> ExpandedIndexProperty =
            AvaloniaProperty.Register<DaisyAccordion, int>(nameof(ExpandedIndex), -1);

        public DaisyCollapseVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        public int ExpandedIndex
        {
            get => GetValue(ExpandedIndexProperty);
            set => SetValue(ExpandedIndexProperty, value);
        }

        /// <summary>
        /// Gets or sets the size tier of the accordion headers and content padding.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == ExpandedIndexProperty)
            {
                UpdateExpandedStates();
            }
            else if (change.Property == ItemCountProperty || change.Property == VariantProperty || change.Property == SizeProperty)
            {
                SyncItems();
            }
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            SyncItems();
            UpdateExpandedStates();
        }

        internal void OnItemExpanded(DaisyAccordionItem expandedItem)
        {
            var items = this.GetLogicalChildren().OfType<DaisyAccordionItem>().ToList();
            int index = items.IndexOf(expandedItem);
            if (index >= 0)
            {
                ExpandedIndex = index;
            }

            foreach (var item in items)
            {
                if (item != expandedItem && item.IsExpanded)
                {
                    item.SetCurrentValue(DaisyAccordionItem.IsExpandedProperty, false);
                }
            }
        }

        private void UpdateExpandedStates()
        {
            var items = this.GetLogicalChildren().OfType<DaisyAccordionItem>().ToList();
            for (int i = 0; i < items.Count; i++)
            {
                items[i].SetCurrentValue(DaisyAccordionItem.IsExpandedProperty, i == ExpandedIndex);
            }
        }

        private void SyncItems()
        {
            var items = this.GetLogicalChildren().OfType<DaisyAccordionItem>().ToList();
            foreach (var item in items)
            {
                item.SetCurrentValue(DaisyAccordionItem.VariantProperty, Variant);

                // Items without their own Size follow the accordion through a binding, so an
                // explicit item Size and the global size registry both keep working.
                if (!_itemSizeBindings.ContainsKey(item) && !item.GetBaseValue(DaisyAccordionItem.SizeProperty).HasValue)
                {
                    _itemSizeBindings[item] = item.Bind(DaisyAccordionItem.SizeProperty, this.GetObservable(SizeProperty));
                }
            }

            foreach (var removed in _itemSizeBindings.Keys.Where(item => !items.Contains(item)).ToList())
            {
                _itemSizeBindings[removed].Dispose();
                _itemSizeBindings.Remove(removed);
            }
        }
    }

    public class DaisyAccordionItem : HeaderedContentControl, IScalableControl
    {
        protected override Type StyleKeyOverride => typeof(DaisyAccordionItem);

        private DaisyAccordion? _parentAccordion;

        public static readonly StyledProperty<bool> IsExpandedProperty =
            AvaloniaProperty.Register<DaisyAccordionItem, bool>(nameof(IsExpanded));

        public static readonly StyledProperty<DaisyCollapseVariant> VariantProperty =
            AvaloniaProperty.Register<DaisyAccordionItem, DaisyCollapseVariant>(nameof(Variant), DaisyCollapseVariant.Arrow);

        /// <summary>
        /// Defines the <see cref="Size"/> property. Set by the parent accordion unless set explicitly.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisyAccordionItem, DaisySize>(nameof(Size), DaisySize.Medium);

        public bool IsExpanded
        {
            get => GetValue(IsExpandedProperty);
            set => SetValue(IsExpandedProperty, value);
        }

        public DaisyCollapseVariant Variant
        {
            get => GetValue(VariantProperty);
            set => SetValue(VariantProperty, value);
        }

        /// <summary>
        /// Gets or sets the size tier of the header and content padding.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        /// <inheritdoc/>
        public void ApplyScaleFactor(double scaleFactor)
        {
            var baseFontSize = FlowerySizeManager.GetFontSizeForTier(ResponsiveFontTier.Primary, Size);
            FontSize = FloweryScaleManager.ApplyScale(baseFontSize, 10.0, scaleFactor);
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == IsExpandedProperty && IsExpanded)
            {
                _parentAccordion?.OnItemExpanded(this);
            }
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            _parentAccordion = this.FindAncestorOfType<DaisyAccordion>();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            _parentAccordion = null;
        }
    }
}
