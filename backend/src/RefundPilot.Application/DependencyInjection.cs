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
        services.AddSingleton<IReferenceDataService, ReferenceDataService>();
        services.AddScoped<IAssessmentService, AssessmentService>();
        services.AddScoped<ICaseService, CaseService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddScoped<IExtractionService, ExtractionService>();
        services.AddScoped<IComplaintService, ComplaintService>();
        services.AddScoped<IFollowUpLetterService, FollowUpLetterService>();
        services.AddScoped<IReminderService, ReminderService>();
        return services;
    }
}
