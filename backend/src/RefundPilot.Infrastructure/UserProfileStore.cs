using Microsoft.EntityFrameworkCore;
using RefundPilot.Application.Abstractions;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public sealed class UserProfileStore(AppDbContext db) : IUserProfileStore
{
    public Task<UserProfile?> GetAsync(string userId) =>
        db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new UserProfile(u.FullName, u.Address, u.Iban, u.PreferredPayout))
            .FirstOrDefaultAsync();

    public async Task<bool> UpdateAsync(string userId, UserProfile profile) =>
        await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.FullName, profile.FullName)
            .SetProperty(u => u.Address, profile.Address)
            .SetProperty(u => u.Iban, profile.Iban)
            .SetProperty(u => u.PreferredPayout, profile.PreferredPayout)) > 0;
}
