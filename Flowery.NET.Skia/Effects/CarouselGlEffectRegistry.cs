using System.Diagnostics.CodeAnalysis;

namespace Flowery.Effects
{
    /// <summary>
    /// The registered effects of <see cref="CarouselGlEffectHost"/>, by name.
    /// </summary>
    public enum CarouselGlEffectKind
    {
        /// <summary>No effect; the host draws nothing.</summary>
        None,
        /// <summary>A checkerboard mask between two images.</summary>
        MaskTransition,
        /// <summary>Horizontal blinds.</summary>
        BlindsHorizontal,
        /// <summary>Vertical blinds.</summary>
        BlindsVertical,
        /// <summary>Horizontal slices that slide in.</summary>
        SlicesHorizontal,
        /// <summary>Vertical slices that slide in.</summary>
        SlicesVertical,
        /// <summary>A checkerboard mask.</summary>
        Checkerboard,
        /// <summary>A spiral mask.</summary>
        Spiral,
        /// <summary>Falling columns, as matrix rain.</summary>
        MatrixRain,
        /// <summary>A swirling wormhole mask.</summary>
        Wormhole,
        /// <summary>A dissolve through random grains.</summary>
        Dissolve,
        /// <summary>Pixelates the first image and resolves the second.</summary>
        Pixelate,
        /// <summary>A plane that flips around its vertical axis.</summary>
        FlipPlane,
        /// <summary>A flip around the horizontal axis.</summary>
        FlipHorizontal,
        /// <summary>A flip around the vertical axis.</summary>
        FlipVertical,
        /// <summary>A cube that turns to the left.</summary>
        CubeLeft,
        /// <summary>A cube that turns to the right.</summary>
        CubeRight,
        /// <summary>An image moving inside the letters of a text.</summary>
        TextFill,
        /// <summary>A crossfade.</summary>
        Fade,
        /// <summary>A fade through black.</summary>
        FadeThroughBlack,
        /// <summary>A fade through white.</summary>
        FadeThroughWhite,
        /// <summary>A slide transition named after the direction left.</summary>
        SlideLeft,
        /// <summary>A slide transition named after the direction right.</summary>
        SlideRight,
        /// <summary>A slide transition named after the direction up.</summary>
        SlideUp,
        /// <summary>A slide transition named after the direction down.</summary>
        SlideDown,
        /// <summary>A push transition named after the direction left.</summary>
        PushLeft,
        /// <summary>A push transition named after the direction right.</summary>
        PushRight,
        /// <summary>A push transition named after the direction up.</summary>
        PushUp,
        /// <summary>A push transition named after the direction down.</summary>
        PushDown,
        /// <summary>A zoom transition that scales the new image up to its size.</summary>
        ZoomIn,
        /// <summary>A zoom transition that scales the new image down to its size.</summary>
        ZoomOut,
        /// <summary>A zoom transition that scales both images.</summary>
        ZoomCross,
        /// <summary>A cover transition named after the direction left.</summary>
        CoverLeft,
        /// <summary>A cover transition named after the direction right.</summary>
        CoverRight,
        /// <summary>A reveal transition named after the direction left.</summary>
        RevealLeft,
        /// <summary>A reveal transition named after the direction right.</summary>
        RevealRight,
        /// <summary>A wipe transition named after the direction left.</summary>
        WipeLeft,
        /// <summary>A wipe transition named after the direction right.</summary>
        WipeRight,
        /// <summary>A wipe transition named after the direction up.</summary>
        WipeUp,
        /// <summary>A wipe transition named after the direction down.</summary>
        WipeDown,
        /// <summary>A transition picked at random from the fades, slides, pushes, covers, reveals, wipes and zooms.</summary>
        Random,
        /// <summary>A continuous pan and zoom of one image.</summary>
        EffectPanAndZoom,
        /// <summary>A continuous zoom in.</summary>
        EffectZoomIn,
        /// <summary>A continuous zoom out.</summary>
        EffectZoomOut,
        /// <summary>A continuous pan to the left.</summary>
        EffectPanLeft,
        /// <summary>A continuous pan to the right.</summary>
        EffectPanRight,
        /// <summary>A continuous pan upward.</summary>
        EffectPanUp,
        /// <summary>A continuous pan downward.</summary>
        EffectPanDown,
        /// <summary>A slow drift with a slight turn.</summary>
        EffectDrift,
        /// <summary>A pulse in size and opacity.</summary>
        EffectPulse,
        /// <summary>A slow breath in size and opacity.</summary>
        EffectBreath,
        /// <summary>A throw across the area.</summary>
        EffectThrow
    }

    /// <summary>
    /// The program family that draws an effect, with the asset keys of its images.
    /// </summary>
    public enum CarouselGlMode
    {
        /// <summary>A mask pattern between two images: <c>maskA</c> and <c>maskB</c>.</summary>
        Mask,
        /// <summary>A flip or cube between two images: <c>flipA</c> and <c>flipB</c>.</summary>
        Flip,
        /// <summary>A transition between two images: <c>transitionA</c> and <c>transitionB</c>.</summary>
        Transition,
        /// <summary>A continuous effect on one image: <c>effect</c>.</summary>
        Effect,
        /// <summary>One image inside the letters of a text: <c>text</c>.</summary>
        Text
    }

    /// <summary>
    /// A registered effect: its mode, its variant within the mode, and the images it draws.
    /// </summary>
    /// <param name="kind">The effect.</param>
    /// <param name="mode">The program family that draws it.</param>
    /// <param name="assets">The images by asset key of the mode, as <c>avares://</c> URIs, paths, data URIs or URLs.</param>
    /// <param name="variant">The variant within the mode, such as <c>checkerboard</c> or <c>slide-left</c>.</param>
    public sealed class CarouselGlEffectDefinition(
        CarouselGlEffectKind kind,
        CarouselGlMode mode,
        IReadOnlyDictionary<string, string> assets,
        string? variant = null)
    {
        /// <summary>Gets the effect.</summary>
        public CarouselGlEffectKind Kind { get; } = kind;

        /// <summary>Gets the program family that draws the effect.</summary>
        public CarouselGlMode Mode { get; } = mode;

        /// <summary>Gets the images by asset key of the mode.</summary>
        public IReadOnlyDictionary<string, string> Assets { get; } = assets;

        /// <summary>Gets the variant within the mode.</summary>
        public string? Variant { get; } = variant;
    }

    /// <summary>
    /// The effects that <see cref="CarouselGlEffectHost"/> can draw. The built-in definitions have no images; an app
    /// registers its own with <see cref="Register"/> before a host loads.
    /// </summary>
    public static class CarouselGlEffectRegistry
    {
        private static readonly IReadOnlyDictionary<string, string> EmptyAssets = new Dictionary<string, string>();

        private static readonly Dictionary<CarouselGlEffectKind, CarouselGlEffectDefinition> Definitions = CreateDefinitions();

        private static Dictionary<CarouselGlEffectKind, CarouselGlEffectDefinition> CreateDefinitions()
        {
            (CarouselGlEffectKind Kind, CarouselGlMode Mode, string Variant)[] table =
            [
                (CarouselGlEffectKind.MaskTransition, CarouselGlMode.Mask, "checkerboard"),
                (CarouselGlEffectKind.BlindsHorizontal, CarouselGlMode.Mask, "blinds-h"),
                (CarouselGlEffectKind.BlindsVertical, CarouselGlMode.Mask, "blinds-v"),
                (CarouselGlEffectKind.SlicesHorizontal, CarouselGlMode.Mask, "slices-h"),
                (CarouselGlEffectKind.SlicesVertical, CarouselGlMode.Mask, "slices-v"),
                (CarouselGlEffectKind.Checkerboard, CarouselGlMode.Mask, "checkerboard"),
                (CarouselGlEffectKind.Spiral, CarouselGlMode.Mask, "spiral"),
                (CarouselGlEffectKind.MatrixRain, CarouselGlMode.Mask, "matrix"),
                (CarouselGlEffectKind.Wormhole, CarouselGlMode.Mask, "wormhole"),
                (CarouselGlEffectKind.Dissolve, CarouselGlMode.Mask, "dissolve"),
                (CarouselGlEffectKind.Pixelate, CarouselGlMode.Mask, "pixelate"),
                (CarouselGlEffectKind.FlipPlane, CarouselGlMode.Flip, "flip-plane"),
                (CarouselGlEffectKind.FlipHorizontal, CarouselGlMode.Flip, "flip-h"),
                (CarouselGlEffectKind.FlipVertical, CarouselGlMode.Flip, "flip-v"),
                (CarouselGlEffectKind.CubeLeft, CarouselGlMode.Flip, "cube-left"),
                (CarouselGlEffectKind.CubeRight, CarouselGlMode.Flip, "cube-right"),
                (CarouselGlEffectKind.TextFill, CarouselGlMode.Text, "text-fill"),
                (CarouselGlEffectKind.Fade, CarouselGlMode.Transition, "fade"),
                (CarouselGlEffectKind.FadeThroughBlack, CarouselGlMode.Transition, "fade-black"),
                (CarouselGlEffectKind.FadeThroughWhite, CarouselGlMode.Transition, "fade-white"),
                (CarouselGlEffectKind.SlideLeft, CarouselGlMode.Transition, "slide-left"),
                (CarouselGlEffectKind.SlideRight, CarouselGlMode.Transition, "slide-right"),
                (CarouselGlEffectKind.SlideUp, CarouselGlMode.Transition, "slide-up"),
                (CarouselGlEffectKind.SlideDown, CarouselGlMode.Transition, "slide-down"),
                (CarouselGlEffectKind.PushLeft, CarouselGlMode.Transition, "push-left"),
                (CarouselGlEffectKind.PushRight, CarouselGlMode.Transition, "push-right"),
                (CarouselGlEffectKind.PushUp, CarouselGlMode.Transition, "push-up"),
                (CarouselGlEffectKind.PushDown, CarouselGlMode.Transition, "push-down"),
                (CarouselGlEffectKind.ZoomIn, CarouselGlMode.Transition, "zoom-in"),
                (CarouselGlEffectKind.ZoomOut, CarouselGlMode.Transition, "zoom-out"),
                (CarouselGlEffectKind.ZoomCross, CarouselGlMode.Transition, "zoom-cross"),
                (CarouselGlEffectKind.CoverLeft, CarouselGlMode.Transition, "cover-left"),
                (CarouselGlEffectKind.CoverRight, CarouselGlMode.Transition, "cover-right"),
                (CarouselGlEffectKind.RevealLeft, CarouselGlMode.Transition, "reveal-left"),
                (CarouselGlEffectKind.RevealRight, CarouselGlMode.Transition, "reveal-right"),
                (CarouselGlEffectKind.WipeLeft, CarouselGlMode.Transition, "wipe-left"),
                (CarouselGlEffectKind.WipeRight, CarouselGlMode.Transition, "wipe-right"),
                (CarouselGlEffectKind.WipeUp, CarouselGlMode.Transition, "wipe-up"),
                (CarouselGlEffectKind.WipeDown, CarouselGlMode.Transition, "wipe-down"),
                (CarouselGlEffectKind.Random, CarouselGlMode.Transition, "random"),
                (CarouselGlEffectKind.EffectPanAndZoom, CarouselGlMode.Effect, "pan-zoom"),
                (CarouselGlEffectKind.EffectZoomIn, CarouselGlMode.Effect, "zoom-in"),
                (CarouselGlEffectKind.EffectZoomOut, CarouselGlMode.Effect, "zoom-out"),
                (CarouselGlEffectKind.EffectPanLeft, CarouselGlMode.Effect, "pan-left"),
                (CarouselGlEffectKind.EffectPanRight, CarouselGlMode.Effect, "pan-right"),
                (CarouselGlEffectKind.EffectPanUp, CarouselGlMode.Effect, "pan-up"),
                (CarouselGlEffectKind.EffectPanDown, CarouselGlMode.Effect, "pan-down"),
                (CarouselGlEffectKind.EffectDrift, CarouselGlMode.Effect, "drift"),
                (CarouselGlEffectKind.EffectPulse, CarouselGlMode.Effect, "pulse"),
                (CarouselGlEffectKind.EffectBreath, CarouselGlMode.Effect, "breath"),
                (CarouselGlEffectKind.EffectThrow, CarouselGlMode.Effect, "throw")
            ];
            var definitions = new Dictionary<CarouselGlEffectKind, CarouselGlEffectDefinition>(table.Length);
            foreach (var (kind, mode, variant) in table)
                definitions.Add(kind, new CarouselGlEffectDefinition(kind, mode, EmptyAssets, variant));
            return definitions;
        }

        /// <summary>
        /// Gets the definition of <paramref name="kind"/>; returns false for <see cref="CarouselGlEffectKind.None"/>.
        /// </summary>
        public static bool TryGet(CarouselGlEffectKind kind, [NotNullWhen(true)] out CarouselGlEffectDefinition? definition) =>
            Definitions.TryGetValue(kind, out definition);

        /// <summary>
        /// Adds a definition or replaces the one of its <see cref="CarouselGlEffectDefinition.Kind"/>, such as one with
        /// the images of an app. A host that is already loaded picks it up with its next property change.
        /// </summary>
        public static void Register(CarouselGlEffectDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(definition);
            Definitions[definition.Kind] = definition;
        }
    }
}
