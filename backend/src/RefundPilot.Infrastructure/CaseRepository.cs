using Microsoft.EntityFrameworkCore;
using RefundPilot.Application.Abstractions;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public sealed class CaseRepository(AppDbContext db) : ICaseRepository
{
    public Task<List<RefundCase>> ListAsync() =>
        db.Cases.AsNoTracking().OrderByDescending(c => c.CreatedAt).ThenByDescending(c => c.Id).ToListAsync();

    public Task<RefundCase?> GetAsync(int id) => db.Cases.FirstOrDefaultAsync(c => c.Id == id);

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

    public async Task<bool> DeleteAsync(int id) =>
        await db.Cases.Where(c => c.Id == id).ExecuteDeleteAsync() > 0;
}
