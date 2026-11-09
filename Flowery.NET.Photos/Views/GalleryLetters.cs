using System.Text;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;

namespace Flowery.NET.Photos.Views;

/// <summary>
/// The first letters of the file names beside a gallery sorted by name: digits, A to Z, the umlauts and # for anything
/// else; the digits and the umlauts are optional. A click goes to the first file with that letter; letters that no file
/// starts with are dimmed.
/// </summary>
public sealed class GalleryLetters : UserControl
{
    internal const string Digits = "0–9";
    internal const string Other = "#";
    internal static readonly string[] Letters =
        [Digits, .. Enumerable.Range('A', 26).Select(letter => ((char)letter).ToString()), "Ä", "Ö", "Ü", Other];

    private readonly Dictionary<string, DaisyButton> _buttons = [];
    private readonly List<DaisyButton> _order = [];
    private bool _digits = true, _umlauts = true;

    /// <summary>Raised with the letter of the button that was clicked.</summary>
    public event Action<string>? LetterChosen;

    public GalleryLetters()
    {
        var grid = new UniformGrid { Columns = 1, Width = 36 };
        // One tab stop for the whole bar instead of 31; the arrow keys go from letter to letter.
        KeyboardNavigation.SetTabNavigation(grid, KeyboardNavigationMode.Once);
        grid.KeyDown += OnLetterKeyDown;
        foreach (var letter in Letters)
        {
            var button = new DaisyButton
            {
                Content = letter, Tag = letter, Variant = DaisyButtonVariant.Ghost, Size = DaisySize.ExtraSmall,
                Height = double.NaN, MinHeight = 0, MinWidth = 0, Padding = new Thickness(0),
                HorizontalAlignment = HorizontalAlignment.Stretch, VerticalAlignment = VerticalAlignment.Stretch
            };
            AutomationProperties.SetAutomationId(button, "GalleryView.Letter." + letter switch { Digits => "Digits", Other => "Other", _ => letter });
            button.Click += OnLetterClick;
            _buttons[letter] = button;
            _order.Add(button);
            grid.Children.Add(button);
        }
        // 31 letters need more height than a short window leaves them; the bar then shrinks as a whole instead of
        // cutting off the last ones.
        Content = new Viewbox { Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly, Child = grid };
    }

    // Up and Down go to the next letter that files start with, Home and End to the first and last. Handled here, the
    // moves do not depend on the bar's geometry, which the view box scales.
    private void OnLetterKeyDown(object? sender, KeyEventArgs e)
    {
        var index = e.Source is DaisyButton current ? _order.IndexOf(current) : -1;
        if (index < 0) return;
        var (start, step) = e.Key switch
        {
            Key.Up => (index - 1, -1), Key.Down => (index + 1, 1), Key.Home => (0, 1), Key.End => (_order.Count - 1, -1),
            _ => (-1, 0)
        };
        if (step == 0) return;
        e.Handled = true;
        for (var next = start; next >= 0 && next < _order.Count; next += step)
        {
            if (!_order[next].IsEffectivelyEnabled || !_order[next].IsVisible) continue;
            _order[next].Focus(NavigationMethod.Directional);
            return;
        }
    }

    private void OnLetterClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Control { Tag: string letter }) LetterChosen?.Invoke(letter);
    }

    /// <summary>
    /// Shows the digits and the umlauts or leaves them out, and dims the letters that none of the file names starts with.
    /// Without their own entries, names with a digit first go to # and those with an umlaut to its base letter.
    /// </summary>
    public void Update(IEnumerable<string> fileNames, bool digits, bool umlauts)
    {
        (_digits, _umlauts) = (digits, umlauts);
        _buttons[Digits].IsVisible = digits;
        foreach (var umlaut in (string[])["Ä", "Ö", "Ü"]) _buttons[umlaut].IsVisible = umlauts;
        var present = new HashSet<string>();
        foreach (var name in fileNames) present.Add(LetterFor(name));
        foreach (var (letter, button) in _buttons) button.IsEnabled = present.Contains(letter);
    }

    /// <summary>The entry of a file name in the bar as it is shown now, with or without digits and umlauts.</summary>
    internal string LetterFor(string fileName) => LetterOf(fileName, _digits, _umlauts);

    /// <summary>
    /// The bar's letter of a file name: digits share one, A to Z and the umlauts have their own, other letters with
    /// marks go to their base letter, such as É to E, and anything else to #.
    /// </summary>
    /// <remarks>
    /// The first character goes as it is seen, with its marks: an emoji is two code units, which alone are no valid
    /// text and took the gallery down, and a name in decomposed form, as macOS keeps it, starts with A and a separate
    /// diaeresis but belongs to Ä.
    /// </remarks>
    /// <param name="fileName">The file name.</param>
    /// <param name="digits">Whether the digits have their entry; else they go to #.</param>
    /// <param name="umlauts">Whether the umlauts have their entries; else they go to their base letters.</param>
    internal static string LetterOf(string fileName, bool digits = true, bool umlauts = true)
    {
        if (string.IsNullOrEmpty(fileName) || !Rune.TryGetRuneAt(fileName, 0, out _)) return Other;
        var first = StringInfo.GetNextTextElement(fileName).Normalize(NormalizationForm.FormC).ToUpperInvariant();
        // The dotless ı of Turkish has no invariant capital but sorts as I; the culture's own upper case would depend on
        // the machine, and a Linux container's invariant culture left it under #.
        if (first == "ı") first = "I";
        if (char.IsAsciiDigit(first[0])) return digits ? Digits : Other;
        if (umlauts && first is "Ä" or "Ö" or "Ü") return first;
        var bare = first.Normalize(NormalizationForm.FormD)[0];
        return bare is >= 'A' and <= 'Z' ? bare.ToString() : Other;
    }
}
