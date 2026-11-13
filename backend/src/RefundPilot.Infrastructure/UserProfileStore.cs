using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using RefundPilot.Application.Abstractions;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public sealed class UserProfileStore(AppDbContext db, IDataProtectionProvider dataProtection) : IUserProfileStore
{
    private readonly IbanCipher _cipher = new(dataProtection);

    public async Task<UserProfile?> GetAsync(string userId)
    {
        var row = await db.Users.AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => new { u.FullName, u.Address, u.Iban, u.PreferredPayout })
            .FirstOrDefaultAsync();
        return row is null ? null : new UserProfile(row.FullName, row.Address, _cipher.Unprotect(row.Iban), row.PreferredPayout);
    }

    public async Task<bool> UpdateAsync(string userId, UserProfile profile)
    {
        var encryptedIban = _cipher.Protect(profile.Iban);
        return await db.Users.Where(u => u.Id == userId).ExecuteUpdateAsync(s => s
            .SetProperty(u => u.FullName, profile.FullName)
            .SetProperty(u => u.Address, profile.Address)
            .SetProperty(u => u.Iban, encryptedIban)
            .SetProperty(u => u.PreferredPayout, profile.PreferredPayout)) > 0;
    }
}
