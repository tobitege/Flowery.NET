using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls.ColorPicker;

namespace Flowery.NET.Gallery.Examples
{
    public partial class ColorPickerExamples : UserControl, IScrollableExample
    {
        private Dictionary<string, Visual>? _sectionTargetsById;

        public ColorPickerExamples()
        {
            InitializeComponent();

            // Wire up events
            var colorWheel1 = this.FindControl<DaisyColorWheel>("ColorWheel1");
            var colorWheelValue1 = this.FindControl<TextBlock>("ColorWheelValue1");
            if (colorWheel1 != null && colorWheelValue1 != null)
            {
                colorWheel1.ColorChanged += (s, e) =>
                {
                    colorWheelValue1.Text = $"Color: #{e.Color.R:X2}{e.Color.G:X2}{e.Color.B:X2}";
                };
            }

            var colorGrid1 = this.FindControl<DaisyColorGrid>("ColorGrid1");
            var colorGridValue1 = this.FindControl<TextBlock>("ColorGridValue1");
            if (colorGrid1 != null && colorGridValue1 != null)
            {
                colorGrid1.ColorChanged += (s, e) =>
                {
                    colorGridValue1.Text = $"Selected: #{e.Color.R:X2}{e.Color.G:X2}{e.Color.B:X2}";
                };
            }

            var screenPicker1 = this.FindControl<DaisyScreenColorPicker>("ScreenPicker1");
            var screenPickerValue1 = this.FindControl<TextBlock>("ScreenPickerValue1");
            if (screenPicker1 != null && screenPickerValue1 != null)
            {
                screenPicker1.ColorChanged += (s, e) =>
                {
                    screenPickerValue1.Text = $"Picked: #{e.Color.R:X2}{e.Color.G:X2}{e.Color.B:X2}";
                };
            }

            var openDialogButton = this.FindControl<Flowery.Controls.DaisyButton>("OpenDialogButton");
            var selectedColorPreview = this.FindControl<Border>("SelectedColorPreview");
            var selectedColorText = this.FindControl<TextBlock>("SelectedColorText");
            if (openDialogButton != null)
            {
                openDialogButton.Click += async (s, e) =>
                {
                    var currentColor = selectedColorPreview?.Background is SolidColorBrush brush
                        ? brush.Color
                        : Colors.Red;

                    Color? result;
                    var topLevel = TopLevel.GetTopLevel(this);

                    if (topLevel is Window window)
                    {
                        // Desktop: use native window dialog
                        result = await DaisyColorPickerDialog.ShowDialogAsync(window, currentColor);
                    }
                    else
                    {
                        // Browser/WASM: use overlay dialog
                        result = await DaisyColorPickerDialog.ShowOverlayAsync(this, currentColor);
                    }

                    if (result.HasValue && selectedColorPreview != null && selectedColorText != null)
                    {
                        selectedColorPreview.Background = new SolidColorBrush(result.Value);
                        selectedColorText.Text = $"Selected: #{result.Value.R:X2}{result.Value.G:X2}{result.Value.B:X2}";
                    }
                };
            }
        }

        public void ScrollToSection(string sectionName)
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            if (scrollViewer == null) return;

            var target = GetSectionTarget(sectionName);
            if (target == null) return;

            var transform = target.TransformToVisual(scrollViewer);
            if (transform.HasValue)
            {
                var point = transform.Value.Transform(new Point(0, 0));
                scrollViewer.Offset = new Vector(0, point.Y + scrollViewer.Offset.Y);
            }
        }

        private Visual? GetSectionTarget(string sectionId)
        {
            if (_sectionTargetsById == null)
            {
                _sectionTargetsById = new Dictionary<string, Visual>(StringComparer.OrdinalIgnoreCase);
                foreach (var header in this.GetVisualDescendants().OfType<SectionHeader>())
                {
                    if (!string.IsNullOrWhiteSpace(header.SectionId))
                        _sectionTargetsById[header.SectionId] = header.Parent as Visual ?? header;
                }
            }

            return _sectionTargetsById.TryGetValue(sectionId, out var target) ? target : null;
        }
    }
}

