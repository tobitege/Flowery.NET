using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
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
        /// Optional custom display name that overrides the localized text.
        /// When null or empty, the localized text is used.
        /// </summary>
        public string? DisplayNameOverride { get; set; }

        /// <summary>
        /// Localized display name for the size.
        /// Priority: DisplayNameOverride → Localized text → Name fallback.
        /// </summary>
        public string DisplayName => 
            !string.IsNullOrEmpty(DisplayNameOverride) 
                ? DisplayNameOverride! 
                : (FloweryLocalization.GetStringInternal($"Size_{Name}") is string s && s != $"Size_{Name}" ? s : Name);

        /// <summary>
        /// Short abbreviation for compact display (e.g., "XS", "S", "M", "L", "XL").
        /// </summary>
        public string Abbreviation { get; set; } = "";

        /// <summary>
        /// Gets or sets whether this size option is visible in the dropdown.
        /// Default is true. Set to false to hide this size from the user.
        /// </summary>
        public bool IsVisible { get; set; } = true;
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

        /// <summary>
        /// Defines the <see cref="SizeOptions"/> property.
        /// </summary>
        public static readonly StyledProperty<IList<SizePreviewInfo>?> SizeOptionsProperty =
            AvaloniaProperty.Register<DaisySizeDropdown, IList<SizePreviewInfo>?>(nameof(SizeOptions));

        /// <summary>
        /// Gets or sets the custom size options to display in the dropdown.
        /// When set, only sizes with <see cref="SizePreviewInfo.IsVisible"/> = true will be shown.
        /// Use <see cref="SizePreviewInfo.DisplayNameOverride"/> to customize the displayed text.
        /// When null, all sizes are shown with default localized names.
        /// </summary>
        /// <example>
        /// <code>
        /// &lt;controls:DaisySizeDropdown&gt;
        ///     &lt;controls:DaisySizeDropdown.SizeOptions&gt;
        ///         &lt;x:Array Type="{x:Type controls:SizePreviewInfo}"&gt;
        ///             &lt;controls:SizePreviewInfo Size="Small" Name="Small" DisplayNameOverride="Compact" /&gt;
        ///             &lt;controls:SizePreviewInfo Size="Medium" Name="Medium" DisplayNameOverride="Normal" /&gt;
        ///             &lt;controls:SizePreviewInfo Size="Large" Name="Large" DisplayNameOverride="Large" /&gt;
        ///         &lt;/x:Array&gt;
        ///     &lt;/controls:DaisySizeDropdown.SizeOptions&gt;
        /// &lt;/controls:DaisySizeDropdown&gt;
        /// </code>
        /// </example>
        public IList<SizePreviewInfo>? SizeOptions
        {
            get => GetValue(SizeOptionsProperty);
            set => SetValue(SizeOptionsProperty, value);
        }

        private List<SizePreviewInfo>? _instanceSizes;
        private bool _isSyncing;

        public DaisySizeDropdown()
        {
            // Enable keyboard navigation by DisplayName
            TextSearch.SetTextBinding(this, new Binding(nameof(SizePreviewInfo.DisplayName)));
        }

        protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
        {
            base.OnApplyTemplate(e);
            
            // Apply the correct item template based on ShowAbbreviations
            ApplyItemTemplate();
            
            // Initialize with visible sizes after template is applied
            RefreshVisibleSizes();
        }

        private void RefreshVisibleSizes()
        {
            var sizes = GetVisibleSizeInfos();
            ItemsSource = sizes;

            // Sync to current global size
            var currentSize = FlowerySizeManager.CurrentSize;
            SyncToSize(currentSize, sizes);
        }

        private void SyncToSize(DaisySize size, List<SizePreviewInfo>? sizes = null)
        {
            sizes ??= GetVisibleSizeInfos();
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

        /// <summary>
        /// Gets the visible size options, either from custom SizeOptions or default sizes.
        /// Filters by IsVisible property.
        /// </summary>
        private List<SizePreviewInfo> GetVisibleSizeInfos()
        {
            // If custom options are specified, use those (filtered by visibility)
            if (SizeOptions is { Count: > 0 })
            {
                return SizeOptions.Where(s => s.IsVisible).ToList();
            }

            // Otherwise, return cached default sizes (all visible by default)
            if (_instanceSizes != null) return _instanceSizes;

            _instanceSizes = new List<SizePreviewInfo>
            {
                new SizePreviewInfo { Size = DaisySize.ExtraSmall, Name = "ExtraSmall", Abbreviation = "XS" },
                new SizePreviewInfo { Size = DaisySize.Small, Name = "Small", Abbreviation = "S" },
                new SizePreviewInfo { Size = DaisySize.Medium, Name = "Medium", Abbreviation = "M" },
                new SizePreviewInfo { Size = DaisySize.Large, Name = "Large", Abbreviation = "L" },
                new SizePreviewInfo { Size = DaisySize.ExtraLarge, Name = "ExtraLarge", Abbreviation = "XL" },
            };

            return _instanceSizes;
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
            else if (change.Property == SizeOptionsProperty)
            {
                // Refresh the visible sizes when SizeOptions changes
                RefreshVisibleSizes();
            }
            else if (change.Property == ShowAbbreviationsProperty)
            {
                // Switch to the appropriate template
                ApplyItemTemplate();
            }
        }

        private void ApplyItemTemplate()
        {
            // Look up the appropriate template from resources
            var templateKey = ShowAbbreviations ? "SizeAbbreviationTemplate" : "SizeItemTemplate";
            if (this.TryFindResource(templateKey, out var resource) && resource is IDataTemplate template)
            {
                ItemTemplate = template;
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
            _instanceSizes = null;
            var sizes = GetVisibleSizeInfos();
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
