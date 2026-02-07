using System;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Flowery.Enums;

namespace Flowery.Helpers
{
    /// <summary>
    /// Parameters for slide effect animations.
    /// </summary>
    public record FlowerySlideEffectParams
    {
        /// <summary>Zoom scale factor (0.0 to 0.5). Default 0.2 = zoom to 120%.</summary>
        public double ZoomIntensity { get; init; } = 0.2;

        /// <summary>Pan distance in pixels. Default 50.</summary>
        public double PanDistance { get; init; } = 50.0;

        /// <summary>Pulse amplitude (0.0 to 0.2). Default 0.08 = pulse to 108%.</summary>
        public double PulseIntensity { get; init; } = 0.08;

        /// <summary>Vertical pan multiplier (relative to horizontal). Default 0.6.</summary>
        public double VerticalPanRatio { get; init; } = 0.6;

        /// <summary>Subtle zoom multiplier for pan effects. Default 0.1.</summary>
        public double SubtleZoomRatio { get; init; } = 0.1;

        /// <summary>Pan And Zoom pan speed in pixels per second. Default 4.</summary>
        public double PanAndZoomPanSpeed { get; init; } = 4.0;

        /// <summary>Pan And Zoom intensity (0.0 to 0.2). Default 0.2 = zoom to 120%.</summary>
        public double PanAndZoomZoom { get; init; } = 0.2;

        /// <summary>Locks Pan And Zoom zoom (pan only). Default false.</summary>
        public bool PanAndZoomLockZoom { get; init; }

        /// <summary>
        /// When true, portrait images (much taller than the viewport) only pan downward
        /// to avoid cutting off faces/heads at the top. Default true.
        /// </summary>
        public bool VerticalLock { get; init; } = true;

        /// <summary>
        /// Height multiplier threshold for VerticalLock to apply.
        /// Image is considered "very tall" when height > viewport height * this value. Default 1.5.
        /// </summary>
        public double VerticalLockRatio { get; init; } = 1.5;

        /// <summary>
        /// Optimization ratio for very long/tall images. When an image extends far beyond
        /// the viewport, only pan this fraction of the total delta to prevent dizzying motion.
        /// Applied when delta > viewport * 0.7. Default 0.6 (pan only 60% of total delta).
        /// </summary>
        public double OptimizeRatio { get; init; } = 0.6;

        /// <summary>Breath effect peak intensity. Default 0.3 = zoom to 130% at peak.</summary>
        public double BreathIntensity { get; init; } = 0.3;

        /// <summary>Throw effect scale at center. Default 1.0 (full size at center, 0.7 at edges).</summary>
        public double ThrowScale { get; init; } = 1.0;
    }

    /// <summary>
    /// Helper methods for parsing FlowerySlideEffect from strings.
    /// </summary>
    public static class FlowerySlideEffectParser
    {
        /// <summary>
        /// Parses a string to FlowerySlideEffect. Returns None for invalid/null strings.
        /// </summary>
        public static FlowerySlideEffect Parse(string? value)
        {
            if (string.IsNullOrEmpty(value)) return FlowerySlideEffect.None;

            return value switch
            {
                "PanAndZoom" => FlowerySlideEffect.PanAndZoom,
                "ZoomIn" => FlowerySlideEffect.ZoomIn,
                "ZoomOut" => FlowerySlideEffect.ZoomOut,
                "PanLeft" => FlowerySlideEffect.PanLeft,
                "PanRight" => FlowerySlideEffect.PanRight,
                "PanUp" => FlowerySlideEffect.PanUp,
                "PanDown" => FlowerySlideEffect.PanDown,
                "Drift" => FlowerySlideEffect.Drift,
                "Pulse" => FlowerySlideEffect.Pulse,
                "Breath" => FlowerySlideEffect.Breath,
                "Throw" => FlowerySlideEffect.Throw,
                _ => FlowerySlideEffect.None
            };
        }

        /// <summary>
        /// Tries to parse a string to FlowerySlideEffect.
        /// </summary>
        public static bool TryParse(string? value, out FlowerySlideEffect effect)
        {
            effect = Parse(value);
            return !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>
    /// Helper methods for parsing FlowerySlideshowMode from strings.
    /// </summary>
    public static class FlowerySlideshowModeParser
    {
        /// <summary>
        /// Parses a string to FlowerySlideshowMode. Returns Manual for invalid/null strings.
        /// </summary>
        public static FlowerySlideshowMode Parse(string? value)
        {
            if (string.IsNullOrEmpty(value)) return FlowerySlideshowMode.Manual;

            return value switch
            {
                "Manual" => FlowerySlideshowMode.Manual,
                "Slideshow" => FlowerySlideshowMode.Slideshow,
                "Random" => FlowerySlideshowMode.Random,
                "Kiosk" => FlowerySlideshowMode.Kiosk,
                _ => FlowerySlideshowMode.Manual
            };
        }

        /// <summary>
        /// Tries to parse a string to FlowerySlideshowMode.
        /// </summary>
        public static bool TryParse(string? value, out FlowerySlideshowMode mode)
        {
            mode = Parse(value);
            return !string.IsNullOrEmpty(value);
        }
    }

    /// <summary>
    /// Parameters for slide transition animations.
    /// </summary>
    public record FlowerySlideTransitionParams
    {
        /// <summary>Duration of the transition animation. Default 400ms.</summary>
        public TimeSpan Duration { get; init; } = TimeSpan.FromMilliseconds(400);

        /// <summary>Easing mode for the transition. Default EaseInOut.</summary>
        public EasingMode EasingMode { get; init; } = EasingMode.EaseInOut;

        /// <summary>Number of slices for Slices/Blinds transitions. Default 8.</summary>
        public int SliceCount { get; init; } = 8;

        /// <summary>Whether slice animations should be staggered or simultaneous. Default true.</summary>
        public bool StaggerSlices { get; init; } = true;

        /// <summary>Base stagger delay between slices in ms. Default 50ms.</summary>
        public double SliceStaggerMs { get; init; } = 50;

        /// <summary>Grid size for Checkerboard transition (squares per row/column). Default 6.</summary>
        public int CheckerboardSize { get; init; } = 6;

        /// <summary>Pixel block size for Pixelate transition at peak. Default 20.</summary>
        public int PixelateSize { get; init; } = 20;

        /// <summary>Noise density for Dissolve transition (0.0 to 1.0). Default 0.5.</summary>
        public double DissolveDensity { get; init; } = 0.5;

        /// <summary>Rotation angle for flip transitions in degrees. Default 90.</summary>
        public double FlipAngle { get; init; } = 90;

        /// <summary>Perspective depth for 3D transitions. Default 1000.</summary>
        public double PerspectiveDepth { get; init; } = 1000;

        /// <summary>Scale factor for zoom transitions. Default 0.8 (zoom from 80%).</summary>
        public double ZoomScale { get; init; } = 0.8;

        /// <summary>Color for FadeThroughBlack/White transitions. Auto-detected if not set.</summary>
        public Color? FadeThroughColor { get; init; }
    }

    /// <summary>
    /// Helper methods and utilities for FlowerySlideTransition.
    /// </summary>
    public static class FlowerySlideTransitionParser
    {
        private static readonly Random _random = new();

        /// <summary>
        /// Parses a string to FlowerySlideTransition. Returns None for invalid/null strings.
        /// </summary>
        public static FlowerySlideTransition Parse(string? value)
        {
            if (string.IsNullOrEmpty(value)) return FlowerySlideTransition.None;

            return value switch
            {
                "Random" => FlowerySlideTransition.Random,
                "Fade" => FlowerySlideTransition.Fade,
                "FadeThroughBlack" => FlowerySlideTransition.FadeThroughBlack,
                "FadeThroughWhite" => FlowerySlideTransition.FadeThroughWhite,
                "SlideLeft" => FlowerySlideTransition.SlideLeft,
                "SlideRight" => FlowerySlideTransition.SlideRight,
                "SlideUp" => FlowerySlideTransition.SlideUp,
                "SlideDown" => FlowerySlideTransition.SlideDown,
                "PushLeft" => FlowerySlideTransition.PushLeft,
                "PushRight" => FlowerySlideTransition.PushRight,
                "PushUp" => FlowerySlideTransition.PushUp,
                "PushDown" => FlowerySlideTransition.PushDown,
                "ZoomIn" => FlowerySlideTransition.ZoomIn,
                "ZoomOut" => FlowerySlideTransition.ZoomOut,
                "ZoomCross" => FlowerySlideTransition.ZoomCross,
                "FlipHorizontal" => FlowerySlideTransition.FlipHorizontal,
                "FlipVertical" => FlowerySlideTransition.FlipVertical,
                "CubeLeft" => FlowerySlideTransition.CubeLeft,
                "CubeRight" => FlowerySlideTransition.CubeRight,
                "CoverLeft" => FlowerySlideTransition.CoverLeft,
                "CoverRight" => FlowerySlideTransition.CoverRight,
                "CoverUp" => FlowerySlideTransition.CoverUp,
                "CoverDown" => FlowerySlideTransition.CoverDown,
                "RevealLeft" => FlowerySlideTransition.RevealLeft,
                "RevealRight" => FlowerySlideTransition.RevealRight,
                "WipeLeft" => FlowerySlideTransition.WipeLeft,
                "WipeRight" => FlowerySlideTransition.WipeRight,
                "WipeUp" => FlowerySlideTransition.WipeUp,
                "WipeDown" => FlowerySlideTransition.WipeDown,
                "BlindsHorizontal" => FlowerySlideTransition.BlindsHorizontal,
                "BlindsVertical" => FlowerySlideTransition.BlindsVertical,
                "SlicesHorizontal" => FlowerySlideTransition.SlicesHorizontal,
                "SlicesVertical" => FlowerySlideTransition.SlicesVertical,
                "Checkerboard" => FlowerySlideTransition.Checkerboard,
                "Spiral" => FlowerySlideTransition.Spiral,
                "MatrixRain" => FlowerySlideTransition.MatrixRain,
                "Wormhole" => FlowerySlideTransition.Wormhole,
                "Dissolve" => FlowerySlideTransition.Dissolve,
                "Pixelate" => FlowerySlideTransition.Pixelate,
                _ => FlowerySlideTransition.None
            };
        }

        /// <summary>
        /// Tries to parse a string to FlowerySlideTransition.
        /// </summary>
        public static bool TryParse(string? value, out FlowerySlideTransition transition)
        {
            transition = Parse(value);
            return !string.IsNullOrEmpty(value);
        }

        /// <summary>
        /// Gets the implementation tier for a transition.
        /// </summary>
        public static FloweryTransitionTier GetTier(FlowerySlideTransition transition)
        {
            return transition switch
            {
                FlowerySlideTransition.None or
                FlowerySlideTransition.Random or
                FlowerySlideTransition.Fade or
                FlowerySlideTransition.FadeThroughBlack or
                FlowerySlideTransition.FadeThroughWhite or
                FlowerySlideTransition.SlideLeft or
                FlowerySlideTransition.SlideRight or
                FlowerySlideTransition.SlideUp or
                FlowerySlideTransition.SlideDown or
                FlowerySlideTransition.PushLeft or
                FlowerySlideTransition.PushRight or
                FlowerySlideTransition.PushUp or
                FlowerySlideTransition.PushDown or
                FlowerySlideTransition.ZoomIn or
                FlowerySlideTransition.ZoomOut or
                FlowerySlideTransition.ZoomCross or
                FlowerySlideTransition.FlipHorizontal or
                FlowerySlideTransition.FlipVertical or
                FlowerySlideTransition.CubeLeft or
                FlowerySlideTransition.CubeRight or
                FlowerySlideTransition.CoverLeft or
                FlowerySlideTransition.CoverRight or
                FlowerySlideTransition.CoverUp or
                FlowerySlideTransition.CoverDown or
                FlowerySlideTransition.RevealLeft or
                FlowerySlideTransition.RevealRight
                    => FloweryTransitionTier.Transform,

                FlowerySlideTransition.WipeLeft or
                FlowerySlideTransition.WipeRight or
                FlowerySlideTransition.WipeUp or
                FlowerySlideTransition.WipeDown or
                FlowerySlideTransition.BlindsHorizontal or
                FlowerySlideTransition.BlindsVertical or
                FlowerySlideTransition.SlicesHorizontal or
                FlowerySlideTransition.SlicesVertical or
                FlowerySlideTransition.Checkerboard or
                FlowerySlideTransition.Spiral or
                FlowerySlideTransition.MatrixRain or
                FlowerySlideTransition.Wormhole
                    => FloweryTransitionTier.Clip,

                FlowerySlideTransition.Dissolve or
                FlowerySlideTransition.Pixelate
                    => FloweryTransitionTier.Skia,

                _ => FloweryTransitionTier.Transform
            };
        }

        /// <summary>
        /// Gets all transitions of a specific tier.
        /// </summary>
        public static FlowerySlideTransition[] GetTransitionsByTier(FloweryTransitionTier tier)
        {
            return Enum.GetValues(typeof(FlowerySlideTransition))
                .Cast<FlowerySlideTransition>()
                .Where(t => t is not FlowerySlideTransition.None and not FlowerySlideTransition.Random)
                .Where(t => GetTier(t) == tier)
                .ToArray();
        }

        /// <summary>
        /// Gets all Tier 1 (Transform) transitions for random selection on all platforms.
        /// </summary>
        public static FlowerySlideTransition[] GetUniversalTransitions()
        {
            return GetTransitionsByTier(FloweryTransitionTier.Transform);
        }

        /// <summary>
        /// Picks a random transition, optionally filtered by tier.
        /// </summary>
        public static FlowerySlideTransition PickRandom(FloweryTransitionTier? maxTier = null)
        {
            FlowerySlideTransition[] available = maxTier switch
            {
                FloweryTransitionTier.Transform => GetTransitionsByTier(FloweryTransitionTier.Transform),
                FloweryTransitionTier.Clip => GetTransitionsByTier(FloweryTransitionTier.Transform)
                                                .Concat(GetTransitionsByTier(FloweryTransitionTier.Clip))
                                                .ToArray(),
                FloweryTransitionTier.Skia => Enum.GetValues(typeof(FlowerySlideTransition))
                                                .Cast<FlowerySlideTransition>()
                                                .Where(t => t is not FlowerySlideTransition.None and not FlowerySlideTransition.Random)
                                                .ToArray(),
                _ => GetUniversalTransitions()
            };

            return available[_random.Next(available.Length)];
        }

        /// <summary>
        /// Gets a display-friendly name for the transition.
        /// </summary>
        public static string GetDisplayName(FlowerySlideTransition transition)
        {
            return transition switch
            {
                FlowerySlideTransition.None => "None",
                FlowerySlideTransition.Random => "Random",
                FlowerySlideTransition.Fade => "Fade",
                FlowerySlideTransition.FadeThroughBlack => "Fade (Black)",
                FlowerySlideTransition.FadeThroughWhite => "Fade (White)",
                FlowerySlideTransition.SlideLeft => "Slide Left",
                FlowerySlideTransition.SlideRight => "Slide Right",
                FlowerySlideTransition.SlideUp => "Slide Up",
                FlowerySlideTransition.SlideDown => "Slide Down",
                FlowerySlideTransition.PushLeft => "Push Left",
                FlowerySlideTransition.PushRight => "Push Right",
                FlowerySlideTransition.PushUp => "Push Up",
                FlowerySlideTransition.PushDown => "Push Down",
                FlowerySlideTransition.ZoomIn => "Zoom In",
                FlowerySlideTransition.ZoomOut => "Zoom Out",
                FlowerySlideTransition.ZoomCross => "Zoom Cross",
                FlowerySlideTransition.FlipHorizontal => "Flip Horizontal",
                FlowerySlideTransition.FlipVertical => "Flip Vertical",
                FlowerySlideTransition.CubeLeft => "Cube Left",
                FlowerySlideTransition.CubeRight => "Cube Right",
                FlowerySlideTransition.CoverLeft => "Cover Left",
                FlowerySlideTransition.CoverRight => "Cover Right",
                FlowerySlideTransition.CoverUp => "Cover Up",
                FlowerySlideTransition.CoverDown => "Cover Down",
                FlowerySlideTransition.RevealLeft => "Reveal Left",
                FlowerySlideTransition.RevealRight => "Reveal Right",
                FlowerySlideTransition.WipeLeft => "Wipe Left",
                FlowerySlideTransition.WipeRight => "Wipe Right",
                FlowerySlideTransition.WipeUp => "Wipe Up",
                FlowerySlideTransition.WipeDown => "Wipe Down",
                FlowerySlideTransition.BlindsHorizontal => "Blinds Horizontal",
                FlowerySlideTransition.BlindsVertical => "Blinds Vertical",
                FlowerySlideTransition.SlicesHorizontal => "Slices Horizontal",
                FlowerySlideTransition.SlicesVertical => "Slices Vertical",
                FlowerySlideTransition.Checkerboard => "Checkerboard",
                FlowerySlideTransition.Spiral => "Spiral",
                FlowerySlideTransition.MatrixRain => "Matrix Rain",
                FlowerySlideTransition.Wormhole => "Wormhole",
                FlowerySlideTransition.Dissolve => "Dissolve ⚡",
                FlowerySlideTransition.Pixelate => "Pixelate ⚡",
                _ => transition.ToString()
            };
        }
    }

    /// <summary>
    /// Event args for slide change events in slideshow controls.
    /// </summary>
    public class FlowerySlideChangedEventArgs(int oldIndex, int newIndex, FlowerySlideEffect appliedEffect = FlowerySlideEffect.None, Visual? currentElement = null) : EventArgs
    {
        /// <summary>Index before the change, or -1 if this is the first load.</summary>
        public int OldIndex { get; } = oldIndex;

        /// <summary>Compatibility alias for OldIndex.</summary>
        public int PreviousIndex => OldIndex;

        /// <summary>Current index after the change.</summary>
        public int NewIndex { get; } = newIndex;

        /// <summary>The visual effect that was applied to the new slide.</summary>
        public FlowerySlideEffect AppliedEffect { get; } = appliedEffect;

        /// <summary>The current slide element (can be used for dimension debugging).</summary>
        public Visual? CurrentElement { get; } = currentElement;

        public override string ToString()
        {
            return $"OldIndex={OldIndex}, NewIndex={NewIndex}, AppliedEffect={AppliedEffect}";
        }
    }
}
