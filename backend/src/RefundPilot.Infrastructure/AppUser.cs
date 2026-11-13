using Microsoft.AspNetCore.Identity;
using RefundPilot.Domain;

namespace RefundPilot.Infrastructure;

public sealed class AppUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? Address { get; set; }
    public string? Iban { get; set; }
    public PayoutPreference PreferredPayout { get; set; }
}
