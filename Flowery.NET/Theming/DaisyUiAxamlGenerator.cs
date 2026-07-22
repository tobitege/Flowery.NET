using System.Collections.Generic;
using System.Text;

namespace Flowery.Theming
{
    /// <summary>
    /// Generates Avalonia AXAML ResourceDictionary files from parsed DaisyUI themes.
    /// </summary>
    public static class DaisyUiAxamlGenerator
    {
        /// <summary>
        /// Mapping from DaisyUI color keys to Avalonia resource keys.
        /// </summary>
        private static readonly Dictionary<string, string> ColorKeyMapping = new Dictionary<string, string>
        {
            // Base colors
            { "color-base-100", "DaisyBase100" },
            { "color-base-200", "DaisyBase200" },
            { "color-base-300", "DaisyBase300" },
            { "color-base-content", "DaisyBaseContent" },

            // Primary
            { "color-primary", "DaisyPrimary" },
            { "color-primary-content", "DaisyPrimaryContent" },

            // Secondary
            { "color-secondary", "DaisySecondary" },
            { "color-secondary-content", "DaisySecondaryContent" },

            // Accent
            { "color-accent", "DaisyAccent" },
            { "color-accent-content", "DaisyAccentContent" },

            // Neutral
            { "color-neutral", "DaisyNeutral" },
            { "color-neutral-content", "DaisyNeutralContent" },

            // Semantic colors
            { "color-info", "DaisyInfo" },
            { "color-info-content", "DaisyInfoContent" },
            { "color-success", "DaisySuccess" },
            { "color-success-content", "DaisySuccessContent" },
            { "color-warning", "DaisyWarning" },
            { "color-warning-content", "DaisyWarningContent" },
            { "color-error", "DaisyError" },
            { "color-error-content", "DaisyErrorContent" },
        };

        /// <summary>
        /// Generate Avalonia AXAML ResourceDictionary from a parsed theme.
        /// Outputs both Color and SolidColorBrush resources.
        /// </summary>
        /// <param name="theme">The parsed DaisyUI theme.</param>
        /// <returns>AXAML ResourceDictionary content as string.</returns>
        public static string Generate(DaisyUiTheme theme)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"");
            sb.AppendLine("                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
            sb.AppendLine($"    <!-- DaisyUI {theme.Name} Theme Palette -->");
            sb.AppendLine($"    <!-- Color scheme: {(theme.IsDark ? "Dark" : "Light")} -->");

            foreach (var kvp in theme.Colors)
            {
                var avaloniaKey = GetAvaloniaKey(kvp.Key);
                sb.AppendLine($"    <Color x:Key=\"{avaloniaKey}Color\">{kvp.Value}</Color>");
                sb.AppendLine($"    <SolidColorBrush x:Key=\"{avaloniaKey}Brush\" Color=\"{{StaticResource {avaloniaKey}Color}}\" />");
            }

            sb.AppendLine("</ResourceDictionary>");
            return sb.ToString();
        }

        /// <summary>
        /// Generate a combined AXAML with ThemeDictionaries for light and dark themes.
        /// </summary>
        /// <param name="lightTheme">The light theme.</param>
        /// <param name="darkTheme">The dark theme.</param>
        /// <returns>AXAML ResourceDictionary with ThemeDictionaries.</returns>
        public static string GenerateCombined(DaisyUiTheme lightTheme, DaisyUiTheme darkTheme)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<ResourceDictionary xmlns=\"https://github.com/avaloniaui\"");
            sb.AppendLine("                    xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">");
            sb.AppendLine($"    <!-- Generated from DaisyUI themes: Light={lightTheme.Name}, Dark={darkTheme.Name} -->");
            sb.AppendLine();
            sb.AppendLine("    <ResourceDictionary.ThemeDictionaries>");

            // Light theme
            sb.AppendLine("        <ResourceDictionary x:Key=\"Light\">");
            foreach (var kvp in lightTheme.Colors)
            {
                var avaloniaKey = GetAvaloniaKey(kvp.Key);
                sb.AppendLine($"            <Color x:Key=\"{avaloniaKey}Color\">{kvp.Value}</Color>");
                sb.AppendLine($"            <SolidColorBrush x:Key=\"{avaloniaKey}Brush\" Color=\"{{StaticResource {avaloniaKey}Color}}\" />");
            }
            sb.AppendLine("        </ResourceDictionary>");

            // Dark theme
            sb.AppendLine("        <ResourceDictionary x:Key=\"Dark\">");
            foreach (var kvp in darkTheme.Colors)
            {
                var avaloniaKey = GetAvaloniaKey(kvp.Key);
                sb.AppendLine($"            <Color x:Key=\"{avaloniaKey}Color\">{kvp.Value}</Color>");
                sb.AppendLine($"            <SolidColorBrush x:Key=\"{avaloniaKey}Brush\" Color=\"{{StaticResource {avaloniaKey}Color}}\" />");
            }
            sb.AppendLine("        </ResourceDictionary>");

            sb.AppendLine("    </ResourceDictionary.ThemeDictionaries>");
            sb.AppendLine("</ResourceDictionary>");
            return sb.ToString();
        }

        /// <summary>
        /// Get the Avalonia resource key for a DaisyUI color key.
        /// </summary>
        /// <param name="daisyKey">DaisyUI CSS variable key (e.g., "color-primary").</param>
        /// <returns>Avalonia resource key (e.g., "DaisyPrimary").</returns>
        public static string GetAvaloniaKey(string daisyKey)
        {
            return ColorKeyMapping.TryGetValue(daisyKey, out var mapped)
                ? mapped
                : "Daisy" + ToPascalCase(daisyKey.Replace("color-", ""));
        }

        /// <summary>
        /// Convert kebab-case to PascalCase (e.g., "base-100" -> "Base100").
        /// </summary>
        private static string ToPascalCase(string input)
        {
            var parts = input.Split('-');
            var sb = new StringBuilder();
            foreach (var part in parts)
            {
                if (part.Length > 0)
                {
                    sb.Append(char.ToUpper(part[0]));
                    if (part.Length > 1)
                        sb.Append(part.Substring(1));
                }
            }
            return sb.ToString();
        }
    }
}
