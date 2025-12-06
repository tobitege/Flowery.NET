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
                    var topLevel = TopLevel.GetTopLevel(this);
                    if (topLevel is Window window)
                    {
                        var currentColor = selectedColorPreview?.Background is SolidColorBrush brush
                            ? brush.Color
                            : Colors.Red;

                        var result = await DaisyColorPickerDialog.ShowDialogAsync(window, currentColor);
                        if (result.HasValue && selectedColorPreview != null && selectedColorText != null)
                        {
                            selectedColorPreview.Background = new SolidColorBrush(result.Value);
                            selectedColorText.Text = $"Selected: #{result.Value.R:X2}{result.Value.G:X2}{result.Value.B:X2}";
                        }
                    }
                };
            }
        }

        public void ScrollToSection(string sectionName)
        {
            var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
            if (scrollViewer == null) return;

            var sectionHeader = this.GetVisualDescendants()
                .OfType<SectionHeader>()
                .FirstOrDefault(h => h.SectionId == sectionName);

            if (sectionHeader?.Parent is Visual parent)
            {
                var transform = parent.TransformToVisual(scrollViewer);
                if (transform.HasValue)
                {
                    var point = transform.Value.Transform(new Point(0, 0));
                    scrollViewer.Offset = new Vector(0, point.Y + scrollViewer.Offset.Y);
                }
            }
        }
    }
}

