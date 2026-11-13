using System.ComponentModel.DataAnnotations;
using RefundPilot.Domain;

namespace RefundPilot.Application.Validation;

/// <summary>Requires a valid IBAN (letters, digits, spaces; ISO 13616 checksum). Null and empty pass.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class IbanAttribute : ValidationAttribute
{
    public const string Message = "Numer IBAN jest nieprawidłowy. Podaj 26 cyfr lub numer z kodem kraju (np. PL), bez innych znaków.";

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        value is not string text || string.IsNullOrWhiteSpace(text) || Iban.TryNormalize(TextRules.Canonicalize(text), out _)
            ? ValidationResult.Success
            : new ValidationResult(Message, validationContext.MemberName is { } member ? [member] : null);
}
