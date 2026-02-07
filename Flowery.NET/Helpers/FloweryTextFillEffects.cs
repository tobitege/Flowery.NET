using System;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Media;
using Avalonia.VisualTree;

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

        #region ImageSource Attached Property

        public static readonly AttachedProperty<IImage?> ImageSourceProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, IImage?>(
                "ImageSource",
                typeof(FloweryTextFillEffects),
                null);

        static FloweryTextFillEffects()
        {
            ImageSourceProperty.Changed.AddClassHandler<TextBlock>(OnImageSourceChanged);
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

        private static void StartIfConfigured(TextBlock textBlock)
        {
            if (GetImageSource(textBlock) == null)
            {
                StopEffect(textBlock);
                return;
            }

            if (!GetAnimate(textBlock) || !GetAutoStart(textBlock))
            {
                StopEffect(textBlock);
                return;
            }

            if (textBlock.IsAttachedToVisualTree())
            {
                StartEffect(textBlock);
            }
            else
            {
                textBlock.AttachedToVisualTree += OnElementAttached;
            }
        }

        private static void OnElementAttached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                textBlock.AttachedToVisualTree -= OnElementAttached;
                StartEffect(textBlock);
                textBlock.DetachedFromVisualTree += OnElementDetached;
            }
        }

        private static void OnElementDetached(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                textBlock.DetachedFromVisualTree -= OnElementDetached;
                StopEffect(textBlock);
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
                textBlock.Foreground = brush;
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
                textBlock.Foreground = original;
                _originalForegrounds.Remove(textBlock);
            }
        }

        public static void StartEffect(TextBlock textBlock)
        {
            // Animation of brushes is complex in Avalonia via attached properties without a custom shader or behavior.
            // For now, we just set the static brush. Full pan animation would require a custom control or shader.
            var source = GetImageSource(textBlock);
            if (source != null)
            {
                ApplyImageBrush(textBlock, source);
            }
        }

        public static void StopEffect(TextBlock textBlock)
        {
            // Cleanup logic if we had active animations
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
