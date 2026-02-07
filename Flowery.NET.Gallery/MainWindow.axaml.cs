using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Flowery.Localization;

namespace Flowery.NET.Gallery;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        UpdateFlowDirection();
        FloweryLocalization.CultureChanged += (_, _) => Dispatcher.UIThread.InvokeAsync(UpdateFlowDirection);

        RestoreWindowPlacement();
        Closing += (_, _) => SaveWindowPlacement();
    }

    private void UpdateFlowDirection()
    {
        FlowDirection = FloweryLocalization.Instance.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    private void RestoreWindowPlacement()
    {
        var placement = GallerySettings.LoadWindowPlacement();
        if (placement == null)
            return;

        if (placement.Width >= 300)
            Width = placement.Width;
        if (placement.Height >= 300)
            Height = placement.Height;

        Position = new PixelPoint(placement.X, placement.Y);

        if (Enum.TryParse<WindowState>(placement.WindowState, ignoreCase: true, out var state))
        {
            if (state is WindowState.Maximized or WindowState.FullScreen)
                WindowState = state;
        }
    }

    private void SaveWindowPlacement()
    {
        var existing = GallerySettings.LoadWindowPlacement();
        var persistedState = WindowState switch
        {
            WindowState.Maximized => WindowState.Maximized,
            WindowState.FullScreen => WindowState.FullScreen,
            _ => WindowState.Normal
        };

        var width = Bounds.Width;
        var height = Bounds.Height;
        var x = Position.X;
        var y = Position.Y;

        // When not in normal state, keep the last known normal bounds if available.
        if (persistedState != WindowState.Normal && existing != null)
        {
            width = existing.Width;
            height = existing.Height;
            x = existing.X;
            y = existing.Y;
        }

        if (double.IsNaN(width) || width < 300)
            width = 1200;
        if (double.IsNaN(height) || height < 300)
            height = 800;

        GallerySettings.SaveWindowPlacement(new GallerySettings.WindowPlacement
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            WindowState = persistedState.ToString()
        });
    }
}
