using System;
using System.Text;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;
using Flowery.Controls;

namespace Flowery.Helpers
{
    /// <summary>
    /// Parsed WinForms-style mnemonic label information.
    /// </summary>
    public readonly struct FloweryMnemonicInfo
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FloweryMnemonicInfo"/> struct.
        /// </summary>
        /// <param name="displayText">Text with mnemonic markers removed.</param>
        /// <param name="mnemonicIndex">Index of the mnemonic character in <paramref name="displayText"/>, or -1.</param>
        /// <param name="mnemonicChar">Mnemonic character, or null when none.</param>
        public FloweryMnemonicInfo(string displayText, int mnemonicIndex, char? mnemonicChar)
        {
            DisplayText = displayText;
            MnemonicIndex = mnemonicIndex;
            MnemonicChar = mnemonicChar;
        }

        /// <summary>
        /// Gets the display text with <c>&amp;</c> markers removed (<c>&amp;&amp;</c> becomes a literal <c>&amp;</c>).
        /// </summary>
        public string DisplayText { get; }

        /// <summary>
        /// Gets the zero-based index of the mnemonic character in <see cref="DisplayText"/>, or -1 when none.
        /// </summary>
        public int MnemonicIndex { get; }

        /// <summary>
        /// Gets the mnemonic character, or null when the label has no mnemonic.
        /// </summary>
        public char? MnemonicChar { get; }
    }

    /// <summary>
    /// WinForms-style mnemonic parsing helpers (<c>&amp;</c> marks the access character, <c>&amp;&amp;</c> is a literal <c>&amp;</c>).
    /// Display-only: applications must implement their own Alt+key handling.
    /// </summary>
    public static class FloweryMnemonicHelpers
    {
        /// <summary>
        /// Data template that renders string content with WinForms-style mnemonic underlines.
        /// </summary>
        public static readonly IDataTemplate StringContentTemplate =
            new FuncDataTemplate<string>(
                (_, _) =>
                {
                    var result = new DaisyMnemonicText();
                    result.Bind(TextBlock.TextProperty, new Binding());
                    return result;
                },
                true);

        /// <summary>
        /// Parses a WinForms-style mnemonic label.
        /// </summary>
        /// <param name="text">Source text that may contain <c>&amp;</c> markers.</param>
        /// <returns>Display text and optional mnemonic metadata.</returns>
        public static FloweryMnemonicInfo Parse(string? text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return new FloweryMnemonicInfo(string.Empty, -1, null);
            }

            var builder = new StringBuilder(text.Length);
            var mnemonicIndex = -1;
            char? mnemonicChar = null;

            for (var i = 0; i < text.Length; i++)
            {
                var current = text[i];
                if (current != '&')
                {
                    builder.Append(current);
                    continue;
                }

                if (i + 1 >= text.Length)
                {
                    // Trailing '&' is ignored (WinForms behavior).
                    break;
                }

                var next = text[i + 1];
                if (next == '&')
                {
                    builder.Append('&');
                    i++;
                    continue;
                }

                if (mnemonicIndex < 0)
                {
                    mnemonicIndex = builder.Length;
                    mnemonicChar = next;
                }

                builder.Append(next);
                i++;
            }

            return new FloweryMnemonicInfo(builder.ToString(), mnemonicIndex, mnemonicChar);
        }

        /// <summary>
        /// Gets the display text with mnemonic markers removed.
        /// </summary>
        /// <param name="text">Source text that may contain <c>&amp;</c> markers.</param>
        /// <returns>Display text.</returns>
        public static string GetDisplayText(string? text) => Parse(text).DisplayText;

        /// <summary>
        /// Gets the mnemonic character from a WinForms-style label, if present.
        /// </summary>
        /// <param name="text">Source text that may contain <c>&amp;</c> markers.</param>
        /// <returns>Mnemonic character, or null.</returns>
        public static char? GetMnemonicChar(string? text) => Parse(text).MnemonicChar;

        /// <summary>
        /// Registers the shared string mnemonic template on a control host when missing.
        /// </summary>
        /// <param name="host">Host that owns data templates (for example Button, TabControl, or ListBox).</param>
        public static void EnsureStringContentTemplate(IDataTemplateHost host)
        {
            if (host == null)
            {
                throw new ArgumentNullException(nameof(host));
            }

            foreach (var template in host.DataTemplates)
            {
                if (ReferenceEquals(template, StringContentTemplate))
                {
                    return;
                }
            }

            host.DataTemplates.Add(StringContentTemplate);
        }
    }
}
