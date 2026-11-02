using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Flowery.Localization;

namespace Flowery.NET.Kiosk;

public partial class App : Application
{
    private KioskOptions _options = new(false, null);
    private KioskSmokeReport? _report;
    private IClassicDesktopStyleApplicationLifetime? _desktop;
    private MainWindow? _window;
    private int _finishing;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        Dispatcher.UIThread.UnhandledException += OnUnhandledException;
        _desktop = ApplicationLifetime as IClassicDesktopStyleApplicationLifetime;
        try
        {
            _options = KioskOptions.Parse(_desktop?.Args ?? []);
            _report = _options.SmokeReport is { } path ? new KioskSmokeReport(path) : null;
            if (_report is not null)
                FloweryLocalization.SetCulture("en-US");
            ConfigureStateStorage();
            FloweryScaleManager.IsEnabled = true;
            if (!DaisyThemeManager.ApplyTheme("Dark"))
                throw new InvalidOperationException("Unable to apply the Dark theme.");
            if (_desktop is not null)
            {
                _window = new MainWindow(_options.Manual);
                _window.KioskPage.NavigationFailed += OnNavigationFailed;
                _window.Opened += OnWindowOpened;
                _desktop.MainWindow = _window;
            }
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
        base.OnFrameworkInitializationCompleted();
    }

    private async void OnWindowOpened(object? sender, EventArgs args)
    {
        if (_window is null)
            return;
        _window.Opened -= OnWindowOpened;
        if (_report is null)
            return;
        try
        {
            await _window.KioskPage.Ready.WaitAsync(TimeSpan.FromSeconds(20));
            await KioskSmokeTests.RunAsync(_window, _report);
            Finish(_report.Failed == 0 ? 0 : 1);
        }
        catch (Exception exception)
        {
            Fail(exception);
        }
    }

    private void OnNavigationFailed(object? sender, Exception exception) => Fail(exception);

    private void OnUnhandledException(object? sender, DispatcherUnhandledExceptionEventArgs args)
    {
        args.Handled = true;
        Fail(args.Exception);
    }

    private void Fail(Exception exception)
    {
        if (_report is not null)
            _report.Add("StartupOrRuntimeFailure", false, exception.ToString());
        else
        {
            try
            {
                var root = GetStateRoot();
                Directory.CreateDirectory(root);
                File.WriteAllText(Path.Combine(root, "crash.log"), exception.ToString());
            }
            catch (Exception loggingException)
            {
                Console.Error.WriteLine(loggingException);
            }
            Console.Error.WriteLine(exception);
        }
        Finish(1);
    }

    // Posted, so that a failure during the start shuts the app down only once its lifetime runs.
    private void Finish(int exitCode)
    {
        if (Interlocked.Exchange(ref _finishing, 1) != 0)
            return;
        Dispatcher.UIThread.Post(() => FinishAsync(exitCode));
    }

    private async void FinishAsync(int exitCode)
    {
        try
        {
            if (_window is not null)
            {
                _window.KioskPage.NavigationFailed -= OnNavigationFailed;
                await _window.KioskPage.ShutdownAsync();
            }
        }
        catch (Exception exception)
        {
            exitCode = 1;
            _report?.Add("ShutdownFailure", false, exception.ToString());
            Console.Error.WriteLine(exception);
        }
        try
        {
            _report?.Save();
        }
        catch (Exception exception)
        {
            exitCode = 1;
            Console.Error.WriteLine(exception);
        }
        if (_desktop is not null)
            _desktop.Shutdown(exitCode);
        else
            Environment.Exit(exitCode);
    }

    private static void ConfigureStateStorage()
    {
        // FileStateStorage combines its name with LocalApplicationData; an absolute path replaces that folder.
        StateStorageProvider.Configure(new FileStateStorage(GetStateRoot()));
    }

    private static string GetStateRoot()
    {
        var configured = Environment.GetEnvironmentVariable("FLOWERY_STATE_ROOT");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            if (!Path.IsPathFullyQualified(configured))
                throw new ArgumentException("FLOWERY_STATE_ROOT must be an absolute path.");
            return Path.GetFullPath(configured);
        }
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Flowery.NET.Kiosk");
    }
}
