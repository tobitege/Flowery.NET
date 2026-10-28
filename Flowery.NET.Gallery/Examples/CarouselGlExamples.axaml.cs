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
        private static readonly Dictionary<CarouselGlEffectKind, GlTransitionDefaults> GlTransitionDefaultsByKind =
            new()
            {
                [CarouselGlEffectKind.MaskTransition] = new GlTransitionDefaults(SliceCount: 8m, Stagger: true, StaggerMs: 50m),
                [CarouselGlEffectKind.BlindsHorizontal] = new GlTransitionDefaults(SliceCount: 10m, Stagger: true, StaggerMs: 50m),
                [CarouselGlEffectKind.BlindsVertical] = new GlTransitionDefaults(SliceCount: 10m, Stagger: true, StaggerMs: 50m),
                [CarouselGlEffectKind.SlicesHorizontal] = new GlTransitionDefaults(SliceCount: 12m, Stagger: true, StaggerMs: 50m),
                [CarouselGlEffectKind.SlicesVertical] = new GlTransitionDefaults(SliceCount: 12m, Stagger: true, StaggerMs: 50m),
                [CarouselGlEffectKind.Checkerboard] = new GlTransitionDefaults(SliceCount: 8m, Stagger: true, StaggerMs: 50m),
                [CarouselGlEffectKind.Spiral] = new GlTransitionDefaults(SliceCount: 8m, Stagger: true, StaggerMs: 50m),
                [CarouselGlEffectKind.MatrixRain] = new GlTransitionDefaults(SliceCount: 16m, Stagger: true, StaggerMs: 50m),
                [CarouselGlEffectKind.Wormhole] = new GlTransitionDefaults(SliceCount: 8m, Stagger: true, StaggerMs: 50m),
                [CarouselGlEffectKind.Dissolve] = new GlTransitionDefaults(SliceCount: 12m, Stagger: true, StaggerMs: 50m, DissolveDensity: 0.5m),
                [CarouselGlEffectKind.Pixelate] = new GlTransitionDefaults(SliceCount: 10m, Stagger: true, StaggerMs: 50m, PixelateSize: 20m),
                [CarouselGlEffectKind.FlipPlane] = new GlTransitionDefaults(FlipAngle: 180m),
                [CarouselGlEffectKind.FlipHorizontal] = new GlTransitionDefaults(FlipAngle: 180m),
                [CarouselGlEffectKind.FlipVertical] = new GlTransitionDefaults(FlipAngle: 180m),
                [CarouselGlEffectKind.CubeLeft] = new GlTransitionDefaults(FlipAngle: 180m),
                [CarouselGlEffectKind.CubeRight] = new GlTransitionDefaults(FlipAngle: 180m)
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
            SetDebugDefines();

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
            if (GlIntervalBox != null)
            {
                GlIntervalBox.Value = 3m;
                GlIntervalBox.Minimum = 1m;
                GlIntervalBox.Maximum = 300m;
                GlIntervalBox.Increment = 1m;
                GlIntervalBox.ValueChanged += OnGlIntervalValueChanged;
            }

            SetGlNumericDefaults(GlSliceCountBox, 8m, 2m, 50m, 1m);
            SetGlNumericDefaults(GlStaggerMsBox, 50m, 10m, 200m, 1m);
            SetGlNumericDefaults(GlPixelateSizeBox, 20m, 4m, 100m, 1m);
            if (GlDissolveDensityBox != null)
            {
                GlDissolveDensityBox.Value = 0.5m;
                GlDissolveDensityBox.Minimum = 0.1m;
                GlDissolveDensityBox.Maximum = 1.0m;
                GlDissolveDensityBox.Increment = 0.05m;
            }

            if (GlFlipAngleBox != null)
            {
                GlFlipAngleBox.Value = 90m;
                GlFlipAngleBox.Minimum = 0m;
                GlFlipAngleBox.Maximum = 180m;
                GlFlipAngleBox.Increment = 5m;
            }

            if (GlSliceCountBox != null)
                GlSliceCountBox.ValueChanged += OnGlParamValueChanged;
            if (GlStaggerToggle != null)
                GlStaggerToggle.IsCheckedChanged += GlStaggerToggle_Changed;
            if (GlStaggerMsBox != null)
                GlStaggerMsBox.ValueChanged += OnGlParamValueChanged;
            if (GlPixelateSizeBox != null)
                GlPixelateSizeBox.ValueChanged += OnGlParamValueChanged;
            if (GlDissolveDensityBox != null)
                GlDissolveDensityBox.ValueChanged += OnGlParamValueChanged;
            if (GlFlipAngleBox != null)
                GlFlipAngleBox.ValueChanged += OnGlParamValueChanged;
        }

        private void OnGlIntervalValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            _ = sender;
            _ = e;

            if (_isApplyingSettings)
                return;

            var intervalBox = GlIntervalBox;
            if (intervalBox == null)
                return;

            var normalizedInterval = NormalizeIntervalValue(intervalBox.Value, fallback: 3m);
            if (intervalBox.Value != normalizedInterval)
            {
                _isApplyingSettings = true;
                try
                {
                    intervalBox.Value = normalizedInterval;
                }
                finally
                {
                    _isApplyingSettings = false;
                }

                return;
            }

            UpdateGlTimerInterval();
            UpdateGlPreviewInterval();
            PersistGlSettings();
        }

        private void OnGlParamValueChanged(object? sender, NumericUpDownValueChangedEventArgs e)
        {
            _ = sender;
            _ = e;

            if (_isApplyingSettings)
                return;

            UpdateGlPreviewParameters();
            PersistGlSettings();
        }

        private void GlStaggerToggle_Changed(object? sender, RoutedEventArgs e)
        {
            _ = sender;
            _ = e;

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
                var settings = GallerySettings.LoadCarouselGlSettings();
                if (settings != null)
                {
                    if (GlPreviewEnabledToggle != null)
                        GlPreviewEnabledToggle.IsChecked = settings.Enabled;

                    SelectByTag(GlModeSelect, settings.ModeTag);
                    SelectByTag(GlTransitionSelect, settings.TransitionTag);
                    SelectByTag(GlDurationSelect, settings.DurationTag);
                    SelectByTag(GlEffectSelect, settings.EffectTag);

                    if (GlIntervalBox != null)
                        GlIntervalBox.Value = settings.IntervalSeconds;
                    if (GlInfiniteToggle != null)
                        GlInfiniteToggle.IsChecked = settings.Infinite;
                    if (GlSliceCountBox != null)
                        GlSliceCountBox.Value = settings.SliceCount;
                    if (GlStaggerToggle != null)
                        GlStaggerToggle.IsChecked = settings.Stagger;
                    if (GlStaggerMsBox != null)
                        GlStaggerMsBox.Value = settings.StaggerMs;
                    if (GlPixelateSizeBox != null)
                        GlPixelateSizeBox.Value = settings.PixelateSize;
                    if (GlDissolveDensityBox != null)
                        GlDissolveDensityBox.Value = settings.DissolveDensity;
                    if (GlFlipAngleBox != null)
                        GlFlipAngleBox.Value = settings.FlipAngle;
                }

                NormalizeIntervalBox(GlIntervalBox, fallback: 3m);
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

            var intervalSeconds = NormalizeIntervalValue(GlIntervalBox?.Value, fallback: 3m);
            var settings = new CarouselGlSettings(
                Enabled: GlPreviewEnabledToggle?.IsChecked ?? true,
                ModeTag: GetSelectedTag(GlModeSelect),
                TransitionTag: GetSelectedTag(GlTransitionSelect),
                DurationTag: GetSelectedTag(GlDurationSelect),
                EffectTag: GetSelectedTag(GlEffectSelect),
                IntervalSeconds: intervalSeconds,
                Infinite: GlInfiniteToggle?.IsChecked == true,
                SliceCount: GlSliceCountBox?.Value ?? 8m,
                Stagger: GlStaggerToggle?.IsChecked == true,
                StaggerMs: GlStaggerMsBox?.Value ?? 50m,
                PixelateSize: GlPixelateSizeBox?.Value ?? 20m,
                DissolveDensity: GlDissolveDensityBox?.Value ?? 0.5m,
                FlipAngle: GlFlipAngleBox?.Value ?? 90m);

            GallerySettings.SaveCarouselGlSettings(settings);
        }

        private static void SetGlNumericDefaults(DaisyNumericUpDown? box, decimal value, decimal min, decimal max, decimal increment)
        {
            if (box == null)
                return;

            box.Value = value;
            box.Minimum = min;
            box.Maximum = max;
            box.Increment = increment;
        }

        private static decimal NormalizeIntervalValue(decimal? value, decimal fallback)
        {
            if (!value.HasValue)
            {
                return fallback;
            }

            var interval = value.Value;
            return interval < 1m ? fallback : interval;
        }

        private static void NormalizeIntervalBox(DaisyNumericUpDown? box, decimal fallback)
        {
            if (box == null)
                return;

            var normalized = NormalizeIntervalValue(box.Value, fallback);
            if (box.Value != normalized)
            {
                box.Value = normalized;
            }
        }

        private void NormalizeGlParamBoxes()
        {
            if (_isApplyingSettings)
                return;

            _isApplyingSettings = true;
            try
            {
                var transition = GetSelectedTransition();
                if (!TryGetTransitionDefaults(transition, out var defaults))
                {
                    return;
                }

                if (defaults.SliceCount.HasValue)
                    NormalizeGlNumericBox(GlSliceCountBox, defaults.SliceCount.Value);

                if (defaults.Stagger.HasValue
                    && GlStaggerToggle is { } staggerToggle
                    && staggerToggle.IsChecked is null)
                {
                    staggerToggle.IsChecked = defaults.Stagger.Value;
                }

                if (defaults.StaggerMs.HasValue)
                    NormalizeGlNumericBox(GlStaggerMsBox, defaults.StaggerMs.Value);

                if (defaults.PixelateSize.HasValue)
                    NormalizeGlNumericBox(GlPixelateSizeBox, defaults.PixelateSize.Value);

                if (defaults.DissolveDensity.HasValue)
                    NormalizeGlNumericBox(GlDissolveDensityBox, defaults.DissolveDensity.Value);

                if (defaults.FlipAngle.HasValue)
                    NormalizeGlNumericBox(GlFlipAngleBox, defaults.FlipAngle.Value);
            }
            finally
            {
                _isApplyingSettings = false;
            }
        }

        private static bool TryGetTransitionDefaults(CarouselGlEffectKind kind, out GlTransitionDefaults defaults)
        {
            return GlTransitionDefaultsByKind.TryGetValue(kind, out defaults);
        }

        private readonly record struct GlTransitionDefaults(
            decimal? SliceCount = null,
            bool? Stagger = null,
            decimal? StaggerMs = null,
            decimal? PixelateSize = null,
            decimal? DissolveDensity = null,
            decimal? FlipAngle = null);

        private static void NormalizeGlNumericBox(DaisyNumericUpDown? box, decimal fallback)
        {
            if (box == null)
                return;

            var value = box.Value ?? fallback;
            var min = box.Minimum;
            var max = box.Maximum;
            var normalized = value < min || value > max
                ? Math.Clamp(fallback, min, max)
                : value;

            if (normalized != value)
            {
                box.Value = normalized;
            }
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
            if (GlPreviewHost == null)
                return;

            if (GlPreviewEnabledToggle?.IsChecked != true)
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

        private FlowerySlideshowMode GetSelectedMode()
        {
            if (GlModeSelect?.SelectedItem is ComboBoxItem item)
            {
                return FlowerySlideshowModeParser.Parse(item.Tag?.ToString());
            }

            return FlowerySlideshowMode.Manual;
        }

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
            var interval = (double)(GlIntervalBox?.Value ?? 3m);
            if (interval < 1)
                interval = 1;

            var transitionSeconds = GetSelectedTransitionSeconds();
            return interval + Math.Max(transitionSeconds, 0.01d);
        }

        private static CarouselGlEffectKind GetSelectedKind(DaisySelect? select)
        {
            if (select?.SelectedItem is not ComboBoxItem item)
                return CarouselGlEffectKind.None;

            return TryParseKind(item.Tag?.ToString());
        }

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
            if (GlEffectSelect == null)
                return effects;

            foreach (var item in GlEffectSelect.Items.OfType<ComboBoxItem>())
            {
                effects.Add(TryParseKind(item.Tag?.ToString()));
            }

            return effects;
        }

        private List<CarouselGlEffectKind> GetTransitionPool()
        {
            var transitions = new List<CarouselGlEffectKind>();
            if (GlTransitionSelect == null)
                return transitions;

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
            if (GlTransitionSelect == null)
                return;

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
            if (GlPreviewHost == null)
                return;

            if (effect == CarouselGlEffectKind.TextFill)
            {
                GlPreviewHost.Effect = effect;
                GlPreviewHost.OverlayEffect = CarouselGlEffectKind.None;
            }
            else if (transition != CarouselGlEffectKind.None)
            {
                GlPreviewHost.Effect = transition;
                GlPreviewHost.OverlayEffect = IsOverlayEffectKind(effect) ? effect : CarouselGlEffectKind.None;
            }
            else
            {
                GlPreviewHost.Effect = effect;
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

        private static string? GetSelectedTag(DaisySelect? select)
        {
            return select?.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() : null;
        }

        private static void SelectByTag(DaisySelect? select, string? tag)
        {
            if (select == null || string.IsNullOrWhiteSpace(tag))
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

        private static void SelectByTag(DaisySelect? select, CarouselGlEffectKind kind)
        {
            if (select == null)
                return;

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

            if (GlPreviewEnabledToggle?.IsChecked != true)
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
                    if (GlInfiniteToggle?.IsChecked != true && _glCycleIndex >= cycleSet.Count - 1)
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
                    if (GlInfiniteToggle?.IsChecked != true && _glCycleIndex >= cycleSet.Count - 1)
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
                    if (GlInfiniteToggle?.IsChecked == true)
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
            if (GlPreviewHost == null)
                return;

            if (GlPreviewEnabledToggle?.IsChecked != true)
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
            if (GlPreviewHost == null)
                return;

            GlPreviewHost.IntervalSeconds = 0;
        }

        private void UpdateGlPreviewTransitionSeconds()
        {
            if (GlPreviewHost == null)
                return;

            GlPreviewHost.TransitionSeconds = GetSelectedTransitionSeconds();
        }

        private void UpdateGlPreviewParameters()
        {
            if (GlPreviewHost == null)
                return;

            GlPreviewHost.TransitionSliceCount = (int)(GlSliceCountBox?.Value ?? 8m);
            GlPreviewHost.TransitionStaggerSlices = GlStaggerToggle?.IsChecked == true;
            GlPreviewHost.TransitionSliceStaggerMs = (double)(GlStaggerMsBox?.Value ?? 50m);
            GlPreviewHost.TransitionPixelateSize = (int)(GlPixelateSizeBox?.Value ?? 20m);
            GlPreviewHost.TransitionDissolveDensity = (double)(GlDissolveDensityBox?.Value ?? 0.5m);
            GlPreviewHost.TransitionFlipAngle = (double)(GlFlipAngleBox?.Value ?? 90m);
        }

        private void SetDebugDefines()
        {
            if (DebugDefinesText == null)
            {
                return;
            }

            DebugDefinesText.Text = BuildDefinesText();
        }

        private static string BuildDefinesText() => "Avalonia / Skia SkSL renderer";
    }
}
