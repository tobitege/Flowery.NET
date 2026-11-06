using System.Globalization;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Styling;
using Flowery.Controls;
using Flowery.Controls.Scheduler.Utilities;
using Flowery.Localization;
using Flowery.NET.Kanban.Controls;
using Flowery.Services;
using Xunit.v3;

[assembly: Flowery.NET.Tests.GlobalStateGuard]

namespace Flowery.NET.Tests;

/// <summary>
/// The tests share one application. A test that leaves global state changed would break whichever test runs after
/// it, depending on the order, so this fails the test that left it changed and puts the state back.
/// The test class is constructed before <see cref="Before"/> and disposed after <see cref="After"/>, so what its
/// constructor changes cannot be told from a leak. After each test the state goes back to the one at startup, so that
/// it cannot escape into the next test either.
/// </summary>
[AttributeUsage(AttributeTargets.Assembly)]
internal sealed class GlobalStateGuardAttribute : BeforeAfterTestAttribute
{
    // Tests run one at a time on the UI thread, so one snapshot at a time is enough.
    private GlobalState? _before;

    public override void Before(MethodInfo methodUnderTest, IXunitTest test)
    {
        // Controls save their settings, such as the size of a board, to the shared storage and load them when they are
        // created, so each test starts without the settings an earlier one saved.
        if (StateStorageProvider.Instance is InMemoryStateStorage storage)
            storage.Clear();
        _before = GlobalState.Capture();
    }

    public override void After(MethodInfo methodUnderTest, IXunitTest test)
    {
        if (_before is not { } before)
            return;
        _before = null;
        var after = GlobalState.Capture();
        var changes = before.ChangesTo(after);
        var startup = GlobalState.Startup ?? before;
        if (startup.ChangesTo(after).Count > 0)
            startup.Restore();
        if (changes.Count > 0)
            throw new InvalidOperationException("The test left global state changed: " + string.Join("; ", changes));
    }
}

internal sealed class GlobalState
{
    /// <summary>The state when the application was set up, before any test class was constructed.</summary>
    public static GlobalState? Startup { get; private set; }

    public static void CaptureStartup() => Startup = Capture();

    private Application App { get; } = Application.Current!;
    private string? Theme { get; init; }
    private string[] Excluded { get; init; } = [];
    private string[] Preferred { get; init; } = [];
    private Dictionary<string, (DaisyThemeInfo Info, Func<ResourceDictionary> Palette)> Registered { get; init; } = [];
    private IReadOnlyDictionary<string, DaisyAddedTheme> Added { get; init; } = new Dictionary<string, DaisyAddedTheme>();
    private bool SuppressThemes { get; init; }
    private bool NotifyInternalThemes { get; init; }
    private Func<string, bool>? ThemeApplicator { get; init; }
    private ThemeVariant? Variant { get; init; }
    private (object? Base, object? Primary) Palette { get; init; }
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
    private bool GlassMode { get; init; }
    private GlassBlurMode GlassModeBlur { get; init; }
    private string? BackdropOverride { get; init; }
    private Avalonia.Media.Color? BaseOverride { get; init; }
    private Func<IActivatableLifetime?>? KanbanLifetime { get; init; }

    public static GlobalState Capture() => new()
    {
        Theme = DaisyThemeManager.CurrentThemeName,
        Excluded = [.. DaisyThemeManager.ExcludedThemes.Order(StringComparer.OrdinalIgnoreCase)],
        Preferred = [.. DaisyThemeManager.PreferredThemes.Order(StringComparer.OrdinalIgnoreCase)],
        // The excluded themes too, with the palette each one was registered with.
        Registered = ThemeRegistrations.Capture().ToDictionary(static name => name,
            static name => (DaisyThemeManager.GetThemeInfo(name)!, DaisyThemeManager.GetPaletteFactory(name)!), StringComparer.OrdinalIgnoreCase),
        Added = DaisyThemeManager.AddedThemes,
        SuppressThemes = DaisyThemeManager.SuppressThemeApplication,
        NotifyInternalThemes = DaisyThemeManager.NotifyForInternalThemesChanged,
        ThemeApplicator = DaisyThemeManager.CustomThemeApplicator,
        Variant = Application.Current!.RequestedThemeVariant,
        // Another palette can be left behind under the same theme name, so two of its colors count as well.
        Palette = (TestControls.Resource("DaisyBase100Color"), TestControls.Resource("DaisyPrimaryColor")),
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
        GlassRenderer = DaisyGlassLiveRenderer.Current,
        GlassMode = DaisyGlassSurface.GlobalEnabled,
        GlassModeBlur = DaisyGlassSurface.GlobalBlurMode,
        BackdropOverride = DaisyThemeManager.BackdropOverride,
        BaseOverride = DaisyThemeManager.BaseOverride,
        KanbanLifetime = FlowKanban.ActivatableLifetimeSource
    };

    private static bool SameRegistration((DaisyThemeInfo Info, Func<ResourceDictionary> Palette) before,
        (DaisyThemeInfo Info, Func<ResourceDictionary> Palette) after) =>
        before.Info.Name == after.Info.Name && before.Info.IsDark == after.Info.IsDark && before.Palette == after.Palette;

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
        var removed = Registered.Keys.Except(after.Registered.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        var registered = after.Registered.Keys.Except(Registered.Keys, StringComparer.OrdinalIgnoreCase).ToList();
        Check("registered themes", removed.Count == 0 && registered.Count == 0, string.Join(",", removed), string.Join(",", registered));
        // A theme registered again under the same name can bring another palette.
        var replaced = Registered.Where(pair => after.Registered.TryGetValue(pair.Key, out var now) && !SameRegistration(pair.Value, now))
            .Select(static pair => pair.Key).ToList();
        Check("theme registrations", replaced.Count == 0, null, "replaced " + string.Join(",", replaced));
        Check("added themes", Added.Count == after.Added.Count && Added.All(pair => after.Added.TryGetValue(pair.Key, out var entry) && entry == pair.Value),
            string.Join(",", Added.Values.Select(static entry => entry.Name)), string.Join(",", after.Added.Values.Select(static entry => entry.Name)));
        Check("SuppressThemeApplication", SuppressThemes == after.SuppressThemes, SuppressThemes, after.SuppressThemes);
        Check("NotifyForInternalThemesChanged", NotifyInternalThemes == after.NotifyInternalThemes, NotifyInternalThemes, after.NotifyInternalThemes);
        Check("CustomThemeApplicator", ThemeApplicator == after.ThemeApplicator, ThemeApplicator, after.ThemeApplicator);
        Check("RequestedThemeVariant", Equals(Variant, after.Variant), Variant, after.Variant);
        // A theme applied again is a new instance of the same palette, so the dictionaries compare by count.
        Check("palette colors", Equals(Palette, after.Palette), Palette, after.Palette);
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
        Check("DaisyGlassSurface.GlobalEnabled", GlassMode == after.GlassMode, GlassMode, after.GlassMode);
        Check("DaisyGlassSurface.GlobalBlurMode", GlassModeBlur == after.GlassModeBlur, GlassModeBlur, after.GlassModeBlur);
        Check("DaisyThemeManager.BackdropOverride", BackdropOverride == after.BackdropOverride, BackdropOverride, after.BackdropOverride);
        Check("DaisyThemeManager.BaseOverride", BaseOverride == after.BaseOverride, BaseOverride, after.BaseOverride);
        Check("FlowKanban.ActivatableLifetimeSource", KanbanLifetime == after.KanbanLifetime, KanbanLifetime, after.KanbanLifetime);
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
        // Before the theme is applied again, which brings the styles and the tint of the glass mode in line with it.
        DaisyGlassSurface.GlobalBlurMode = GlassModeBlur;
        DaisyGlassSurface.GlobalEnabled = GlassMode;
        DaisyThemeManager.BackdropOverride = BackdropOverride;
        DaisyThemeManager.BaseOverride = BaseOverride;
        // Themes the test unregistered or registered again come back first, so the theme is applied with its palette.
        foreach (var (name, (info, palette)) in Registered)
        {
            if (DaisyThemeManager.GetThemeInfo(name) != info || DaisyThemeManager.GetPaletteFactory(name) != palette)
                DaisyThemeManager.RegisterTheme(info, palette);
        }
        DaisyThemeManager.SetCurrentTheme(string.Empty);
        if (Theme is not null)
            DaisyThemeManager.ApplyTheme(Theme);
        App.RequestedThemeVariant = Variant;
        ThemeRegistrations.RemoveAddedSince([.. Registered.Keys]);
        foreach (var name in DaisyThemeManager.AddedThemes.Keys.Except(Added.Keys, StringComparer.OrdinalIgnoreCase).ToList())
            DaisyThemeManager.RemoveAddedTheme(name);
        foreach (var entry in Added.Values)
        {
            // A theme the test unregistered can no longer be added; the rest of the state is still put back.
            try { DaisyThemeManager.AddTheme(entry.Name, entry.DisplayName, entry.Preferred); }
            catch (ArgumentException) { }
        }
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
        FlowKanban.ActivatableLifetimeSource = KanbanLifetime!;
    }
}
