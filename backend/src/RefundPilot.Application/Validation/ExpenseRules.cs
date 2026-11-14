namespace RefundPilot.Application.Validation;

/// <summary>Limits for the documented costs a passenger of a regional carrier claims back.</summary>
public static class ExpenseRules
{
    public const int MaxDescription = 500;
    public const double MaxAmount = 100000;
    public const string AmountMessage = "Kwota kosztów musi być większa od zera i nie większa niż 100 000 zł.";
}
