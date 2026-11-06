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
using Flowery.NET.Gallery.Effects;

namespace Flowery.NET.Gallery.Examples
{
    public sealed partial class CarouselGlTransitionsExamples : UserControl, IScrollableExample
    {
        private const decimal DefaultIntervalSeconds = 1m;
        private const double NormalTierThresholdSeconds = 0.4d;
        private const double NormalTierDurationScale = 2.0d;

        // The catalog of the engine, in its order.
        private static readonly string[] TransitionNames =
            Array.ConvertAll(FloweryCarouselGLTransitions.GlTransitions, static entry => entry.Name);

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
            GlIntervalBox.Value = DefaultIntervalSeconds;
            GlIntervalBox.Minimum = 1m;
            GlIntervalBox.Maximum = 300m;
            GlIntervalBox.Increment = 0.5m;
            GlIntervalBox.ValueChanged += OnGlIntervalValueChanged;
        }

        private void EnsureTransitionItems()
        {
            if (_hasInitializedTransitions)
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

        // The page applies the selections right after, so the defaults apply nothing themselves.
        private void EnsureGlDefaults()
        {
            _isApplyingSettings = true;
            try
            {
                EnsureGlSelection(GlModeSelect, defaultIndex: 1);
                EnsureGlSelection(GlTransitionSelect, defaultIndex: 0);
                EnsureGlSelection(GlDurationSelect, defaultIndex: 2);
            }
            finally
            {
                _isApplyingSettings = false;
            }
        }

        private static void EnsureGlSelection(DaisySelect select, int defaultIndex)
        {
            if (select.SelectedIndex < 0 && select.Items.Count > 0)
                select.SelectedIndex = Math.Min(defaultIndex, select.Items.Count - 1);
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
            if (_isApplyingSettings)
                return;

            // An empty box shows the interval the timer and the preview use from now on.
            var normalizedInterval = NormalizeIntervalValue(GlIntervalBox.Value);
            if (GlIntervalBox.Value != normalizedInterval)
            {
                _isApplyingSettings = true;
                try
                {
                    GlIntervalBox.Value = normalizedInterval;
                }
                finally
                {
                    _isApplyingSettings = false;
                }
            }

            UpdateGlTimerInterval();
            UpdateGlPreviewInterval();
        }

        private void ApplyGlSettings()
        {
            if (GlPreviewEnabledToggle.IsChecked != true)
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

        private FlowerySlideshowMode GetSelectedMode() => FlowerySlideshowModeParser.Parse(GetSelectedTag(GlModeSelect));

        private string GetSelectedTransitionName() => GetSelectedTag(GlTransitionSelect) ?? string.Empty;

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

        private static string? GetSelectedTag(DaisySelect select)
        {
            return select.SelectedItem is ComboBoxItem item ? item.Tag?.ToString() : null;
        }

        private void SetGlPreview(string? transitionName, bool updateSelection)
        {
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

        private static void SelectByTag(DaisySelect select, string tag)
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

            if (GlPreviewEnabledToggle.IsChecked != true)
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

            var holdSeconds = (double)NormalizeIntervalValue(GlIntervalBox.Value);
            // The duration the engine draws: the page's own scale, and the engine's for the shader.
            var transitionSeconds = GetScaledTransitionSeconds()
                * FloweryCarouselGLTransitions.GetTransitionDurationScale(GetSelectedTransitionName());
            var cycleSeconds = Math.Max(holdSeconds, (holdSeconds * 2) + transitionSeconds);
            _glTimer.Interval = TimeSpan.FromSeconds(cycleSeconds);
        }

        private void UpdateGlPreviewInterval()
        {
            GlPreviewHost.IntervalSeconds = (double)NormalizeIntervalValue(GlIntervalBox.Value);
        }

        private void UpdateGlPreviewTransitionSeconds()
        {
            GlPreviewHost.TransitionSeconds = GetScaledTransitionSeconds();
        }

        private static decimal NormalizeIntervalValue(decimal? value) =>
            value is { } interval && interval >= 1m ? interval : DefaultIntervalSeconds;
    }
}
