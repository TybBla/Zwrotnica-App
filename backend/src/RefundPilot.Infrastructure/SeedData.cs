using Microsoft.EntityFrameworkCore;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public static class SeedData
{
    private static readonly DateTime Today = new(2026, 11, 13, 9, 0, 0, DateTimeKind.Utc);
    private static readonly EligibilityService Eligibility = new();
    private static readonly CompensationCalculator Calculator = new(new CompensationOptions());

    public static async Task EnsureSeededAsync(AppDbContext db, string userId)
    {
        if (await db.Cases.AnyAsync(c => c.UserId == userId))
            return;

        var filedAt = Today.AddDays(-10);
        var paidFiledAt = Today.AddDays(-45);
        // Past its deadline and not reminded yet: the first reminder run writes an e-mail for it (local outbox).
        var overdueFiledAt = Today.AddDays(-35);

        db.Cases.AddRange(
            Build(userId, "PKP Intercity", "IC", "Gdynia Główna", "Kraków Główny", "IC-4821-0093", 12,
                IncidentType.Delay, 95, 148.00m, CaseStatus.New, DelayCause.Technical),
            Build(userId, "PKP Intercity", "EIC", "Warszawa Centralna", "Wrocław Główny", "EIC-7714-5502", 8,
                IncidentType.Delay, 140, 189.00m, CaseStatus.Sent, DelayCause.Unknown,
                filedAt: filedAt, dueDate: filedAt.AddMonths(1)),
            Build(userId, "PKP Intercity", "IC", "Poznań Główny", "Katowice", "IC-3306-1187", 5,
                IncidentType.Cancellation, 0, 96.50m, CaseStatus.New, null),
            Build(userId, "PKP Intercity", "TLK", "Lublin Główny", "Warszawa Wschodnia", "TLK-2290-7741", 40,
                IncidentType.Delay, 75, 64.00m, CaseStatus.Paid, DelayCause.Weather,
                filedAt: paidFiledAt, dueDate: paidFiledAt.AddMonths(1)),
            Build(userId, "Polregio", null, "Szczecin Główny", "Stargard", "PR-5518-0412", 3,
                IncidentType.Delay, 70, 22.00m, CaseStatus.New, DelayCause.PersonsOnTrack),
            Build(userId, "PKP Intercity", "EIP", "Kraków Główny", "Gdańsk Główny", "EIP-6120-3358", 37,
                IncidentType.Delay, 125, 219.00m, CaseStatus.Sent, DelayCause.Technical,
                filedAt: overdueFiledAt, dueDate: overdueFiledAt.AddMonths(1)));

        await db.SaveChangesAsync();
    }

    private static RefundCase Build(string userId, string carrier, string? category, string from, string to, string ticketNumber,
        int daysAgo, IncidentType type, int delayMinutes, decimal price, CaseStatus status, DelayCause? cause,
        DateTime? filedAt = null, DateTime? dueDate = null)
    {
        var eligibility = Eligibility.Assess(carrier, category);
        var amount = eligibility.Verdict == Verdict.Eligible
            ? Calculator.Calculate(type, delayMinutes, price).Amount
            : 0m;

        return new RefundCase
        {
            UserId = userId,
            Carrier = carrier,
            TrainCategory = category,
            FromStation = from,
            ToStation = to,
            TicketNumber = ticketNumber,
            TravelDate = DateOnly.FromDateTime(Today.AddDays(-daysAgo)),
            TicketPrice = price,
            IncidentType = type,
            DelayMinutes = delayMinutes,
            DelayCause = cause,
            Verdict = eligibility.Verdict,
            CompensationAmount = amount,
            PayoutPreference = PayoutPreference.OriginalPayment,
            Status = status,
            FiledAt = filedAt,
            ResponseDueDate = dueDate,
            CreatedAt = Today.AddDays(-daysAgo),
        };
    }
}
