using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos;

public partial class MainWindow : Window
{
    private bool _closing;
    private Avalonia.Platform.IPlatformSettings? PlatformSettings => Application.Current?.PlatformSettings;
    public MainWindow() : this(null) { }

    public MainWindow(string[]? args)
    {
        InitializeComponent();
        Shell.TitleViewChanged += visible => TitleViewButton.IsVisible = visible;
        TitleViewButton.Click += (_, _) => Shell.ShowViewOptions();
        TitleViewButton.DataContext = Localization.PhotosLocalization.Instance;
        TitleViewButton.Bind(Avalonia.Automation.AutomationProperties.NameProperty,
            new Avalonia.Data.Binding("[Photos_ViewOptions]"));
        TitleViewButton.Bind(ToolTip.TipProperty, new Avalonia.Data.Binding("[Photos_ViewOptions]"));
        PhotoLibrary.Instance.Initialize(Dispatcher.UIThread);
        var saved = PhotosSettings.LoadWindowBounds();
        if (saved != null)
        {
            Width = saved.Width; Height = saved.Height;
            var point = new PixelPoint(saved.X, saved.Y);
            if (Screens.ScreenFromPoint(point) != null) Position = point;
        }
        // Explicit test/demo placement never changes the user's display configuration.
        if (args?.Contains("--screen=2") == true && Screens.All.Count > 1)
        {
            var screen = Screens.All.FirstOrDefault(s => !s.IsPrimary) ?? Screens.All[1];
            Position = screen.WorkingArea.Position;
            Width = screen.WorkingArea.Width / screen.Scaling;
            Height = screen.WorkingArea.Height / screen.Scaling;
        }
        Opened += async (_, _) =>
        {
            ApplyAccent();
            if (PlatformSettings != null) PlatformSettings.ColorValuesChanged += OnColorsChanged;
            await PhotoLibrary.Instance.RescanAsync();
            var path = args?.FirstOrDefault(arg => File.Exists(arg) && ImageMetadataReader.IsSupported(arg));
            if (path != null) await Shell.OpenFileAsync(path);
        };
        Closing += async (_, e) =>
        {
            if (_closing) return;
            e.Cancel = true;
            if (!await Shell.CanLeaveViewerAsync()) return;
            _closing = true;
            if (WindowState == WindowState.Normal)
                PhotosSettings.SaveWindowBounds(new WindowBounds(Position.X, Position.Y, (int)Width, (int)Height));
            Shell.Dispose();
            if (PlatformSettings != null) PlatformSettings.ColorValuesChanged -= OnColorsChanged;
            await PhotoLibrary.Instance.DisposeAsync();
            Close();
        };
    }

    private void OnColorsChanged(object? sender, Avalonia.Platform.PlatformColorValues e) => ApplyAccent();
    private void ApplyAccent()
    {
        var accent = PlatformSettings?.GetColorValues().AccentColor1 ?? Colors.DodgerBlue;
        Resources["PhotosAccentBrush"] = new SolidColorBrush(accent);
        BorderBrush = new SolidColorBrush(accent);
        BorderThickness = new Thickness(1);
        if (OperatingSystem.IsWindows() && TryGetPlatformHandle() is { } handle)
        {
            var color = accent.R | accent.G << 8 | accent.B << 16;
            DwmSetWindowAttribute(handle.Handle, 34, ref color, sizeof(int));
        }
    }
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
