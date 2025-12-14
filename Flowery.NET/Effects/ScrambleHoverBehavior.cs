using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;

namespace Flowery.Effects
{
    /// <summary>
    /// Scrambles text characters on hover, then resolves back to original text.
    /// Works on TextBlock controls via attached properties.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;TextBlock Text="Hover Me!"
    ///            fx:ScrambleHoverBehavior.IsEnabled="True"
    ///            fx:ScrambleHoverBehavior.ScrambleChars="!@#$%^&amp;*()"/&gt;
    /// </code>
    /// </example>
    public static class ScrambleHoverBehavior
    {
        private static readonly Random _random = new();
        private const string DefaultScrambleChars = "!@#$%^&*()[]{}|;:,.<>?/~`";

        #region Attached Properties

        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, bool>(
                "IsEnabled", typeof(ScrambleHoverBehavior), false);

        public static readonly AttachedProperty<string> ScrambleCharsProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, string>(
                "ScrambleChars", typeof(ScrambleHoverBehavior), DefaultScrambleChars);

        public static readonly AttachedProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, TimeSpan>(
                "Duration", typeof(ScrambleHoverBehavior), TimeSpan.FromMilliseconds(500));

        public static readonly AttachedProperty<int> FrameRateProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, int>(
                "FrameRate", typeof(ScrambleHoverBehavior), 30);

        // Internal: store original text
        private static readonly AttachedProperty<string?> OriginalTextProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, string?>(
                "OriginalText", typeof(ScrambleHoverBehavior), null);

        // Internal: active timer
        private static readonly AttachedProperty<DispatcherTimer?> TimerProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, DispatcherTimer?>(
                "Timer", typeof(ScrambleHoverBehavior), null);

        // Internal: track animation progress
        private static readonly AttachedProperty<int> FrameCountProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, int>(
                "FrameCount", typeof(ScrambleHoverBehavior), 0);

        #endregion

        #region Getters/Setters

        public static bool GetIsEnabled(TextBlock element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(TextBlock element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static string GetScrambleChars(TextBlock element) => element.GetValue(ScrambleCharsProperty);
        public static void SetScrambleChars(TextBlock element, string value) => element.SetValue(ScrambleCharsProperty, value);

        public static TimeSpan GetDuration(TextBlock element) => element.GetValue(DurationProperty);
        public static void SetDuration(TextBlock element, TimeSpan value) => element.SetValue(DurationProperty, value);

        public static int GetFrameRate(TextBlock element) => element.GetValue(FrameRateProperty);
        public static void SetFrameRate(TextBlock element, int value) => element.SetValue(FrameRateProperty, value);

        #endregion

        static ScrambleHoverBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<TextBlock>(OnIsEnabledChanged);
        }

        private static void OnIsEnabledChanged(TextBlock element, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                element.PointerEntered += OnPointerEntered;
                element.PointerExited += OnPointerExited;
            }
            else
            {
                element.PointerEntered -= OnPointerEntered;
                element.PointerExited -= OnPointerExited;
                StopScramble(element);
            }
        }

        private static void OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is not TextBlock textBlock) return;
            StartScramble(textBlock);
        }

        private static void OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is not TextBlock textBlock) return;
            // Continue resolving but faster on exit
        }

        private static void StartScramble(TextBlock textBlock)
        {
            // Store original text
            var originalText = textBlock.Text ?? string.Empty;
            textBlock.SetValue(OriginalTextProperty, originalText);
            textBlock.SetValue(FrameCountProperty, 0);

            // Stop any existing timer
            StopScramble(textBlock);

            var duration = GetDuration(textBlock);
            var frameRate = GetFrameRate(textBlock);
            var scrambleChars = GetScrambleChars(textBlock);

            var totalFrames = (int)(duration.TotalMilliseconds / (1000.0 / frameRate));
            var interval = TimeSpan.FromMilliseconds(1000.0 / frameRate);

            var timer = new DispatcherTimer { Interval = interval };
            timer.Tick += (_, _) =>
            {
                var frameCount = textBlock.GetValue(FrameCountProperty);
                var stored = textBlock.GetValue(OriginalTextProperty) ?? string.Empty;

                if (frameCount >= totalFrames || string.IsNullOrEmpty(stored))
                {
                    // Animation complete - restore original
                    textBlock.Text = stored;
                    StopScramble(textBlock);
                    return;
                }

                // Calculate how many characters are resolved (left to right)
                var progress = (double)frameCount / totalFrames;
                var resolvedCount = (int)(stored.Length * progress);

                // Build scrambled string
                var chars = new char[stored.Length];
                for (int i = 0; i < stored.Length; i++)
                {
                    if (i < resolvedCount)
                    {
                        // Resolved - show original character
                        chars[i] = stored[i];
                    }
                    else if (char.IsWhiteSpace(stored[i]))
                    {
                        // Preserve whitespace
                        chars[i] = stored[i];
                    }
                    else
                    {
                        // Scramble this character
                        chars[i] = scrambleChars[_random.Next(scrambleChars.Length)];
                    }
                }

                textBlock.Text = new string(chars);
                textBlock.SetValue(FrameCountProperty, frameCount + 1);
            };

            textBlock.SetValue(TimerProperty, timer);
            timer.Start();
        }

        private static void StopScramble(TextBlock textBlock)
        {
            var timer = textBlock.GetValue(TimerProperty);
            if (timer != null)
            {
                timer.Stop();
                textBlock.SetValue(TimerProperty, null);
            }
        }
    }
}
