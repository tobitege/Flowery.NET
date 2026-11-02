namespace Flowery.NET.Kiosk;

public partial class MainWindow : Window
{
    // The previewer needs a parameterless constructor.
    public MainWindow() : this(false)
    {
    }

    public MainWindow(bool manual)
    {
        InitializeComponent();
        MainPage.ConfigureManualMode(manual);
        Closed += OnClosed;
    }

    internal KioskMainPage KioskPage => MainPage;

    private async void OnClosed(object? sender, EventArgs args)
    {
        Closed -= OnClosed;
        try
        {
            await MainPage.ShutdownAsync();
        }
        catch (Exception exception)
        {
            MainPage.ReportFailure(exception);
        }
    }
}
