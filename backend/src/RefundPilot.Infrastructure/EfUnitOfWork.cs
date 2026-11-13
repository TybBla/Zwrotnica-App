using Microsoft.EntityFrameworkCore;
using RefundPilot.Application.Abstractions;

namespace RefundPilot.Infrastructure;

public sealed class EfUnitOfWork(AppDbContext db) : IUnitOfWork
{
    public async Task ExecuteInTransactionAsync(Func<Task> work, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await work();
        await transaction.CommitAsync(ct);
    }
}
