using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
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

    public event EventHandler? OpenModalRequested;
    public event EventHandler<ModalRadiiEventArgs>? OpenModalWithRadiiRequested;

    public ActionsExamples()
    {
        InitializeComponent();
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
