using System.ComponentModel.DataAnnotations;
using RefundPilot.Application.Validation;
using RefundPilot.Domain;

namespace RefundPilot.Tests.Application;

public sealed class IbanTests
{
    private const string Valid = "PL61109010140000071219812874";

    private static ValidationResult? Validate(string? value) =>
        new IbanAttribute().GetValidationResult(value, new ValidationContext(new object()) { MemberName = "Iban" });

    // Independent mod-97 check, so the constants above are verified rather than trusted.
    private static bool Mod97(string iban)
    {
        var s = iban[4..] + iban[..4];
        var n = 0;
        foreach (var c in s)
            n = (char.IsDigit(c) ? n * 10 + (c - '0') : n * 100 + (c - 'A' + 10)) % 97;
        return n == 1;
    }

    [Fact]
    public void TestConstants_HaveValidChecksum()
    {
        Assert.True(Mod97(Valid));
        Assert.True(Mod97("PL27114020040000300201355387"));
    }

    [Theory]
    [InlineData("PL61109010140000071219812874")]
    [InlineData("PL61 1090 1014 0000 0712 1981 2874")]
    [InlineData("pl61 1090 1014 0000 0712 1981 2874")]
    [InlineData("61109010140000071219812874")]
    [InlineData("61 1090 1014 0000 0712 1981 2874")]
    [InlineData("DE89370400440532013000")]
    public void Valid_PassesAndNormalizes(string input)
    {
        Assert.Equal(ValidationResult.Success, Validate(input));
        Assert.True(Iban.TryNormalize(input, out var normalized));
        Assert.Equal(input.Replace(" ", "").ToUpperInvariant() is var s && char.IsAsciiDigit(s[0]) ? "PL" + s : s, normalized);
    }

    [Fact]
    public void Normalize_GivesCanonicalPolishIban()
    {
        Assert.Equal(Valid, Iban.Normalize("pl61 1090 1014 0000 0712 1981 2874"));
        Assert.Equal(Valid, Iban.Normalize("61109010140000071219812874"));
    }

    [Theory]
    [InlineData("PL61109010140000071219812875")]   // wrong checksum
    [InlineData("PL6110901014000007121981287A")]   // letter inside
    [InlineData("PL61-1090-1014-0000-0712-1981-2874")]
    [InlineData("PL61109010140000071219812874😀")]
    [InlineData("{{IBAN}}")]
    [InlineData("PL61")]
    [InlineData("PL611090101400000712198128741234567890")] // too long
    public void Invalid_Fails(string input)
    {
        var result = Validate(input);

        Assert.NotNull(result);
        Assert.Contains("IBAN", result!.ErrorMessage);
    }

    [Fact]
    public void NullOrEmpty_Passes()
    {
        Assert.Equal(ValidationResult.Success, Validate(null));
        Assert.Equal(ValidationResult.Success, Validate(""));
    }
}
