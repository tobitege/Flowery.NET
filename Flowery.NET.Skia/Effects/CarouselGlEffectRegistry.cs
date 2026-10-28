using System.Collections.Generic;

namespace Flowery.Effects
{
    public enum CarouselGlEffectKind
    {
        None,
        MaskTransition,
        BlindsHorizontal,
        BlindsVertical,
        SlicesHorizontal,
        SlicesVertical,
        Checkerboard,
        Spiral,
        MatrixRain,
        Wormhole,
        Dissolve,
        Pixelate,
        FlipPlane,
        FlipHorizontal,
        FlipVertical,
        CubeLeft,
        CubeRight,
        TextFill,
        Fade,
        FadeThroughBlack,
        FadeThroughWhite,
        SlideLeft,
        SlideRight,
        SlideUp,
        SlideDown,
        PushLeft,
        PushRight,
        PushUp,
        PushDown,
        ZoomIn,
        ZoomOut,
        ZoomCross,
        CoverLeft,
        CoverRight,
        RevealLeft,
        RevealRight,
        WipeLeft,
        WipeRight,
        WipeUp,
        WipeDown,
        Random,
        EffectPanAndZoom,
        EffectZoomIn,
        EffectZoomOut,
        EffectPanLeft,
        EffectPanRight,
        EffectPanUp,
        EffectPanDown,
        EffectDrift,
        EffectPulse,
        EffectBreath,
        EffectThrow
    }

    public sealed class CarouselGlEffectDefinition
    {
        public CarouselGlEffectDefinition(
            CarouselGlEffectKind kind,
            string mode,
            IReadOnlyDictionary<string, string> assets,
            string? variant = null)
        {
            Kind = kind;
            Mode = mode;
            Assets = assets;
            Variant = variant;
        }

        public CarouselGlEffectKind Kind { get; }

        public string Mode { get; }

        public IReadOnlyDictionary<string, string> Assets { get; }

        public string? Variant { get; }
    }

    public static class CarouselGlEffectRegistry
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyAssets = new Dictionary<string, string>();

        private static readonly Dictionary<CarouselGlEffectKind, CarouselGlEffectDefinition> Definitions = new()
        {
            [CarouselGlEffectKind.MaskTransition] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.MaskTransition,
                "mask",
                EmptyAssets,
                "checkerboard"),
            [CarouselGlEffectKind.BlindsHorizontal] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.BlindsHorizontal,
                "mask",
                EmptyAssets,
                "blinds-h"),
            [CarouselGlEffectKind.BlindsVertical] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.BlindsVertical,
                "mask",
                EmptyAssets,
                "blinds-v"),
            [CarouselGlEffectKind.SlicesHorizontal] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.SlicesHorizontal,
                "mask",
                EmptyAssets,
                "slices-h"),
            [CarouselGlEffectKind.SlicesVertical] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.SlicesVertical,
                "mask",
                EmptyAssets,
                "slices-v"),
            [CarouselGlEffectKind.Checkerboard] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.Checkerboard,
                "mask",
                EmptyAssets,
                "checkerboard"),
            [CarouselGlEffectKind.Spiral] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.Spiral,
                "mask",
                EmptyAssets,
                "spiral"),
            [CarouselGlEffectKind.MatrixRain] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.MatrixRain,
                "mask",
                EmptyAssets,
                "matrix"),
            [CarouselGlEffectKind.Wormhole] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.Wormhole,
                "mask",
                EmptyAssets,
                "wormhole"),
            [CarouselGlEffectKind.Dissolve] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.Dissolve,
                "mask",
                EmptyAssets,
                "dissolve"),
            [CarouselGlEffectKind.Pixelate] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.Pixelate,
                "mask",
                EmptyAssets,
                "pixelate"),
            [CarouselGlEffectKind.FlipPlane] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.FlipPlane,
                "flip",
                EmptyAssets,
                "flip-plane"),
            [CarouselGlEffectKind.FlipHorizontal] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.FlipHorizontal,
                "flip",
                EmptyAssets,
                "flip-h"),
            [CarouselGlEffectKind.FlipVertical] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.FlipVertical,
                "flip",
                EmptyAssets,
                "flip-v"),
            [CarouselGlEffectKind.CubeLeft] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.CubeLeft,
                "flip",
                EmptyAssets,
                "cube-left"),
            [CarouselGlEffectKind.CubeRight] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.CubeRight,
                "flip",
                EmptyAssets,
                "cube-right"),
            [CarouselGlEffectKind.TextFill] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.TextFill,
                "text",
                EmptyAssets,
                "text-fill"),
            [CarouselGlEffectKind.Fade] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.Fade,
                "transition",
                EmptyAssets,
                "fade"),
            [CarouselGlEffectKind.FadeThroughBlack] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.FadeThroughBlack,
                "transition",
                EmptyAssets,
                "fade-black"),
            [CarouselGlEffectKind.FadeThroughWhite] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.FadeThroughWhite,
                "transition",
                EmptyAssets,
                "fade-white"),
            [CarouselGlEffectKind.SlideLeft] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.SlideLeft,
                "transition",
                EmptyAssets,
                "slide-left"),
            [CarouselGlEffectKind.SlideRight] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.SlideRight,
                "transition",
                EmptyAssets,
                "slide-right"),
            [CarouselGlEffectKind.SlideUp] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.SlideUp,
                "transition",
                EmptyAssets,
                "slide-up"),
            [CarouselGlEffectKind.SlideDown] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.SlideDown,
                "transition",
                EmptyAssets,
                "slide-down"),
            [CarouselGlEffectKind.PushLeft] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.PushLeft,
                "transition",
                EmptyAssets,
                "push-left"),
            [CarouselGlEffectKind.PushRight] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.PushRight,
                "transition",
                EmptyAssets,
                "push-right"),
            [CarouselGlEffectKind.PushUp] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.PushUp,
                "transition",
                EmptyAssets,
                "push-up"),
            [CarouselGlEffectKind.PushDown] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.PushDown,
                "transition",
                EmptyAssets,
                "push-down"),
            [CarouselGlEffectKind.ZoomIn] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.ZoomIn,
                "transition",
                EmptyAssets,
                "zoom-in"),
            [CarouselGlEffectKind.ZoomOut] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.ZoomOut,
                "transition",
                EmptyAssets,
                "zoom-out"),
            [CarouselGlEffectKind.ZoomCross] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.ZoomCross,
                "transition",
                EmptyAssets,
                "zoom-cross"),
            [CarouselGlEffectKind.CoverLeft] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.CoverLeft,
                "transition",
                EmptyAssets,
                "cover-left"),
            [CarouselGlEffectKind.CoverRight] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.CoverRight,
                "transition",
                EmptyAssets,
                "cover-right"),
            [CarouselGlEffectKind.RevealLeft] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.RevealLeft,
                "transition",
                EmptyAssets,
                "reveal-left"),
            [CarouselGlEffectKind.RevealRight] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.RevealRight,
                "transition",
                EmptyAssets,
                "reveal-right"),
            [CarouselGlEffectKind.WipeLeft] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.WipeLeft,
                "transition",
                EmptyAssets,
                "wipe-left"),
            [CarouselGlEffectKind.WipeRight] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.WipeRight,
                "transition",
                EmptyAssets,
                "wipe-right"),
            [CarouselGlEffectKind.WipeUp] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.WipeUp,
                "transition",
                EmptyAssets,
                "wipe-up"),
            [CarouselGlEffectKind.WipeDown] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.WipeDown,
                "transition",
                EmptyAssets,
                "wipe-down"),
            [CarouselGlEffectKind.Random] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.Random,
                "transition",
                EmptyAssets,
                "random"),
            [CarouselGlEffectKind.EffectPanAndZoom] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectPanAndZoom,
                "effect",
                EmptyAssets,
                "pan-zoom"),
            [CarouselGlEffectKind.EffectZoomIn] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectZoomIn,
                "effect",
                EmptyAssets,
                "zoom-in"),
            [CarouselGlEffectKind.EffectZoomOut] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectZoomOut,
                "effect",
                EmptyAssets,
                "zoom-out"),
            [CarouselGlEffectKind.EffectPanLeft] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectPanLeft,
                "effect",
                EmptyAssets,
                "pan-left"),
            [CarouselGlEffectKind.EffectPanRight] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectPanRight,
                "effect",
                EmptyAssets,
                "pan-right"),
            [CarouselGlEffectKind.EffectPanUp] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectPanUp,
                "effect",
                EmptyAssets,
                "pan-up"),
            [CarouselGlEffectKind.EffectPanDown] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectPanDown,
                "effect",
                EmptyAssets,
                "pan-down"),
            [CarouselGlEffectKind.EffectDrift] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectDrift,
                "effect",
                EmptyAssets,
                "drift"),
            [CarouselGlEffectKind.EffectPulse] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectPulse,
                "effect",
                EmptyAssets,
                "pulse"),
            [CarouselGlEffectKind.EffectBreath] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectBreath,
                "effect",
                EmptyAssets,
                "breath"),
            [CarouselGlEffectKind.EffectThrow] = new CarouselGlEffectDefinition(
                CarouselGlEffectKind.EffectThrow,
                "effect",
                EmptyAssets,
                "throw")
        };

        public static bool TryGet(CarouselGlEffectKind kind, out CarouselGlEffectDefinition? definition)
        {
            return Definitions.TryGetValue(kind, out definition);
        }

        public static void Register(CarouselGlEffectDefinition definition)
        {
            Definitions[definition.Kind] = definition;
        }
    }
}
