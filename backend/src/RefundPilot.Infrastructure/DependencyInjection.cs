using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RefundPilot.Application.Abstractions;

namespace RefundPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ICaseRepository, CaseRepository>();
        services.AddScoped<IUserProfileStore, UserProfileStore>();
        return services;
    }
}
