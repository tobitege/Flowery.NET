using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Effects;
using Flowery.Enums;
using Flowery.Helpers;
using Flowery.NET.Gallery.Effects;

namespace Flowery.NET.Gallery.Examples
{
    public sealed partial class CarouselGlExamples : UserControl, IScrollableExample
    {
        // The values of a fresh page, which an empty box falls back to. A flip turns half around by default: 180 is the
        // default of the engine, and at 90 the flip plane stays edge-on while it holds.
        private const decimal DefaultIntervalSeconds = 3m;
        private const decimal DefaultSliceCount = 8m;
        private const decimal DefaultStaggerMs = 50m;
        private const decimal DefaultPixelateSize = 20m;
        private const decimal DefaultDissolveDensity = 0.5m;
        private const decimal DefaultFlipAngle = 180m;

        // The masks with more slices than the default, as their variants in the engine.
        private static readonly Dictionary<CarouselGlEffectKind, decimal> SliceCountByKind = new()
        {
            [CarouselGlEffectKind.BlindsHorizontal] = 10m,
            [CarouselGlEffectKind.BlindsVertical] = 10m,
            [CarouselGlEffectKind.SlicesHorizontal] = 12m,
            [CarouselGlEffectKind.SlicesVertical] = 12m,
            [CarouselGlEffectKind.MatrixRain] = 16m,
            [CarouselGlEffectKind.Dissolve] = 12m,
            [CarouselGlEffectKind.Pixelate] = 10m
        };

        private readonly Random _random = new();
        private DispatcherTimer? _glTimer;
        private int _glCycleIndex;
        private bool _isUpdatingGlSelection;
        private bool _isApplyingSettings;
        private bool _hasRestoredSettings;
        private bool _hasInitializedControls;
        private FlowerySlideshowMode _glMode = FlowerySlideshowMode.Manual;
        private CarouselGlEffectKind _lastRandomEffect = CarouselGlEffectKind.None;
        private CarouselGlEffectKind _lastRandomTransition = CarouselGlEffectKind.None;

        public CarouselGlExamples()
        {
            CarouselGlGalleryPresets.EnsureRegistered();
            // Loading the XAML applies the initial selections and toggle states and raises their events before the named
            // fields are set; nothing is applied or saved until the page restores its settings.
            _isApplyingSettings = true;
            try
            {
                InitializeComponent();
            }
            finally
            {
                _isApplyingSettings = false;
            }
            DebugDefinesText.Text = "Avalonia / Skia SkSL renderer";

            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public void ScrollToSection(string sectionName)
        {
            var header = this.GetVisualDescendants().OfType<SectionHeader>()
                .FirstOrDefault(item => string.Equals(item.SectionId, sectionName, StringComparison.OrdinalIgnoreCase));
            if (header?.TransformToVisual(MainScrollViewer) is { } transform)
                MainScrollViewer.Offset = new Vector(0, transform.Transform(new Point(0, 0)).Y + MainScrollViewer.Offset.Y);
        }

        // RefreshLocalizationBindings and EnsureGlDefaults have no counterpart: {loc:Localize} updates the texts in place,
        // so the selections stay and need no repair after a language change.
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            var reloaded = _hasRestoredSettings;
            InitializeGlControls();
            RestoreGlSettings();
            // A page that loads again keeps its values and starts the timer that Unloaded stopped. WinUI's
            // InitializeGlControls reset the values to their defaults on every load and saved them over the settings.
            if (reloaded)
                ApplyGlSettings();
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            StopGlTimer();
            PersistGlSettings();
        }

        // The Narrow and Wide visual states of WinUI (AdaptiveTrigger MinWindowWidth 720).
        protected override void OnSizeChanged(SizeChangedEventArgs e)
        {
            base.OnSizeChanged(e);
            ApplyResponsiveState();
        }

        private void ApplyResponsiveState()
        {
            var windowWidth = TopLevel.GetTopLevel(this)?.ClientSize.Width ?? Bounds.Width;
            var wide = windowWidth >= 720;
            ContentStack.Margin = wide ? new Thickness(24, 0, 24, 32) : new Thickness(16, 0, 16, 24);
            GlPreviewBorder.Height = wide ? 220 : 180;
            GlMaskPreviewBorder.Height = wide ? 220 : 180;
            GlFlipPreviewBorder.Height = wide ? 220 : 180;
            GlTextPreviewBorder.Height = wide ? 200 : 160;
        }

        private void InitializeGlControls()
        {
            // The values and handlers are set once; WinUI registered the callbacks again on every load.
            if (_hasInitializedControls)
                return;

            _hasInitializedControls = true;
            SetGlNumericDefaults(GlIntervalBox, DefaultIntervalSeconds, 1m, 300m, 1m);
            GlIntervalBox.ValueChanged += OnGlIntervalValueChanged;

            SetGlNumericDefaults(GlSliceCountBox, DefaultSliceCount, 2m, 50m, 1m);
            SetGlNumericDefaults(GlStaggerMsBox, DefaultStaggerMs, 10m, 200m, 1m);
            SetGlNumericDefaults(GlPixelateSizeBox, DefaultPixelateSize, 4m, 100m, 1m);
            SetGlNumericDefaults(GlDissolveDensityBox, DefaultDissolveDensity, 0.1m, 1.0m, 0.05m);
            SetGlNumericDefaults(GlFlipAngleBox, DefaultFlipAngle, 0m, 180m, 5m);

            GlSliceCountBox.ValueChanged += OnGlParamValueChanged;
            GlStaggerToggle.IsCheckedChanged += GlStaggerToggle_Changed;
            GlStaggerMsBox.ValueChanged += OnGlParamValueChanged;
            GlPixelateSizeBox.ValueChanged += OnGlParamValueChanged;
            GlDissolveDensityBox.ValueChanged += OnGlParamValueChanged;
            GlFlipAngleBox.ValueChanged += OnGlParamValueChanged;
        }

        private void OnGlIntervalValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (_isApplyingSettings)
                return;

            // An empty box shows the interval the timer and the preview use from now on.
            _isApplyingSettings = true;
            try
            {
                NormalizeIntervalBox(GlIntervalBox);
            }
            finally
            {
                _isApplyingSettings = false;
            }

            UpdateGlTimerInterval();
            UpdateGlPreviewInterval();
            PersistGlSettings();
        }

        private void OnGlParamValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            if (_isApplyingSettings)
                return;

            NormalizeGlParamBoxes();
            UpdateGlPreviewParameters();
            PersistGlSettings();
        }

        private void GlStaggerToggle_Changed(object? sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
                return;

            UpdateGlPreviewParameters();
            PersistGlSettings();
        }

        private void RestoreGlSettings()
        {
            if (_hasRestoredSettings)
                return;

            _hasRestoredSettings = true;
            _isApplyingSettings = true;
            try
            {
                if (GallerySettings.LoadCarouselGlSettings() is { } settings)
                {
                    GlPreviewEnabledToggle.IsChecked = settings.Enabled;

                    SelectByTag(GlModeSelect, settings.ModeTag);
                    SelectByTag(GlTransitionSelect, settings.TransitionTag);
                    SelectByTag(GlDurationSelect, settings.DurationTag);
                    SelectByTag(GlEffectSelect, settings.EffectTag);

                    GlIntervalBox.Value = settings.IntervalSeconds;
                    GlInfiniteToggle.IsChecked = settings.Infinite;
                    GlSliceCountBox.Value = settings.SliceCount;
                    GlStaggerToggle.IsChecked = settings.Stagger;
                    GlStaggerMsBox.Value = settings.StaggerMs;
                    GlPixelateSizeBox.Value = settings.PixelateSize;
                    GlDissolveDensityBox.Value = settings.DissolveDensity;
                    GlFlipAngleBox.Value = settings.FlipAngle;
                }

                NormalizeIntervalBox(GlIntervalBox);
            }
            finally
            {
                _isApplyingSettings = false;
            }

            ApplyGlSettings();
        }

        private void PersistGlSettings()
        {
            if (_isApplyingSettings)
                return;

            var settings = new CarouselGlSettings(
                Enabled: GlPreviewEnabledToggle.IsChecked ?? true,
                ModeTag: GetSelectedTag(GlModeSelect),
                TransitionTag: GetSelectedTag(GlTransitionSelect),
                DurationTag: GetSelectedTag(GlDurationSelect),
                EffectTag: GetSelectedTag(GlEffectSelect),
                IntervalSeconds: NormalizeIntervalValue(GlIntervalBox.Value),
                Infinite: GlInfiniteToggle.IsChecked == true,
                SliceCount: GlSliceCountBox.Value ?? DefaultSliceCount,
                Stagger: GlStaggerToggle.IsChecked == true,
                StaggerMs: GlStaggerMsBox.Value ?? DefaultStaggerMs,
                PixelateSize: GlPixelateSizeBox.Value ?? DefaultPixelateSize,
                DissolveDensity: GlDissolveDensityBox.Value ?? DefaultDissolveDensity,
                FlipAngle: GlFlipAngleBox.Value ?? DefaultFlipAngle);

            GallerySettings.SaveCarouselGlSettings(settings);
        }

        private static void SetGlNumericDefaults(DaisyNumericUpDown box, decimal value, decimal min, decimal max, decimal increment)
        {
            box.Value = value;
            box.Minimum = min;
            box.Maximum = max;
            box.Increment = increment;
        }

        private static decimal NormalizeIntervalValue(decimal? value) =>
            value is { } interval && interval >= 1m ? interval : DefaultIntervalSeconds;

        private static void NormalizeIntervalBox(DaisyNumericUpDown box)
        {
            var normalized = NormalizeIntervalValue(box.Value);
            if (box.Value != normalized)
                box.Value = normalized;
        }

        // Fills an empty or out-of-range box with the default, the slice count with the one of the selected mask.
        private void NormalizeGlParamBoxes()
        {
            if (_isApplyingSettings)
                return;

            _isApplyingSettings = true;
            try
            {
                NormalizeGlNumericBox(GlSliceCountBox,
                    SliceCountByKind.TryGetValue(GetSelectedTransition(), out var sliceCount) ? sliceCount : DefaultSliceCount);
                NormalizeGlNumericBox(GlStaggerMsBox, DefaultStaggerMs);
                NormalizeGlNumericBox(GlPixelateSizeBox, DefaultPixelateSize);
                NormalizeGlNumericBox(GlDissolveDensityBox, DefaultDissolveDensity);
                NormalizeGlNumericBox(GlFlipAngleBox, DefaultFlipAngle);
            }
            finally
            {
                _isApplyingSettings = false;
            }
        }

        private static void NormalizeGlNumericBox(DaisyNumericUpDown box, decimal fallback)
        {
            if (box.Value is not { } value || value < box.Minimum || value > box.Maximum)
                box.Value = Math.Clamp(fallback, box.Minimum, box.Maximum);
        }

        private void GlPreviewEnabledToggle_Changed(object? sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
                return;

            ApplyGlSettings();
            PersistGlSettings();
        }

        private void GlModeSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingGlSelection || _isApplyingSettings)
                return;

            ApplyGlSettings();
            PersistGlSettings();
        }

        private void GlTransitionSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingGlSelection || _isApplyingSettings)
                return;

            ApplyGlSettings();
            PersistGlSettings();
        }

        private void GlDurationSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingGlSelection || _isApplyingSettings)
                return;

            ApplyGlSettings();
            PersistGlSettings();
        }

        private void GlEffectSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingGlSelection || _isApplyingSettings)
                return;

            ApplyGlSettings();
            PersistGlSettings();
        }

        private void GlInfiniteToggle_Changed(object? sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
                return;

            ApplyGlSettings();
            PersistGlSettings();
        }

        private void ApplyGlSettings()
        {
            if (GlPreviewEnabledToggle.IsChecked != true)
            {
                StopGlTimer();
                SetGlPreview(CarouselGlEffectKind.None, CarouselGlEffectKind.None, updateEffectSelection: false);
                UpdateGlPreviewInterval();
                return;
            }

            var mode = GetSelectedMode();
            _glMode = mode;
            UpdateGlPreviewInterval();
            UpdateGlPreviewTransitionSeconds();
            NormalizeGlParamBoxes();
            UpdateGlPreviewParameters();
            var transition = GetSelectedTransition();
            var effect = GetSelectedEffectKind();

            SetGlPreview(transition, effect, updateEffectSelection: false);

            if (mode == FlowerySlideshowMode.Manual)
            {
                StopGlTimer();
                return;
            }

            EnsureGlTimer();
            UpdateGlTimerInterval();
            ResetGlCycleState();
            StartGlTimer();
        }

        private FlowerySlideshowMode GetSelectedMode() => FlowerySlideshowModeParser.Parse(GetSelectedTag(GlModeSelect));

        private CarouselGlEffectKind GetSelectedTransition()
        {
            return GetSelectedKind(GlTransitionSelect);
        }

        private CarouselGlEffectKind GetSelectedEffectKind()
        {
            return GetSelectedKind(GlEffectSelect);
        }

        private double GetSelectedTransitionSeconds()
        {
            var tag = GetSelectedTag(GlDurationSelect);
            if (string.IsNullOrWhiteSpace(tag))
            {
                return 0.4d;
            }

            return double.TryParse(tag, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)
                ? Math.Max(seconds, 0.01d)
                : 0.4d;
        }

        private double GetGlCycleSeconds()
        {
            var interval = (double)NormalizeIntervalValue(GlIntervalBox.Value);
            var transitionSeconds = GetSelectedTransitionSeconds();
            return interval + Math.Max(transitionSeconds, 0.01d);
        }

        private static CarouselGlEffectKind GetSelectedKind(DaisySelect select) => TryParseKind(GetSelectedTag(select));

        private static CarouselGlEffectKind TryParseKind(string? tag)
        {
            if (!string.IsNullOrWhiteSpace(tag)
                && Enum.TryParse(tag, ignoreCase: true, out CarouselGlEffectKind kind))
            {
                return kind;
            }

            return CarouselGlEffectKind.None;
        }

        private List<CarouselGlEffectKind> GetEffectCycle()
        {
            var effects = new List<CarouselGlEffectKind>();
            foreach (var item in GlEffectSelect.Items.OfType<ComboBoxItem>())
            {
                effects.Add(TryParseKind(item.Tag?.ToString()));
            }

            return effects;
        }

        private List<CarouselGlEffectKind> GetTransitionPool()
        {
            var transitions = new List<CarouselGlEffectKind>();
            foreach (var item in GlTransitionSelect.Items.OfType<ComboBoxItem>())
            {
                var kind = TryParseKind(item.Tag?.ToString());
                if (kind == CarouselGlEffectKind.Random)
                    continue;

                transitions.Add(kind);
            }

            return transitions;
        }

        private void ResetGlCycleState()
        {
            var effects = GetEffectCycle();
            if (_glMode == FlowerySlideshowMode.Random)
            {
                _glCycleIndex = 0;
            }
            else
            {
                var current = GetSelectedEffectKind();
                _glCycleIndex = effects.IndexOf(current);
                if (_glCycleIndex < 0)
                    _glCycleIndex = 0;
            }

            _lastRandomEffect = GetSelectedEffectKind();
            _lastRandomTransition = GetSelectedTransition();
        }

        private CarouselGlEffectKind PickRandomEffect(bool avoidRepeat)
        {
            var effects = GetEffectCycle();
            if (effects.Count == 0)
                return CarouselGlEffectKind.None;

            if (!avoidRepeat || effects.Count == 1)
            {
                return effects[_random.Next(effects.Count)];
            }

            CarouselGlEffectKind next;
            do
            {
                next = effects[_random.Next(effects.Count)];
            } while (next == _lastRandomEffect);

            _lastRandomEffect = next;
            return next;
        }

        private CarouselGlEffectKind PickRandomTransition(bool avoidRepeat)
        {
            var transitions = GetTransitionPool();
            if (transitions.Count == 0)
                return CarouselGlEffectKind.None;

            if (!avoidRepeat || transitions.Count == 1)
            {
                return transitions[_random.Next(transitions.Count)];
            }

            CarouselGlEffectKind next;
            do
            {
                next = transitions[_random.Next(transitions.Count)];
            } while (next == _lastRandomTransition);

            _lastRandomTransition = next;
            return next;
        }

        private void UpdateGlTransitionSelection(CarouselGlEffectKind transition)
        {
            _isUpdatingGlSelection = true;
            try
            {
                SelectByTag(GlTransitionSelect, transition);
            }
            finally
            {
                _isUpdatingGlSelection = false;
            }
        }

        private void SetGlPreview(CarouselGlEffectKind transition, CarouselGlEffectKind effect, bool updateEffectSelection)
        {
            if (effect == CarouselGlEffectKind.TextFill)
            {
                GlPreviewHost.EffectKind = effect;
                GlPreviewHost.OverlayEffect = CarouselGlEffectKind.None;
            }
            else if (transition != CarouselGlEffectKind.None)
            {
                GlPreviewHost.EffectKind = transition;
                GlPreviewHost.OverlayEffect = IsOverlayEffectKind(effect) ? effect : CarouselGlEffectKind.None;
            }
            else
            {
                GlPreviewHost.EffectKind = effect;
                GlPreviewHost.OverlayEffect = CarouselGlEffectKind.None;
            }

            if (!updateEffectSelection)
                return;

            _isUpdatingGlSelection = true;
            try
            {
                SelectByTag(GlEffectSelect, effect);
            }
            finally
            {
                _isUpdatingGlSelection = false;
            }
        }

        private static bool IsEffectKind(CarouselGlEffectKind kind)
        {
            return kind is CarouselGlEffectKind.TextFill
                or CarouselGlEffectKind.EffectPanAndZoom
                or CarouselGlEffectKind.EffectZoomIn
                or CarouselGlEffectKind.EffectZoomOut
                or CarouselGlEffectKind.EffectPanLeft
                or CarouselGlEffectKind.EffectPanRight
                or CarouselGlEffectKind.EffectPanUp
                or CarouselGlEffectKind.EffectPanDown
                or CarouselGlEffectKind.EffectDrift
                or CarouselGlEffectKind.EffectPulse
                or CarouselGlEffectKind.EffectBreath
                or CarouselGlEffectKind.EffectThrow;
        }

        private static bool IsOverlayEffectKind(CarouselGlEffectKind kind)
        {
            return IsEffectKind(kind) && kind != CarouselGlEffectKind.TextFill;
        }

        private static string? GetSelectedTag(DaisySelect select)
        {
            return select.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() : null;
        }

        private static void SelectByTag(DaisySelect select, string? tag)
        {
            if (string.IsNullOrWhiteSpace(tag))
                return;

            for (int i = 0; i < select.Items.Count; i++)
            {
                if (select.Items[i] is ComboBoxItem item
                    && string.Equals(item.Tag?.ToString(), tag, StringComparison.OrdinalIgnoreCase))
                {
                    if (select.SelectedIndex != i)
                    {
                        select.SelectedIndex = i;
                    }
                    return;
                }
            }
        }

        private static void SelectByTag(DaisySelect select, CarouselGlEffectKind kind)
        {
            string target = kind.ToString();
            for (int i = 0; i < select.Items.Count; i++)
            {
                if (select.Items[i] is ComboBoxItem item && string.Equals(item.Tag?.ToString(), target, StringComparison.Ordinal))
                {
                    if (select.SelectedIndex != i)
                    {
                        select.SelectedIndex = i;
                    }
                    return;
                }
            }
        }

        private void EnsureGlTimer()
        {
            if (_glTimer != null)
                return;

            _glTimer = new DispatcherTimer();
            _glTimer.Tick += OnGlTimerTick;
        }

        private void StartGlTimer()
        {
            if (_glTimer == null)
                return;

            if (!_glTimer.IsEnabled)
            {
                if (GetEffectCycle().Count == 0)
                {
                    return;
                }

                UpdateGlTimerInterval();
                _glTimer.Start();
            }
        }

        private void StopGlTimer()
        {
            _glTimer?.Stop();
        }

        private void OnGlTimerTick(object? sender, EventArgs args)
        {
            if (sender is not DispatcherTimer timer)
                return;

            if (GlPreviewEnabledToggle.IsChecked != true)
            {
                timer.Stop();
                return;
            }

            if (_glMode == FlowerySlideshowMode.Manual)
            {
                timer.Stop();
                return;
            }

            var cycleSet = GetEffectCycle();
            if (cycleSet.Count == 0)
            {
                timer.Stop();
                return;
            }

            UpdateGlTimerInterval();

            bool pauseAfterUpdate = false;
            CarouselGlEffectKind nextEffect;
            CarouselGlEffectKind transition;
            switch (_glMode)
            {
                case FlowerySlideshowMode.Slideshow:
                    if (GlInfiniteToggle.IsChecked != true && _glCycleIndex >= cycleSet.Count - 1)
                    {
                        PauseGlPreviewInterval();
                        timer.Stop();
                        return;
                    }

                    _glCycleIndex = (_glCycleIndex + 1) % cycleSet.Count;
                    nextEffect = cycleSet[_glCycleIndex];
                    transition = GetSelectedTransition();
                    break;
                case FlowerySlideshowMode.Kiosk:
                    if (GlInfiniteToggle.IsChecked != true && _glCycleIndex >= cycleSet.Count - 1)
                    {
                        PauseGlPreviewInterval();
                        timer.Stop();
                        return;
                    }

                    _glCycleIndex = (_glCycleIndex + 1) % cycleSet.Count;
                    nextEffect = cycleSet[_glCycleIndex];
                    transition = PickRandomTransition(avoidRepeat: false);
                    UpdateGlTransitionSelection(transition);
                    break;
                case FlowerySlideshowMode.Random:
                    nextEffect = PickRandomEffect(avoidRepeat: true);
                    transition = PickRandomTransition(avoidRepeat: true);
                    UpdateGlTransitionSelection(transition);
                    _glCycleIndex++;
                    if (GlInfiniteToggle.IsChecked == true)
                    {
                        if (_glCycleIndex >= cycleSet.Count)
                        {
                            _glCycleIndex = 0;
                        }
                    }
                    else if (_glCycleIndex >= cycleSet.Count)
                    {
                        pauseAfterUpdate = true;
                        timer.Stop();
                    }
                    break;
                default:
                    timer.Stop();
                    return;
            }

            SetGlPreview(transition, nextEffect, updateEffectSelection: true);
            if (pauseAfterUpdate)
            {
                PauseGlPreviewInterval();
            }
        }

        private void UpdateGlTimerInterval()
        {
            if (_glTimer == null)
                return;

            var seconds = GetGlCycleSeconds();
            if (seconds < 1)
                seconds = 1;

            _glTimer.Interval = TimeSpan.FromSeconds(seconds);
        }

        private void UpdateGlPreviewInterval()
        {
            if (GlPreviewEnabledToggle.IsChecked != true)
            {
                GlPreviewHost.IntervalSeconds = 0;
                return;
            }

            if (_glMode == FlowerySlideshowMode.Manual)
            {
                GlPreviewHost.IntervalSeconds = 0;
                return;
            }

            var seconds = GetGlCycleSeconds();
            if (seconds < 1)
                seconds = 1;

            GlPreviewHost.IntervalSeconds = seconds;
        }

        private void PauseGlPreviewInterval()
        {
            GlPreviewHost.IntervalSeconds = 0;
        }

        private void UpdateGlPreviewTransitionSeconds()
        {
            GlPreviewHost.TransitionSeconds = GetSelectedTransitionSeconds();
        }

        private void UpdateGlPreviewParameters()
        {
            GlPreviewHost.TransitionSliceCount = (int)(GlSliceCountBox.Value ?? DefaultSliceCount);
            GlPreviewHost.TransitionStaggerSlices = GlStaggerToggle.IsChecked == true;
            GlPreviewHost.TransitionSliceStaggerMs = (double)(GlStaggerMsBox.Value ?? DefaultStaggerMs);
            GlPreviewHost.TransitionPixelateSize = (int)(GlPixelateSizeBox.Value ?? DefaultPixelateSize);
            GlPreviewHost.TransitionDissolveDensity = (double)(GlDissolveDensityBox.Value ?? DefaultDissolveDensity);
            GlPreviewHost.TransitionFlipAngle = (double)(GlFlipAngleBox.Value ?? DefaultFlipAngle);
        }
    }
}
