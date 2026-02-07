using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.NET.Gallery.Examples;

public partial class DataInputExamples : UserControl, IScrollableExample
{
    private Dictionary<string, Visual>? _sectionTargetsById;

    public List<string> TagPickerTags { get; } = new()
    {
        "Avalonia",
        "C#",
        "DaisyUI",
        "Flowery",
        "UI",
        "Desktop",
        "Cross-platform"
    };

    public DataInputExamples()
    {
        InitializeComponent();
        DataContext = this;
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

    private void OnResetCodeClicked(object? sender, RoutedEventArgs e)
    {
        var otp = this.FindControl<DaisyOtpInput>("VerifyOtp");
        if (otp != null)
        {
            otp.Value = string.Empty;
        }
    }

    private void OnPinCodeChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is DaisyPasswordBox pinBox)
        {
            // Show check icon when exactly 6 characters are entered
            if (pinBox.Password?.Length == 6)
            {
                pinBox.EndIcon = this.FindResource("DaisyIconCheck") as StreamGeometry;
            }
            else
            {
                pinBox.EndIcon = null;
            }
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
