using System.ComponentModel.DataAnnotations;
using RefundPilot.Application.Validation;

namespace RefundPilot.Tests.Application;

public sealed class AllowedCharactersAttributeTests
{
    private static ValidationResult? Validate(TextField field, string? value) =>
        new AllowedCharactersAttribute(field).GetValidationResult(value, new ValidationContext(new object()) { MemberName = "X" });

    [Theory]
    [InlineData(TextField.FullName, "Zażółć Gęślą-Jaźń")]
    [InlineData(TextField.FullName, "Jan O'Brien Jr.")]
    [InlineData(TextField.Address, "ul. Długa 5/12, 00-950 Warszawa")]
    [InlineData(TextField.Carrier, "PKP Intercity")]
    [InlineData(TextField.Carrier, "Koleje Mazowieckie (KM)")]
    [InlineData(TextField.Station, "Kraków Główny")]
    [InlineData(TextField.Station, "Warszawa Wschodnia/Centralna")]
    [InlineData(TextField.TrainCategory, "IC")]
    [InlineData(TextField.TrainCategory, "TLK Kraków")]
    [InlineData(TextField.TicketNumber, "IC-TEST-0001/2026")]
    [InlineData(TextField.Notes, "Pociąg stał 40 min (awaria).\nKonduktor: \"brak info\"; zwrot 50%!")]
    [InlineData(TextField.FullName, null)]
    [InlineData(TextField.Notes, "")]
    public void Valid_Passes(TextField field, string? value) => Assert.Equal(ValidationResult.Success, Validate(field, value));

    [Theory]
    [InlineData(TextField.FullName, "Jan 😀")]
    [InlineData(TextField.FullName, "Jan1")]
    [InlineData(TextField.FullName, "<script>")]
    [InlineData(TextField.Address, "ul. Długa 5 ★")]
    [InlineData(TextField.Address, "ul. Długa;5")]
    [InlineData(TextField.Carrier, "PKP {{IBAN}}")]
    [InlineData(TextField.Station, "Kraków★")]
    [InlineData(TextField.Station, "Kraków😀")]
    [InlineData(TextField.TrainCategory, "IC-1")]
    [InlineData(TextField.TrainCategory, "IC 😀")]
    [InlineData(TextField.TicketNumber, "IC-1 😀")]
    [InlineData(TextField.TicketNumber, "IC_1")]
    [InlineData(TextField.Notes, "Super 😀")]
    [InlineData(TextField.Notes, "<b>x</b>")]
    [InlineData(TextField.Notes, "{{IBAN}}")]
    public void Invalid_Fails(TextField field, string value)
    {
        var result = Validate(field, value);

        Assert.NotNull(result);
        Assert.Contains("niedozwolone znaki", result!.ErrorMessage);
    }

    [Fact]
    public void Sanitize_StripsDisallowedCharactersAndTrims()
    {
        Assert.Equal("Kraków Główny", TextRules.Sanitize(TextField.Station, " Kraków★ Główny😀 "));
        Assert.Equal("IC-1/2", TextRules.Sanitize(TextField.TicketNumber, "IC-1/2<>"));
        Assert.Null(TextRules.Sanitize(TextField.Carrier, "😀★"));
    }

    [Theory]
    [InlineData("O’Neil", "O'Neil")]
    [InlineData("O‘Neil ʼx", "O'Neil 'x")]
    [InlineData("Gdynia – Gdańsk — a − b", "Gdynia - Gdańsk - a - b")]
    [InlineData("„brak info” i “ok”", "\"brak info\" i \"ok\"")]
    [InlineData("Czekaj…", "Czekaj...")]
    [InlineData("a\u00A0b\tc\u2003d", "a b c d")]
    [InlineData("  a    b  ", "a b")]
    [InlineData("a\r\nb", "a b")]
    [InlineData("o\u0301", "ó")]
    public void Canonicalize_SingleLine(string input, string expected) =>
        Assert.Equal(expected, TextRules.Canonicalize(input));

    [Fact]
    public void Canonicalize_Multiline_KeepsLineBreaks() =>
        Assert.Equal("a b\nc\nd", TextRules.Canonicalize("a\u00A0\u00A0b\r\nc\rd", multiline: true));

    [Theory]
    [InlineData("O’Neil", "O'Neil")]
    [InlineData("Jan 😀 Kowalski", "Jan Kowalski")]
    [InlineData("Gdynia – Gdańsk", "Gdynia - Gdańsk")]
    public void Sanitize_CanonicalizesAndCollapsesSpaces(string input, string expected) =>
        Assert.Equal(expected, TextRules.Sanitize(TextField.Station, input));

    [Fact]
    public void Notes_RejectTabsAfterNoCanonicalization() =>
        Assert.NotNull(Validate(TextField.Notes, "a\tb"));
}
