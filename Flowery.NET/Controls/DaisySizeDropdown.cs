using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Threading;
using Flowery.Localization;

namespace Flowery.Controls
{
    /// <summary>
    /// Contains display information for a DaisySize option.
    /// </summary>
    public class SizePreviewInfo
    {
        /// <summary>
        /// The DaisySize enum value.
        /// </summary>
        public DaisySize Size { get; set; }

        /// <summary>
        /// Internal size name (e.g., "ExtraSmall", "Medium").
        /// </summary>
        public string Name { get; set; } = "";

        /// <summary>
        /// Localized display name for the size.
        /// Falls back to Name if no localization is available.
        /// </summary>
        public string DisplayName => FloweryLocalization.GetString($"Size_{Name}") is string s && s != $"Size_{Name}" ? s : Name;

        /// <summary>
        /// Short abbreviation for compact display (e.g., "XS", "S", "M", "L", "XL").
        /// </summary>
        public string Abbreviation { get; set; } = "";
    }

    /// <summary>
    /// A dropdown control for selecting a global DaisySize.
    /// When the user selects a size, it is applied globally via <see cref="FlowerySizeManager"/>.
    /// </summary>
    /// <remarks>
    /// Controls that subscribe to <see cref="FlowerySizeManager.SizeChanged"/> will automatically
    /// update their Size property when a new size is selected.
    /// </remarks>
    /// <example>
    /// <code>
    /// &lt;controls:DaisySizeDropdown Width="120" /&gt;
    /// </code>
    /// </example>
    public class DaisySizeDropdown : ComboBox
    {
        protected override Type StyleKeyOverride => typeof(DaisySizeDropdown);

        /// <summary>
        /// Defines the <see cref="SelectedSize"/> property.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SelectedSizeProperty =
            AvaloniaProperty.Register<DaisySizeDropdown, DaisySize>(nameof(SelectedSize), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the currently selected size.
        /// </summary>
        public DaisySize SelectedSize
        {
            get => GetValue(SelectedSizeProperty);
            set => SetValue(SelectedSizeProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="ShowAbbreviations"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowAbbreviationsProperty =
            AvaloniaProperty.Register<DaisySizeDropdown, bool>(nameof(ShowAbbreviations), false);

        /// <summary>
        /// When true, displays size abbreviations (XS, S, M, L, XL) instead of full names.
        /// Default is false.
        /// </summary>
        public bool ShowAbbreviations
        {
            get => GetValue(ShowAbbreviationsProperty);
            set => SetValue(ShowAbbreviationsProperty, value);
        }

        /// <summary>
        /// Defines the <see cref="Size"/> property for the dropdown's own appearance.
        /// </summary>
        public static readonly StyledProperty<DaisySize> SizeProperty =
            AvaloniaProperty.Register<DaisySizeDropdown, DaisySize>(nameof(Size), DaisySize.Medium);

        /// <summary>
        /// Gets or sets the size of this dropdown control itself (not the selected value).
        /// This property controls the visual appearance of the dropdown.
        /// </summary>
        public DaisySize Size
        {
            get => GetValue(SizeProperty);
            set => SetValue(SizeProperty, value);
        }

        private static List<SizePreviewInfo>? _cachedSizes;
        private bool _isSyncing;

        public DaisySizeDropdown()
        {
            // Enable keyboard navigation by DisplayName
            TextSearch.SetTextBinding(this, new Binding(nameof(SizePreviewInfo.DisplayName)));

            var sizes = GetSizeInfos();
            ItemsSource = sizes;

            // Sync to current global size
            var currentSize = FlowerySizeManager.CurrentSize;
            SyncToSize(currentSize, sizes);
        }

        private void SyncToSize(DaisySize size, List<SizePreviewInfo>? sizes = null)
        {
            sizes ??= GetSizeInfos();
            var match = sizes.FirstOrDefault(s => s.Size == size);
            if (match != null && SelectedItem != match)
            {
                _isSyncing = true;
                try
                {
                    SelectedItem = match;
                    SelectedSize = match.Size;
                }
                finally
                {
                    _isSyncing = false;
                }
            }
        }

        private static List<SizePreviewInfo> GetSizeInfos()
        {
            if (_cachedSizes != null) return _cachedSizes;

            _cachedSizes = new List<SizePreviewInfo>
            {
                new SizePreviewInfo { Size = DaisySize.ExtraSmall, Name = "ExtraSmall", Abbreviation = "XS" },
                new SizePreviewInfo { Size = DaisySize.Small, Name = "Small", Abbreviation = "S" },
                new SizePreviewInfo { Size = DaisySize.Medium, Name = "Medium", Abbreviation = "M" },
                new SizePreviewInfo { Size = DaisySize.Large, Name = "Large", Abbreviation = "L" },
                new SizePreviewInfo { Size = DaisySize.ExtraLarge, Name = "ExtraLarge", Abbreviation = "XL" },
            };

            return _cachedSizes;
        }

        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == SelectedItemProperty && change.NewValue is SizePreviewInfo sizeInfo)
            {
                SelectedSize = sizeInfo.Size;
                if (!_isSyncing)
                {
                    ApplySize(sizeInfo);
                }
            }
        }

        private void ApplySize(SizePreviewInfo sizeInfo)
        {
            FlowerySizeManager.ApplySize(sizeInfo.Size);
        }

        protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnAttachedToVisualTree(e);
            FlowerySizeManager.SizeChanged += OnSizeChanged;
            FloweryLocalization.CultureChanged += OnCultureChanged;
            SyncWithCurrentSize();
        }

        protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
        {
            base.OnDetachedFromVisualTree(e);
            FlowerySizeManager.SizeChanged -= OnSizeChanged;
            FloweryLocalization.CultureChanged -= OnCultureChanged;
        }

        private void OnCultureChanged(object? sender, CultureInfo culture)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnCultureChanged(sender, culture));
                return;
            }

            // Clear cache to force reload of localized display names
            _cachedSizes = null;
            var sizes = GetSizeInfos();
            var currentSize = SelectedSize;
            ItemsSource = sizes;
            SyncToSize(currentSize, sizes);
        }

        private void OnSizeChanged(object? sender, DaisySize size)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => OnSizeChanged(sender, size));
                return;
            }

            // Update the dropdown's own appearance size
            Size = size;

            // Sync the selected value
            SyncWithCurrentSize();
        }

        private void SyncWithCurrentSize()
        {
            var currentSize = FlowerySizeManager.CurrentSize;
            SyncToSize(currentSize);
        }
    }
}
