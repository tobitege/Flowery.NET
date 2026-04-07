using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Flowery.Effects
{
    /// <summary>
    /// Determines the scramble behavior direction.
    /// </summary>
    public enum ScrambleMode
    {
        /// <summary>
        /// Text starts scrambled and reveals on hover/click (default).
        /// Useful for teasers, spoilers, hidden content.
        /// </summary>
        RevealOnHover,

        /// <summary>
        /// Text starts readable and scrambles on hover/click.
        /// Original smoothui behavior - purely decorative glitch effect.
        /// </summary>
        ScrambleOnHover
    }

    /// <summary>
    /// Determines how characters animate during reveal/scramble.
    /// </summary>
    public enum RevealStyle
    {
        /// <summary>
        /// Characters show random symbols until revealed (default).
        /// Digital/glitchy aesthetic.
        /// </summary>
        Random,

        /// <summary>
        /// Characters cycle through alphabet sequentially like airport split-flap displays.
        /// Each character settles independently when it reaches the target letter.
        /// Mechanical/retro aesthetic.
        /// </summary>
        SplitFlap
    }

    /// <summary>
    /// Scrambles or reveals text characters on hover/click.
    /// Works on TextBlock controls via attached properties.
    /// </summary>
    /// <remarks>
    /// Default mode is <see cref="ScrambleMode.RevealOnHover"/>: text starts scrambled
    /// and progressively reveals left-to-right on hover or click (for mobile).
    /// </remarks>
    /// <example>
    /// <code>
    /// &lt;!-- Reveal on hover (default) - starts scrambled --&gt;
    /// &lt;TextBlock Text="Secret Message"
    ///            fx:ScrambleHoverBehavior.IsEnabled="True"/&gt;
    ///
    /// &lt;!-- Scramble on hover - starts readable --&gt;
    /// &lt;TextBlock Text="Glitch Me!"
    ///            fx:ScrambleHoverBehavior.IsEnabled="True"
    ///            fx:ScrambleHoverBehavior.Mode="ScrambleOnHover"/&gt;
    /// </code>
    /// </example>
    public static class ScrambleHoverBehavior
    {
        private static readonly Random _random = new();
        private const string DefaultScrambleChars = "!@#$%^&*()[]{}|;:,.<>?/~`";
        private const string SplitFlapChars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ ";

        #region Attached Properties

        public static readonly AttachedProperty<bool> IsEnabledProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, bool>(
                "IsEnabled", typeof(ScrambleHoverBehavior), false);

        public static readonly AttachedProperty<ScrambleMode> ModeProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, ScrambleMode>(
                "Mode", typeof(ScrambleHoverBehavior), ScrambleMode.RevealOnHover);

        public static readonly AttachedProperty<string> ScrambleCharsProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, string>(
                "ScrambleChars", typeof(ScrambleHoverBehavior), DefaultScrambleChars);

        public static readonly AttachedProperty<TimeSpan> DurationProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, TimeSpan>(
                "Duration", typeof(ScrambleHoverBehavior), TimeSpan.FromMilliseconds(500));

        public static readonly AttachedProperty<int> FrameRateProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, int>(
                "FrameRate", typeof(ScrambleHoverBehavior), 30);

        public static readonly AttachedProperty<RevealStyle> RevealStyleProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, RevealStyle>(
                "RevealStyle", typeof(ScrambleHoverBehavior), RevealStyle.Random);

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

        // Internal: store current character positions for SplitFlap effect
        private static readonly AttachedProperty<int[]?> CharPositionsProperty =
            AvaloniaProperty.RegisterAttached<TextBlock, int[]?>(
                "CharPositions", typeof(ScrambleHoverBehavior), null);

        #endregion

        #region Getters/Setters

        public static bool GetIsEnabled(TextBlock element) => element.GetValue(IsEnabledProperty);
        public static void SetIsEnabled(TextBlock element, bool value) => element.SetValue(IsEnabledProperty, value);

        public static ScrambleMode GetMode(TextBlock element) => element.GetValue(ModeProperty);
        public static void SetMode(TextBlock element, ScrambleMode value) => element.SetValue(ModeProperty, value);

        public static string GetScrambleChars(TextBlock element) => element.GetValue(ScrambleCharsProperty);
        public static void SetScrambleChars(TextBlock element, string value) => element.SetValue(ScrambleCharsProperty, value);

        public static TimeSpan GetDuration(TextBlock element) => element.GetValue(DurationProperty);
        public static void SetDuration(TextBlock element, TimeSpan value) => element.SetValue(DurationProperty, value);

        public static int GetFrameRate(TextBlock element) => element.GetValue(FrameRateProperty);
        public static void SetFrameRate(TextBlock element, int value) => element.SetValue(FrameRateProperty, value);

        public static RevealStyle GetRevealStyle(TextBlock element) => element.GetValue(RevealStyleProperty);
        public static void SetRevealStyle(TextBlock element, RevealStyle value) => element.SetValue(RevealStyleProperty, value);

        #endregion

        static ScrambleHoverBehavior()
        {
            IsEnabledProperty.Changed.AddClassHandler<TextBlock>(OnIsEnabledChanged);
        }

        /// <summary>
        /// Programmatically triggers the animation (reveal or scramble based on mode).
        /// Useful for demos and automated showcases.
        /// </summary>
        public static void TriggerScramble(TextBlock textBlock)
        {
            if (textBlock == null) return;
            StopAnimation(textBlock);
            StartAnimation(textBlock);
        }

        private static void OnIsEnabledChanged(TextBlock element, AvaloniaPropertyChangedEventArgs e)
        {
            if (e.NewValue is true)
            {
                element.PointerEntered += OnPointerEntered;
                element.PointerExited += OnPointerExited;
                element.PointerPressed += OnPointerPressed;
                element.AttachedToVisualTree += OnAttachedToVisualTree;

                // If already attached, initialize now
                if (TopLevel.GetTopLevel(element) != null)
                {
                    InitializeText(element);
                }
            }
            else
            {
                element.PointerEntered -= OnPointerEntered;
                element.PointerExited -= OnPointerExited;
                element.PointerPressed -= OnPointerPressed;
                element.AttachedToVisualTree -= OnAttachedToVisualTree;
                StopAnimation(element);
                RestoreOriginalText(element);
            }
        }

        private static void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
        {
            if (sender is TextBlock textBlock)
            {
                InitializeText(textBlock);
            }
        }

        private static void InitializeText(TextBlock textBlock)
        {
            var currentText = textBlock.Text ?? string.Empty;
            if (string.IsNullOrEmpty(currentText)) return;

            // Store original if not already stored
            var storedOriginal = textBlock.GetValue(OriginalTextProperty);
            if (string.IsNullOrEmpty(storedOriginal))
            {
                textBlock.SetValue(OriginalTextProperty, currentText);
            }

            var mode = GetMode(textBlock);
            if (mode == ScrambleMode.RevealOnHover)
            {
                // Start with scrambled text
                var original = textBlock.GetValue(OriginalTextProperty) ?? currentText;
                var revealStyle = GetRevealStyle(textBlock);
                textBlock.Text = ScrambleAllChars(original, GetScrambleChars(textBlock), revealStyle);
            }
        }

        private static void OnPointerEntered(object? sender, PointerEventArgs e)
        {
            if (sender is not TextBlock textBlock) return;
            StartAnimation(textBlock);
        }

        private static void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is not TextBlock textBlock) return;
            // For touch/click: toggle or trigger animation
            StartAnimation(textBlock);
        }

        private static void OnPointerExited(object? sender, PointerEventArgs e)
        {
            if (sender is not TextBlock textBlock) return;
            var mode = GetMode(textBlock);

            StopAnimation(textBlock);

            if (mode == ScrambleMode.RevealOnHover)
            {
                // Go back to scrambled state
                var original = textBlock.GetValue(OriginalTextProperty);
                if (!string.IsNullOrEmpty(original))
                {
                    var revealStyle = GetRevealStyle(textBlock);
                    textBlock.Text = ScrambleAllChars(original!, GetScrambleChars(textBlock), revealStyle);
                }
            }
            else
            {
                // Go back to readable state
                RestoreOriginalText(textBlock);
            }
        }

        private static void StartAnimation(TextBlock textBlock)
        {
            // If already running, don't restart
            var existingTimer = textBlock.GetValue(TimerProperty);
            if (existingTimer != null) return;

            var storedOriginal = textBlock.GetValue(OriginalTextProperty);
            if (string.IsNullOrEmpty(storedOriginal))
            {
                // First time - store the original
                storedOriginal = textBlock.Text ?? string.Empty;
                if (string.IsNullOrEmpty(storedOriginal)) return;
                textBlock.SetValue(OriginalTextProperty, storedOriginal);
            }

            textBlock.SetValue(FrameCountProperty, 0);

            var mode = GetMode(textBlock);
            var revealStyle = GetRevealStyle(textBlock);
            var duration = GetDuration(textBlock);
            var frameRate = GetFrameRate(textBlock);
            var scrambleChars = GetScrambleChars(textBlock);

            var totalFrames = (int)(duration.TotalMilliseconds / (1000.0 / frameRate));
            var interval = TimeSpan.FromMilliseconds(1000.0 / frameRate);

            // Initialize character positions for SplitFlap style
            if (revealStyle == RevealStyle.SplitFlap)
            {
                var positions = new int[storedOriginal!.Length];
                for (int i = 0; i < storedOriginal.Length; i++)
                {
                    // Start each character at a random position in the flap sequence
                    positions[i] = _random.Next(SplitFlapChars.Length);
                }
                textBlock.SetValue(CharPositionsProperty, positions);
            }

            var timer = new DispatcherTimer { Interval = interval };
            timer.Tick += (_, _) =>
            {
                var frameCount = textBlock.GetValue(FrameCountProperty);
                var original = textBlock.GetValue(OriginalTextProperty) ?? string.Empty;

                if (frameCount >= totalFrames || string.IsNullOrEmpty(original))
                {
                    // Animation complete
                    if (mode == ScrambleMode.RevealOnHover)
                    {
                        textBlock.Text = original;
                    }
                    else
                    {
                        textBlock.Text = ScrambleAllChars(original, scrambleChars, revealStyle);
                    }
                    StopAnimation(textBlock);
                    return;
                }

                var progress = (double)frameCount / totalFrames;

                if (revealStyle == RevealStyle.SplitFlap)
                {
                    var positions = textBlock.GetValue(CharPositionsProperty);
                    var (text, allSettled) = BuildSplitFlapText(original, positions, mode);
                    textBlock.Text = text;

                    // Stop early if all characters have settled
                    if (allSettled)
                    {
                        textBlock.Text = original;
                        StopAnimation(textBlock);
                        return;
                    }

                    // Advance only positions that haven't reached their target
                    if (positions != null)
                    {
                        for (int i = 0; i < original.Length; i++)
                        {
                            if (char.IsWhiteSpace(original[i])) continue;

                            var targetChar = char.ToUpperInvariant(original[i]);
                            var targetIndex = SplitFlapChars.IndexOf(targetChar);
                            if (targetIndex < 0) targetIndex = SplitFlapChars.Length - 1;

                            // Only advance if not yet at target
                            if (positions[i] != targetIndex)
                            {
                                positions[i] = (positions[i] + 1) % SplitFlapChars.Length;
                            }
                        }
                    }
                }
                else
                {
                    textBlock.Text = BuildAnimatedText(original, progress, mode, scrambleChars);
                }

                textBlock.SetValue(FrameCountProperty, frameCount + 1);
            };

            textBlock.SetValue(TimerProperty, timer);
            timer.Start();
        }

        private static string BuildAnimatedText(string original, double progress, ScrambleMode mode, string scrambleChars)
        {
            var resolvedCount = (int)(original.Length * progress);
            var chars = new char[original.Length];

            for (int i = 0; i < original.Length; i++)
            {
                bool isResolved = mode == ScrambleMode.RevealOnHover
                    ? i < resolvedCount   // Reveal left-to-right
                    : i >= resolvedCount; // Scramble left-to-right

                if (isResolved)
                {
                    chars[i] = original[i];
                }
                else if (char.IsWhiteSpace(original[i]))
                {
                    chars[i] = original[i];
                }
                else
                {
                    chars[i] = scrambleChars[_random.Next(scrambleChars.Length)];
                }
            }

            return new string(chars);
        }

        private static (string text, bool allSettled) BuildSplitFlapText(string original, int[]? positions, ScrambleMode mode)
        {
            var chars = new char[original.Length];
            var isRevealing = mode == ScrambleMode.RevealOnHover;
            var allSettled = true;

            for (int i = 0; i < original.Length; i++)
            {
                if (char.IsWhiteSpace(original[i]))
                {
                    chars[i] = original[i];
                    continue;
                }

                var targetChar = char.ToUpperInvariant(original[i]);
                var targetIndex = SplitFlapChars.IndexOf(targetChar);
                if (targetIndex < 0) targetIndex = SplitFlapChars.Length - 1; // Default to space for unknown chars

                var currentPos = positions?[i] ?? 0;

                if (isRevealing)
                {
                    // Check if we've landed on the target
                    if (currentPos == targetIndex)
                    {
                        // Settled - show original character (preserve case)
                        chars[i] = original[i];
                    }
                    else
                    {
                        // Still flipping - show current position
                        chars[i] = SplitFlapChars[currentPos];
                        allSettled = false;
                    }
                }
                else
                {
                    // ScrambleOnHover: show current position (cycling away from original)
                    chars[i] = SplitFlapChars[currentPos];
                    allSettled = false; // Never settles in scramble mode
                }
            }

            return (new string(chars), allSettled);
        }

        private static string ScrambleAllChars(string original, string scrambleChars, RevealStyle style = RevealStyle.Random)
        {
            var chars = new char[original.Length];
            var charSet = style == RevealStyle.SplitFlap ? SplitFlapChars : scrambleChars;

            for (int i = 0; i < original.Length; i++)
            {
                if (char.IsWhiteSpace(original[i]))
                {
                    chars[i] = original[i];
                }
                else
                {
                    chars[i] = charSet[_random.Next(charSet.Length)];
                }
            }
            return new string(chars);
        }

        private static void StopAnimation(TextBlock textBlock)
        {
            var timer = textBlock.GetValue(TimerProperty);
            if (timer != null)
            {
                timer.Stop();
                textBlock.SetValue(TimerProperty, null);
            }
        }

        private static void RestoreOriginalText(TextBlock textBlock)
        {
            var original = textBlock.GetValue(OriginalTextProperty);
            if (!string.IsNullOrEmpty(original))
            {
                textBlock.Text = original;
            }
        }

        /// <summary>
        /// Stops any running animation and restores the original text.
        /// </summary>
        public static void ResetScramble(TextBlock textBlock)
        {
            if (textBlock == null) return;
            StopAnimation(textBlock);
            RestoreOriginalText(textBlock);
        }
    }
}
