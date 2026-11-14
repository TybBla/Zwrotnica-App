using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RefundPilot.Application.Abstractions;
using RefundPilot.Infrastructure.Ai;
using RefundPilot.Infrastructure.Email;
using RefundPilot.Infrastructure.Pdf;

namespace RefundPilot.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<AppDbContext>(options => options.UseSqlite(connectionString));
        services.AddScoped<ICaseRepository, CaseRepository>();
        services.AddScoped<IUserProfileStore, UserProfileStore>();
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddSingleton<IComplaintPdfRenderer, ComplaintPdfRenderer>();
        return services;
    }

    /// <summary>
    /// SMTP when Email:Smtp:Host is set; otherwise .eml files in Email:OutboxPath (default data/outbox under the
    /// content root), so reminders work locally with no mail account.
    /// </summary>
    public static IServiceCollection AddEmail(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        var smtp = configuration.GetSection("Email:Smtp");
        var options = new EmailOptions
        {
            From = NonEmpty(configuration["Email:From"]) ?? new EmailOptions().From,
            FromName = NonEmpty(configuration["Email:FromName"]) ?? new EmailOptions().FromName,
            Smtp = new SmtpOptions
            {
                Host = NonEmpty(smtp["Host"]),
                Port = int.TryParse(smtp["Port"], out var port) && port > 0 ? port : new SmtpOptions().Port,
                User = NonEmpty(smtp["User"]),
                Password = smtp["Password"],
                EnableSsl = !bool.TryParse(smtp["EnableSsl"], out var ssl) || ssl,
            },
        };

        if (options.Smtp.Host is not null)
            services.AddSingleton<IEmailSender>(new SmtpEmailSender(options));
        else
            services.AddSingleton<IEmailSender>(new FileEmailSender(options,
                Path.Combine(contentRoot, NonEmpty(configuration["Email:OutboxPath"]) ?? Path.Combine("data", "outbox"))));
        return services;
    }

    private static string? NonEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// Registers Gemini when a key is present (Gemini:ApiKey or the GEMINI_API_KEY environment variable),
    /// otherwise a no-op client so the app runs with the manual path only.
    /// </summary>
    public static IServiceCollection AddAi(this IServiceCollection services, IConfiguration configuration)
    {
        var apiKey = configuration["Gemini:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            apiKey = Environment.GetEnvironmentVariable("GEMINI_API_KEY");

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddSingleton<IAiClient, NullAiClient>();
            return services;
        }

        var model = configuration["Gemini:Model"];
        services.AddSingleton(new GeminiOptions
        {
            ApiKey = apiKey.Trim(),
            Model = string.IsNullOrWhiteSpace(model) ? GeminiOptions.DefaultModel : model.Trim(),
        });
        services.AddHttpClient<IAiClient, GeminiClient>(http =>
        {
            http.BaseAddress = new Uri("https://generativelanguage.googleapis.com/");
            http.Timeout = TimeSpan.FromSeconds(30);
        });
        return services;
    }
}
