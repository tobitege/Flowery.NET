using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Flowery.Services;

namespace Flowery.NET.PackageTest;

public partial class App : Application
{
    public override void Initialize()
    {
        // Before the first Kanban, which takes the storage when it is created. Every run starts without saved state.
        StateStorageProvider.Configure(new MemoryStateStorage());
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        // The headless check has no lifetime and opens the window itself.
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            desktop.MainWindow = new MainWindow();
        base.OnFrameworkInitializationCompleted();
    }
}
