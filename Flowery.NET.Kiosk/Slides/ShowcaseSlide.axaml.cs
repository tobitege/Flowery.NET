using Flowery.Controls.ColorPicker;

namespace Flowery.NET.Kiosk.Slides;

public partial class ShowcaseSlide : UserControl
{
    public ShowcaseSlide()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object? sender, RoutedEventArgs args)
    {
        ColorWheel.ColorChanged -= OnColorChanged;
        ColorWheel.ColorChanged += OnColorChanged;
        ColorWheel.Color = Color.FromArgb(255, 255, 87, 34);
        UpdatePreview(ColorWheel.Color);
    }

    private void OnUnloaded(object? sender, RoutedEventArgs args) => ColorWheel.ColorChanged -= OnColorChanged;

    private void OnColorChanged(object? sender, ColorChangedEventArgs args) => UpdatePreview(args.Color);

    private void UpdatePreview(Color color)
    {
        ColorPreview.Background = new SolidColorBrush(color);
        HexText.Text = $"#{color.R:X2}{color.G:X2}{color.B:X2}";
    }
}
