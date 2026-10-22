using Avalonia.Controls;

namespace Flowery.NET.RuntimeTests;

/// <summary>
/// The window, host panel and artifact directory of the current runtime test run.
/// </summary>
internal static class RuntimeTestContext
{
    public static Window? Window { get; private set; }
    public static Panel? HostPanel { get; private set; }

    /// <summary>Directory next to the report for images and other evidence that tests save.</summary>
    public static string? ArtifactsDirectory { get; private set; }

    public static void Initialize(Window window, Panel hostPanel, string artifactsDirectory)
    {
        Window = window;
        HostPanel = hostPanel;
        ArtifactsDirectory = artifactsDirectory;
    }
}
