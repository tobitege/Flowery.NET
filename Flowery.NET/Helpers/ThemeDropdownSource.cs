using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Flowery.Localization;

namespace Flowery.Controls;

/// <summary>Keeps the input source and its binding separate from the resolved display list.</summary>
internal sealed class ThemeDropdownSource<T> where T : IThemeListItem
{
    private readonly ComboBox _owner;
    private readonly Func<IEnumerable<T>> _defaults;
    private readonly Func<DaisyAddedTheme, T> _createOverride;
    private readonly Action _refresh;
    private readonly Dictionary<string, DaisyAddedTheme> _overrides = new(StringComparer.OrdinalIgnoreCase);
    private IEnumerable? _source;
    private INotifyCollectionChanged? _observed;
    private bool _attached;
    private bool _writingView;

    internal ThemeDropdownSource(ComboBox owner, Func<IEnumerable<T>> defaults,
        Func<DaisyAddedTheme, T> createOverride, Action refresh)
    {
        _owner = owner;
        _defaults = defaults;
        _createOverride = createOverride;
        _refresh = refresh;
        _source = owner.ItemsSource;
        owner.PropertyChanged += OnPropertyChanged;
    }

    internal List<T> Resolve() => ThemeListResolver.Resolve(_source?.Cast<T>() ?? _defaults(), _overrides, _createOverride);

    internal void SetView(List<T> view)
    {
        _writingView = true;
        try
        {
            _owner.SetCurrentValue(ItemsControl.ItemsSourceProperty, view);
        }
        finally
        {
            _writingView = false;
        }
    }

    internal void Add(string name, string label, bool preferred)
    {
        var entry = DaisyThemeManager.CreateAddedTheme(name, label, preferred);
        if (_overrides.TryGetValue(entry.Name, out var old) && old == entry) return;
        _overrides[entry.Name] = entry;
        _refresh();
    }

    internal bool Remove(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (!_overrides.Remove(name)) return false;
        _refresh();
        return true;
    }

    internal void Attach()
    {
        if (_attached) return;
        _attached = true;
        ObserveSource();
        DaisyThemeManager.AvailableThemesChanged += OnAvailableThemesChanged;
        FloweryLocalization.CultureChanged += OnCultureChanged;
        _refresh();
    }

    internal void Detach()
    {
        _attached = false;
        StopObserving();
        DaisyThemeManager.AvailableThemesChanged -= OnAvailableThemesChanged;
        FloweryLocalization.CultureChanged -= OnCultureChanged;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ItemsControl.ItemsSourceProperty || _writingView) return;
        StopObserving();
        _source = e.NewValue as IEnumerable;
        if (_attached) ObserveSource();
        _refresh();
    }

    private void ObserveSource()
    {
        _observed = _source as INotifyCollectionChanged;
        if (_observed != null) _observed.CollectionChanged += OnCollectionChanged;
    }

    private void StopObserving()
    {
        if (_observed != null) _observed.CollectionChanged -= OnCollectionChanged;
        _observed = null;
    }

    private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!_attached || !ReferenceEquals(sender, _observed)) return;
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() => OnCollectionChanged(sender, e));
            return;
        }
        _refresh();
    }

    private void OnAvailableThemesChanged(object? sender, EventArgs e) => RefreshAttached();
    private void OnCultureChanged(object? sender, System.Globalization.CultureInfo e) => RefreshAttached();

    private void RefreshAttached()
    {
        if (!_attached) return;
        if (!Dispatcher.UIThread.CheckAccess()) Dispatcher.UIThread.Post(RefreshAttached);
        else _refresh();
    }
}
