using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    [AvaloniaFact]
    public async Task InfoOmitsMissingMetadataAndProvidesSelectableValuesAndFolderAction()
    {
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 1100, Height = 850 };
        try
        {
            window.Show();
            var photo = new PhotoItem(CreateImage()) { PixelWidth = 240, PixelHeight = 160 };
            await viewer.OpenAsync(photo, [photo]);
            Invoke(viewer, "info"); window.UpdateLayout();
            var panel = viewer.FindControl<StackPanel>("InfoPanel")!;
            var controls = panel.GetVisualDescendants().OfType<Control>().ToArray();
            Assert.DoesNotContain(controls, control => AutomationProperties.GetAutomationId(control) is "Viewer.Info.Camera" or "Viewer.Info.Exposure" or "Viewer.Info.Lens");
            Assert.IsType<SelectableTextBlock>(controls.Single(control => AutomationProperties.GetAutomationId(control) == "Viewer.Info.Dimensions"));
            var folder = Assert.IsType<DaisyButton>(controls.Single(control => AutomationProperties.GetAutomationId(control) == "Viewer.Info.Folder"));
            Assert.Equal("reveal", folder.Tag);
            photo.CameraModel = "Test camera";
            photo.Details = new PhotoDetails("Test lens", "ISO 400 · 50 mm", null, null);
            Invoke(viewer, "info");
            Assert.False(viewer.FindControl<Border>("SidePanel")!.IsVisible);
            Invoke(viewer, "info"); window.UpdateLayout();
            controls = panel.GetVisualDescendants().OfType<Control>().ToArray();
            Assert.Equal("ISO 400 · 50 mm", Assert.IsType<SelectableTextBlock>(controls.Single(control => AutomationProperties.GetAutomationId(control) == "Viewer.Info.Exposure")).Text);
        }
        finally { window.Close(); }
    }
}
