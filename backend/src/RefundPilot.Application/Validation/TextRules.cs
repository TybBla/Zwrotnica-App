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
        [TextField.Notes] = new("Uwagi", "litery, cyfry, odstępy i znaki . , ; : ! ? - ( ) / ' \" %", @"\p{L}\p{N}\s.,;:!?\-()/'""%"),
    };

    // Decomposed input (letter + combining accent) is composed first so Polish text typed either way is accepted.
    private static string Compose(string value) => value.Normalize(NormalizationForm.FormC);

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
        var text = Rules[field].Disallowed.Replace(Compose(value), "").Trim();
        return text.Length == 0 ? null : text;
    }
}
