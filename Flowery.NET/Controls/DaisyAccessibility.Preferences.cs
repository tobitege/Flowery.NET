using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Platform;
using Avalonia.Threading;

namespace Flowery.Controls;

public static partial class DaisyAccessibility
{
    private static bool? _highContrastOverride;
    private static bool? _reducedMotionOverride;
    private static bool _systemReducedMotion;
    private static bool _publishedContrastDark;
    private static PlatformColorValues _systemColors = new();
    private static readonly ConditionalWeakTable<TopLevel, WindowPreferences> Windows = new();
    private static readonly ConditionalWeakTable<Control, MotionObserver> MotionObservers = new();
    private static readonly List<WeakReference<MotionObserver>> MotionObserverReferences = new();

    /// <summary>Observable preferences used by templates and application bindings.</summary>
    public static DaisyAccessibilityPreferences Preferences { get; } = new();

    /// <summary>Overrides system high contrast. Null follows supported platform settings.</summary>
    public static bool? HighContrastOverride
    {
        get => _highContrastOverride;
        set { Dispatcher.UIThread.VerifyAccess(); _highContrastOverride = value; PublishPreferences(); }
    }

    /// <summary>Overrides reduced motion. Null follows Windows client-area animation settings; other hosts can supply an override.</summary>
    public static bool? ReducedMotionOverride
    {
        get => _reducedMotionOverride;
        set { Dispatcher.UIThread.VerifyAccess(); _reducedMotionOverride = value; PublishPreferences(); }
    }

    public static bool IsHighContrast => Preferences.IsHighContrast;
    public static bool IsReducedMotion => !Preferences.MotionEnabled;

    /// <summary>Raised on the UI thread when effective accessibility preferences change.</summary>
    public static event EventHandler? PreferencesChanged;

    /// <summary>Optional host contrast handler. Return true to own contrast resources, including restoration when false is supplied.</summary>
    public static Func<bool, bool>? CustomContrastApplicator { get; set; }

    public static readonly AttachedProperty<bool> MotionEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("MotionEnabled", typeof(DaisyAccessibility), true, inherits: true);
    public static bool GetMotionEnabled(Control control) => control.GetValue(MotionEnabledProperty);
    public static void SetMotionEnabled(Control control, bool value) => control.SetValue(MotionEnabledProperty, value);

    public static readonly AttachedProperty<bool> HighContrastEnabledProperty =
        AvaloniaProperty.RegisterAttached<Control, bool>("HighContrastEnabled", typeof(DaisyAccessibility), false, inherits: true);
    public static bool GetHighContrastEnabled(Control control) => control.GetValue(HighContrastEnabledProperty);
    public static void SetHighContrastEnabled(Control control, bool value) => control.SetValue(HighContrastEnabledProperty, value);

    static DaisyAccessibility()
    {
        RefreshSystemPreferences();
        Control.LoadedEvent.AddClassHandler<Control>((control, _) =>
        {
            if (TopLevel.GetTopLevel(control) is { } topLevel)
                Windows.GetValue(topLevel, static window => new WindowPreferences(window));
        });
    }

    /// <summary>Reads supported system motion settings without changing operating-system preferences.</summary>
    public static void RefreshSystemPreferences()
    {
        if (OperatingSystem.IsWindows() && SystemParametersInfo(0x1042, 0, out var enabled, 0))
            _systemReducedMotion = enabled == 0;
        PublishPreferences();
    }

    /// <summary>Observes a platform settings source until the returned subscription is disposed.</summary>
    public static IDisposable ObservePlatformSettings(IPlatformSettings settings)
    {
        Dispatcher.UIThread.VerifyAccess();
        ArgumentNullException.ThrowIfNull(settings);
        return new PlatformSubscription(settings);
    }

    private static void PublishPreferences()
    {
        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(PublishPreferences);
            return;
        }
        var contrast = _highContrastOverride ?? _systemColors.ContrastPreference == ColorContrastPreference.High;
        var motion = !(_reducedMotionOverride ?? _systemReducedMotion);
        var dark = _systemColors.ThemeVariant == PlatformThemeVariant.Dark;
        var contrastChanged = contrast != IsHighContrast || dark != _publishedContrastDark;
        var motionChanged = motion != Preferences.MotionEnabled;
        var changed = contrast != IsHighContrast || motionChanged;
        Preferences.SetCurrentValue(DaisyAccessibilityPreferences.IsHighContrastProperty, contrast);
        Preferences.SetCurrentValue(DaisyAccessibilityPreferences.MotionEnabledProperty, motion);
        _publishedContrastDark = dark;
        if (contrastChanged) RefreshContrastPalette();
        if (motionChanged)
        {
            foreach (var reference in MotionObserverReferences.ToArray())
                if (reference.TryGetTarget(out var observer)) observer.Notify();
            MotionObserverReferences.RemoveAll(static reference => !reference.TryGetTarget(out _));
        }
        if (changed) PreferencesChanged?.Invoke(null, EventArgs.Empty);
    }

    internal static void RegisterMotionHandler(Control control, Action<Control> callback)
    {
        if (!MotionObservers.TryGetValue(control, out var observer))
        {
            observer = new MotionObserver(control);
            MotionObservers.Add(control, observer);
            MotionObserverReferences.Add(new WeakReference<MotionObserver>(observer));
        }
        observer.Add(callback);
    }

    private sealed class MotionObserver(Control target)
    {
        private readonly WeakReference<Control> _target = new(target);
        private readonly List<Action<Control>> _callbacks = new();
        public void Add(Action<Control> callback) { if (!_callbacks.Contains(callback)) _callbacks.Add(callback); }
        public void Notify()
        {
            if (_target.TryGetTarget(out var control) && TopLevel.GetTopLevel(control) != null)
                foreach (var callback in _callbacks.ToArray()) callback(control);
        }
    }

    internal static void RefreshContrastPalette() =>
        DaisyThemeManager.ApplyContrastResources(IsHighContrast, _systemColors.ThemeVariant == PlatformThemeVariant.Dark);

    [DllImport("user32.dll", EntryPoint = "SystemParametersInfoW", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, out int value, uint flags);

    private sealed class PlatformSubscription : IDisposable
    {
        private IPlatformSettings? _settings;
        public PlatformSubscription(IPlatformSettings settings)
        {
            _settings = settings;
            settings.ColorValuesChanged += OnColorsChanged;
            OnColorsChanged(settings, settings.GetColorValues());
        }

        private void OnColorsChanged(object? sender, PlatformColorValues values)
        {
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() => { if (_settings != null) OnColorsChanged(sender, values); });
                return;
            }
            _systemColors = values;
            PublishPreferences();
        }

        public void Dispose()
        {
            if (_settings == null) return;
            _settings.ColorValuesChanged -= OnColorsChanged;
            _settings = null;
        }
    }

    private sealed class WindowPreferences
    {
        private readonly TopLevel _window;
        private readonly IDisposable? _subscription;
        private readonly DispatcherTimer _motionPoll = new() { Interval = TimeSpan.FromSeconds(2) };
        public WindowPreferences(TopLevel window)
        {
            _window = window;
            if (Application.Current?.PlatformSettings is { } settings) _subscription = ObservePlatformSettings(settings);
            window.Closed += OnClosed;
            if (OperatingSystem.IsWindows())
            {
                _motionPoll.Tick += OnPoll;
                _motionPoll.Start();
            }
            RefreshSystemPreferences();
        }

        private void OnPoll(object? sender, EventArgs e) => RefreshSystemPreferences();
        private void OnClosed(object? sender, EventArgs e)
        {
            _window.Closed -= OnClosed;
            _subscription?.Dispose();
            _motionPoll.Stop();
            _motionPoll.Tick -= OnPoll;
            Windows.Remove(_window);
        }
    }
}

/// <summary>Read-only effective accessibility preferences. Configure overrides through DaisyAccessibility.</summary>
public sealed class DaisyAccessibilityPreferences : AvaloniaObject
{
    public static readonly StyledProperty<bool> IsHighContrastProperty =
        AvaloniaProperty.Register<DaisyAccessibilityPreferences, bool>(nameof(IsHighContrast));
    public static readonly StyledProperty<bool> MotionEnabledProperty =
        AvaloniaProperty.Register<DaisyAccessibilityPreferences, bool>(nameof(MotionEnabled), true);
    public bool IsHighContrast => GetValue(IsHighContrastProperty);
    public bool MotionEnabled => GetValue(MotionEnabledProperty);
}
