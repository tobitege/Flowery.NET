using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Theming;

namespace Flowery.NET.Gallery.Examples;

public partial class ThemingExamples : UserControl, IScrollableExample
{
    private DaisyUiTheme? _lastParsedTheme;
    private Dictionary<string, Visual>? _sectionTargetsById;

    public ThemingExamples()
    {
        InitializeComponent();
    }

    public async void BrowseCssFile_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var storage = topLevel.StorageProvider;
        var files = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select DaisyUI CSS Theme File",
            AllowMultiple = false,
            FileTypeFilter = new[]
            {
                new FilePickerFileType("CSS Files") { Patterns = new[] { "*.css" } },
                new FilePickerFileType("All Files") { Patterns = new[] { "*.*" } }
            }
        });

        if (files.Count > 0)
        {
            var pathInput = this.FindControl<DaisyInput>("CssFilePathInput");
            if (pathInput != null)
            {
                pathInput.Text = files[0].Path.LocalPath;
            }
        }
    }

    public void ParseAndApplyTheme_Click(object? sender, RoutedEventArgs e)
    {
        var pathInput = this.FindControl<DaisyInput>("CssFilePathInput");
        var statusText = this.FindControl<TextBlock>("ThemeStatusText");
        var axamlBorder = this.FindControl<Border>("AxamlOutputBorder");

        if (pathInput == null || statusText == null) return;

        var filePath = pathInput.Text?.Trim();
        if (string.IsNullOrEmpty(filePath))
        {
            statusText.Text = "Please enter a CSS file path.";
            statusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#FF627D"));
            return;
        }

        if (!File.Exists(filePath))
        {
            statusText.Text = $"File not found: {filePath}";
            statusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#FF627D"));
            return;
        }

        try
        {
            _lastParsedTheme = DaisyUiCssParser.Parse(File.ReadAllText(filePath), Path.GetFileNameWithoutExtension(filePath), out var errors);

            var colorCount = _lastParsedTheme.Colors.Count;
            var errorInfo = errors.Count > 0 ? $" ({errors.Count} parse warnings)" : "";

            DaisyThemeLoader.ApplyThemeToApplication(_lastParsedTheme);

            statusText.Text = $"✓ Theme '{_lastParsedTheme.Name}' applied! ({colorCount} colors, {(_lastParsedTheme.IsDark ? "Dark" : "Light")} mode){errorInfo}";
            statusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#00D390"));

            if (axamlBorder != null) axamlBorder.IsVisible = false;
        }
        catch (Exception ex)
        {
            statusText.Text = $"Error parsing theme: {ex.Message}";
            statusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#FF627D"));
        }
    }

    public async void ExportAxaml_Click(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var pathInput = this.FindControl<DaisyInput>("CssFilePathInput");
        var statusText = this.FindControl<TextBlock>("ThemeStatusText");
        var axamlBorder = this.FindControl<Border>("AxamlOutputBorder");
        var axamlText = this.FindControl<TextBlock>("AxamlOutputText");

        if (pathInput == null || statusText == null || axamlBorder == null || axamlText == null) return;

        var filePath = pathInput.Text?.Trim();
        if (string.IsNullOrEmpty(filePath))
        {
            statusText.Text = "Please enter a CSS file path first.";
            statusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#FF627D"));
            return;
        }

        if (!File.Exists(filePath))
        {
            statusText.Text = $"File not found: {filePath}";
            statusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#FF627D"));
            return;
        }

        try
        {
            var theme = DaisyUiCssParser.ParseFile(filePath);
            var axaml = DaisyUiAxamlGenerator.Generate(theme);

            axamlText.Text = axaml;
            axamlBorder.IsVisible = true;

            statusText.Text = $"✓ AXAML generated for '{theme.Name}'. You can copy the output below or save to file.";
            statusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#00BAFE"));

            var storage = topLevel.StorageProvider;
            var file = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "Save AXAML Theme File",
                SuggestedFileName = $"{theme.Name}.axaml",
                FileTypeChoices = new[]
                {
                    new FilePickerFileType("AXAML Files") { Patterns = new[] { "*.axaml" } }
                }
            });

            if (file != null)
            {
                await using var stream = await file.OpenWriteAsync();
                await using var writer = new StreamWriter(stream);
                await writer.WriteAsync(axaml);
                statusText.Text = $"✓ AXAML saved to: {file.Path.LocalPath}";
            }
        }
        catch (Exception ex)
        {
            statusText.Text = $"Error: {ex.Message}";
            statusText.Foreground = new global::Avalonia.Media.SolidColorBrush(global::Avalonia.Media.Color.Parse("#FF627D"));
        }
    }

    public void ScrollToSection(string sectionName)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (scrollViewer == null) return;

        var target = GetSectionTarget(sectionName);
        if (target == null) return;

        var transform = target.TransformToVisual(scrollViewer);
        if (transform.HasValue)
        {
            var point = transform.Value.Transform(new Point(0, 0));
            // Add current scroll offset to get absolute position in content
            scrollViewer.Offset = new Vector(0, point.Y + scrollViewer.Offset.Y);
        }
    }

    private Visual? GetSectionTarget(string sectionId)
    {
        if (_sectionTargetsById == null)
        {
            _sectionTargetsById = new Dictionary<string, Visual>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in this.GetVisualDescendants().OfType<SectionHeader>())
            {
                if (!string.IsNullOrWhiteSpace(header.SectionId))
                    _sectionTargetsById[header.SectionId] = header.Parent as Visual ?? header;
            }
        }

        return _sectionTargetsById.TryGetValue(sectionId, out var target) ? target : null;
    }
}
