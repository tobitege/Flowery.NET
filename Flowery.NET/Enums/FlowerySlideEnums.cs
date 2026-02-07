namespace Flowery.Enums
{
    /// <summary>
    /// Specifies the easing mode for animations.
    /// </summary>
    public enum EasingMode
    {
        /// <summary>Acceleration at the start.</summary>
        EaseIn,
        /// <summary>Deceleration at the end.</summary>
        EaseOut,
        /// <summary>Acceleration at the start and deceleration at the end.</summary>
        EaseInOut
    }

    /// <summary>
    /// Specifies visual effects for slide/image animations.
    /// Usable by any control that displays content with cinematic transitions.
    /// </summary>
    public enum FlowerySlideEffect
    {
        /// <summary>No effect - static display.</summary>
        None,
        /// <summary>Pan And Zoom effect - randomized pan and zoom combination for documentary-style motion.</summary>
        PanAndZoom,
        /// <summary>Slow zoom in from 1.0x to 1.0x + intensity.</summary>
        ZoomIn,
        /// <summary>Slow zoom out from 1.0x + intensity to 1.0x.</summary>
        ZoomOut,
        /// <summary>Slow pan to the left.</summary>
        PanLeft,
        /// <summary>Slow pan to the right.</summary>
        PanRight,
        /// <summary>Slow pan upward.</summary>
        PanUp,
        /// <summary>Slow pan downward.</summary>
        PanDown,
        /// <summary>Random direction pan per slide.</summary>
        Drift,
        /// <summary>Subtle breathing/pulsing zoom effect.</summary>
        Pulse,
        /// <summary>Breathing zoom with bounce: zooms in to peak then settles back slightly.</summary>
        Breath,
        /// <summary>Dramatic parallax fly-through: scales up at center while panning across.</summary>
        Throw
    }

    /// <summary>
    /// Specifies visual transitions that occur between slides during navigation.
    /// Unlike SlideEffects (which animate the currently visible slide), SlideTransitions
    /// animate the change FROM one slide TO another.
    /// </summary>
    /// <remarks>
    /// Transitions are categorized by implementation complexity:
    /// - Tier 1 (CompositeTransform): Fade, Slide, Push, Zoom, Flip - works everywhere
    /// - Tier 2 (Clip-based): Wipe, Blinds, Slices - uses animated clip geometries
    /// - Tier 3 (SkiaSharp): Blur, Dissolve, Pixelate - requires Skia-enabled targets
    /// </remarks>
    public enum FlowerySlideTransition
    {
        // === SPECIAL ===
        /// <summary>No transition - instant snap (default behavior).</summary>
        None,
        /// <summary>Pick a random transition for each navigation.</summary>
        Random,

        // === TIER 1: FADE FAMILY (Opacity-based) ===
        /// <summary>Crossfade - old fades out while new fades in.</summary>
        Fade,
        /// <summary>Fade through black - old fades to black, then new fades in from black.</summary>
        FadeThroughBlack,
        /// <summary>Fade through white - old fades to white, then new fades in from white.</summary>
        FadeThroughWhite,

        // === TIER 1: SLIDE FAMILY (New enters, old exits) ===
        /// <summary>New slide enters from right, old exits to left.</summary>
        SlideLeft,
        /// <summary>New slide enters from left, old exits to right.</summary>
        SlideRight,
        /// <summary>New slide enters from bottom, old exits to top.</summary>
        SlideUp,
        /// <summary>New slide enters from top, old exits to bottom.</summary>
        SlideDown,

        // === TIER 1: PUSH FAMILY (New pushes old out) ===
        /// <summary>New slide pushes old to the left.</summary>
        PushLeft,
        /// <summary>New slide pushes old to the right.</summary>
        PushRight,
        /// <summary>New slide pushes old upward.</summary>
        PushUp,
        /// <summary>New slide pushes old downward.</summary>
        PushDown,

        // === TIER 1: ZOOM FAMILY (Scale transitions) ===
        /// <summary>New slide zooms in from small to full size.</summary>
        ZoomIn,
        /// <summary>Old slide zooms out to small, revealing new.</summary>
        ZoomOut,
        /// <summary>Old zooms out while new zooms in (crossover).</summary>
        ZoomCross,

        // === TIER 1: FLIP FAMILY (3D rotation using PlaneProjection) ===
        /// <summary>Card flip on horizontal axis (top/bottom).</summary>
        FlipHorizontal,
        /// <summary>Card flip on vertical axis (left/right).</summary>
        FlipVertical,
        /// <summary>3D cube rotation to the left.</summary>
        CubeLeft,
        /// <summary>3D cube rotation to the right.</summary>
        CubeRight,

        // === TIER 1: COVER/REVEAL FAMILY ===
        /// <summary>New slide covers old from the right (old stays in place).</summary>
        CoverLeft,
        /// <summary>New slide covers old from the left.</summary>
        CoverRight,
        /// <summary>New slide covers old from the bottom.</summary>
        CoverUp,
        /// <summary>New slide covers old from the top.</summary>
        CoverDown,
        /// <summary>Old slide reveals new underneath by exiting left.</summary>
        RevealLeft,
        /// <summary>Old slide reveals new underneath by exiting right.</summary>
        RevealRight,

        // === TIER 2: WIPE FAMILY (Animated clip geometry) ===
        /// <summary>Rectangular wipe from right to left.</summary>
        WipeLeft,
        /// <summary>Rectangular wipe from left to right.</summary>
        WipeRight,
        /// <summary>Rectangular wipe from bottom to top.</summary>
        WipeUp,
        /// <summary>Rectangular wipe from top to bottom.</summary>
        WipeDown,

        // === TIER 2: BLINDS FAMILY (Multiple animated clips) ===
        /// <summary>Horizontal venetian blinds reveal.</summary>
        BlindsHorizontal,
        /// <summary>Vertical blinds reveal.</summary>
        BlindsVertical,

        // === TIER 2: SLICES FAMILY (Random-order strip reveals) ===
        /// <summary>Horizontal slices reveal in random order.</summary>
        SlicesHorizontal,
        /// <summary>Vertical slices reveal in random order.</summary>
        SlicesVertical,
        /// <summary>Grid squares reveal in random order.</summary>
        Checkerboard,
        /// <summary>Grid squares reveal in a spiral pattern from center. </summary>
        Spiral,
        /// <summary>Columns fall down in a digital rain pattern.</summary>
        MatrixRain,
        /// <summary>Extreme radial stretch and vortex effect.</summary>
        Wormhole,

        // === TIER 3: SKIA-BASED (Requires SkiaSharp) ===
        /// <summary>Pixel noise dissolve pattern. Requires SkiaSharp.</summary>
        Dissolve,
        /// <summary>Mosaic pixelation transition. Requires SkiaSharp.</summary>
        Pixelate,
    }

    /// <summary>
    /// Categorizes slide transitions by their implementation requirements.
    /// </summary>
    public enum FloweryTransitionTier
    {
        /// <summary>Uses CompositeTransform/PlaneProjection - works everywhere.</summary>
        Transform,
        /// <summary>Uses animated RectangleGeometry clips - works on most platforms.</summary>
        Clip,
        /// <summary>Requires SkiaSharp rendering - only on Skia-enabled targets.</summary>
        Skia
    }

    /// <summary>
    /// Specifies the navigation mode for slideshow controls.
    /// Usable by carousels, galleries, and other slideshow-style components.
    /// </summary>
    public enum FlowerySlideshowMode
    {
        /// <summary>Manual navigation via user interaction only.</summary>
        Manual,
        /// <summary>Automatic sequential advancement that loops infinitely.</summary>
        Slideshow,
        /// <summary>Automatic random advancement with non-repeating order until all items shown.</summary>
        Random,
        /// <summary>Automatic random advancement with random effects per slide (full kiosk/screensaver mode).</summary>
        Kiosk
    }
}
