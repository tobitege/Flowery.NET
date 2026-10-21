using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Editing;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaFact]
    public async Task EditorGroupsAdjustmentsAndRefreshesFilterPixelsOnUndo()
    {
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 1100, Height = 950 };
        try
        {
            window.Show();
            var photo = new PhotoItem(CreateImage());
            await viewer.OpenAsync(photo, [photo]);
            viewer.BeginEditing(); window.UpdateLayout();
            var groups = viewer.GetVisualDescendants().OfType<DaisyCollapse>().ToArray();
            Assert.Equal(4, groups.Length);
            Assert.Equal(2, groups.Count(group => group.IsExpanded));
            Assert.All(groups, group => Assert.False(string.IsNullOrWhiteSpace(ControlAutomationPeer.CreatePeerForElement(group)!.GetName())));
            var originalPreview = PreviewBytes(viewer);
            var brightness = viewer.GetVisualDescendants().OfType<DaisyRange>().Single(range => AutomationProperties.GetAutomationId(range) == "Viewer.Adjust.Brightness");
            Assert.IsAssignableFrom<IRangeValueProvider>(ControlAutomationPeer.CreatePeerForElement(brightness)).SetValue(50);
            Invoke(viewer, "filters"); await viewer.FilterPreviewUpdate;
            Assert.NotEqual(originalPreview, PreviewBytes(viewer));
            Invoke(viewer, "undo"); await viewer.FilterPreviewUpdate;
            Assert.Equal(originalPreview, PreviewBytes(viewer));
            var filters = viewer.FindControl<WrapPanel>("Filters")!;
            var first = (DaisyButton)filters.Children[1];
            first.RaiseEvent(new RoutedEventArgs(DaisyButton.ClickEvent));
            viewer.FindControl<DaisyRange>("FilterIntensity")!.Value = 0;
            var second = (DaisyButton)filters.Children[2];
            second.RaiseEvent(new RoutedEventArgs(DaisyButton.ClickEvent));
            Assert.Equal(100, viewer.FindControl<PhotoCanvas>("Canvas")!.State.FilterIntensity);
            Assert.True(second.IsActive); Assert.False(first.IsActive);
        }
        finally { window.Close(); }
    }

    private static byte[] PreviewBytes(ViewerView viewer)
    {
        var button = (DaisyButton)viewer.FindControl<WrapPanel>("Filters")!.Children[0];
        var stack = (StackPanel)button.Content!;
        var bitmap = (Bitmap)((Image)stack.Children[0]).Source!;
        using var stream = new MemoryStream();
        bitmap.Save(stream, PngBitmapEncoderOptions.Default);
        return stream.ToArray();
    }
}
