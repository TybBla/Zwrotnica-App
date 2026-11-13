using System.ComponentModel.DataAnnotations;

namespace RefundPilot.Application.Validation;

/// <summary>Rejects text containing characters outside the field's allowlist. Null and empty pass (use [Required] for that).</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class AllowedCharactersAttribute(TextField field) : ValidationAttribute
{
    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext) =>
        value is not string { Length: > 0 } text || TextRules.IsAllowed(field, text)
            ? ValidationResult.Success
            : new ValidationResult(TextRules.Message(field),
                validationContext.MemberName is { } member ? [member] : null);
}
