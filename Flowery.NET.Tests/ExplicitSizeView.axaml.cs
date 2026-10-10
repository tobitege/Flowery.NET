using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Flowery.NET.Tests;

/// <summary>
/// Compiled XAML with explicit Size values, used to verify that XAML-set sizes
/// are never overwritten by the global size.
/// </summary>
public partial class ExplicitSizeView : UserControl
{
    public ExplicitSizeView()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
