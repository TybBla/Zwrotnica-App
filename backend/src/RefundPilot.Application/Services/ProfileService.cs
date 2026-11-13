using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Dtos;
using RefundPilot.Domain;

namespace RefundPilot.Application.Services;

public interface IProfileService
{
    Task<ProfileDto?> GetAsync();
    Task<ProfileDto?> UpdateAsync(UpdateProfileRequest request);
}

public sealed class ProfileService(ICurrentUser currentUser, IUserProfileStore store) : IProfileService
{
    public async Task<ProfileDto?> GetAsync()
    {
        if (currentUser.UserId is not { } userId)
            return null;

        var profile = await store.GetAsync(userId);
        return profile is null ? null : ToDto(profile);
    }

    public async Task<ProfileDto?> UpdateAsync(UpdateProfileRequest request)
    {
        if (currentUser.UserId is not { } userId)
            return null;

        var profile = new UserProfile(Clean(request.FullName), Clean(request.Address), Clean(request.Iban),
            request.PreferredPayout);
        return await store.UpdateAsync(userId, profile) ? ToDto(profile) : null;
    }

    private ProfileDto ToDto(UserProfile p) =>
        new(currentUser.Email ?? string.Empty, p.FullName, p.Address, p.Iban, p.PreferredPayout);

    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
