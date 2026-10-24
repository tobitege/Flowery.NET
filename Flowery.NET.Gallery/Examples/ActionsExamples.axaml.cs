using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.NET.Gallery.Examples;

public class ModalRadiiEventArgs : EventArgs
{
    public double TopLeft { get; set; } = 16;
    public double TopRight { get; set; } = 16;
    public double BottomLeft { get; set; } = 16;
    public double BottomRight { get; set; } = 16;
    public string Title { get; set; } = "Modal";
}

public partial class ActionsExamples : UserControl, IScrollableExample
{
    private Dictionary<string, Visual>? _sectionTargetsById;

    public List<string> DropdownMenuItems { get; } = new()
    {
        "Edit",
        "Duplicate",
        "Archive",
        "Delete"
    };

    public event EventHandler? OpenModalRequested;
    public event EventHandler<ModalRadiiEventArgs>? OpenModalWithRadiiRequested;

    // The glass button demo's ball, in DIPs from the top-left of its area and DIPs per second.
    private Point _ballPosition = new(40, 30);
    private Vector _ballVelocity = new(190, 140);
    private TimeSpan? _lastBallFrame;
    private bool _ballFrameRequested;

    public ActionsExamples()
    {
        InitializeComponent();
        DataContext = this;
        GlassButtonMotionToggle.IsCheckedChanged += (_, _) => RequestBallFrame();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        RequestBallFrame();
    }

    private void RequestBallFrame()
    {
        if (_ballFrameRequested || GlassButtonMotionToggle.IsChecked != true || TopLevel.GetTopLevel(this) is not { } topLevel)
            return;
        _ballFrameRequested = true;
        topLevel.RequestAnimationFrame(OnBallFrame);
    }

    private void OnBallFrame(TimeSpan time)
    {
        _ballFrameRequested = false;
        if (GlassButtonMotionToggle.IsChecked != true || TopLevel.GetTopLevel(this) == null
            || GlassDemoBall is not { Parent: Control area, RenderTransform: TranslateTransform transform })
        {
            _lastBallFrame = null;
            return;
        }

        // A long gap, such as after the page was hidden, must not throw the ball through a wall.
        var elapsed = _lastBallFrame is { } last ? Math.Min((time - last).TotalSeconds, 0.05) : 0;
        _lastBallFrame = time;
        var (x, vx) = Bounce(_ballPosition.X + _ballVelocity.X * elapsed, _ballVelocity.X, area.Bounds.Width - GlassDemoBall.Bounds.Width);
        var (y, vy) = Bounce(_ballPosition.Y + _ballVelocity.Y * elapsed, _ballVelocity.Y, area.Bounds.Height - GlassDemoBall.Bounds.Height);
        _ballPosition = new Point(x, y);
        _ballVelocity = new Vector(vx, vy);
        transform.X = x;
        transform.Y = y;
        RequestBallFrame();
    }

    // Reflects a position that passed a wall back inside, and turns the velocity away from that wall.
    private static (double Position, double Velocity) Bounce(double position, double velocity, double limit)
    {
        if (limit <= 0)
            return (0, velocity);
        if (position < 0)
            return (Math.Min(-position, limit), Math.Abs(velocity));
        if (position > limit)
            return (Math.Max(2 * limit - position, 0), -Math.Abs(velocity));
        return (position, velocity);
    }

    public void OpenGlassModal_Click(object? sender, RoutedEventArgs e)
    {
        GlassShowcaseModal.IsOpen = true;
    }

    public void CloseGlassModal_Click(object? sender, RoutedEventArgs e)
    {
        GlassShowcaseModal.IsOpen = false;
    }

    public void OpenModalBtn_Click(object? sender, RoutedEventArgs e)
    {
        OpenModalRequested?.Invoke(this, EventArgs.Empty);
    }

    public void OpenDefaultModal_Click(object? sender, RoutedEventArgs e)
    {
        OpenModalWithRadiiRequested?.Invoke(this, new ModalRadiiEventArgs
        {
            TopLeft = 16, TopRight = 16, BottomLeft = 16, BottomRight = 16,
            Title = "Default Corners"
        });
    }

    public void OpenPillTopModal_Click(object? sender, RoutedEventArgs e)
    {
        OpenModalWithRadiiRequested?.Invoke(this, new ModalRadiiEventArgs
        {
            TopLeft = 24, TopRight = 24, BottomLeft = 8, BottomRight = 8,
            Title = "Pill Top"
        });
    }

    public void OpenSharpModal_Click(object? sender, RoutedEventArgs e)
    {
        OpenModalWithRadiiRequested?.Invoke(this, new ModalRadiiEventArgs
        {
            TopLeft = 0, TopRight = 0, BottomLeft = 0, BottomRight = 0,
            Title = "Sharp Corners"
        });
    }

    private void OnFabItemClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control control && control.Tag is string tag)
        {
            ShowToast($"FAB item clicked: {tag}");
        }
    }

    private void ShowToast(string message)
    {
        var toast = this.FindControl<DaisyToast>("ActionsToast");
        if (toast != null)
        {
            var alert = new DaisyAlert
            {
                Content = message,
                Variant = DaisyAlertVariant.Success,
                Margin = new Thickness(0, 4)
            };

            toast.Items.Add(alert);

            // Auto remove after 3 seconds
            DispatcherTimer.RunOnce(() =>
            {
                toast.Items.Remove(alert);
            }, TimeSpan.FromSeconds(3));
        }
    }

    private void OnMenuDropdownSelectedItemChanged(object? sender, DaisyDropdownSelectionChangedEventArgs e)
    {
        if (e.SelectedItem is string s && !string.IsNullOrWhiteSpace(s))
        {
            ShowToast($"Action: {s}");
        }

        if (sender is DaisyDropdown dropdown)
        {
            Dispatcher.UIThread.Post(() => dropdown.SelectedItem = null, DispatcherPriority.Background);
        }
    }

    public void ScrollToSection(string sectionId)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (scrollViewer == null) return;

        var target = GetSectionTarget(sectionId);
        if (target == null) return;

        var transform = target.TransformToVisual(scrollViewer);
        if (transform.HasValue)
        {
            var point = transform.Value.Transform(new Point(0, 0));
            // Add current scroll offset to get absolute position in content
            scrollViewer.Offset = new Vector(0, point.Y + scrollViewer.Offset.Y);
        }
    }

    private Visual? GetSectionTarget(string sectionId)
    {
        if (_sectionTargetsById == null)
        {
            _sectionTargetsById = new Dictionary<string, Visual>(StringComparer.OrdinalIgnoreCase);
            foreach (var header in this.GetVisualDescendants().OfType<SectionHeader>())
            {
                if (!string.IsNullOrWhiteSpace(header.SectionId))
                    _sectionTargetsById[header.SectionId] = header.Parent as Visual ?? header;
            }
        }

        return _sectionTargetsById.TryGetValue(sectionId, out var target) ? target : null;
    }
}
