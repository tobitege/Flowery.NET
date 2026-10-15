using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.VisualTree;
using Flowery.Enums;
using Flowery.Controls;

namespace Flowery.Helpers
{
    /// <summary>
    /// Attached properties for applying slide effects to any Control.
    /// Usage in XAML: &lt;Image helpers:FlowerySlideEffects.Effect="PanAndZoom" /&gt;
    /// </summary>
    public static class FlowerySlideEffects
    {
        private static readonly ConditionalWeakTable<Control, Control> _effectTargets = new();
        private static readonly ConditionalWeakTable<Control, RunRequest> _requests = new();
        private sealed class RunRequest { public bool Requested { get; set; } }

        #region Effect Attached Property

        public static readonly AttachedProperty<FlowerySlideEffect> EffectProperty =
            AvaloniaProperty.RegisterAttached<Control, FlowerySlideEffect>(
                "Effect",
                typeof(FlowerySlideEffects),
                FlowerySlideEffect.None);

        static FlowerySlideEffects()
        {
            EffectProperty.Changed.AddClassHandler<Control>(OnEffectChanged);
            AutoStartProperty.Changed.AddClassHandler<Control>(OnEffectChanged);
        }

        public static FlowerySlideEffect GetEffect(Control element)
        {
            return element.GetValue(EffectProperty);
        }

        public static void SetEffect(Control element, FlowerySlideEffect value)
        {
            element.SetValue(EffectProperty, value);
        }

        private static void OnEffectChanged(Control element, AvaloniaPropertyChangedEventArgs e)
        {
            StopCurrentEffect(element);
            var request = _requests.GetValue(element, static _ => new RunRequest());
            request.Requested = GetAutoStart(element) && GetEffect(element) != FlowerySlideEffect.None;
            DaisyAccessibility.RegisterMotionHandler(element, OnMotionChanged);
            element.AttachedToVisualTree -= OnElementAttached;
            element.DetachedFromVisualTree -= OnElementDetached;

            var effect = GetEffect(element);
            if (effect == FlowerySlideEffect.None)
            {
                return;
            }

            if (effect != FlowerySlideEffect.None)
            {
                element.AttachedToVisualTree += OnElementAttached;
                element.DetachedFromVisualTree += OnElementDetached;
                if (request.Requested && element.IsAttachedToVisualTree())
                {
                    StartEffect(element);
                }
            }
        }

        #endregion

        #region Duration Attached Property

        public static readonly AttachedProperty<double> DurationProperty =
            AvaloniaProperty.RegisterAttached<Control, double>(
                "Duration",
                typeof(FlowerySlideEffects),
                3.0);

        public static double GetDuration(Control element)
        {
            return element.GetValue(DurationProperty);
        }

        public static void SetDuration(Control element, double value)
        {
            element.SetValue(DurationProperty, value);
        }

        #endregion

        #region ZoomIntensity Attached Property

        public static readonly AttachedProperty<double> ZoomIntensityProperty =
            AvaloniaProperty.RegisterAttached<Control, double>(
                "ZoomIntensity",
                typeof(FlowerySlideEffects),
                0.15);

        public static double GetZoomIntensity(Control element)
        {
            return element.GetValue(ZoomIntensityProperty);
        }

        public static void SetZoomIntensity(Control element, double value)
        {
            element.SetValue(ZoomIntensityProperty, value);
        }

        #endregion

        #region PanDistance Attached Property

        public static readonly AttachedProperty<double> PanDistanceProperty =
            AvaloniaProperty.RegisterAttached<Control, double>(
                "PanDistance",
                typeof(FlowerySlideEffects),
                50.0);

        public static double GetPanDistance(Control element)
        {
            return element.GetValue(PanDistanceProperty);
        }

        public static void SetPanDistance(Control element, double value)
        {
            element.SetValue(PanDistanceProperty, value);
        }

        #endregion

        #region PulseIntensity Attached Property

        public static readonly AttachedProperty<double> PulseIntensityProperty =
            AvaloniaProperty.RegisterAttached<Control, double>(
                "PulseIntensity",
                typeof(FlowerySlideEffects),
                0.08);

        public static double GetPulseIntensity(Control element)
        {
            return element.GetValue(PulseIntensityProperty);
        }

        public static void SetPulseIntensity(Control element, double value)
        {
            element.SetValue(PulseIntensityProperty, value);
        }

        #endregion

        #region AutoStart Attached Property

        public static readonly AttachedProperty<bool> AutoStartProperty =
            AvaloniaProperty.RegisterAttached<Control, bool>(
                "AutoStart",
                typeof(FlowerySlideEffects),
                true);

        public static bool GetAutoStart(Control element)
        {
            return element.GetValue(AutoStartProperty);
        }

        public static void SetAutoStart(Control element, bool value)
        {
            element.SetValue(AutoStartProperty, value);
        }

        #endregion

        private static void OnElementAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control element)
            {
                if (_requests.TryGetValue(element, out var request) && request.Requested) StartCurrentEffect(element);
            }
        }

        private static void OnElementDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is Control element)
            {
                StopCurrentEffect(element);
            }
        }

        public static void StartEffect(Control element)
        {
            _requests.GetValue(element, static _ => new RunRequest()).Requested = true;
            DaisyAccessibility.RegisterMotionHandler(element, OnMotionChanged);
            element.AttachedToVisualTree -= OnElementAttached;
            element.AttachedToVisualTree += OnElementAttached;
            StartCurrentEffect(element);
        }

        private static void OnMotionChanged(Control element)
        {
            if (DaisyAccessibility.IsReducedMotion) StopCurrentEffect(element);
            else if (_requests.TryGetValue(element, out var request) && request.Requested) StartCurrentEffect(element);
        }

        private static void StartCurrentEffect(Control element)
        {
            StopCurrentEffect(element);
            if (DaisyAccessibility.IsReducedMotion || !element.IsAttachedToVisualTree()) return;
            element.DetachedFromVisualTree -= OnElementDetached;
            element.DetachedFromVisualTree += OnElementDetached;
            var effect = GetEffect(element);
            if (effect == FlowerySlideEffect.None) return;

            var target = ResolveEffectTarget(element);
            _effectTargets.Remove(element);
            _effectTargets.Add(element, target);

            var @params = new FlowerySlideEffectParams
            {
                ZoomIntensity = GetZoomIntensity(element),
                PanDistance = GetPanDistance(element),
                PulseIntensity = GetPulseIntensity(element)
            };

            FloweryAnimationHelpers.ApplySlideEffect(target, effect, TimeSpan.FromSeconds(GetDuration(element)), @params);
        }

        public static void StopEffect(Control element)
        {
            _requests.GetValue(element, static _ => new RunRequest()).Requested = false;
            StopCurrentEffect(element);
        }

        private static void StopCurrentEffect(Control element)
        {
            if (_effectTargets.TryGetValue(element, out var target))
            {
                FloweryAnimationHelpers.StopAnimations(target);
                FloweryAnimationHelpers.ResetTransform(target);
                _effectTargets.Remove(element);
            }
        }

        public static Control ResolveEffectTarget(Control element)
        {
            Control current = element;
            while (true)
            {
                var next = TryUnwrap(current);
                if (next == current) break;
                current = next;
            }
            return current;

            static Control TryUnwrap(Control el)
            {
                if (el is Border border) return border.Child as Control ?? el;
                if (el is ContentControl contentControl) return contentControl.Content as Control ?? el;
                if (el is ContentPresenter contentPresenter) return contentPresenter.Content as Control ?? el;
                if (el is Panel panel && panel.Children.Count == 1) return panel.Children[0] as Control ?? el;
                return el;
            }
        }
    }
}
