using Android.App;
using Android.Content.PM;
using Avalonia.Android;

namespace Flowery.NET.Gallery.Android;

[Activity(
    Label = "Flowery.NET.Gallery.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@drawable/icon",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity
{
}
