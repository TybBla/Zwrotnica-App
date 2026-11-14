using RefundPilot.Domain;

namespace RefundPilot.Application.Abstractions;

/// <summary>A case whose reminder is due, with its owner's account e-mail (the only recipient it may go to).</summary>
public sealed record DueReminder(RefundCase Case, string Email);

public interface ICaseRepository
{
    Task<List<RefundCase>> ListAsync(string userId);
    Task<RefundCase?> GetAsync(int id, string userId);
    Task AddAsync(RefundCase refundCase);
    Task UpdateAsync(RefundCase refundCase);
    Task<bool> DeleteAsync(int id, string userId);

    /// <summary>
    /// In one query: Sent cases whose ResponseDueDate is at or before <paramref name="now"/>, not reminded yet,
    /// whose owner has reminders on.
    /// </summary>
    Task<List<DueReminder>> ListDueRemindersAsync(DateTime now, CancellationToken ct);

    /// <summary>Sets ReminderSentAt for all the given cases in one statement.</summary>
    Task MarkRemindersSentAsync(IReadOnlyCollection<int> caseIds, DateTime sentAt, CancellationToken ct);
}
