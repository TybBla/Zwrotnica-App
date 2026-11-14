using Microsoft.EntityFrameworkCore;
using RefundPilot.Application.Abstractions;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public sealed class CaseRepository(AppDbContext db) : ICaseRepository
{
    public Task<List<RefundCase>> ListAsync(string userId) =>
        db.Cases.AsNoTracking()
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id)
            .ToListAsync();

    public Task<RefundCase?> GetAsync(int id, string userId) =>
        db.Cases.FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

    public async Task AddAsync(RefundCase refundCase)
    {
        db.Cases.Add(refundCase);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(RefundCase refundCase)
    {
        db.Cases.Update(refundCase);
        await db.SaveChangesAsync();
    }

    public async Task<bool> DeleteAsync(int id, string userId) =>
        await db.Cases.Where(c => c.Id == id && c.UserId == userId).ExecuteDeleteAsync() > 0;

    public Task<List<DueReminder>> ListDueRemindersAsync(DateTime now, CancellationToken ct) =>
        (from c in db.Cases.AsNoTracking()
         join u in db.Users on c.UserId equals u.Id
         where c.Status == CaseStatus.Sent && c.ReminderSentAt == null
               && c.ResponseDueDate != null && c.ResponseDueDate <= now
               && u.EmailReminders && u.Email != null
         orderby c.Id
         select new DueReminder(c, u.Email!))
        .ToListAsync(ct);

    public Task MarkRemindersSentAsync(IReadOnlyCollection<int> caseIds, DateTime sentAt, CancellationToken ct) =>
        db.Cases.Where(c => caseIds.Contains(c.Id) && c.ReminderSentAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReminderSentAt, sentAt), ct);
}
