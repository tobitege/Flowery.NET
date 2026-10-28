using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Flowery.Controls;
using Flowery.NET.Gallery.Localization;
using DaisyIconSymbol = Flowery.Enums.DaisyIconSymbol;

namespace Flowery.NET.Gallery.Examples;

public sealed partial class TourExamples
{
    private readonly DaisyTourEditor _tourEditor = new();
    private readonly StackPanel _editorFiles = new() { Spacing = 8 };
    private readonly ContentControl _editorFileHost = new();
    private readonly TextBlock _editorFileStatus = new() { TextWrapping = TextWrapping.Wrap };
    private int _editorGeneration;
    private bool _editorFileOperation;

    private void InitializeTourDesigner()
    {
        var commands = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var import = new DaisyButton { IconSymbol = DaisyIconSymbol.Folder, Shape = DaisyButtonShape.Square, Variant = DaisyButtonVariant.Ghost, Size = DaisySize.Small };
        var export = new DaisyButton { IconSymbol = DaisyIconSymbol.Save, Shape = DaisyButtonShape.Square, Variant = DaisyButtonVariant.Ghost, Size = DaisySize.Small };
        FlowerySizeManager.SetIgnoreGlobalSize(import, true);
        FlowerySizeManager.SetIgnoreGlobalSize(export, true);
        BindText(import, AutomationProperties.NameProperty, "Gallery_Tour_Import");
        BindText(export, AutomationProperties.NameProperty, "Gallery_Tour_Export");
        BindText(import, ToolTip.TipProperty, "Gallery_Tour_Import");
        BindText(export, ToolTip.TipProperty, "Gallery_Tour_Export");
        _editorFileStatus.FontSize = 12;
        _editorFileStatus.IsVisible = false;
        _editorFileStatus.PropertyChanged += (_, args) =>
        {
            if (args.Property == TextBlock.TextProperty)
                _editorFileStatus.IsVisible = !string.IsNullOrEmpty(_editorFileStatus.Text);
        };
        AutomationProperties.SetAutomationId(import, "TourDesignerImport");
        AutomationProperties.SetAutomationId(export, "TourDesignerExport");
        AutomationProperties.SetLiveSetting(_editorFileStatus, AutomationLiveSetting.Polite);
        import.Click += ImportTourProfile_Click;
        export.Click += ExportTourProfile_Click;
        commands.Children.Add(import);
        commands.Children.Add(export);
        _editorFiles.Children.Add(commands);
        _editorFiles.Children.Add(_editorFileStatus);
        _editorFileHost.Content = _editorFiles;
        _tourEditor.ToolbarContent = _editorFileHost;
        Unloaded += TourDesigner_Unloaded;

        AddDesignerStep("straight", new DaisyTourAnnotation { Preset = DaisyTourAnnotationPreset.ThinArrow });
        AddDesignerStep("curved", new DaisyTourAnnotation { Preset = DaisyTourAnnotationPreset.CurvedArrow });
        AddDesignerStep("bezier", new DaisyTourAnnotation
        {
            Path = DaisyTourAnnotationPath.Bezier,
            ControlPoint1X = 0.25, ControlPoint1Y = -0.25,
            ControlPoint2X = 0.75, ControlPoint2Y = 0.25
        });
        AddDesignerStep("block", new DaisyTourAnnotation { Preset = DaisyTourAnnotationPreset.BlockArrow });
        var bubble = new DaisyTourAnnotation { Preset = DaisyTourAnnotationPreset.SpeechBubble };
        BindText(bubble, DaisyTourAnnotation.TextProperty, "Gallery_Tour_BubbleText");
        AddDesignerStep("bubble", bubble, connectTargets: false);
        var scrolled = new DaisyTourStep
        {
            Id = "nested-scroll", Target = NestedTarget, PrepareAsync = PrepareOverviewAsync
        };
        BindText(scrolled, DaisyTourStep.TitleProperty, "Gallery_Tour_ScrollTitle");
        BindText(scrolled, DaisyTourStep.DescriptionProperty, "Gallery_Tour_ScrollDescription");
        scrolled.Annotations.Add(new DaisyTourAnnotation { Id = "scroll-arrow", Preset = DaisyTourAnnotationPreset.ThinArrow });
        EditorTour.Steps.Add(scrolled);
        var prepared = new DaisyTourStep { Id = "prepared-tab", PrepareAsync = PrepareDetailsAsync };
        BindText(prepared, DaisyTourStep.TitleProperty, "Gallery_Tour_DeferredTitle");
        BindText(prepared, DaisyTourStep.DescriptionProperty, "Gallery_Tour_DeferredDescription");
        prepared.Annotations.Add(new DaisyTourAnnotation { Id = "prepared-arrow", Preset = DaisyTourAnnotationPreset.ThinArrow });
        EditorTour.Steps.Add(prepared);
    }

    private void AddDesignerStep(string id, DaisyTourAnnotation annotation, bool connectTargets = true)
    {
        var step = new DaisyTourStep
        {
            Id = id, Title = annotation.Preset == DaisyTourAnnotationPreset.Default
                ? annotation.Path.ToString() : annotation.Preset.ToString(),
            Target = PrimaryTarget, PrepareAsync = PrepareOverviewAsync
        };
        BindText(step, DaisyTourStep.DescriptionProperty, "Gallery_Tour_DesignDescription");
        annotation.Id = id + "-shape";
        annotation.Source = connectTargets ? PrimaryTarget : null;
        annotation.Target = connectTargets ? RelatedTarget : PrimaryTarget;
        step.Annotations.Add(annotation);
        EditorTour.Steps.Add(step);
    }

    private async void DesignTour_Click(object? sender, RoutedEventArgs e)
    {
        _editorGeneration++;
        Tour.Cancel();
        EditorTour.Cancel();
        _editorFileStatus.Text = string.Empty;
        DesignerStatus.Text = string.Empty;
        try
        {
            await _tourEditor.OpenAsync(EditorTour);
        }
        catch (Exception error)
        {
            DesignerStatus.Text = error.Message;
        }
    }

    private async void ReplayDesign_Click(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
        Tour.Cancel();
        DesignerStatus.Text = string.Empty;
        try
        {
            await EditorTour.StartAsync();
        }
        catch (Exception error)
        {
            DesignerStatus.Text = error.Message;
        }
    }

    private void TourDesigner_Unloaded(object? sender, RoutedEventArgs e)
    {
        CloseDesigner();
    }

    private void CloseDesigner()
    {
        _editorGeneration++;
        if (_tourEditor.IsOpen)
            _tourEditor.Discard();
        EditorTour.Cancel();
    }

    private static readonly FilePickerFileType JsonFileType = new("JSON")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
        AppleUniformTypeIdentifiers = ["public.json"]
    };

    private async void ImportTourProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (_editorFileOperation || !_tourEditor.IsOpen || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanOpen: true } storage)
            return;
        var generation = _editorGeneration;
        _editorFileOperation = true;
        _editorFileHost.IsEnabled = false;
        try
        {
            var result = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                AllowMultiple = false,
                FileTypeFilter = [JsonFileType]
            });
            if (result is not [var file])
                return;
            string json;
            using (file)
                json = await ReadProfileAsync(file);
            if (generation != _editorGeneration || !_tourEditor.IsOpen)
                return;
            _tourEditor.ImportJson(json);
            _editorFileStatus.Text = GalleryLocalization.GetString("Gallery_Tour_Imported");
        }
        catch (Exception error)
        {
            if (generation == _editorGeneration && _tourEditor.IsOpen)
                _editorFileStatus.Text = GalleryLocalization.GetString("Gallery_Tour_FileError") + " " + error.Message;
        }
        finally
        {
            _editorFileOperation = false;
            _editorFileHost.IsEnabled = true;
        }
    }

    private async void ExportTourProfile_Click(object? sender, RoutedEventArgs e)
    {
        if (_editorFileOperation || !_tourEditor.IsOpen || TopLevel.GetTopLevel(this)?.StorageProvider is not { CanSave: true } storage)
            return;
        var generation = _editorGeneration;
        _editorFileOperation = true;
        _editorFileHost.IsEnabled = false;
        try
        {
            var json = _tourEditor.ExportJson();
            using var result = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                SuggestedFileName = "gallery-tour", DefaultExtension = "json",
                FileTypeChoices = [JsonFileType],
                ShowOverwritePrompt = true
            });
            if (result is null || generation != _editorGeneration || !_tourEditor.IsOpen)
                return;
            await WriteProfileAsync(result, json);
            if (generation == _editorGeneration && _tourEditor.IsOpen)
                _editorFileStatus.Text = GalleryLocalization.GetString("Gallery_Tour_Exported");
        }
        catch (Exception error)
        {
            if (generation == _editorGeneration && _tourEditor.IsOpen)
                _editorFileStatus.Text = GalleryLocalization.GetString("Gallery_Tour_FileError") + " " + error.Message;
        }
        finally
        {
            _editorFileOperation = false;
            _editorFileHost.IsEnabled = true;
        }
    }

    // The storage provider reads files also where they have no file system path, such as in the browser.
    internal static async Task<string> ReadProfileAsync(IStorageFile file)
    {
        using var reader = new StreamReader(await file.OpenReadAsync(), Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    internal static async Task WriteProfileAsync(IStorageFile file, string json)
    {
        if (file.TryGetLocalPath() is { Length: > 0 } path)
        {
            await WriteProfileFileAsync(path, json);
            return;
        }
        // A file without a file system path, such as a browser download, takes the profile through its own stream.
        await using var stream = await file.OpenWriteAsync();
        await WriteProfileStreamAsync(stream, json);
    }

    // A platform stream can open with the previous content; a seekable one is emptied before the profile is written.
    internal static async Task WriteProfileStreamAsync(Stream stream, string json)
    {
        if (stream.CanSeek)
            stream.SetLength(0);
        await stream.WriteAsync(Encoding.UTF8.GetBytes(json));
        await stream.FlushAsync();
    }

    // Writes and flushes an adjacent temporary file, closes it, then replaces the destination. A failure names
    // the temporary file when it exists, so the incomplete file can be examined.
    internal static async Task WriteProfileFileAsync(string path, string json)
    {
        var partialPath = path + "." + Guid.NewGuid().ToString("N") + ".partial";
        try
        {
            await using (var stream = new FileStream(partialPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 4096, FileOptions.Asynchronous))
            {
                await stream.WriteAsync(Encoding.UTF8.GetBytes(json));
                await stream.FlushAsync();
                stream.Flush(flushToDisk: true);
            }
            File.Move(partialPath, path, overwrite: true);
        }
        catch (Exception error) when (File.Exists(partialPath))
        {
            throw new IOException(error.Message + Environment.NewLine + partialPath, error);
        }
    }
}
