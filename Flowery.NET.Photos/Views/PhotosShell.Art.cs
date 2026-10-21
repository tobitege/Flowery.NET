using Avalonia.Controls;
using Avalonia.Layout;
using Ellipse = Avalonia.Controls.Shapes.Ellipse;

namespace Flowery.NET.Photos.Views;

public partial class PhotosShell
{
    private Canvas CreateHeroArt()
    {
        var art = new Canvas { Width = 250, Height = 176, Margin = new Thickness(24, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, IsHitTestVisible = false };
        IBrush? Brush(string key) => this.FindResource(key) as IBrush;
        void Place(Control control, double x, double y)
        {
            Canvas.SetLeft(control, x); Canvas.SetTop(control, y); art.Children.Add(control);
        }
        foreach (var (x, y, w, h, angle, brushKey) in new[]
        {
            (112d, 12d, 118d, 90d, 9d, "DaisySecondaryBrush"),
            (30d, 38d, 134d, 102d, -7d, "DaisyPrimaryBrush")
        })
        {
            var image = new Grid();
            image.Children.Add(new Border { CornerRadius = new CornerRadius(7), Background = Brush(brushKey), Opacity = .22 });
            image.Children.Add(new PathIcon { Data = this.FindResource("PhotosIconLandscape") as Geometry, Foreground = Brush(brushKey), Margin = new Thickness(18, 14), HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch });
            Place(new Border { Width = w, Height = h, Padding = new Thickness(7), CornerRadius = new CornerRadius(12), BorderThickness = new Thickness(1.5),
                Background = Brush("DaisyBase100Brush"), BorderBrush = Brush("DaisyBase300Brush"), RenderTransform = new RotateTransform(angle), Child = image }, x, y);
        }
        var camera = new Grid { Width = 66, Height = 66 };
        camera.Children.Add(new Ellipse { Fill = Brush("DaisyPrimaryBrush"), Stroke = Brush("DaisyBase200Brush"), StrokeThickness = 4 });
        camera.Children.Add(new PathIcon { Data = this.FindResource("PhotosIconCamera") as Geometry, Foreground = Brush("DaisyPrimaryContentBrush"), Width = 30, Height = 30 });
        Place(camera, 150, 98);
        var aperture = CreateIconBadge(this.FindResource("PhotosIconAperture") as Geometry, "DaisyAccentBrush");
        aperture.Width = aperture.Height = 42; Place(aperture, 10, 122);
        Place(new PathIcon { Data = this.FindResource("PhotosIconHeart") as Geometry, Foreground = Brush("DaisyErrorBrush"), Width = 28, Height = 28 }, 216, 2);
        Place(new PathIcon { Data = this.FindResource("PhotosIconAutoFix") as Geometry, Foreground = Brush("DaisyAccentBrush"), Width = 24, Height = 24, Opacity = .85 }, 4, 8);
        foreach (var (x, y, size, brushKey, opacity) in new[]
        { (234d, 122d, 9d, "DaisyPrimaryBrush", .5), (98d, 158d, 7d, "DaisySecondaryBrush", .6), (70d, 14d, 6d, "DaisyAccentBrush", .6) })
            Place(new Ellipse { Width = size, Height = size, Fill = Brush(brushKey), Opacity = opacity }, x, y);
        return art;
    }
}
