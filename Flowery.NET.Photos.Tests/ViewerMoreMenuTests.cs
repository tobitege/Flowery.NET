using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed class ViewerMoreMenuTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClickingMoreShowsTheViewerMenu(bool autoHide)
    {
        var previous = PhotosSettings.AutoHideToolbar;
        PhotosSettings.AutoHideToolbar = autoHide;
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 900, Height = 700 };
        try
        {
            window.Show();
            var media = Path.Combine(AppContext.BaseDirectory, "Media");
            var image = new PhotoItem(Path.Combine(media, "photo.png"));
            var next = new PhotoItem(Path.Combine(media, "exif.jpg"));
            viewer.AlbumMenu = item => new MenuItem { Header = item.FileName };
            await viewer.OpenAsync(image, [image, next]);
            var more = viewer.FindControl<DaisyButton>("MoreButton")!;
            var flyout = Assert.IsType<MenuFlyout>(more.Flyout);
            await CheckMenuAsync(image.FileName);
            flyout.Hide();
            await viewer.NavigateAsync(1);
            await CheckMenuAsync(next.FileName);

            async Task CheckMenuAsync(string fileName)
            {
                window.UpdateLayout();
                var center = more.TranslatePoint(new Point(more.Bounds.Width / 2, more.Bounds.Height / 2), window)!.Value;
                window.MouseMove(center);
                window.MouseDown(center, MouseButton.Left);
                window.MouseUp(center, MouseButton.Left);
                Assert.True(flyout.IsOpen);
                var presenter = Assert.IsType<MenuFlyoutPresenter>(flyout.Popup.Child);
                await Dispatcher.UIThread.InvokeAsync(() => TopLevel.GetTopLevel(presenter)?.UpdateLayout(), DispatcherPriority.Background);
                var items = presenter.GetVisualDescendants().OfType<MenuItem>().ToList();
                Assert.Single(items, item => AutomationProperties.GetAutomationId(item) == "Viewer.More.copyPath");
                Assert.Single(items, item => Equals(item.Header, fileName));
            }
        }
        finally
        {
            window.Close();
            PhotosSettings.AutoHideToolbar = previous;
        }
    }
}
