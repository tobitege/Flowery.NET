using System;
using Avalonia.Media;
using Avalonia.Controls;

namespace Flowery.Theming
{
    /// <summary>
    /// Holds all color values for a DaisyUI theme palette.
    /// Matches the resource keys used by existing AXAML palettes.
    /// </summary>
    public sealed class ThemePalette
    {
        public string Primary { get; set; } = "#605DFF";
        public string PrimaryFocus { get; set; } = "#4C4ACC";
        public string PrimaryContent { get; set; } = "#FFFFFF";

        public string Secondary { get; set; } = "#F43098";
        public string SecondaryFocus { get; set; } = "#C32679";
        public string SecondaryContent { get; set; } = "#FFFFFF";

        public string Accent { get; set; } = "#00D3BB";
        public string AccentFocus { get; set; } = "#00A895";
        public string AccentContent { get; set; } = "#FFFFFF";

        public string Neutral { get; set; } = "#09090B";
        public string NeutralFocus { get; set; } = "#070708";
        public string NeutralContent { get; set; } = "#E4E4E7";

        public string Base100 { get; set; } = "#1D232A";
        public string Base200 { get; set; } = "#191E24";
        public string Base300 { get; set; } = "#374151";
        public string BaseContent { get; set; } = "#FFFFFF";

        public string Info { get; set; } = "#00BAFE";
        public string InfoContent { get; set; } = "#000000";
        public string Success { get; set; } = "#00D390";
        public string SuccessContent { get; set; } = "#000000";
        public string Warning { get; set; } = "#FCB700";
        public string WarningContent { get; set; } = "#000000";
        public string Error { get; set; } = "#FF627D";
        public string ErrorContent { get; set; } = "#000000";
    }

    /// <summary>
    /// Factory for creating Avalonia ResourceDictionaries from ThemePalette data.
    /// Used by product themes to avoid generating 96 AXAML palette files.
    /// </summary>
    public static class DaisyPaletteFactory
    {
        public static ResourceDictionary Create(ThemePalette palette)
        {
            if (palette == null) throw new ArgumentNullException(nameof(palette));

            var dict = new ResourceDictionary();

            AddColorAndBrush(dict, "DaisyPrimary", palette.Primary);
            AddColorAndBrush(dict, "DaisyPrimaryFocus", palette.PrimaryFocus);
            AddColorAndBrush(dict, "DaisyPrimaryContent", palette.PrimaryContent);

            AddColorAndBrush(dict, "DaisySecondary", palette.Secondary);
            AddColorAndBrush(dict, "DaisySecondaryFocus", palette.SecondaryFocus);
            AddColorAndBrush(dict, "DaisySecondaryContent", palette.SecondaryContent);

            AddColorAndBrush(dict, "DaisyAccent", palette.Accent);
            AddColorAndBrush(dict, "DaisyAccentFocus", palette.AccentFocus);
            AddColorAndBrush(dict, "DaisyAccentContent", palette.AccentContent);

            AddColorAndBrush(dict, "DaisyNeutral", palette.Neutral);
            AddColorAndBrush(dict, "DaisyNeutralFocus", palette.NeutralFocus);
            AddColorAndBrush(dict, "DaisyNeutralContent", palette.NeutralContent);

            AddColorAndBrush(dict, "DaisyBase100", palette.Base100);
            AddColorAndBrush(dict, "DaisyBase200", palette.Base200);
            AddColorAndBrush(dict, "DaisyBase300", palette.Base300);
            AddColorAndBrush(dict, "DaisyBaseContent", palette.BaseContent);

            AddColorAndBrush(dict, "DaisyInfo", palette.Info);
            AddColorAndBrush(dict, "DaisyInfoContent", palette.InfoContent);
            AddColorAndBrush(dict, "DaisySuccess", palette.Success);
            AddColorAndBrush(dict, "DaisySuccessContent", palette.SuccessContent);
            AddColorAndBrush(dict, "DaisyWarning", palette.Warning);
            AddColorAndBrush(dict, "DaisyWarningContent", palette.WarningContent);
            AddColorAndBrush(dict, "DaisyError", palette.Error);
            AddColorAndBrush(dict, "DaisyErrorContent", palette.ErrorContent);

            return dict;
        }

        private static void AddColorAndBrush(ResourceDictionary dict, string keyPrefix, string hex)
        {
            var color = TryParseColor(hex);
            dict[keyPrefix + "Color"] = color;
            dict[keyPrefix + "Brush"] = new SolidColorBrush(color);
        }

        private static Color TryParseColor(string hex)
        {
            return Color.TryParse(hex, out var parsed) ? parsed : Colors.Transparent;
        }
    }
}
