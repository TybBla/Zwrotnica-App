using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using RefundPilot.Api;
using RefundPilot.Application;
using RefundPilot.Application.Abstractions;
using RefundPilot.Application.Services;
using RefundPilot.Infrastructure;

const string FrontendCorsPolicy = "Frontend";

// Development only, and before configuration is built, so GEMINI_API_KEY from the repo-root .env is picked up.
if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
    DotEnv.Load(Directory.GetCurrentDirectory());

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false)));
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddInfrastructure(
    builder.Configuration.GetConnectionString("Default") ?? "DataSource=refundpilot.db");
builder.Services.AddAi(builder.Configuration);
// Encrypts the stored IBAN. Keys persist to the default per-user location, or to DataProtection:KeysPath when set.
var dataProtection = builder.Services.AddDataProtection().SetApplicationName("RefundPilot");
if (builder.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keysPath)
    dataProtection.PersistKeysToFileSystem(new DirectoryInfo(keysPath));
builder.Services.AddIdentityApiEndpoints<AppUser>(o => o.User.RequireUniqueEmail = true)
    .AddEntityFrameworkStores<AppDbContext>();

// API clients get 401/403 instead of redirects to a login page.
builder.Services.ConfigureApplicationCookie(o =>
{
    o.Cookie.HttpOnly = true;
    o.Cookie.SameSite = SameSiteMode.Lax;
    // Production is HTTPS-only (Azure terminates TLS; ForwardedHeaders restores the scheme), so never send the cookie in clear.
    if (!builder.Environment.IsDevelopment())
        o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    o.Events.OnRedirectToLogin = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        return Task.CompletedTask;
    };
    o.Events.OnRedirectToAccessDenied = ctx =>
    {
        ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<ICurrentUser, CurrentUser>();
builder.Services.AddApplication();

// Reminder e-mails one month after filing: SMTP when Email:Smtp:Host is set, otherwise .eml files in a local outbox.
builder.Services.AddEmail(builder.Configuration, builder.Environment.ContentRootPath);
var publicBaseUrl = builder.Configuration["PublicBaseUrl"];
builder.Services.AddSingleton(new ReminderOptions(
    string.IsNullOrWhiteSpace(publicBaseUrl) ? "http://localhost:5173" : publicBaseUrl.Trim()));
if (builder.Configuration.GetValue("Reminders:Enabled", defaultValue: true))
{
    var intervalMinutes = builder.Configuration.GetValue("Reminders:IntervalMinutes", defaultValue: 60);
    builder.Services.AddHostedService(sp => new ReminderBackgroundService(
        sp.GetRequiredService<IServiceScopeFactory>(),
        TimeSpan.FromMinutes(intervalMinutes > 0 ? intervalMinutes : 60),
        sp.GetRequiredService<ILogger<ReminderBackgroundService>>()));
}

// Anonymous extraction calls the paid AI API: limit per client IP (fixed window, default 5/min, configurable).
// In a commercial version the quota would depend on the user's plan (free vs paid).
const string ExtractionsRateLimit = "extractions";
var extractionsPerMinute = int.TryParse(builder.Configuration["RateLimiting:ExtractionsPerMinute"], out var configuredLimit)
    && configuredLimit > 0 ? configuredLimit : 5;
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy(ExtractionsRateLimit, httpContext => RateLimitPartition.GetFixedWindowLimiter(
        httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = extractionsPerMinute,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
    o.OnRejected = async (context, ct) =>
    {
        context.HttpContext.Response.StatusCode = StatusCodes.Status429TooManyRequests;
        await context.HttpContext.Response.WriteAsJsonAsync(new ProblemDetails
        {
            Status = StatusCodes.Status429TooManyRequests,
            Title = "Too Many Requests",
            Detail = "Zbyt wiele zapytań. Spróbuj ponownie za minutę.",
        }, (JsonSerializerOptions?)null, "application/problem+json", ct);
    };
});

builder.Services.AddCors(o => o.AddPolicy(FrontendCorsPolicy, policy => policy
    .WithOrigins("http://localhost:5173")
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

// Behind Azure App Service's front end: take the client IP (rate limiter) and the https scheme from X-Forwarded-*.
// Only a proxy on a private/link-local address is trusted, and only for the one hop it appends, so a client
// cannot forge its IP. Skipped when the framework's own ASPNETCORE_FORWARDEDHEADERS_ENABLED switch is set
// (that would process the headers twice and trusts any sender).
var useForwardedHeaders = builder.Configuration.GetValue<bool>("ForwardedHeaders:Enabled")
    && string.IsNullOrEmpty(Environment.GetEnvironmentVariable("ASPNETCORE_FORWARDEDHEADERS_ENABLED"));
if (useForwardedHeaders)
{
    builder.Services.Configure<ForwardedHeadersOptions>(o =>
    {
        o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        o.ForwardLimit = 1;
        o.KnownProxies.Clear();
        o.KnownIPNetworks.Clear();
        foreach (var network in TrustedProxyNetworks)
            o.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
    });
}

var app = builder.Build();

if (app.Configuration["DataProtection:KeysPath"] is { Length: > 0 } keyRingPath)
    StorageDirectory.Ensure(keyRingPath, "DataProtection__KeysPath", app.Logger);

if (!app.Environment.IsDevelopment() && string.IsNullOrWhiteSpace(publicBaseUrl))
    app.Logger.LogWarning("PublicBaseUrl is not set: links in reminder e-mails point to http://localhost:5173.");

var seedEnabled = app.Configuration.GetValue("Seed:Enabled", defaultValue: true);
await DatabaseInitializer.InitializeAsync(
    app.Services,
    seedEnabled ? app.Configuration["Seed:DemoEmail"] : null,
    seedEnabled ? app.Configuration["Seed:DemoPassword"] : null,
    useRollbackJournal: !app.Environment.IsDevelopment());

if (useForwardedHeaders)
    app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// The built SPA (frontend/dist copied to wwwroot at publish) is served from the API's own origin.
app.UseDefaultFiles();
app.UseStaticFiles();

app.UseCors(FrontendCorsPolicy);
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGroup("/api/auth").MapIdentityApi<AppUser>();
app.MapControllers();
app.MapGet("/api/health", () => Results.Ok(new { status = "ok" })).AllowAnonymous();

// Client-side routes (/sprawy, /profil, ...) get index.html; /api and /swagger keep their own 404s.
app.MapFallbackToFile("{*path:nonfile:regex(^(?!(api|swagger)(/|$)).*$)}", "index.html");

app.Run();

public partial class Program
{
    /// <summary>Private, CGNAT, link-local and loopback ranges: where Azure's front end connects from.</summary>
    private static readonly string[] TrustedProxyNetworks =
    [
        "10.0.0.0/8", "172.16.0.0/12", "192.168.0.0/16", "169.254.0.0/16", "100.64.0.0/10", "127.0.0.0/8",
        "::1/128", "fc00::/7", "fe80::/10",
    ];
}
