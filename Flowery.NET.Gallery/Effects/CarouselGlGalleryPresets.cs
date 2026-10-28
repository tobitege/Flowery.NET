using System.Collections.Generic;
using Flowery.Effects;

namespace Flowery.NET.Gallery.Effects
{
    public static class CarouselGlGalleryPresets
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
            RegisterGroupWithWallpaperPairs(MaskKinds, "maskA", "maskB");
            RegisterGroup(FlipAssets, FlipKinds);
            RegisterGroup(TextAssets, TextKinds);
            RegisterGroupWithWallpaperPairs(TransitionKinds, "transitionA", "transitionB");
            RegisterGroupWithWallpaperSingles(EffectKinds, "effect");
        }

        private static void RegisterGroup(IReadOnlyDictionary<string, string> assets, CarouselGlEffectKind[] kinds)
        {
            foreach (var kind in kinds)
            {
                if (!CarouselGlEffectRegistry.TryGet(kind, out var definition) || definition is null)
                {
                    continue;
                }

                CarouselGlEffectRegistry.Register(
                    new CarouselGlEffectDefinition(kind, definition.Mode, assets, definition.Variant));
            }
        }

        private static void RegisterGroupWithWallpaperPairs(CarouselGlEffectKind[] kinds, string keyA, string keyB)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                if (!CarouselGlEffectRegistry.TryGet(kind, out var definition) || definition is null)
                {
                    continue;
                }

                var assets = CreateWallpaperPair(keyA, keyB, i);
                CarouselGlEffectRegistry.Register(
                    new CarouselGlEffectDefinition(kind, definition.Mode, assets, definition.Variant));
            }
        }

        private static void RegisterGroupWithWallpaperSingles(CarouselGlEffectKind[] kinds, string key)
        {
            for (int i = 0; i < kinds.Length; i++)
            {
                var kind = kinds[i];
                if (!CarouselGlEffectRegistry.TryGet(kind, out var definition) || definition is null)
                {
                    continue;
                }

                var assets = CreateWallpaperSingle(key, i);
                CarouselGlEffectRegistry.Register(
                    new CarouselGlEffectDefinition(kind, definition.Mode, assets, definition.Variant));
            }
        }

        private static IReadOnlyDictionary<string, string> CreateWallpaperPair(string keyA, string keyB, int offset = 0)
        {
            var first = WallpaperAssets[offset % WallpaperAssets.Length];
            var second = WallpaperAssets[(offset + 1) % WallpaperAssets.Length];
            return new Dictionary<string, string>
            {
                [keyA] = first,
                [keyB] = second
            };
        }

        private static IReadOnlyDictionary<string, string> CreateWallpaperSingle(string key, int offset = 0)
        {
            return new Dictionary<string, string>
            {
                [key] = WallpaperAssets[offset % WallpaperAssets.Length]
            };
        }
    }
}
