namespace Flowery.Enums
{
    /// <summary>
    /// Animated backgrounds for a glass surface. <c>DaisyGlassGradientBackground</c> draws them with moving gradients;
    /// <c>DaisyGlassShaderBackground</c> in Flowery.NET.Skia draws them with shaders.
    /// </summary>
    public enum DaisyGlassAnimation
    {
        /// <summary>Flowing magenta, cyan, violet and orange color fields.</summary>
        Plasma,
        /// <summary>Green, teal and violet curtains drifting over a night sky.</summary>
        Aurora,
        /// <summary>Molten gold with moving sheen and glints.</summary>
        LiquidGold,
        /// <summary>
        /// Molten metal like <see cref="LiquidGold"/> in <c>DaisyGlassBackground.EffectColor</c>, or in the accent color of the
        /// system when no color is set.
        /// </summary>
        LiquidColor
    }
}
