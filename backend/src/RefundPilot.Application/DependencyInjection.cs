using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RefundPilot.Application.Services;
using RefundPilot.Domain;

namespace RefundPilot.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddSingleton(new CompensationOptions());
        services.AddSingleton<EligibilityService>();
        services.AddSingleton<CompensationCalculator>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<IProfileService, ProfileService>();
        return services;
    }
}
