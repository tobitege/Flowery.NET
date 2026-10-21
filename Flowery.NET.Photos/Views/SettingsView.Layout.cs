using Avalonia.Controls;

namespace Flowery.NET.Photos.Views;

public partial class SettingsView
{
    private Window? _host;

    private void InitializeModalLayout()
    {
        Opened += (_, _) =>
        {
            _host = Owner as Window;
            if (_host != null)
            {
                _host.SizeChanged += OnHostSizeChanged;
                if (_host.TryFindResource("PhotosAccentBrush", out var accent)) Resources["PhotosAccentBrush"] = accent;
            }
            FlowerySizeManager.SizeChanged += OnDialogSizeChanged;
            FitDialog();
        };
        Closed += (_, _) =>
        {
            if (_host != null) _host.SizeChanged -= OnHostSizeChanged;
            FlowerySizeManager.SizeChanged -= OnDialogSizeChanged;
        };
    }

    private void OnHostSizeChanged(object? sender, SizeChangedEventArgs e) => FitDialog();
    private void OnDialogSizeChanged(object? sender, DaisySize size) => FitDialog();

    private void FitDialog()
    {
        var available = _host?.ClientSize ?? ClientSize;
        if (_host != null) { Width = available.Width; Height = available.Height; Position = _host.PointToScreen(default); }
        var factor = FlowerySizeManager.CurrentSize switch
        {
            DaisySize.ExtraSmall => .85, DaisySize.Small => 1, DaisySize.Medium => 1.12,
            DaisySize.Large => 1.25, DaisySize.ExtraLarge => 1.4, _ => 1
        };
        DialogContent.Width = Math.Max(1, Math.Min(900 * factor, available.Width - 96));
        DialogContent.Height = Math.Max(1, Math.Min(860, available.Height - 120));
    }
}
