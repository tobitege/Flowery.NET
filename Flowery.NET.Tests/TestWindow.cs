using Avalonia.Controls;

namespace Flowery.NET.Tests;

/// <summary>
/// A shown window that closes when disposed.
/// </summary>
internal sealed class TestWindow : IDisposable
{
    public TestWindow(Control content, double width = 400, double height = 300)
    {
        Window = new Window { Width = width, Height = height, Content = content };
        Window.Show();
        Window.UpdateLayout();
    }

    public Window Window { get; }

    /// <summary>Replaces the content and applies its template.</summary>
    public void Show(Control content)
    {
        Window.Content = content;
        Window.UpdateLayout();
    }

    public void Dispose() => Window.Close();
}
