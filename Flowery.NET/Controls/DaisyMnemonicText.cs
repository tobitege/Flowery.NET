using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Flowery.Helpers;

namespace Flowery.Controls
{
    /// <summary>
    /// Displays WinForms-style mnemonic labels: <c>&amp;</c> marks the access character (underlined),
    /// <c>&amp;&amp;</c> is a literal <c>&amp;</c>. Display-only; applications must handle Alt+key themselves.
    /// </summary>
    public class DaisyMnemonicText : TextBlock
    {
        /// <summary>
        /// Defines the <see cref="ShowMnemonic"/> property.
        /// </summary>
        public static readonly StyledProperty<bool> ShowMnemonicProperty =
            AvaloniaProperty.Register<DaisyMnemonicText, bool>(nameof(ShowMnemonic), true);

        private char? _mnemonicChar;

        /// <summary>
        /// Gets or sets whether the mnemonic character is underlined.
        /// </summary>
        public bool ShowMnemonic
        {
            get => GetValue(ShowMnemonicProperty);
            set => SetValue(ShowMnemonicProperty, value);
        }

        /// <summary>
        /// Gets the mnemonic character parsed from <see cref="TextBlock.Text"/>, or null when none.
        /// </summary>
        public char? MnemonicChar => _mnemonicChar;

        /// <inheritdoc/>
        protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
        {
            base.OnPropertyChanged(change);

            if (change.Property == TextProperty || change.Property == ShowMnemonicProperty)
            {
                ApplyMnemonicPresentation();
            }
        }

        private void ApplyMnemonicPresentation()
        {
            var info = FloweryMnemonicHelpers.Parse(Text);
            _mnemonicChar = info.MnemonicChar;

            var inlines = new InlineCollection();
            var display = info.DisplayText ?? string.Empty;

            if (string.IsNullOrEmpty(display))
            {
                Inlines = inlines;
                return;
            }

            if (!ShowMnemonic || info.MnemonicIndex < 0 || info.MnemonicIndex >= display.Length)
            {
                inlines.Add(new Run(display));
                Inlines = inlines;
                return;
            }

            if (info.MnemonicIndex > 0)
            {
                inlines.Add(new Run(display.Substring(0, info.MnemonicIndex)));
            }

            inlines.Add(new Run(display.Substring(info.MnemonicIndex, 1))
            {
                TextDecorations = Avalonia.Media.TextDecorations.Underline
            });

            if (info.MnemonicIndex + 1 < display.Length)
            {
                inlines.Add(new Run(display.Substring(info.MnemonicIndex + 1)));
            }

            Inlines = inlines;
        }
    }
}
