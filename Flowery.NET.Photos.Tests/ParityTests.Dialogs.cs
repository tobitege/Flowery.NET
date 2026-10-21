using Avalonia.Automation;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Localization;
using Flowery.NET.Photos.Localization;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Flowery.Services;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaFact]
    public void ModalKeepsDefaultWidthAndCanResizeItsBoxWithoutShrinkingTheBackdrop()
    {
        var content = new Border { Width = 1100, Height = 100 };
        var modal = new DaisyModal { IsOpen = true, Content = content };
        var window = new Window { Content = modal, Width = 1200, Height = 1000 };
        try
        {
            window.Show(); window.UpdateLayout();
            var frame = content.GetVisualAncestors().OfType<Border>().First();
            Assert.Equal(500, frame.Bounds.Width);
            Assert.Equal(window.ClientSize.Width, modal.Bounds.Width);
            modal.DialogMaxWidth = 900;
            window.UpdateLayout();
            Assert.Equal(900, frame.Bounds.Width);
            Assert.Equal(window.ClientSize.Width, modal.Bounds.Width);
        }
        finally { window.Close(); }
    }

    [AvaloniaTheory]
    [InlineData(650, 550)]
    [InlineData(900, 1100)]
    [InlineData(1396, 1198)]
    public async Task SettingsModalFitsHostAndUsesReadableLanguages(double width, double height)
    {
        var size = FlowerySizeManager.CurrentSize;
        var library = new PhotoLibrary();
        var owner = new Window { Width = width, Height = height };
        owner.Resources["PhotosAccentBrush"] = Brushes.Orange;
        SettingsView? settings = null;
        try
        {
            owner.Show(); owner.UpdateLayout();
            settings = new SettingsView(library);
            var dialog = settings.ShowDialog(owner);
            settings.UpdateLayout();
            Assert.True(settings.FindControl<DaisyModal>("SettingsModal")!.IsOpen);
            var content = settings.FindControl<Grid>("DialogContent")!;
            foreach (var value in Enum.GetValues<DaisySize>())
            {
                FlowerySizeManager.ApplySize(value);
                await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
                settings.UpdateLayout();
                Assert.InRange(content.Bounds.Width, 1, owner.ClientSize.Width - 96);
                Assert.InRange(content.Bounds.Height, 1, owner.ClientSize.Height - 120);
                var scroll = content.GetVisualDescendants().OfType<ScrollViewer>().First();
                Assert.True(scroll.Viewport.Height > 0);
                Assert.True(content.Bounds.Bottom <= settings.ClientSize.Height);
                var frame = content.GetVisualAncestors().OfType<Border>().First();
                var contentOrigin = content.TranslatePoint(default, frame)!.Value;
                Assert.True(contentOrigin.X >= frame.Padding.Left,
                    $"Size {value}: content {contentOrigin}, {content.Bounds}; frame {frame.Bounds}, padding {frame.Padding}");
                Assert.True(contentOrigin.X + content.Bounds.Width <= frame.Bounds.Width - frame.Padding.Right + 1,
                    $"Size {value}: content {contentOrigin}, {content.Bounds}; frame {frame.Bounds}, padding {frame.Padding}");
                Assert.True(contentOrigin.Y + content.Bounds.Height <= frame.Bounds.Height - frame.Padding.Bottom + 1);
                foreach (var button in content.GetVisualDescendants().OfType<DaisyButton>().Where(button =>
                    AutomationProperties.GetAutomationId(button) is "SettingsView.Photos_Cancel" or "SettingsView.Photos_Ok"))
                {
                    var origin = button.TranslatePoint(default, frame)!.Value;
                    Assert.InRange(origin.X, frame.Padding.Left, frame.Bounds.Width - button.Bounds.Width);
                    Assert.InRange(origin.Y, frame.Padding.Top, frame.Bounds.Height - button.Bounds.Height);
                }
            }
            var language = settings.FindControl<DaisySelect>("Language")!;
            var german = Assert.IsType<TextBlock>(language.ItemTemplate!.Build("de"));
            Assert.Equal(FloweryLocalization.LanguageDisplayNames["de"], german.Text);
            settings.Close(); await dialog;
        }
        finally { settings?.Close(); owner.Close(); FlowerySizeManager.ApplySize(size); await library.DisposeAsync(); }
    }

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
