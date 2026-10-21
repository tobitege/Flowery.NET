using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Flowery.Controls;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaTheory]
    [InlineData(WindowState.Normal)]
    [InlineData(WindowState.Maximized)]
    [InlineData(WindowState.FullScreen)]
    public async Task SlideshowHidesChromeWrapsAndRestoresWindowState(WindowState previous)
    {
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 1100, Height = 850 };
        try
        {
            window.Show(); window.WindowState = previous;
            var first = new PhotoItem(CreateImage());
            var last = new PhotoItem(CreateImage());
            await viewer.OpenAsync(last, [first, last]);
            viewer.BeginEditing();
            viewer.FindControl<DaisyCheckBox>("Compare")!.IsChecked = true;
            viewer.StartSlideshow(); window.UpdateLayout();
            Assert.True(viewer.IsSlideshow);
            Assert.Equal(WindowState.FullScreen, window.WindowState);
            Assert.False(viewer.FindControl<Grid>("Toolbar")!.IsVisible);
            Assert.False(viewer.FindControl<Border>("SidePanel")!.IsVisible);
            Assert.False(viewer.FindControl<WrapPanel>("StatusPanel")!.IsVisible);
            Assert.False(viewer.FindControl<DaisyCheckBox>("Compare")!.IsChecked);
            Assert.Equal(0, Grid.GetRow(viewer.FindControl<Grid>("MainArea")!));
            Assert.Equal(3, Grid.GetRowSpan(viewer.FindControl<Grid>("MainArea")!));
            await viewer.NavigateAsync(1);
            Assert.Same(first, viewer.Current);
            Assert.True(viewer.IsSlideshow);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            Assert.False(viewer.IsSlideshow);
            Assert.Equal(previous, window.WindowState);
            Assert.True(viewer.FindControl<Grid>("Toolbar")!.IsVisible);
            Assert.Equal(PhotosSettings.ShowStatusBar, viewer.FindControl<WrapPanel>("StatusPanel")!.IsVisible);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task AutoHideOverlaysImageAndEditingRemembersLastTabAndEscapeOrder()
    {
        var autoHide = PhotosSettings.AutoHideToolbar;
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 1100, Height = 850 };
        try
        {
            window.Show();
            var photo = new PhotoItem(CreateImage());
            await viewer.OpenAsync(photo, [photo]);
            viewer.FindControl<DaisyCheckBox>("AutoHide")!.IsChecked = true;
            window.UpdateLayout();
            var area = viewer.FindControl<Grid>("MainArea")!;
            Assert.Equal(0, Grid.GetRow(area)); Assert.Equal(2, Grid.GetRowSpan(area));
            var bounds = area.Bounds;
            var canvas = viewer.FindControl<PhotoCanvas>("Canvas")!;
            var center = canvas.TranslatePoint(new Rect(canvas.Bounds.Size).Center, window)!.Value;
            var titleView = false;
            viewer.TitleViewChanged += visible => titleView = visible;
            window.MouseDown(center, MouseButton.Left); window.MouseUp(center, MouseButton.Left);
            window.UpdateLayout();
            Assert.Equal(0, viewer.FindControl<Grid>("Toolbar")!.Opacity);
            Assert.Equal(bounds, area.Bounds);
            Assert.True(titleView);
            window.MouseMove(new Point(100, 10));
            Assert.Equal(1, viewer.FindControl<Grid>("Toolbar")!.Opacity);
            viewer.BeginEditing();
            Assert.True(viewer.FindControl<StackPanel>("AdjustPanel")!.IsVisible);
            Invoke(viewer, "markup");
            viewer.BeginEditing(); Assert.False(viewer.FindControl<Border>("SidePanel")!.IsVisible);
            viewer.BeginEditing(); Assert.True(canvas.IsDrawing);
            viewer.FindControl<DaisyCheckBox>("Compare")!.IsChecked = true;
            canvas.Focus();
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            Assert.False(canvas.Compare); Assert.True(viewer.FindControl<Border>("SidePanel")!.IsVisible);
            window.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            Assert.False(viewer.FindControl<Border>("SidePanel")!.IsVisible);
            Assert.False(canvas.IsDrawing);
        }
        finally
        {
            window.Close(); PhotosSettings.AutoHideToolbar = autoHide; PhotosSettings.SaveOptions();
        }
    }
}
