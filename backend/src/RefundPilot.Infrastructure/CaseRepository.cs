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
}
