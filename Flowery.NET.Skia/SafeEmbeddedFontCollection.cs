using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Avalonia.Logging;
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
    /// read, and the process ends with an access violation. For a Skia typeface this collection makes the stand-in
    /// itself, as Avalonia.Skia does, with every native object held through the call that uses it. Other platform
    /// typefaces keep Avalonia's way.
    /// </remarks>
    public sealed class SafeEmbeddedFontCollection : EmbeddedFontCollection
    {
        private const string SkiaTypefaceName = "Avalonia.Skia.SkiaTypeface";

        private static readonly Uri InterKey = new("fonts:Inter");
        private static readonly Uri InterSource = new("avares://Avalonia.Fonts.Inter/Assets");

        private static int s_reportedMissingMembers;

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

            // The base reads no stream when nothing is simulated, the family is unknown or the stand-in is cached.
            var key = new FontCollectionKey(style, weight, stretch);
            var family = glyphTypeface.FamilyName;
            if (simulations == FontSimulations.None
                || !TryGetFamilyTypefaces(family, out _)
                || (TryGetGlyphTypeface(family, key, allowNearestMatch: false, out var cached) && cached.FontSimulations == simulations))
            {
                return base.TryCreateSyntheticGlyphTypeface(glyphTypeface, style, weight, stretch, out syntheticGlyphTypeface);
            }

            var platformTypeface = glyphTypeface.PlatformTypeface;
            syntheticGlyphTypeface = null;
            if (SkiaMembers(platformTypeface) is not { } members)
            {
                // The unsafe read is SkiaTypeface's; other platform typefaces read their streams themselves.
                if (platformTypeface.GetType().FullName != SkiaTypefaceName)
                    return base.TryCreateSyntheticGlyphTypeface(glyphTypeface, style, weight, stretch, out syntheticGlyphTypeface);

                // Avalonia.Skia changed: the regular face stands in, rather than the unsafe read.
                if (Interlocked.Exchange(ref s_reportedMissingMembers, 1) == 0)
                    Logger.TryGet(LogEventLevel.Warning, LogArea.Fonts)?.Log(this,
                        "{Type} lacks its SKTypeface property or its (SKTypeface, FontSimulations) constructor, so fonts get no bold or oblique stand-ins.",
                        SkiaTypefaceName);
                return false;
            }

            if (members.GetTypeface.GetValue(platformTypeface) is not SKTypeface typeface || CopyTypeface(typeface) is not { } copy)
                return false;
            var standIn = new GlyphTypeface((IPlatformTypeface)members.Constructor.Invoke([copy, simulations]), simulations);
            TryAddGlyphTypeface(standIn, key);

            // Another thread can have cached its stand-in first, as the base keeps the one it finds.
            if (TryGetGlyphTypeface(family, key, allowNearestMatch: false, out var winner) && !ReferenceEquals(winner, standIn)
                && winner.FontSimulations == simulations)
            {
                standIn.Dispose();
                standIn = winner;
            }

            syntheticGlyphTypeface = standIn;
            return true;
        }

        // A typeface with Avalonia.Skia's shape: its SKTypeface property and its constructor (SKTypeface,
        // FontSimulations), which Avalonia.Skia's font manager calls for a stand-in, are public, though the type is
        // internal. Null for a platform typeface without them.
        [DynamicDependency(DynamicallyAccessedMemberTypes.PublicConstructors | DynamicallyAccessedMemberTypes.PublicProperties,
            SkiaTypefaceName, "Avalonia.Skia")]
        [UnconditionalSuppressMessage("Trimming", "IL2075", Justification = "The DynamicDependency keeps the members.")]
        private static (PropertyInfo GetTypeface, ConstructorInfo Constructor)? SkiaMembers(IPlatformTypeface platformTypeface)
        {
            var type = platformTypeface.GetType();
            if (type.GetProperty("SKTypeface", BindingFlags.Public | BindingFlags.Instance) is { } property
                && property.PropertyType == typeof(SKTypeface)
                && type.GetConstructor([typeof(SKTypeface), typeof(FontSimulations)]) is { } constructor)
                return (property, constructor);
            return null;
        }

        private static SKTypeface? CopyTypeface(SKTypeface typeface)
        {
            // SKData.Create keeps the stream alive through its native read, and each using keeps its object alive
            // through the native call after it.
            using var asset = typeface.OpenStream();
            if (asset is null || asset.Length <= 0)
                return null;
            using var data = SKData.Create(asset);
            return data is null ? null : SKTypeface.FromData(data);
        }
    }
}
