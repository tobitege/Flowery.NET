using System;
using System.Collections.Generic;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Flowery.Controls.Scheduler.Models;
using Flowery.Services;

namespace Flowery.Controls;

public partial class DaisyScheduler
{
    internal const double MinimumColumnZoom = 1;
    internal const double MaximumColumnZoom = 4;
    // A double tap on a column heading switches between the fitted columns and this zoom.
    private const double WideColumnZoom = 2;
    // One notch of the mouse wheel with Ctrl.
    private const double WheelZoomStep = 1.25;
    private const string ColumnZoomState = "columnZoom=";

    // The x in the body that a zoom keeps where it is: under the fingers or the pointer; null keeps the middle.
    private double? _zoomFocus;
    private double? _pinchStartZoom;
    private bool _loadingState;
    private bool _savePending;

    // Agenda and Calendar have no columns or hours to widen.
    private bool ZoomsColumns => CurrentView is not (SchedulerView.Agenda or SchedulerView.Calendar);

    private void ZoomByUser(double zoom, double focus)
    {
        _zoomFocus = focus;
        try
        {
            SetCurrentValue(ColumnZoomProperty, zoom);
        }
        finally
        {
            _zoomFocus = null;
        }
    }

    // Resizes the columns of the shown view for the new zoom and keeps the content under the focus where it was.
    private void ZoomColumns()
    {
        if (_bodyScroll is not { Viewport.Width: > 0 } scroll)
            return;
        var focus = _zoomFocus ?? scroll.Viewport.Width / 2;
        var oldExtent = scroll.Extent.Width;
        var anchor = scroll.Offset.X + focus;
        UpdateViewGeometry();
        scroll.UpdateLayout();
        if (oldExtent > 0)
            scroll.Offset = new Vector(Math.Max(0, anchor * scroll.Extent.Width / oldExtent - focus), scroll.Offset.Y);
    }

    private void OnPinch(object? sender, PinchEventArgs e)
    {
        if (!ZoomsColumns || _bodyScroll is not { } scroll)
            return;
        if (_pinchStartZoom is null)
        {
            _pinchStartZoom = ColumnZoom;
            // What the first finger began is neither a tap nor a drag.
            _pendingTap = null;
            _taps = 0;
            CancelGesture();
        }
        var origin = (e.Source as Visual)?.TranslatePoint(e.ScaleOrigin, scroll) ?? e.ScaleOrigin;
        ZoomByUser(_pinchStartZoom.Value * e.Scale, origin.X);
        e.Handled = true;
    }

    private void OnPinchEnded(object? sender, PinchEndedEventArgs e) => _pinchStartZoom = null;

    private void OnZoomWheel(object? sender, PointerWheelEventArgs e)
    {
        if ((e.KeyModifiers & (KeyModifiers.Control | KeyModifiers.Meta)) == 0 || e.Delta.Y == 0 || !ZoomsColumns ||
            _bodyScroll is not { } scroll)
            return;
        ZoomByUser(ColumnZoom * Math.Pow(WheelZoomStep, e.Delta.Y), e.GetPosition(scroll).X);
        e.Handled = true;
    }

    // A double click on a column heading, or on touch and pen two taps in a row, switches between the fitted columns
    // and wide ones. A touch press is decided on release like any other (see PendingTap): a swipe that starts on a
    // heading only scrolls, and DoubleTapped would count it.
    private void AttachColumnHeading(Control heading) => heading.PointerPressed += OnColumnHeadingPressed;

    private void OnColumnHeadingPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control heading || !e.GetCurrentPoint(heading).Properties.IsLeftButtonPressed)
            return;
        e.Handled = true;
        if (e.Pointer.Type != PointerType.Mouse)
            _pendingTap = _flinging ? null : new PendingTap(e.Pointer, e.GetPosition(this), null, null, default, e.ClickCount, Heading: true);
        else if (e.ClickCount == 2)
            ToggleWideColumns(e);
    }

    private void ToggleWideColumns(PointerEventArgs e)
    {
        if (_bodyScroll is { } scroll)
            ZoomByUser(ColumnZoom > MinimumColumnZoom ? MinimumColumnZoom : WideColumnZoom, e.GetPosition(scroll).X);
    }

    private string? StorageKey => string.IsNullOrWhiteSpace(StateKey) ? null : StateStorageKeyHelper.BuildScopedKey("scheduler", StateKey);

    // The settings are a convenience: a storage that cannot be read or written keeps the current ones.
    private void LoadState()
    {
        if (!_attached || StorageKey is not { } key)
            return;
        IReadOnlyList<string> lines;
        try
        {
            lines = StateStorageProvider.Instance.LoadLines(key);
        }
        catch (Exception)
        {
            return;
        }
        foreach (var line in lines)
        {
            if (!line.StartsWith(ColumnZoomState, StringComparison.Ordinal) ||
                !double.TryParse(line.AsSpan(ColumnZoomState.Length), NumberStyles.Float, CultureInfo.InvariantCulture, out var zoom) ||
                !double.IsFinite(zoom))
                continue;
            _loadingState = true;
            try
            {
                SetCurrentValue(ColumnZoomProperty, zoom);
            }
            finally
            {
                _loadingState = false;
            }
        }
    }

    private void SaveState()
    {
        if (_loadingState || _savePending || !_attached || StorageKey is null)
            return;
        // A pinch changes the zoom many times; one write after it is enough.
        _savePending = true;
        Dispatcher.UIThread.Post(() =>
        {
            _savePending = false;
            if (StorageKey is not { } key)
                return;
            try
            {
                StateStorageProvider.Instance.SaveLines(key, [ColumnZoomState + ColumnZoom.ToString("R", CultureInfo.InvariantCulture)]);
            }
            catch (Exception)
            {
                // Kept for this session only.
            }
        }, DispatcherPriority.Background);
    }
}
