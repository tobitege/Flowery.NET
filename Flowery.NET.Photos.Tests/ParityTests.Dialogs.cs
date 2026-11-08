using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
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
    public async Task FolderRemovalRequiresConfirmationAndPromptUsesActionAndEnter()
    {
        var oldState = PhotosSettings.StateDirectory;
        var root = Path.Combine(Path.GetTempPath(), "flowery-photos-parity", Guid.NewGuid().ToString("N"));
        var oldRoot = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", root);
        PhotosSettings.Initialize(root);
        var library = new PhotoLibrary(); library.Initialize(Dispatcher.UIThread);
        var folder = Path.GetDirectoryName(CreateImage())!;
        library.AddFolder(folder); await library.RescanAsync();
        var owner = new Window { Width = 900, Height = 900 };
        SettingsView? settings = null;
        try
        {
            owner.Show(); settings = new SettingsView(library);
            var dialog = settings.ShowDialog(owner); settings.UpdateLayout();
            var remove = settings.GetVisualDescendants().OfType<DaisyButton>().Single(button => AutomationProperties.GetAutomationId(button) == "SettingsView.RemoveFolder." + folder);
            remove.RaiseEvent(new RoutedEventArgs(DaisyButton.ClickEvent));
            var prompt = Assert.Single(settings.OwnedWindows.OfType<PromptDialog>());
            Assert.Contains(folder, library.Folders);
            var accept = prompt.FindControl<DaisyButton>("AcceptButton")!;
            Assert.Equal(PhotosLocalization.GetString("Photos_Remove"), accept.Content);
            Assert.Equal(DaisyButtonVariant.Error, accept.Variant);
            prompt.KeyPress(Key.Escape, RawInputModifiers.None, PhysicalKey.Escape, "");
            Assert.Contains(folder, library.Folders);
            remove.RaiseEvent(new RoutedEventArgs(DaisyButton.ClickEvent));
            prompt = Assert.Single(settings.OwnedWindows.OfType<PromptDialog>());
            prompt.KeyPress(Key.Enter, RawInputModifiers.None, PhysicalKey.Enter, "");
            await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
            Assert.DoesNotContain(folder, library.Folders);
            Assert.True(Directory.Exists(folder));
            settings.Close(); await dialog;
            Assert.Contains(folder, library.Folders);
            await library.RescanAsync();
        }
        finally
        {
            settings?.Close(); owner.Close(); await library.DisposeAsync();
            Environment.SetEnvironmentVariable("FLOWERY_STATE_ROOT", oldRoot); PhotosSettings.Initialize(oldState);
        }
    }
}
