using System;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Effects
{
    /// <summary>
    /// Creates a typewriter animation effect on TextBlock controls.
    /// </summary>
    public static class TypewriterBehavior
    {
        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, bool>("IsEnabled", typeof(TypewriterBehavior), false);

        public static readonly AttachedProperty<TimeSpan> SpeedProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, TimeSpan>("Speed", typeof(TypewriterBehavior), TimeSpan.FromMilliseconds(50));

        private static readonly AttachedProperty<CancellationTokenSource?> CtsProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, CancellationTokenSource?>("Cts", typeof(TypewriterBehavior), null);

        private static readonly AttachedProperty<string?> FullTextProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, string?>("FullText", typeof(TypewriterBehavior), null);

        private static readonly AttachedProperty<bool> IsAttachedProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, bool>("IsAttached", typeof(TypewriterBehavior), false);

        public static bool GetIsEnabled(TextBlock element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(TextBlock element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static TimeSpan GetSpeed(TextBlock element) => element.GetValue(SpeedProperty);
        public static void SetSpeed(TextBlock element, TimeSpan value) => element.SetValue(SpeedProperty, value);

        static TypewriterBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<TextBlock>(OnIsEnabledChanged);
        }

        private static void OnIsEnabledChanged(TextBlock element, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                if (!element.GetValue(IsAttachedProperty))
                {
                    element.SetValue(IsAttachedProperty, true);
                    element.AttachedToVisualTree += OnAttachedToVisualTree;
                    element.DetachedFromVisualTree += OnDetachedFromVisualTree;
                }

                if (TopLevel.GetTopLevel(element) != null)
                {
                    ScheduleTypewriter(element);
                }
            }
            else
            {
                StopTypewriter(element);
            }
        }

        private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBlock tb && GetIsEnabled(tb))
                ScheduleTypewriter(tb);
        }

        private static void OnDetachedFromVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBlock tb) StopTypewriter(tb);
        }

        private static void ScheduleTypewriter(TextBlock textBlock)
        {
            // Defer to next frame so XAML parser has finished setting Text property
            Dispatcher.UIThread.Post(() => StartTypewriter(textBlock), DispatcherPriority.Loaded);
        }

        private static async void StartTypewriter(TextBlock textBlock)
        {
            // If already running, don't restart
            var existingCts = textBlock.GetValue(CtsProperty);
            if (existingCts != null) return;

            // Check if still enabled (might have been disabled before dispatch)
            if (!GetIsEnabled(textBlock)) return;

            // Get the text to animate
            var storedText = textBlock.GetValue(FullTextProperty);
            var text = storedText ?? textBlock.Text;
            if (string.IsNullOrEmpty(text)) return;

            string content = text!;

            textBlock.SetValue(FullTextProperty, content);
            textBlock.Text = string.Empty;

            var cts = new CancellationTokenSource();
            textBlock.SetValue(CtsProperty, cts);

            var speed = GetSpeed(textBlock);
            var token = cts.Token;

            try
            {
                for (int i = 0; i <= content.Length; i++)
                {
                    if (token.IsCancellationRequested) break;
                    textBlock.Text = content.Substring(0, i);
                    await Task.Delay(speed, token);
                }
            }
            catch (TaskCanceledException) { }
            finally
            {
                // Clean up CTS when animation completes
                if (textBlock.GetValue(CtsProperty) == cts)
                {
                    textBlock.SetValue(CtsProperty, null);
                }
            }
        }

        private static void StopTypewriter(TextBlock textBlock)
        {
            var cts = textBlock.GetValue(CtsProperty);
            if (cts != null)
            {
                cts.Cancel();
                cts.Dispose();
                textBlock.SetValue(CtsProperty, null);
            }

            var fullText = textBlock.GetValue(FullTextProperty);
            if (fullText != null)
            {
                textBlock.Text = fullText;
                textBlock.SetValue(FullTextProperty, null);
            }
        }

        /// <summary>
        /// Restarts the typewriter animation from the beginning.
        /// </summary>
        public static void Restart(TextBlock textBlock)
        {
            StopTypewriter(textBlock);
            if (GetIsEnabled(textBlock))
            {
                ScheduleTypewriter(textBlock);
            }
        }
    }
}
