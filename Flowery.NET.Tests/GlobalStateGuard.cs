using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Styling;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Utilities;
using Flowery.Localization;
using Flowery.Services;
using Xunit.v3;

[assembly: Flowery.NET.Tests.GlobalStateGuard]

namespace Flowery.NET.Tests;

/// <summary>
/// The tests share one application. A test that leaves global state changed would break whichever test runs after
/// it, depending on the order, so this fails the test that left it changed and puts the state back.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class GlobalStateGuardAttribute : BeforeAfterTestAttribute
{
    // Tests run one at a time on the UI thread, so one snapshot at a time is enough.
    private GlobalState? _before;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test) => _before = GlobalState.Capture();

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (_before is not { } before)
            return;
        _before = null;
        var changes = before.ChangesTo(GlobalState.Capture());
        if (changes.Count == 0)
            return;
        before.Restore();
        throw new InvalidOperationException("The test left global state changed: " + string.Join("; ", changes));
    }
}

internal sealed class GlobalState
{
    private Application App { get; } = Application.Current!;
    private string? Theme { get; init; }
    private string[] Excluded { get; init; } = [];
    private string[] Preferred { get; init; } = [];
    private string[] Registered { get; init; } = [];
    private bool SuppressThemes { get; init; }
    private bool NotifyInternalThemes { get; init; }
    private Func<string, bool>? ThemeApplicator { get; init; }
    private ThemeVariant? Variant { get; init; }
    private IResourceProvider[] Dictionaries { get; init; } = [];
    private IStyle[] Styles { get; init; } = [];
    private CultureInfo Localization { get; init; } = CultureInfo.InvariantCulture;
    private Func<string, string>? Resolver { get; init; }
    private CultureInfo Culture { get; init; } = CultureInfo.InvariantCulture;
    private CultureInfo UICulture { get; init; } = CultureInfo.InvariantCulture;
    private CultureInfo? DefaultCulture { get; init; }
    private CultureInfo? DefaultUICulture { get; init; }
    private bool? HighContrast { get; init; }
    private bool? ReducedMotion { get; init; }
    private Func<bool, bool>? ContrastApplicator { get; init; }
    private DaisySize Size { get; init; }
    private bool GlobalSizeByDefault { get; init; }
    private bool GlobalAutoSize { get; init; }
    private Window? SizeWindow { get; init; }
    private (double Width, double Height, double Min, double Max) Scale { get; init; }
    private Func<DateTime, DateTime, string>? RangeFormatter { get; init; }
    private IStateStorage? Storage { get; init; }
    private DaisyGlassLiveRenderer? GlassRenderer { get; init; }

    public static GlobalState Capture() => new()
    {
        Theme = DaisyThemeManager.CurrentThemeName,
        Excluded = [.. DaisyThemeManager.ExcludedThemes.Order(StringComparer.OrdinalIgnoreCase)],
        Preferred = [.. DaisyThemeManager.PreferredThemes],
        Registered = [.. DaisyThemeManager.AvailableThemes.Select(static theme => theme.Name).Order(StringComparer.OrdinalIgnoreCase)],
        SuppressThemes = DaisyThemeManager.SuppressThemeApplication,
        NotifyInternalThemes = DaisyThemeManager.NotifyForInternalThemesChanged,
        ThemeApplicator = DaisyThemeManager.CustomThemeApplicator,
        Variant = Application.Current!.RequestedThemeVariant,
        Dictionaries = [.. Application.Current.Resources.MergedDictionaries],
        Styles = [.. Application.Current.Styles],
        Localization = FloweryLocalization.CurrentCulture,
        Resolver = FloweryLocalization.CustomResolver,
        Culture = CultureInfo.CurrentCulture,
        UICulture = CultureInfo.CurrentUICulture,
        DefaultCulture = CultureInfo.DefaultThreadCurrentCulture,
        DefaultUICulture = CultureInfo.DefaultThreadCurrentUICulture,
        HighContrast = DaisyAccessibility.HighContrastOverride,
        ReducedMotion = DaisyAccessibility.ReducedMotionOverride,
        ContrastApplicator = DaisyAccessibility.CustomContrastApplicator,
        Size = FlowerySizeManager.CurrentSize,
        GlobalSizeByDefault = FlowerySizeManager.UseGlobalSizeByDefault,
        GlobalAutoSize = FlowerySizeManager.EnableGlobalAutoSize,
        SizeWindow = FlowerySizeManager.MainWindow,
        Scale = (FloweryScaleConfig.ReferenceWidth, FloweryScaleConfig.ReferenceHeight,
            FloweryScaleConfig.MinScaleFactor, FloweryScaleConfig.MaxScaleFactor),
        RangeFormatter = SchedulerTimeFormatter.RangeFormatter,
        Storage = StateStorageProvider.Instance,
        GlassRenderer = DaisyGlassLiveRenderer.Current
    };

    public List<string> ChangesTo(GlobalState after)
    {
        var changes = new List<string>();
        void Check(string name, bool same, object? from, object? to)
        {
            if (!same)
                changes.Add($"{name}: {from ?? "null"} -> {to ?? "null"}");
        }

        Check("current theme", string.Equals(Theme, after.Theme, StringComparison.OrdinalIgnoreCase), Theme, after.Theme);
        Check("excluded themes", Excluded.SequenceEqual(after.Excluded, StringComparer.OrdinalIgnoreCase), string.Join(",", Excluded), string.Join(",", after.Excluded));
        Check("preferred themes", Preferred.SequenceEqual(after.Preferred, StringComparer.OrdinalIgnoreCase), string.Join(",", Preferred), string.Join(",", after.Preferred));
        Check("registered themes", Registered.SequenceEqual(after.Registered, StringComparer.OrdinalIgnoreCase),
            string.Join(",", Registered.Except(after.Registered)), string.Join(",", after.Registered.Except(Registered)));
        Check("SuppressThemeApplication", SuppressThemes == after.SuppressThemes, SuppressThemes, after.SuppressThemes);
        Check("NotifyForInternalThemesChanged", NotifyInternalThemes == after.NotifyInternalThemes, NotifyInternalThemes, after.NotifyInternalThemes);
        Check("CustomThemeApplicator", ThemeApplicator == after.ThemeApplicator, ThemeApplicator, after.ThemeApplicator);
        Check("RequestedThemeVariant", Equals(Variant, after.Variant), Variant, after.Variant);
        // A theme applied again is a new instance of the same palette, so the dictionaries compare by count.
        Check("merged dictionaries", Dictionaries.Length == after.Dictionaries.Length, Dictionaries.Length, after.Dictionaries.Length);
        Check("application styles", Styles.SequenceEqual(after.Styles), Styles.Length, after.Styles.Length);
        Check("localization culture", Equals(Localization, after.Localization), Localization.Name, after.Localization.Name);
        Check("CustomResolver", Resolver == after.Resolver, Resolver, after.Resolver);
        Check("CurrentCulture", Equals(Culture, after.Culture), Culture.Name, after.Culture.Name);
        Check("CurrentUICulture", Equals(UICulture, after.UICulture), UICulture.Name, after.UICulture.Name);
        Check("DefaultThreadCurrentCulture", Equals(DefaultCulture, after.DefaultCulture), DefaultCulture?.Name, after.DefaultCulture?.Name);
        Check("DefaultThreadCurrentUICulture", Equals(DefaultUICulture, after.DefaultUICulture), DefaultUICulture?.Name, after.DefaultUICulture?.Name);
        Check("HighContrastOverride", HighContrast == after.HighContrast, HighContrast, after.HighContrast);
        Check("ReducedMotionOverride", ReducedMotion == after.ReducedMotion, ReducedMotion, after.ReducedMotion);
        Check("CustomContrastApplicator", ContrastApplicator == after.ContrastApplicator, ContrastApplicator, after.ContrastApplicator);
        Check("global size", Size == after.Size, Size, after.Size);
        Check("UseGlobalSizeByDefault", GlobalSizeByDefault == after.GlobalSizeByDefault, GlobalSizeByDefault, after.GlobalSizeByDefault);
        Check("EnableGlobalAutoSize", GlobalAutoSize == after.GlobalAutoSize, GlobalAutoSize, after.GlobalAutoSize);
        Check("FlowerySizeManager.MainWindow", SizeWindow == after.SizeWindow, SizeWindow, after.SizeWindow);
        Check("FloweryScaleConfig", Scale == after.Scale, Scale, after.Scale);
        Check("SchedulerTimeFormatter.RangeFormatter", RangeFormatter == after.RangeFormatter, RangeFormatter, after.RangeFormatter);
        Check("StateStorageProvider", ReferenceEquals(Storage, after.Storage), Storage, after.Storage);
        Check("DaisyGlassLiveRenderer", ReferenceEquals(GlassRenderer, after.GlassRenderer), GlassRenderer, after.GlassRenderer);
        return changes;
    }

    public void Restore()
    {
        DaisyThemeManager.CustomThemeApplicator = ThemeApplicator;
        DaisyThemeManager.SuppressThemeApplication = SuppressThemes;
        DaisyThemeManager.NotifyForInternalThemesChanged = NotifyInternalThemes;
        // Dictionaries the test added go, and the theme manager applies the theme again, so it knows its palette.
        foreach (var dictionary in App.Resources.MergedDictionaries.Except(Dictionaries).ToList())
            App.Resources.MergedDictionaries.Remove(dictionary);
        App.Styles.Clear();
        App.Styles.AddRange(Styles);
        DaisyThemeManager.SetCurrentTheme(string.Empty);
        if (Theme is not null)
            DaisyThemeManager.ApplyTheme(Theme);
        App.RequestedThemeVariant = Variant;
        var added = ThemeRegistrations.Capture().Except(Registered, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var name in added)
            DaisyThemeManager.UnregisterTheme(name);
        DaisyThemeManager.ExcludedThemes = Excluded;
        DaisyThemeManager.PreferredThemes = Preferred;
        FloweryLocalization.CustomResolver = Resolver;
        FloweryLocalization.SetCulture(Localization);
        CultureInfo.CurrentCulture = Culture;
        CultureInfo.CurrentUICulture = UICulture;
        CultureInfo.DefaultThreadCurrentCulture = DefaultCulture;
        CultureInfo.DefaultThreadCurrentUICulture = DefaultUICulture;
        DaisyAccessibility.HighContrastOverride = HighContrast;
        DaisyAccessibility.ReducedMotionOverride = ReducedMotion;
        DaisyAccessibility.CustomContrastApplicator = ContrastApplicator;
        FlowerySizeManager.UseGlobalSizeByDefault = GlobalSizeByDefault;
        FlowerySizeManager.EnableGlobalAutoSize = GlobalAutoSize;
        FlowerySizeManager.MainWindow = SizeWindow;
        FlowerySizeManager.ApplySize(Size);
        (FloweryScaleConfig.ReferenceWidth, FloweryScaleConfig.ReferenceHeight,
            FloweryScaleConfig.MinScaleFactor, FloweryScaleConfig.MaxScaleFactor) = Scale;
        SchedulerTimeFormatter.RangeFormatter = RangeFormatter!;
        if (Storage is not null)
            StateStorageProvider.Configure(Storage);
        DaisyGlassLiveRenderer.Register(GlassRenderer);
    }
}
