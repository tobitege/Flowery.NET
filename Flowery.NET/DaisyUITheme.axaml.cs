using Avalonia.Styling;
using Avalonia.Markup.Xaml;

namespace Flowery
{
    public class DaisyUITheme : Styles
    {
        public DaisyUITheme()
        {
            AvaloniaXamlLoader.Load(this);
        }
    }
}
