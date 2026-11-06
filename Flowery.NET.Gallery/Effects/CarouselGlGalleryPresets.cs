using System;
using System.Collections.Generic;
using Flowery.Effects;

namespace Flowery.NET.Gallery.Effects
{
    internal static class CarouselGlGalleryPresets
    {
        private static bool _registered;

        // Avalonia resources: Android and the browser have no application files.
        private static readonly string[] WallpaperAssets =
        [
            "avares://Flowery.NET.Gallery/Assets/wallpaper_blue.jpg",
            "avares://Flowery.NET.Gallery/Assets/wallpaper_green.jpg",
            "avares://Flowery.NET.Gallery/Assets/wallpaper_red.jpg",
            "avares://Flowery.NET.Gallery/Assets/wallpaper_yellow.jpg"
        ];

        private static readonly IReadOnlyDictionary<string, string> FlipAssets = new Dictionary<string, string>
        {
            ["flipA"] = "avares://Flowery.NET.Gallery/Assets/tile_blue.jpg",
            ["flipB"] = "avares://Flowery.NET.Gallery/Assets/tile_green.jpg"
        };

        private static readonly IReadOnlyDictionary<string, string> TextAssets = new Dictionary<string, string>
        {
            ["text"] = "avares://Flowery.NET.Gallery/Assets/tile_purple.jpg"
        };

        private static readonly CarouselGlEffectKind[] MaskKinds =
        [
            CarouselGlEffectKind.MaskTransition,
            CarouselGlEffectKind.BlindsHorizontal,
            CarouselGlEffectKind.BlindsVertical,
            CarouselGlEffectKind.SlicesHorizontal,
            CarouselGlEffectKind.SlicesVertical,
            CarouselGlEffectKind.Checkerboard,
            CarouselGlEffectKind.Spiral,
            CarouselGlEffectKind.MatrixRain,
            CarouselGlEffectKind.Wormhole,
            CarouselGlEffectKind.Dissolve,
            CarouselGlEffectKind.Pixelate
        ];

        private static readonly CarouselGlEffectKind[] FlipKinds =
        [
            CarouselGlEffectKind.FlipPlane,
            CarouselGlEffectKind.FlipHorizontal,
            CarouselGlEffectKind.FlipVertical,
            CarouselGlEffectKind.CubeLeft,
            CarouselGlEffectKind.CubeRight
        ];

        private static readonly CarouselGlEffectKind[] TextKinds =
        [
            CarouselGlEffectKind.TextFill
        ];

        private static readonly CarouselGlEffectKind[] TransitionKinds =
        [
            CarouselGlEffectKind.Fade,
            CarouselGlEffectKind.FadeThroughBlack,
            CarouselGlEffectKind.FadeThroughWhite,
            CarouselGlEffectKind.SlideLeft,
            CarouselGlEffectKind.SlideRight,
            CarouselGlEffectKind.SlideUp,
            CarouselGlEffectKind.SlideDown,
            CarouselGlEffectKind.PushLeft,
            CarouselGlEffectKind.PushRight,
            CarouselGlEffectKind.PushUp,
            CarouselGlEffectKind.PushDown,
            CarouselGlEffectKind.ZoomIn,
            CarouselGlEffectKind.ZoomOut,
            CarouselGlEffectKind.ZoomCross,
            CarouselGlEffectKind.CoverLeft,
            CarouselGlEffectKind.CoverRight,
            CarouselGlEffectKind.RevealLeft,
            CarouselGlEffectKind.RevealRight,
            CarouselGlEffectKind.WipeLeft,
            CarouselGlEffectKind.WipeRight,
            CarouselGlEffectKind.WipeUp,
            CarouselGlEffectKind.WipeDown,
            CarouselGlEffectKind.Random
        ];

        private static readonly CarouselGlEffectKind[] EffectKinds =
        [
            CarouselGlEffectKind.EffectPanAndZoom,
            CarouselGlEffectKind.EffectZoomIn,
            CarouselGlEffectKind.EffectZoomOut,
            CarouselGlEffectKind.EffectPanLeft,
            CarouselGlEffectKind.EffectPanRight,
            CarouselGlEffectKind.EffectPanUp,
            CarouselGlEffectKind.EffectPanDown,
            CarouselGlEffectKind.EffectDrift,
            CarouselGlEffectKind.EffectPulse,
            CarouselGlEffectKind.EffectBreath,
            CarouselGlEffectKind.EffectThrow
        ];

        public static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }

            _registered = true;
            // A pair or a single wallpaper per effect, each effect starting one wallpaper further.
            Register(MaskKinds, static i => WallpaperPair("maskA", "maskB", i));
            Register(FlipKinds, static _ => FlipAssets);
            Register(TextKinds, static _ => TextAssets);
            Register(TransitionKinds, static i => WallpaperPair("transitionA", "transitionB", i));
            Register(EffectKinds, static i => new Dictionary<string, string> { ["effect"] = Wallpaper(i) });
        }

        // Registers the effects again with the images of the Gallery.
        private static void Register(CarouselGlEffectKind[] kinds, Func<int, IReadOnlyDictionary<string, string>> assets)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                if (CarouselGlEffectRegistry.TryGet(kinds[i], out var definition))
                {
                    CarouselGlEffectRegistry.Register(
                        new CarouselGlEffectDefinition(kinds[i], definition.Mode, assets(i), definition.Variant));
                }
            }
        }

        private static IReadOnlyDictionary<string, string> WallpaperPair(string keyA, string keyB, int offset) =>
            new Dictionary<string, string>
            {
                [keyA] = Wallpaper(offset),
                [keyB] = Wallpaper(offset + 1)
            };

        private static string Wallpaper(int index) => WallpaperAssets[index % WallpaperAssets.Length];
    }
}
