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
        // A test run can put the window on another screen: FLOWERY_KIOSK_POSITION is its top left in pixels, "x,y".
        if (Environment.GetEnvironmentVariable("FLOWERY_KIOSK_POSITION")?.Split(',') is [var x, var y] &&
            int.TryParse(x, out var left) && int.TryParse(y, out var top))
        {
            WindowStartupLocation = WindowStartupLocation.Manual;
            Position = new PixelPoint(left, top);
        }
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
