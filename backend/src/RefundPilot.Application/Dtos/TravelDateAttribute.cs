using System.ComponentModel.DataAnnotations;

namespace RefundPilot.Application.Dtos;

/// <summary>Requires a real travel date: not omitted (default) and not in the future.</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class TravelDateAttribute : ValidationAttribute
{
    public TravelDateAttribute() : base("Podaj poprawną datę podróży (nie z przyszłości).") { }

    protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
    {
        if (value is not DateOnly date || date == default)
            return new ValidationResult("Podaj datę podróży.");

        var clock = validationContext.GetService(typeof(TimeProvider)) as TimeProvider ?? TimeProvider.System;
        var today = WarsawClock.Today(clock);
        return date > today ? new ValidationResult("Data podróży nie może być z przyszłości.") : ValidationResult.Success;
    }
}
