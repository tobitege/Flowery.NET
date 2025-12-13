using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.VisualTree;

namespace Flowery.NET.Gallery.Examples;

/// <summary>
/// Examples for Date Display controls: DaisyDateTimeline and DaisyTimeline.
/// </summary>
public partial class DateDisplayExamples : UserControl, IScrollableExample
{
    private Dictionary<string, Visual>? _sectionTargetsById;

    public DateDisplayExamples()
    {
        InitializeComponent();
        InitializeMarkedDates();
    }

    /// <summary>
    /// Sets up marked dates and event handlers for the BookingTimeline example.
    /// </summary>
    private void InitializeMarkedDates()
    {
        // Set up marked dates for the BookingTimeline example
        var bookingTimeline = this.FindControl<Flowery.Controls.DaisyDateTimeline>("BookingTimeline");
        var bookingToast = this.FindControl<Flowery.Controls.DaisyToast>("BookingToast");
        
        if (bookingTimeline != null)
        {
            // Add some example marked dates with tooltips
            bookingTimeline.MarkedDates = new List<Flowery.Controls.DateMarker>
            {
                new(System.DateTime.Today.AddDays(2), "Dr. Smith - 10:00 AM"),
                new(System.DateTime.Today.AddDays(5), "Team Meeting - 2:00 PM"),
                new(System.DateTime.Today.AddDays(8), "Dentist Appointment"),
                new(System.DateTime.Today.AddDays(12), "Project Review"),
            };

            // Wire up events to show toasts
            bookingTimeline.DateClicked += (s, date) =>
            {
                ShowBookingToast(bookingToast, $"Clicked: {date:ddd, MMM d}", Flowery.Controls.DaisyAlertVariant.Info);
            };

            bookingTimeline.DateConfirmed += (s, date) =>
            {
                ShowBookingToast(bookingToast, $"Confirmed: {date:ddd, MMM d}", Flowery.Controls.DaisyAlertVariant.Success);
            };

            bookingTimeline.EscapePressed += (s, e) =>
            {
                ShowBookingToast(bookingToast, "Jumped to today", Flowery.Controls.DaisyAlertVariant.Warning);
            };
        }
    }

    /// <summary>
    /// Shows a toast notification for BookingTimeline events.
    /// </summary>
    private void ShowBookingToast(Flowery.Controls.DaisyToast? toast, string message, Flowery.Controls.DaisyAlertVariant variant)
    {
        if (toast == null) return;
        
        var alert = new Flowery.Controls.DaisyAlert
        {
            Content = message,
            Variant = variant
        };
        
        toast.Items.Add(alert);
        
        // Auto-remove after 2 seconds
        var timer = new Avalonia.Threading.DispatcherTimer { Interval = System.TimeSpan.FromSeconds(2) };
        timer.Tick += (s, e) =>
        {
            timer.Stop();
            toast.Items.Remove(alert);
        };
        timer.Start();
    }

    /// <summary>
    /// Scrolls to the specified section in the view.
    /// </summary>
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
