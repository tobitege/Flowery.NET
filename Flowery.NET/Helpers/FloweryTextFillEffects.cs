using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Flowery.Controls;

namespace Flowery.Helpers
{
    /// <summary>
    /// Attached properties to paint text with an ImageBrush and optionally animate the brush transform.
    /// Usage in XAML: helpers:FloweryTextFillEffects.ImageSource="avares://Flowery.NET/Assets/hero.jpg"
    /// </summary>
    public static class FloweryTextFillEffects
    {
        private static readonly ConditionalWeakTable<TextBlock, ImageBrush> _brushes = new();
        private static readonly ConditionalWeakTable<TextBlock, IBrush> _originalForegrounds = new();
        private static readonly ConditionalWeakTable<TextBlock, PanAnimation> _animations = new();
        private static readonly AttachedProperty<bool> StoppedProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, bool>("Stopped", typeof(FloweryTextFillEffects));
        private static readonly AttachedProperty<bool> ManualRequestProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, bool>("ManualRequest", typeof(FloweryTextFillEffects));

        #region ImageSource Attached Property

        public static readonly AttachedProperty<IImage?> ImageSourceProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, IImage?>(
                "ImageSource",
                typeof(FloweryTextFillEffects),
                null);

        static FloweryTextFillEffects()
        {
            ImageSourceProperty.Changed.AddClassHandler<TextBlock>(OnImageSourceChanged);
            AnimateProperty.Changed.AddClassHandler<TextBlock>(OnConfigurationChanged);
            AutoStartProperty.Changed.AddClassHandler<TextBlock>(OnConfigurationChanged);
            DurationProperty.Changed.AddClassHandler<TextBlock>(OnConfigurationChanged);
            PanXProperty.Changed.AddClassHandler<TextBlock>(OnConfigurationChanged);
        }

        private static void OnConfigurationChanged(TextBlock target, AvaloniaPropertyChangedEventArgs e)
        {
            target.SetValue(StoppedProperty, false);
            if (e.Property == AnimateProperty || e.Property == AutoStartProperty) target.SetValue(ManualRequestProperty, false);
            StartIfConfigured(target);
        }

        public static IImage? GetImageSource(TextBlock element)
        {
            return element.GetValue(ImageSourceProperty);
        }

        public static void SetImageSource(TextBlock element, IImage? value)
        {
            element.SetValue(ImageSourceProperty, value);
        }

        private static void OnImageSourceChanged(TextBlock textBlock, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is not IImage source)
            {
                StopEffect(textBlock);
                RestoreOriginalForeground(textBlock);
                return;
            }

            textBlock.SetValue(StoppedProperty, false);
            if (ApplyImageBrush(textBlock, source))
            {
                StartIfConfigured(textBlock);
            }
            else
            {
                StopEffect(textBlock);
            }
        }

        #endregion

        #region Animate Attached Property

        public static readonly AttachedProperty<bool> AnimateProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, bool>(
                "Animate",
                typeof(FloweryTextFillEffects),
                false);

        public static bool GetAnimate(TextBlock element)
        {
            return element.GetValue(AnimateProperty);
        }

        public static void SetAnimate(TextBlock element, bool value)
        {
            element.SetValue(AnimateProperty, value);
        }

        #endregion

        #region Duration Attached Property

        public static readonly AttachedProperty<double> DurationProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, double>(
                "Duration",
                typeof(FloweryTextFillEffects),
                6.0);

        public static double GetDuration(TextBlock element)
        {
            return element.GetValue(DurationProperty);
        }

        public static void SetDuration(TextBlock element, double value)
        {
            element.SetValue(DurationProperty, Math.Max(0.2, value));
        }

        #endregion

        #region PanX Attached Property

        public static readonly AttachedProperty<double> PanXProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, double>(
                "PanX",
                typeof(FloweryTextFillEffects),
                0.2);

        public static double GetPanX(TextBlock element)
        {
            return element.GetValue(PanXProperty);
        }

        public static void SetPanX(TextBlock element, double value)
        {
            element.SetValue(PanXProperty, value);
        }

        #endregion

        #region AutoStart Attached Property

        public static readonly AttachedProperty<bool> AutoStartProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, bool>(
                "AutoStart",
                typeof(FloweryTextFillEffects),
                true);

        public static bool GetAutoStart(TextBlock element)
        {
            return element.GetValue(AutoStartProperty);
        }

        public static void SetAutoStart(TextBlock element, bool value)
        {
            element.SetValue(AutoStartProperty, value);
        }

        #endregion

        private static void OnMotionChanged(Control control) => StartIfConfigured((TextBlock)control);

        private static void StartIfConfigured(TextBlock textBlock)
        {
            DaisyAccessibility.RegisterMotionHandler(textBlock, OnMotionChanged);
            textBlock.AttachedToVisualTree -= OnElementAttached;
            textBlock.DetachedFromVisualTree -= OnElementDetached;
            if (GetImageSource(textBlock) == null)
            {
                StopCurrentEffect(textBlock);
                return;
            }

            if ((!GetAnimate(textBlock) || !GetAutoStart(textBlock)) && !textBlock.GetValue(ManualRequestProperty)
                || textBlock.GetValue(StoppedProperty) || DaisyAccessibility.IsReducedMotion)
            {
                StopCurrentEffect(textBlock);
                textBlock.AttachedToVisualTree += OnElementAttached;
                textBlock.DetachedFromVisualTree += OnElementDetached;
                return;
            }

            textBlock.AttachedToVisualTree += OnElementAttached;
            textBlock.DetachedFromVisualTree += OnElementDetached;

            if (textBlock.IsAttachedToVisualTree())
            {
                StartCurrentEffect(textBlock);
            }
        }

        private static void OnElementAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                StartIfConfigured(textBlock);
            }
        }

        private static void OnElementDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                StopCurrentEffect(textBlock);
            }
        }

        private static bool ApplyImageBrush(TextBlock textBlock, IImage source)
        {
            EnsureOriginalForeground(textBlock);

            var brush = GetOrCreateBrush(textBlock);

            try
            {
                // Assign using SetValue to bypass potential interface type mismatches in different build targets (IImage vs IImageBrushSource)
                brush.SetValue(ImageBrush.SourceProperty, source);
                textBlock.SetCurrentValue(TextBlock.ForegroundProperty, brush);
                return true;
            }
            catch
            {
                RestoreOriginalForeground(textBlock);
                return false;
            }
        }

        private static ImageBrush GetOrCreateBrush(TextBlock textBlock)
        {
            if (_brushes.TryGetValue(textBlock, out var brush))
            {
                return brush;
            }

            brush = new ImageBrush
            {
                Stretch = Stretch.UniformToFill,
                AlignmentX = AlignmentX.Center,
                AlignmentY = AlignmentY.Center
            };

            _brushes.Add(textBlock, brush);
            return brush;
        }

        private static void EnsureOriginalForeground(TextBlock textBlock)
        {
            if (_originalForegrounds.TryGetValue(textBlock, out _))
            {
                return;
            }

            if (textBlock.Foreground != null)
            {
                _originalForegrounds.Add(textBlock, textBlock.Foreground);
            }
        }

        private static void RestoreOriginalForeground(TextBlock textBlock)
        {
            if (_originalForegrounds.TryGetValue(textBlock, out var original))
            {
                textBlock.SetCurrentValue(TextBlock.ForegroundProperty, original);
                _originalForegrounds.Remove(textBlock);
            }
        }

        public static void StartEffect(TextBlock textBlock)
        {
            textBlock.SetValue(StoppedProperty, false);
            textBlock.SetValue(ManualRequestProperty, true);
            DaisyAccessibility.RegisterMotionHandler(textBlock, OnMotionChanged);
            textBlock.AttachedToVisualTree -= OnElementAttached;
            textBlock.AttachedToVisualTree += OnElementAttached;
            StartCurrentEffect(textBlock);
        }

        private static void StartCurrentEffect(TextBlock textBlock)
        {
            StopCurrentEffect(textBlock);
            if (DaisyAccessibility.IsReducedMotion) return;
            var source = GetImageSource(textBlock);
            if (source == null || !ApplyImageBrush(textBlock, source) || !textBlock.IsAttachedToVisualTree()) return;
            textBlock.DetachedFromVisualTree -= OnElementDetached;
            textBlock.DetachedFromVisualTree += OnElementDetached;
            var animation = new PanAnimation(textBlock, GetOrCreateBrush(textBlock));
            _animations.Add(textBlock, animation);
            animation.Start();
        }

        public static void StopEffect(TextBlock textBlock)
        {
            textBlock.SetValue(StoppedProperty, true);
            textBlock.SetValue(ManualRequestProperty, false);
            StopCurrentEffect(textBlock);
        }

        private static void StopCurrentEffect(TextBlock textBlock)
        {
            if (!_animations.TryGetValue(textBlock, out var animation)) return;
            animation.Stop();
            _animations.Remove(textBlock);
        }

        private sealed class PanAnimation
        {
            private readonly TextBlock _target;
            private readonly ImageBrush _brush;
            private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMilliseconds(16) };
            private readonly Stopwatch _clock = new();
            private readonly TranslateTransform _translation = new();
            private readonly ITransform? _originalTransform;

            public PanAnimation(TextBlock target, ImageBrush brush)
            {
                _target = target;
                _brush = brush;
                _originalTransform = brush.Transform;
                var transforms = new TransformGroup();
                if (_originalTransform != null) transforms.Children.Add(new MatrixTransform(_originalTransform.Value));
                transforms.Children.Add(_translation);
                brush.Transform = transforms;
                _timer.Tick += OnTick;
            }

            public void Start() { _clock.Start(); _timer.Start(); }

            private void OnTick(object? sender, EventArgs e)
            {
                var duration = GetDuration(_target);
                if (!double.IsFinite(duration) || duration <= 0) duration = .2;
                var phase = _clock.Elapsed.TotalSeconds / duration * Math.PI * 2;
                var pan = GetPanX(_target);
                _translation.X = double.IsFinite(pan) ? Math.Sin(phase) * pan * _target.Bounds.Width : 0;
            }

            public void Stop()
            {
                _timer.Stop();
                _timer.Tick -= OnTick;
                _clock.Stop();
                _brush.Transform = _originalTransform;
            }
        }

        public static void TryStartEffect(Control element)
        {
            if (ResolveTextTarget(element) is TextBlock textBlock)
            {
                StartIfConfigured(textBlock);
            }
        }

        public static void TryStopEffect(Control element)
        {
            if (ResolveTextTarget(element) is TextBlock textBlock)
            {
                StopEffect(textBlock);
            }
        }

        private static TextBlock? ResolveTextTarget(Control element)
        {
            Control current = element;
            while (true)
            {
                if (current is TextBlock tb) return tb;
                var next = TryUnwrap(current);
                if (next == current) break;
                current = next;
            }
            return null;

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
