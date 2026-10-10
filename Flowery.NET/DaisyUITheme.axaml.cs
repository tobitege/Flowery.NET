using System.Runtime.CompilerServices;
using Avalonia.Styling;
using Avalonia.Markup.Xaml;
using Flowery.Controls;

namespace Flowery
{
    public class DaisyUITheme : Styles
    {
        public DaisyUITheme()
        {
            AvaloniaXamlLoader.Load(this);

            // Registers the global size handlers before any control is loaded.
            RuntimeHelpers.RunClassConstructor(typeof(FlowerySizeManager).TypeHandle);
        }
    }
}
