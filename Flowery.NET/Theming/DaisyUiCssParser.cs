using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

namespace Flowery.Theming
{
    /// <summary>
    /// Parses DaisyUI CSS theme files and extracts color/design token values.
    /// </summary>
    public static class DaisyUiCssParser
    {
        /// <summary>
        /// Parse a DaisyUI CSS theme file from disk.
        /// </summary>
        /// <param name="filePath">Path to the CSS file.</param>
        /// <returns>Parsed theme with colors converted to hex.</returns>
        public static DaisyUiTheme ParseFile(string filePath)
        {
            var content = File.ReadAllText(filePath);
            var themeName = Path.GetFileNameWithoutExtension(filePath);
            return Parse(content, themeName);
        }

        /// <summary>
        /// Parse DaisyUI CSS content string.
        /// </summary>
        /// <param name="cssContent">Raw CSS content.</param>
        /// <param name="themeName">Theme name (defaults to "custom").</param>
        /// <returns>Parsed theme with colors converted to hex.</returns>
        public static DaisyUiTheme Parse(string cssContent, string themeName = "custom")
        {
            return Parse(cssContent, themeName, out _);
        }

        /// <summary>
        /// Parse DaisyUI CSS content string with error reporting.
        /// </summary>
        /// <param name="cssContent">Raw CSS content.</param>
        /// <param name="themeName">Theme name.</param>
        /// <param name="parseErrors">List of color keys that failed to parse, with error messages.</param>
        /// <returns>Parsed theme with colors converted to hex.</returns>
        public static DaisyUiTheme Parse(string cssContent, string themeName, out List<string> parseErrors)
        {
            var theme = new DaisyUiTheme { Name = themeName };
            parseErrors = new List<string>();

            // Check color scheme declaration
            var schemeMatch = Regex.Match(cssContent, @"color-scheme:\s*(light|dark)", RegexOptions.IgnoreCase);
            theme.IsDark = schemeMatch.Success && schemeMatch.Groups[1].Value.ToLower() == "dark";

            // Parse OKLCH colors: --color-xxx: oklch(L% C H);
            var colorPattern = new Regex(@"--color-([a-z0-9-]+):\s*oklch\(([^)]+)\)", RegexOptions.IgnoreCase);
            foreach (Match match in colorPattern.Matches(cssContent))
            {
                var key = "color-" + match.Groups[1].Value;
                var oklchValue = match.Groups[2].Value.Trim();

                try
                {
                    var hexColor = ColorConverter.OklchToHex(oklchValue);
                    theme.Colors[key] = hexColor;
                }
                catch (Exception ex)
                {
                    parseErrors.Add($"{key}: '{oklchValue}' - {ex.Message}");
                }
            }

            // Parse radius values: --radius-xxx: Nrem;
            var radiusPattern = new Regex(@"--radius-([a-z]+):\s*([0-9.]+rem)", RegexOptions.IgnoreCase);
            foreach (Match match in radiusPattern.Matches(cssContent))
            {
                var key = "radius-" + match.Groups[1].Value;
                theme.Radii[key] = match.Groups[2].Value;
            }

            return theme;
        }
    }
}
