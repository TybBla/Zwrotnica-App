using System.Text;
using System.Text.RegularExpressions;

namespace RefundPilot.Application.Validation;

/// <summary>Per-field character allowlists: letters and digits of any script plus a few punctuation marks; never emoji or symbols.</summary>
public static class TextRules
{
    private sealed record Rule(string Label, string Allowed, string ClassBody)
    {
        public Regex Full { get; } = new($@"\A[{ClassBody}]+\z", RegexOptions.Compiled | RegexOptions.CultureInvariant);
        public Regex Disallowed { get; } = new($@"[^{ClassBody}]", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    }

    private static readonly Dictionary<TextField, Rule> Rules = new()
    {
        [TextField.FullName] = new("Imię i nazwisko", "litery, spacja i znaki - ' .", @"\p{L} \-'."),
        [TextField.Address] = new("Adres", "litery, cyfry, spacja i znaki . , - / '", @"\p{L}\p{N} .,\-/'"),
        [TextField.Carrier] = new("Przewoźnik", "litery, cyfry, spacja i znaki . - ' ( ) /", @"\p{L}\p{N} .\-'()/"),
        [TextField.Station] = new("Stacja", "litery, cyfry, spacja i znaki . - ' ( ) /", @"\p{L}\p{N} .\-'()/"),
        [TextField.TrainCategory] = new("Kategoria pociągu", "litery, cyfry i spacja", @"\p{L}\p{N} "),
        [TextField.TicketNumber] = new("Numer biletu", "litery, cyfry, spacja i znaki - /", @"\p{L}\p{N} \-/"),
        [TextField.Notes] = new("Uwagi", "litery, cyfry, spacja, nowa linia i znaki . , ; : ! ? - ( ) / ' \" %", @"\p{L}\p{N} \n.,;:!?\-()/'""%"),
    };

    // Decomposed input (letter + combining accent) is composed first so Polish text typed either way is accepted.
    private static string Compose(string value) => value.Normalize(NormalizationForm.FormC);

    /// <summary>
    /// Maps what phone keyboards produce to the plain characters the allowlists accept: smart apostrophes/quotes/dashes,
    /// ellipsis, any space separator or tab to a space, line breaks to "\n" (kept only when <paramref name="multiline"/>,
    /// otherwise a space). Runs of spaces collapse, the result is trimmed and NFC-composed. Only ever yields allowed characters.
    /// </summary>
    public static string Canonicalize(string value, bool multiline = false)
    {
        var sb = new StringBuilder(value.Length);
        for (var i = 0; i < value.Length; i++)
        {
            var c = value[i];
            switch (c)
            {
                case '’' or '‘' or 'ʼ': sb.Append('\''); break;
                case '–' or '—' or '−': sb.Append('-'); break;
                case '“' or '”' or '„': sb.Append('"'); break;
                case '…': sb.Append("..."); break;
                case (char)13 or (char)10 or (char)0x2028 or (char)0x2029:
                    if (c == '\r' && i + 1 < value.Length && value[i + 1] == '\n')
                        i++;
                    sb.Append(multiline ? '\n' : ' ');
                    break;
                default:
                    sb.Append(c == '\t' || char.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.SpaceSeparator ? ' ' : c);
                    break;
            }
        }

        var text = Compose(sb.ToString());
        text = SpaceRuns.Replace(text, " ");
        if (multiline)
            text = text.Replace(" \n", "\n").Replace("\n ", "\n");
        return text.Trim();
    }

    private static readonly Regex SpaceRuns = new(" {2,}", RegexOptions.Compiled);

    public static bool IsAllowed(TextField field, string value) => Rules[field].Full.IsMatch(Compose(value));

    public static string Message(TextField field)
    {
        var rule = Rules[field];
        return $"Pole „{rule.Label}” zawiera niedozwolone znaki. Dozwolone: {rule.Allowed}.";
    }

    /// <summary>For AI-produced text: drops disallowed characters instead of rejecting. Null when nothing usable remains.</summary>
    public static string? Sanitize(TextField field, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;
        var text = Canonicalize(Rules[field].Disallowed.Replace(Canonicalize(value), ""));
        return text.Length == 0 ? null : text;
    }
}
