namespace RefundPilot.Domain;

/// <summary>IBAN normalization and ISO 13616 validation. The single place that decides what an account number looks like.</summary>
public static class Iban
{
    public const int MinLength = 15, MaxLength = 34;

    /// <summary>Removes spaces, upper-cases, and prefixes a bare 26-digit Polish number with "PL". Does not validate.</summary>
    public static string Normalize(string value)
    {
        var compact = value.Replace(" ", "").ToUpperInvariant();
        return compact.Length == 26 && compact.All(char.IsAsciiDigit) ? "PL" + compact : compact;
    }

    /// <summary>True when <paramref name="value"/> is a well-formed IBAN with a correct checksum; gives the normalized form.</summary>
    public static bool TryNormalize(string? value, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(value) || !value.All(c => c == ' ' || char.IsAsciiLetterOrDigit(c)))
            return false;

        var iban = Normalize(value);
        if (iban.Length is < MinLength or > MaxLength
            || !char.IsAsciiLetterUpper(iban[0]) || !char.IsAsciiLetterUpper(iban[1])
            || !char.IsAsciiDigit(iban[2]) || !char.IsAsciiDigit(iban[3])
            || !HasValidChecksum(iban))
            return false;

        normalized = iban;
        return true;
    }

    private static bool HasValidChecksum(string iban)
    {
        var remainder = 0;
        foreach (var c in string.Concat(iban.AsSpan(4), iban.AsSpan(0, 4)))
            remainder = (char.IsAsciiDigit(c) ? remainder * 10 + (c - '0') : remainder * 100 + (c - 'A' + 10)) % 97;
        return remainder == 1;
    }
}
