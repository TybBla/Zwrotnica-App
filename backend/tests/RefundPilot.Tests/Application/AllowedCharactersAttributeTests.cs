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
}
