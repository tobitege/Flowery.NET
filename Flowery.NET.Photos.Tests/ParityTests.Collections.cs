using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaFact]
    public async Task CollectionsVirtualizeCardsAndSearchUsesSingularCount()
    {
        var previousState = PhotosSettings.StateDirectory;
        var state = Path.GetDirectoryName(CreateImage())!;
        PhotosSettings.Initialize(state);
        var library = new PhotoLibrary();
        for (var index = 0; index < 180; index++) library.CreateAlbum($"album {index:000}");
        using var shell = new PhotosShell(library);
        var window = new Window { Content = shell, Width = 1000, Height = 700 };
        try
        {
            window.Show(); window.UpdateLayout();
            var albums = shell.GetVisualDescendants().OfType<DaisyButton>().Single(button => button.Tag as string == "Albums");
            Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(albums)).Invoke();
            window.UpdateLayout();
            var list = shell.FindControl<ListBox>("Collections")!;
            Assert.InRange(list.GetVisualDescendants().OfType<PhotoCard>().Count(), 1, 30);
            list.ScrollIntoView(list.Items[^1]!); window.UpdateLayout();
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.Contains(list.GetVisualDescendants().OfType<PhotoCard>(), card => AutomationProperties.GetName(card)?.Contains("album 179") == true);
            Assert.InRange(list.GetVisualDescendants().OfType<PhotoCard>().Count(), 1, 30);
            shell.FindControl<TextBox>("Search")!.Text = "album 179";
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            window.UpdateLayout();
            Assert.Equal(PhotosLocalization.Format("Photos_ItemCountOne", 1), shell.FindControl<TextBlock>("CollectionCount")!.Text);
            Assert.Single(list.GetVisualDescendants().OfType<PhotoCard>());
        }
        finally { window.Close(); await library.DisposeAsync(); PhotosSettings.Initialize(previousState); }
    }
}
