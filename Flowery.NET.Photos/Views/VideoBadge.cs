using Avalonia.Controls;
using Avalonia.Layout;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

/// <summary>The play mark and length in the corner of a video's thumbnail.</summary>
public sealed class VideoBadge : Border
{
    public VideoBadge(PhotoItem item)
    {
        Background = new SolidColorBrush(Color.FromArgb(179, 0, 0, 0));
        CornerRadius = new CornerRadius(6);
        Padding = new Thickness(6, 3);
        Margin = new Thickness(6);
        HorizontalAlignment = HorizontalAlignment.Right;
        VerticalAlignment = VerticalAlignment.Bottom;
        IsHitTestVisible = false;
        var content = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5 };
        content.Children.Add(new Avalonia.Controls.Shapes.Path
        {
            Data = Geometry.Parse("M0,0 L8,5 L0,10 Z"), Fill = Brushes.White, VerticalAlignment = VerticalAlignment.Center
        });
        if (item.Details?.DurationSeconds is { } seconds)
            content.Children.Add(new TextBlock
            {
                Text = FileOperations.FormatDuration(TimeSpan.FromSeconds(seconds)), Foreground = Brushes.White, FontSize = 12,
                VerticalAlignment = VerticalAlignment.Center
            });
        Child = content;
    }
}
