using System.Runtime.CompilerServices;
using Avalonia.Styling;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Flowery.Controls;

namespace Flowery
{
    public class DaisyUITheme : Styles
    {
        public DaisyUITheme()
        {
            AvaloniaXamlLoader.Load(this);

            // Registers the global size handlers before any control is loaded, and creates
            // DaisyAccessibility.Preferences on the UI thread, which an Avalonia object belongs to.
            RuntimeHelpers.RunClassConstructor(typeof(FlowerySizeManager).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(DaisyAccessibility).TypeHandle);
            RuntimeHelpers.RunClassConstructor(typeof(DaisyNeumorphic).TypeHandle);

            // A glass mode switched on before the app existed takes effect once the app has loaded its styles.
            if (DaisyGlassSurface.GlobalEnabled)
                Dispatcher.UIThread.Post(DaisyThemeManager.RefreshDerivedResources);
        }
    }
}
