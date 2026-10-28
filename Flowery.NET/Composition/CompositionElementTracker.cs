using System;
using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Composition;

internal readonly record struct CompositionElementBounds(Rect Bounds, double RasterizationScale, TopLevel Root);

/// <summary>Reports changed bounds in a host's DIP coordinates after layout or scrolling.</summary>
internal sealed class CompositionElementTracker : IDisposable
{
    private readonly Control _target;
    private readonly Control _host;
    private readonly Action<CompositionElementBounds?> _changed;
    private readonly HashSet<ScrollViewer> _scrollViewers = [];
    private TopLevel? _root;
    private CompositionElementBounds? _bounds;
    private bool _queued;
    private bool _disposed;

    internal CompositionElementTracker(Control target, Control host, Action<CompositionElementBounds?> changed)
    {
        _target = target;
        _host = host;
        _changed = changed;
        target.Loaded += OnLoaded;
        target.Unloaded += OnUnloaded;
        host.Loaded += OnLoaded;
        host.Unloaded += OnUnloaded;
        target.LayoutUpdated += OnLayoutUpdated;
        QueueUpdate();
    }

    private void OnLoaded(object? sender, RoutedEventArgs args)
    {
        StopObserving();
        QueueUpdate();
    }

    private void OnUnloaded(object? sender, RoutedEventArgs args)
    {
        StopObserving();
        Publish(null);
    }

    private void OnLayoutUpdated(object? sender, EventArgs args) => QueueUpdate();
    private void OnRootChanged(object? sender, EventArgs args) => QueueUpdate();
    private void OnViewChanged(object? sender, ScrollChangedEventArgs args) => QueueUpdate();

    private void QueueUpdate()
    {
        if (_disposed || _queued)
            return;
        _queued = true;
        Dispatcher.UIThread.Post(Update, DispatcherPriority.Background);
    }

    private void Update()
    {
        _queued = false;
        if (_disposed)
            return;
        if (TopLevel.GetTopLevel(_target) is not { } root ||
            root != TopLevel.GetTopLevel(_host))
        {
            StopObserving();
            Publish(null);
            return;
        }
        if (_root != root)
        {
            StopObserving();
            _root = root;
            root.ScalingChanged += OnRootChanged;
            ObserveScrollViewers(_target);
            ObserveScrollViewers(_host);
        }
        if (!_target.IsVisible || _target.Bounds.Width <= 0 || _target.Bounds.Height <= 0 ||
            _target.TransformToVisual(_host) is not { } transform)
        {
            Publish(null);
            return;
        }
        var bounds = new Rect(0, 0, _target.Bounds.Width, _target.Bounds.Height).TransformToAABB(transform);
        Publish(new CompositionElementBounds(bounds, root.RenderScaling, root));
    }

    private void ObserveScrollViewers(Visual element)
    {
        for (Visual? current = element; current is not null; current = current.GetVisualParent())
        {
            if (current is ScrollViewer scrollViewer && _scrollViewers.Add(scrollViewer))
                scrollViewer.ScrollChanged += OnViewChanged;
        }
    }

    private void Publish(CompositionElementBounds? bounds)
    {
        if (_bounds == bounds)
            return;
        _bounds = bounds;
        _changed(bounds);
    }

    private void StopObserving()
    {
        if (_root is not null)
            _root.ScalingChanged -= OnRootChanged;
        _root = null;
        foreach (var scrollViewer in _scrollViewers)
            scrollViewer.ScrollChanged -= OnViewChanged;
        _scrollViewers.Clear();
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _target.Loaded -= OnLoaded;
        _target.Unloaded -= OnUnloaded;
        _host.Loaded -= OnLoaded;
        _host.Unloaded -= OnUnloaded;
        _target.LayoutUpdated -= OnLayoutUpdated;
        StopObserving();
    }
}
