using Avalonia;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.NET.Photos.Editing;
using Flowery.NET.Photos.Models;
using Flowery.NET.Photos.Services;
using Flowery.NET.Photos.Views;
using SkiaSharp;
using Xunit;

namespace Flowery.NET.Photos.Tests;

public sealed partial class ParityTests
{
    private static string CreateImage(int width = 240, int height = 160)
    {
        var directory = Path.Combine(Path.GetTempPath(), "flowery-photos-parity", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "original.png");
        using var bitmap = new SKBitmap(width, height);
        using (var canvas = new SKCanvas(bitmap))
        {
            canvas.Clear(SKColors.Blue);
            using var paint = new SKPaint { Color = SKColors.Red };
            canvas.DrawRect(0, 0, width / 2f, height / 2f, paint);
        }
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        using var stream = File.Create(path);
        data.SaveTo(stream);
        return path;
    }

    private static void Invoke(ViewerView viewer, string action)
    {
        viewer.UpdateLayout();
        var button = viewer.GetVisualDescendants().OfType<DaisyButton>().First(control => control.Tag as string == action);
        Assert.IsAssignableFrom<IInvokeProvider>(ControlAutomationPeer.CreatePeerForElement(button)).Invoke();
    }

    [Fact]
    public void RotationKeepsCropAndMarkupAttachedToTheSamePixels()
    {
        using var source = PhotoDecoder.Load(CreateImage());
        var original = EditState.Original with
        {
            Crop = new Rect(.125, .125, .5, .25),
            Strokes = [new MarkupStroke(Colors.Lime, .03f, false, [new System.Numerics.Vector2(.25f, .25f)])]
        };
        using var before = EditRenderer.Render(source, original);
        var rotated = original.Rotate(true);
        Assert.Equal(new Rect(.625, .125, .25, .5), rotated.Crop);
        Assert.Equal(new System.Numerics.Vector2(.75f, .25f), rotated.Strokes[0].Points[0]);
        using var after = EditRenderer.Render(source, rotated);
        Assert.Equal(before.Width, after.Height);
        Assert.Equal(before.Height, after.Width);
        Assert.Equal(before.GetPixel(60, 20), after.GetPixel(after.Width - 21, 60));
        Assert.Equal(original.Crop, rotated.Rotate(false).Crop);
        Assert.Equal(original.Strokes[0].Points, rotated.Rotate(false).Strokes[0].Points);
        Assert.Equal(new Rect(.375, .125, .5, .25), original.Mirror(true).Crop);
        Assert.Equal(new Rect(.125, .625, .5, .25), original.Mirror(false).Crop);
    }

    [AvaloniaFact]
    public async Task ViewerRotationAndRevertAreUndoableWithAnExistingCrop()
    {
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 1100, Height = 850 };
        try
        {
            window.Show();
            var item = new PhotoItem(CreateImage());
            await viewer.OpenAsync(item, [item]);
            viewer.BeginEditing();
            Invoke(viewer, "crop");
            viewer.FindControl<DaisySelect>("Ratio")!.SelectedIndex = 2;
            var canvas = viewer.FindControl<PhotoCanvas>("Canvas")!;
            var crop = canvas.State.Crop;
            Assert.NotEqual(EditState.FullCrop, crop);
            Invoke(viewer, "right");
            var rotated = canvas.State;
            Assert.Equal(new Rect(1 - crop.Bottom, crop.X, crop.Height, crop.Width), rotated.Crop);
            Invoke(viewer, "revert");
            Assert.False(canvas.State.HasChanges);
            Invoke(viewer, "undo");
            Assert.Equal(rotated, canvas.State);
            Invoke(viewer, "redo");
            Assert.False(canvas.State.HasChanges);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task TurningOutsideTheEditorChangesOnlyTheViewAndTheEditorTakesItOver()
    {
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 1100, Height = 850 };
        try
        {
            window.Show();
            var item = new PhotoItem(CreateImage());
            await viewer.OpenAsync(item, [item, new PhotoItem(CreateImage())]);
            var canvas = viewer.FindControl<PhotoCanvas>("Canvas")!;
            Invoke(viewer, "right"); Invoke(viewer, "right"); Invoke(viewer, "left");
            Assert.Equal(1, canvas.State.Rotation);
            Assert.False(viewer.HasChanges);
            Assert.False(viewer.FindControl<DaisyBadge>("EditedBadge")!.IsVisible);

            // The editor starts from the turned view, and the turn is an edit that can be undone.
            viewer.BeginEditing();
            Assert.Equal(1, canvas.State.Rotation);
            Assert.True(viewer.HasChanges);
            Invoke(viewer, "undo");
            Assert.Equal(0, canvas.State.Rotation);
            Invoke(viewer, "redo");
            Assert.Equal(1, canvas.State.Rotation);

            // Outside the editor again, a turn adds to the view only; the next photo starts upright.
            viewer.BeginEditing();
            Invoke(viewer, "right");
            Assert.Equal(2, canvas.State.Rotation);
            await viewer.NavigateAsync(1);
            Assert.Equal(0, canvas.State.Rotation);
            Assert.False(viewer.HasChanges);
        }
        finally { window.Close(); }
    }

    [AvaloniaFact]
    public async Task ViewerKeepsFullResolutionAndClearsTheImageAfterADecodeFailure()
    {
        using var viewer = new ViewerView();
        var window = new Window { Content = viewer, Width = 700, Height = 600 };
        try
        {
            window.Show();
            var item = new PhotoItem(CreateImage(3200, 2400));
            await viewer.OpenAsync(item, [item]);
            var canvas = viewer.FindControl<PhotoCanvas>("Canvas")!;
            Assert.Equal(3200, canvas.Bitmap!.Width);
            canvas.SetZoom(100);
            Assert.Equal(100, canvas.ActualZoom);
            canvas.SetZoom(5);
            Assert.Equal(5, canvas.ActualZoom);
            var broken = new PhotoItem(Path.Combine(Path.GetDirectoryName(item.Path)!, "broken.png"));
            await File.WriteAllTextAsync(broken.Path, "broken", TestContext.Current.CancellationToken);
            await viewer.OpenAsync(broken, [item, broken]);
            Assert.Null(canvas.Bitmap);
            Assert.True(viewer.FindControl<TextBlock>("ErrorText")!.IsVisible);
            Assert.Equal("broken.png", viewer.FindControl<TextBlock>("FileName")!.Text);
            Assert.False(viewer.HasChanges);
            Assert.False(viewer.GetVisualDescendants().OfType<DaisyButton>().Single(button => Avalonia.Automation.AutomationProperties.GetAutomationId(button) == "ViewerView.edit").IsEnabled);
            Assert.True(viewer.FindControl<DaisyButton>("PreviousButton")!.IsEnabled);
            await viewer.NavigateAsync(-1);
            Assert.NotNull(canvas.Bitmap);
            Assert.False(viewer.FindControl<TextBlock>("ErrorText")!.IsVisible);
        }
        finally { window.Close(); }
    }
}
