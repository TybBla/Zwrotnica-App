using System.ComponentModel.DataAnnotations;

namespace RefundPilot.Application.Dtos;

/// <summary>Rejects a decimal with more fraction digits than <c>places</c> (an amount of money has at most two).</summary>
[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class MaxDecimalPlacesAttribute(int places) : ValidationAttribute
{
    public override bool IsValid(object? value) =>
        value is not decimal d || decimal.Round(d, places) == d;
}
