using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.NET.Gallery.Examples;

public partial class DataInputExamples : UserControl, IScrollableExample
{
    private Dictionary<string, Visual>? _sectionTargetsById;

    public DataInputExamples()
    {
        InitializeComponent();
    }

    private void OnFeedbackButtonClicked(object? sender, RoutedEventArgs e)
    {
        var toast = this.FindControl<DaisyToast>("DemoToast");
        if (toast != null)
        {
            var textArea = sender as DaisyTextArea;
            var feedbackText = textArea?.Text ?? "No feedback";
            
            var alert = new DaisyAlert
            {
                Content = $"Feedback submitted: \"{feedbackText}\"",
                Variant = DaisyAlertVariant.Success
            };
            toast.Items.Add(alert);
            
            // Auto-remove after 3 seconds
            var timer = new System.Timers.Timer(3000);
            timer.Elapsed += (s, args) =>
            {
                timer.Stop();
                Avalonia.Threading.Dispatcher.UIThread.Post(() => toast.Items.Remove(alert));
            };
            timer.Start();
        }
    }

    public void ScrollToSection(string sectionName)
    {
        var scrollViewer = this.FindControl<ScrollViewer>("MainScrollViewer");
        if (scrollViewer == null) return;

        var target = GetSectionTarget(sectionName);
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
