using RefundPilot.Domain;

namespace RefundPilot.Application.Abstractions;

public interface IUserProfileStore
{
    Task<UserProfile?> GetAsync(string userId);
    Task<bool> UpdateAsync(string userId, UserProfile profile);
}
