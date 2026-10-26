using Android.App;
using Android.Content.PM;
using Android.Views;
using Avalonia.Android;

namespace Flowery.NET.Gallery.Android;

[Activity(
    Label = "Flowery.NET.Gallery.Android",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.UiMode,
    // The Gallery keeps clear of the on-screen keyboard itself (MainView.KeepClearOfInputPane); panning the window as
    // well pushed the title under the status bar.
    WindowSoftInputMode = SoftInput.AdjustNothing)]
public class MainActivity : AvaloniaMainActivity
{
}
