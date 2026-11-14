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
    /// In one query: Sent cases whose ResponseDueDate is before <paramref name="dueBefore"/>, not reminded yet,
    /// whose owner has reminders on.
    /// </summary>
    Task<List<DueReminder>> ListDueRemindersAsync(DateTime dueBefore, CancellationToken ct);

    /// <summary>
    /// Claims the cases still unclaimed by setting ReminderSentAt = <paramref name="claimedAt"/> (one conditional UPDATE),
    /// then returns the ids this call claimed. A case claimed by another run is not returned.
    /// </summary>
    Task<List<int>> ClaimRemindersAsync(IReadOnlyCollection<int> caseIds, DateTime claimedAt, CancellationToken ct);

    /// <summary>Undoes this run's claim (ReminderSentAt back to null) for cases whose reminder did not go out.</summary>
    Task ReleaseRemindersAsync(IReadOnlyCollection<int> caseIds, DateTime claimedAt, CancellationToken ct);
}
