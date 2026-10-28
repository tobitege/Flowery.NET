using System;
using System.Globalization;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;
using Flowery.Enums;
using Flowery.Helpers;

namespace Flowery.NET.Gallery.Examples
{
    public sealed partial class CarouselGlTransitionsExamples : UserControl, IScrollableExample
    {
        private const double TransitionDurationScale = 1.25d;
        private const double NormalTierThresholdSeconds = 0.4d;
        private const double NormalTierDurationScale = 2.0d;
        private const double CircleTransitionScale = 2.0d;
        private static readonly string[] TransitionNames =
        [
            "angular",
            "BookFlip",
            "Bounce",
            "BowTieHorizontal",
            "BowTieVertical",
            "BowTieWithParameter",
            "burn",
            "ButterflyWaveScrawler",
            "cannabisleaf",
            "circle",
            "CircleCrop",
            "circleopen",
            "colorphase",
            "ColourDistance",
            "coord-from-in",
            "CrazyParametricFun",
            "crosshatch",
            "crosswarp",
            "CrossZoom",
            "cube",
            "Directional",
            "directional-easing",
            "DirectionalScaled",
            "directionalwarp",
            "directionalwipe",
            "displacement",
            "dissolve",
            "DoomScreenTransition",
            "doorway",
            "Dreamy",
            "DreamyZoom",
            "EdgeTransition",
            "fade",
            "fadecolor",
            "fadegrayscale",
            "FilmBurn",
            "flyeye",
            "GlitchDisplace",
            "GlitchMemories",
            "GridFlip",
            "heart",
            "hexagonalize",
            "HorizontalClose",
            "HorizontalOpen",
            "InvertedPageCurl",
            "kaleidoscope",
            "LeftRight",
            "LinearBlur",
            "luma",
            "luminance_melt",
            "morph",
            "Mosaic",
            "mosaic_transition",
            "multiply_blend",
            "Overexposure",
            "perlin",
            "pinwheel",
            "pixelize",
            "polar_function",
            "PolkaDotsCurtain",
            "powerKaleido",
            "Radial",
            "randomNoisex",
            "randomsquares",
            "Rectangle",
            "RectangleCrop",
            "ripple",
            "Rolls",
            "rotate_scale_fade",
            "RotateScaleVanish",
            "rotateTransition",
            "scale-in",
            "SimpleZoom",
            "SimpleZoomOut",
            "Slides",
            "squareswire",
            "squeeze",
            "static_wipe",
            "StaticFade",
            "StereoViewer",
            "swap",
            "Swirl",
            "tangentMotionBlur",
            "TopBottom",
            "TVStatic",
            "undulatingBurnOut",
            "VerticalClose",
            "VerticalOpen",
            "WaterDrop",
            "wind",
            "windowblinds",
            "windowslice",
            "wipeDown",
            "wipeLeft",
            "wipeRight",
            "wipeUp",
            "x_axis_translation",
            "ZoomInCircles",
            "ZoomLeftWipe",
            "ZoomRigthWipe"
        ];

        private readonly Random _random = new();
        private DispatcherTimer? _glTimer;
        private int _glCycleIndex;
        private bool _isUpdatingSelection;
        private bool _isApplyingSettings;
        private bool _hasInitializedTransitions;
        private bool _hasInitializedControls;
        private FlowerySlideshowMode _glMode = FlowerySlideshowMode.Manual;

        public CarouselGlTransitionsExamples()
        {
            // Loading the XAML applies the initial selections and toggle state and raises their events before the named
            // fields are set; the page applies its settings when it loads.
            _isApplyingSettings = true;
            try
            {
                InitializeComponent();
            }
            finally
            {
                _isApplyingSettings = false;
            }

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

        // RefreshLocalizationBindings has no counterpart: {loc:Localize} updates the texts in place, so the selections
        // stay and need no repair after a language change.
        private void OnLoaded(object? sender, RoutedEventArgs e)
        {
            InitializeGlControls();
            EnsureTransitionItems();
            EnsureGlDefaults();
            ApplyGlSettings();
        }

        private void OnUnloaded(object? sender, RoutedEventArgs e)
        {
            StopGlTimer();
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
        }

        private void InitializeGlControls()
        {
            // The values and handlers are set once; WinUI registered the callback again on every load.
            if (_hasInitializedControls)
                return;

            _hasInitializedControls = true;
            if (GlIntervalBox != null)
            {
                GlIntervalBox.Value = 1m;
                GlIntervalBox.Minimum = 1m;
                GlIntervalBox.Maximum = 300m;
                GlIntervalBox.Increment = 0.5m;
                GlIntervalBox.ValueChanged += OnGlIntervalValueChanged;
            }
        }

        private void EnsureTransitionItems()
        {
            if (_hasInitializedTransitions)
                return;

            if (GlTransitionSelect == null)
                return;

            _hasInitializedTransitions = true;
            GlTransitionSelect.Items.Clear();
            foreach (var name in TransitionNames)
            {
                GlTransitionSelect.Items.Add(new ComboBoxItem
                {
                    Content = name,
                    Tag = name
                });
            }
        }

        private void EnsureGlDefaults()
        {
            EnsureGlSelection(GlModeSelect, defaultIndex: 1);
            EnsureGlSelection(GlTransitionSelect, defaultIndex: 0);
            EnsureGlSelection(GlDurationSelect, defaultIndex: 2);
        }

        private static void EnsureGlSelection(DaisySelect? select, int defaultIndex)
        {
            if (select == null || select.Items.Count == 0)
                return;

            var targetIndex = select.SelectedIndex >= 0
                ? select.SelectedIndex
                : Math.Min(defaultIndex, select.Items.Count - 1);

            if (targetIndex < 0)
                return;

            select.SelectedIndex = -1;
            select.SelectedIndex = targetIndex;
            select.SelectedItem = select.Items[targetIndex];
        }

        private void GlPreviewEnabledToggle_Changed(object? sender, RoutedEventArgs e)
        {
            if (_isApplyingSettings)
                return;

            ApplyGlSettings();
        }

        private void GlModeSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingSelection || _isApplyingSettings)
                return;

            ApplyGlSettings();
        }

        private void GlTransitionSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingSelection || _isApplyingSettings)
                return;

            ApplyGlSettings();
        }

        private void GlDurationSelect_SelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            if (_isUpdatingSelection || _isApplyingSettings)
                return;

            ApplyGlSettings();
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
        }

        private void ApplyGlSettings()
        {
            if (GlPreviewHost == null)
                return;

            if (GlPreviewEnabledToggle?.IsChecked != true)
            {
                StopGlTimer();
                GlPreviewHost.TransitionName = string.Empty;
                GlPreviewHost.IntervalSeconds = 0;
                return;
            }

            var mode = GetSelectedMode();
            _glMode = mode;

            UpdateGlPreviewInterval();
            UpdateGlPreviewTransitionSeconds();

            if (mode == FlowerySlideshowMode.Manual)
            {
                StopGlTimer();
                SetGlPreview(GetSelectedTransitionName(), updateSelection: false);
                return;
            }

            EnsureGlTimer();
            UpdateGlTimerInterval();
            SetGlPreview(GetSelectedTransitionName(), updateSelection: false);
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

        private string GetSelectedTransitionName()
        {
            if (GlTransitionSelect?.SelectedItem is ComboBoxItem item)
            {
                return item.Tag?.ToString() ?? string.Empty;
            }

            return string.Empty;
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

        private double GetScaledTransitionSeconds()
        {
            var seconds = GetSelectedTransitionSeconds();
            if (seconds >= NormalTierThresholdSeconds)
            {
                return seconds * NormalTierDurationScale;
            }

            return seconds;
        }

        private static double GetTransitionNameScale(string? transitionName)
        {
            if (string.IsNullOrWhiteSpace(transitionName))
            {
                return 1.0d;
            }

            return string.Equals(transitionName, "circle", StringComparison.OrdinalIgnoreCase)
                ? CircleTransitionScale
                : 1.0d;
        }

        private static string? GetSelectedTag(DaisySelect? select)
        {
            return select?.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() : null;
        }

        private void SetGlPreview(string? transitionName, bool updateSelection)
        {
            if (GlPreviewHost == null)
                return;

            GlPreviewHost.TransitionName = transitionName ?? string.Empty;

            if (!updateSelection || string.IsNullOrWhiteSpace(transitionName))
                return;

            _isUpdatingSelection = true;
            try
            {
                SelectByTag(GlTransitionSelect, transitionName);
            }
            finally
            {
                _isUpdatingSelection = false;
            }
        }

        private static void SelectByTag(DaisySelect? select, string tag)
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

        private void EnsureGlTimer()
        {
            if (_glTimer != null)
                return;

            _glTimer = new DispatcherTimer();
            _glTimer.Tick += OnGlTimerTick;
        }

        private void StartGlTimer()
        {
            if (_glTimer == null || _glTimer.IsEnabled)
                return;

            if (TransitionNames.Length == 0)
                return;

            var selectedName = GetSelectedTransitionName();
            _glCycleIndex = GetCycleIndex(selectedName, TransitionNames);
            var next = _glMode == FlowerySlideshowMode.Random
                ? TransitionNames[_random.Next(TransitionNames.Length)]
                : TransitionNames[_glCycleIndex];

            SetGlPreview(next, updateSelection: true);
            UpdateGlTimerInterval();
            _glTimer.Start();
        }

        private void StopGlTimer()
        {
            _glTimer?.Stop();
        }

        private static int GetCycleIndex(string? name, string[] cycleSet)
        {
            if (!string.IsNullOrWhiteSpace(name))
            {
                for (int i = 0; i < cycleSet.Length; i++)
                {
                    if (string.Equals(cycleSet[i], name, StringComparison.OrdinalIgnoreCase))
                        return i;
                }
            }

            return 0;
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

            if (TransitionNames.Length == 0)
            {
                timer.Stop();
                return;
            }

            string next;
            if (_glMode == FlowerySlideshowMode.Slideshow)
            {
                _glCycleIndex = (_glCycleIndex + 1) % TransitionNames.Length;
                next = TransitionNames[_glCycleIndex];
            }
            else if (_glMode == FlowerySlideshowMode.Random)
            {
                next = TransitionNames[_random.Next(TransitionNames.Length)];
            }
            else
            {
                timer.Stop();
                return;
            }

            SetGlPreview(next, updateSelection: true);
            UpdateGlTimerInterval();
        }

        private void UpdateGlTimerInterval()
        {
            if (_glTimer == null)
                return;

            var holdSeconds = (double)(GlIntervalBox?.Value ?? 1m);
            if (holdSeconds < 1)
                holdSeconds = 1;

            var transitionSeconds = GetScaledTransitionSeconds()
                * TransitionDurationScale
                * GetTransitionNameScale(GetSelectedTransitionName());
            var cycleSeconds = Math.Max(holdSeconds, (holdSeconds * 2) + transitionSeconds);
            _glTimer.Interval = TimeSpan.FromSeconds(cycleSeconds);
        }

        private void UpdateGlPreviewInterval()
        {
            if (GlPreviewHost == null)
                return;

            var seconds = (double)(GlIntervalBox?.Value ?? 1m);
            if (seconds < 1)
                seconds = 1;

            GlPreviewHost.IntervalSeconds = seconds;
        }

        private void UpdateGlPreviewTransitionSeconds()
        {
            if (GlPreviewHost == null)
                return;

            GlPreviewHost.TransitionSeconds = GetScaledTransitionSeconds();
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
    }
}
