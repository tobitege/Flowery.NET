using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Flowery.NET.Photos.Services;

namespace Flowery.NET.Photos.Views;

public partial class ViewerView
{
    private readonly DispatcherTimer _hintTimer = new() { Interval = TimeSpan.FromSeconds(2.5) };
    private WindowState _slideshowPreviousState;
    private bool _nearChrome;
    public bool IsSlideshow => Canvas.IsSlideshow;
    public event Action<bool>? TitleViewChanged;
    private bool ToolbarOverlay => _fullscreen || PhotosSettings.AutoHideToolbar;

    private void InitializeChrome()
    {
        _hintTimer.Tick += (_, _) => { SlideshowHint.IsVisible = false; _hintTimer.Stop(); };
        _toolbarTimer.Tick += (_, _) =>
        {
            _toolbarTimer.Stop();
            if (!_nearChrome && MoreButton.Flyout?.IsOpen != true) SetChromeVisible(false);
        };
        PointerMoved += (_, e) =>
        {
            if (IsSlideshow || !ToolbarOverlay) return;
            var y = e.GetPosition(this).Y;
            _nearChrome = y <= Math.Max(96, Toolbar.Bounds.Height) || _fullscreen && y >= Bounds.Height - 96;
            if (_nearChrome) { SetChromeVisible(true); _toolbarTimer.Stop(); }
            else ScheduleChromeHide(TimeSpan.FromMilliseconds(300));
        };
        PointerExited += (_, _) =>
        {
            _nearChrome = false;
            ScheduleChromeHide(TimeSpan.FromMilliseconds(300));
        };
        Canvas.AddHandler(PointerPressedEvent, (_, e) =>
        {
            if (IsSlideshow) { StopSlideshow(); e.Handled = true; }
            else { _toolbarTimer.Stop(); SetChromeVisible(false); }
        }, RoutingStrategies.Tunnel);
        Toolbar.SizeChanged += (_, _) => UpdatePanelMargin();
        if (MoreButton.Flyout is { } more) more.Closed += (_, _) => SetChromeVisible(false);
    }

    private void ScheduleChromeHide(TimeSpan delay)
    {
        if (!ToolbarOverlay || IsSlideshow || _toolbarTimer.IsEnabled) return;
        _toolbarTimer.Interval = delay; _toolbarTimer.Start();
    }

    private void SetChromeVisible(bool visible)
    {
        if (IsSlideshow) return;
        var toolbar = visible || !ToolbarOverlay;
        Toolbar.Opacity = toolbar ? 1 : 0;
        Toolbar.IsHitTestVisible = toolbar;
        StatusPanel.Opacity = visible || !_fullscreen ? 1 : 0;
        StatusPanel.IsHitTestVisible = visible || !_fullscreen;
        TitleViewChanged?.Invoke(IsVisible && !toolbar && !_fullscreen);
    }

    private void UpdatePanelMargin() => SidePanel.Margin = new Thickness(0,
        ToolbarOverlay ? Math.Max(52, Toolbar.Bounds.Height) : 0, 8,
        _fullscreen && PhotosSettings.ShowStatusBar ? StatusPanel.Bounds.Height + 4 : 0);

    private void ApplyChromeLayout()
    {
        Toolbar.IsVisible = !IsSlideshow;
        StatusPanel.IsVisible = !IsSlideshow && PhotosSettings.ShowStatusBar;
        PreviousButton.IsVisible = NextButton.IsVisible = !IsSlideshow;
        var start = ToolbarOverlay || IsSlideshow ? 0 : 1;
        Grid.SetRow(MainArea, start);
        Grid.SetRowSpan(MainArea, (_fullscreen || IsSlideshow ? 2 : 1) - start + 1);
        CanvasFrame.Margin = _fullscreen || IsSlideshow ? new Thickness(0) : new Thickness(8, 0);
        CanvasFrame.CornerRadius = new CornerRadius(_fullscreen || IsSlideshow ? 0 : 12);
        CanvasFrame.Background = IsSlideshow ? Brushes.Black : this.FindResource("DaisyBase100Brush") as IBrush;
        var brush = this.FindResource("DaisyBase200Brush") as ISolidColorBrush;
        var color = brush?.Color ?? Colors.Black;
        var overlay = new SolidColorBrush(Color.FromArgb(225, color.R, color.G, color.B));
        Toolbar.Background = ToolbarOverlay ? overlay : null;
        StatusPanel.Background = _fullscreen ? overlay : null;
        UpdatePanelMargin();
        _toolbarTimer.Stop();
        SetChromeVisible(true);
        ScheduleChromeHide(TimeSpan.FromSeconds(1.5));
    }

    public void ShowViewOptions() { SelectPanel("view"); SetChromeVisible(true); }

    public void StartSlideshow()
    {
        if (IsSlideshow || Canvas.Bitmap == null || _items.Count == 0) return;
        _slideshowPreviousState = Owner.WindowState;
        Canvas.IsSlideshow = true;
        ClosePanel(); Compare.IsChecked = false;
        Owner.WindowState = WindowState.FullScreen;
        ImmersiveChanged?.Invoke(true); TitleViewChanged?.Invoke(false);
        ApplyChromeLayout(); Canvas.Fit();
        SlideshowHint.IsVisible = true; _hintTimer.Start();
        _slideshow.Interval = TimeSpan.FromSeconds(PhotosSettings.SlideshowSeconds);
        _slideshow.Start(); Canvas.Focus();
    }

    public void StopSlideshow()
    {
        if (!IsSlideshow) return;
        _slideshow.Stop(); _hintTimer.Stop(); SlideshowHint.IsVisible = false;
        Canvas.IsSlideshow = false; Canvas.Opacity = 1;
        Owner.WindowState = _slideshowPreviousState;
        ImmersiveChanged?.Invoke(_fullscreen);
        ApplyChromeLayout(); Canvas.Fit(); RefreshNavigation();
    }

    private void ToggleFullScreen()
    {
        if (IsSlideshow) return;
        if (!_fullscreen) { _previousState = Owner.WindowState; Owner.WindowState = WindowState.FullScreen; }
        else Owner.WindowState = _previousState;
        _fullscreen = !_fullscreen;
        ImmersiveChanged?.Invoke(_fullscreen);
        ClosePanel(); ApplyChromeLayout(); Canvas.Fit();
    }

    private void RefreshNavigation()
    {
        var index = _items.ToList().FindIndex(item => item.Path == _item?.Path);
        PreviousButton.IsEnabled = index > 0;
        NextButton.IsEnabled = index >= 0 && index < _items.Count - 1;
    }
}
