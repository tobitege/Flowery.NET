using System;
using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;

namespace Flowery.NET.Gallery.Android;

[Application]
public class AndroidApp : AvaloniaAndroidApplication<App>
{
    protected AndroidApp(IntPtr javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont()
            .WithNotoFonts();
    }
}
