using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Media;
using Avalonia.Media.Fonts;
using SkiaSharp;

namespace Flowery.Skia
{
    /// <summary>
    /// An embedded font collection that makes the bold and oblique stand-ins of its fonts itself. Use it in place of
    /// <see cref="EmbeddedFontCollection"/>, and <see cref="FlowerySkia.WithSafeInterFont"/> in place of
    /// <c>WithInterFont()</c>, in an app that runs optimized code: published with ReadyToRun or NativeAOT, or running
    /// long enough for the JIT to optimize the font code.
    /// </summary>
    /// <remarks>
    /// When a collection has no face for a requested weight or style, Avalonia makes a stand-in from the bytes that
    /// <c>SkiaTypeface.TryGetStream</c> reads through an <see cref="SKStreamAsset"/>, which it neither disposes nor
    /// keeps alive while Skia reads it. In optimized code the garbage collector can finalize the stream during the
    /// read, and the process ends with an access violation. This collection reads the bytes with the stream held until
    /// it is disposed, makes the stand-in as Avalonia.Skia does, and caches it before Avalonia would make it.
    /// </remarks>
    public sealed class SafeEmbeddedFontCollection : EmbeddedFontCollection
    {
        private static readonly Uri InterKey = new("fonts:Inter");
        private static readonly Uri InterSource = new("avares://Avalonia.Fonts.Inter/Assets");

        private readonly ConcurrentDictionary<(string Family, FontCollectionKey Key), GlyphTypeface> _standIns = new();

        /// <summary>
        /// Creates a collection of the fonts at <paramref name="source"/>.
        /// </summary>
        /// <param name="key">The key of the collection, in the <c>fonts:</c> scheme.</param>
        /// <param name="source">The folder of the font files, such as an <c>avares://</c> URI.</param>
        public SafeEmbeddedFontCollection(Uri key, Uri source) : base(key, source)
        {
        }

        /// <summary>
        /// Creates the collection of the Inter fonts that the Avalonia.Fonts.Inter package holds, under the key that
        /// <c>WithInterFont()</c> uses. The app references that package.
        /// </summary>
        public static SafeEmbeddedFontCollection CreateInter() => new(InterKey, InterSource);

        /// <inheritdoc />
        public override bool TryCreateSyntheticGlyphTypeface(GlyphTypeface glyphTypeface, FontStyle style, FontWeight weight,
            FontStretch stretch, [NotNullWhen(true)] out GlyphTypeface? syntheticGlyphTypeface)
        {
            // The simulations Avalonia chooses, see FontCollectionBase.TryCreateSyntheticGlyphTypeface.
            var simulations = FontSimulations.None;
            if (style != FontStyle.Normal && glyphTypeface.Style != style)
                simulations |= FontSimulations.Oblique;
            if (weight >= FontWeight.DemiBold && glyphTypeface.Weight < weight)
                simulations |= FontSimulations.Bold;
            if (simulations != FontSimulations.None)
            {
                var key = new FontCollectionKey(style, weight, stretch);
                if (!_standIns.ContainsKey((glyphTypeface.FamilyName, key)))
                {
                    if (CreateStandIn(glyphTypeface.PlatformTypeface, simulations) is not { } standIn)
                    {
                        // Without the bytes the regular face stands in, rather than the unsafe read.
                        syntheticGlyphTypeface = null;
                        return false;
                    }
                    _standIns[(glyphTypeface.FamilyName, key)] = standIn;
                    TryAddGlyphTypeface(standIn, key);
                }
            }

            // Finds the stand-in in its cache now, and keeps its own rules for faces that need none.
            return base.TryCreateSyntheticGlyphTypeface(glyphTypeface, style, weight, stretch, out syntheticGlyphTypeface);
        }

        // SkiaTypeface is internal to Avalonia.Skia, but its SKTypeface property and its constructor
        // (SKTypeface, FontSimulations), which Avalonia.Skia's font manager calls for a stand-in, are public.
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties,
            "Avalonia.Skia.SkiaTypeface", "Avalonia.Skia")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The DynamicDependency keeps the members.")]
        private static GlyphTypeface? CreateStandIn(IPlatformTypeface platformTypeface, FontSimulations simulations)
        {
            var type = platformTypeface.GetType();
            if (type.GetProperty("SKTypeface", BindingFlags.Public | BindingFlags.Instance)?.GetValue(platformTypeface) is not SKTypeface typeface
                || type.GetConstructor([typeof(SKTypeface), typeof(FontSimulations)]) is not { } constructor
                || ReadFont(typeface) is not { } data
                || SKTypeface.FromData(SKData.CreateCopy(data)) is not { } copy)
                return null;
            return new GlyphTypeface((IPlatformTypeface)constructor.Invoke([copy, simulations]), simulations);
        }

        private static byte[]? ReadFont(SKTypeface typeface)
        {
            // The using holds the stream until it is disposed, after the read.
            using var asset = typeface.OpenStream();
            if (asset is null || asset.Length <= 0)
                return null;
            var data = new byte[asset.Length];
            return asset.Read(data, data.Length) == data.Length ? data : null;
        }
    }
}
