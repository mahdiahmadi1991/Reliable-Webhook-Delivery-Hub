using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using ReliableWebhookDeliveryHub.WebApi;
using ReliableWebhookDeliveryHub.WebApi.Auditing;
using ReliableWebhookDeliveryHub.WebApi.Destinations;
using ReliableWebhookDeliveryHub.WebApi.Security;
using ReliableWebhookDeliveryHub.Application.Security;
using ReliableWebhookDeliveryHub.Domain.Tenants;
using ReliableWebhookDeliveryHub.Infrastructure.Persistence;
using StackExchange.Redis;

if (string.Equals(Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT"), "Development", StringComparison.OrdinalIgnoreCase))
{
    DevDotEnv.LoadFromCurrentDirectoryIfPresent();
}

var builder = WebApplication.CreateBuilder(args);

var sqlConnectionString = ResolveSqlConnectionString(builder);
var redisConnectionString = ResolveRedisConnectionString(builder);
var otlpEndpoint = builder.Configuration.GetValue<string>("OpenTelemetry:Otlp:Endpoint");

builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options => options.UseSqlServer(sqlConnectionString));
builder.Services.AddSingleton<IConnectionMultiplexer>(_ => ConnectionMultiplexer.Connect(redisConnectionString));
builder.Services.AddScoped<ApiKeyService>();
builder.Services.AddScoped<ITenantContext, TenantContext>();
builder.Services.AddScoped<AuditWriter>();

var dataProtectionBuilder = builder.Services.AddDataProtection();
if (builder.Environment.IsDevelopment())
{
    dataProtectionBuilder.PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(Directory.GetCurrentDirectory(), ".dpkeys")));
}

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService(builder.Environment.ApplicationName))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        tracing.AddHttpClientInstrumentation();
        tracing.AddConsoleExporter();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddHttpClientInstrumentation();
        metrics.AddConsoleExporter();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            metrics.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseMiddleware<TenantAuthMiddleware>();

app.MapGet("/", () => Results.Json(new { status = "running" }));

app.MapGet("/health", async (AppDbContext dbContext, IConnectionMultiplexer connectionMultiplexer, CancellationToken ct) =>
{
    var dependencyStatus = new Dictionary<string, string>();
    var issues = new List<string>();

    if (await dbContext.Database.CanConnectAsync(ct))
    {
        dependencyStatus["sqlServer"] = "ok";
    }
    else
    {
        dependencyStatus["sqlServer"] = "unreachable";
        issues.Add("SQL Server unavailable");
    }

    try
    {
        await connectionMultiplexer.GetDatabase().PingAsync();
        dependencyStatus["redis"] = "ok";
    }
    catch (Exception)
    {
        dependencyStatus["redis"] = "unreachable";
        issues.Add("Redis unavailable");
    }

    if (issues.Count == 0)
    {
        return Results.Json(new { status = "ok", sqlServer = "ok", redis = "ok" });
    }

    return Results.Problem(
        title: "Dependency check failed",
        detail: string.Join("; ", issues),
        statusCode: StatusCodes.Status503ServiceUnavailable,
        extensions: dependencyStatus.ToDictionary(kvp => kvp.Key, kvp => (object?)kvp.Value));
});

app.MapGet("/v1/ping", () => Results.Json(new { status = "ok" }));

if (app.Environment.IsDevelopment())
{
    app.MapPost("/dev/bootstrap/tenants", async (
        [FromBody] BootstrapTenantRequest request,
        AppDbContext dbContext,
        ApiKeyService apiKeyService) =>
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
        {
            return Results.Problem(
                title: "Invalid request",
                detail: "Name is required and must be 200 characters or fewer.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        var tenantId = Guid.NewGuid();
        var generatedKey = apiKeyService.Generate(tenantId);

        var tenant = new Tenant
        {
            Id = tenantId,
            Name = request.Name,
            ApiKeyHash = generatedKey.ApiKeyHash,
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.Tenants.Add(tenant);
        await dbContext.SaveChangesAsync();

        return Results.Json(new { tenantId, apiKey = generatedKey.ApiKey }, statusCode: StatusCodes.Status201Created);
    });
}

app.MapDestinationsEndpoints();

await ApplyMigrationsInDevelopmentAsync(app);

app.Run();

static string ResolveSqlConnectionString(WebApplicationBuilder builder)
{
    var configured = builder.Configuration.GetConnectionString("SqlServer");
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return configured;
    }

    if (!builder.Environment.IsDevelopment())
    {
        throw new InvalidOperationException("Connection string 'SqlServer' must be configured.");
    }

    var password = Environment.GetEnvironmentVariable("MSSQL_SA_PASSWORD");
    if (string.IsNullOrWhiteSpace(password))
    {
        throw new InvalidOperationException("MSSQL_SA_PASSWORD environment variable is not set. Ensure .env is configured.");
    }

    return $"Server=localhost,14333;Database=hub;User Id=sa;Password={password};Encrypt=True;TrustServerCertificate=True";
}

static string ResolveRedisConnectionString(WebApplicationBuilder builder)
{
    var configured = builder.Configuration.GetValue<string>("Redis:ConnectionString");
    if (!string.IsNullOrWhiteSpace(configured))
    {
        return configured;
    }

    if (builder.Environment.IsDevelopment())
    {
        return "localhost:6380";
    }

    throw new InvalidOperationException("Redis connection string must be configured.");
}

static async Task ApplyMigrationsInDevelopmentAsync(WebApplication app)
{
    if (!app.Environment.IsDevelopment())
    {
        return;
    }

    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>()
        .CreateLogger("DatabaseMigration");

    var delay = TimeSpan.FromSeconds(1);
    const int maxAttempts = 10;

    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            await dbContext.Database.MigrateAsync();
            return;
        }
        catch (Exception ex) when (attempt < maxAttempts)
        {
            logger.LogWarning(ex, "Attempt {Attempt} to apply migrations failed. Retrying in {Delay}.", attempt, delay);
            await Task.Delay(delay);
            var nextDelaySeconds = Math.Min(delay.TotalSeconds * 2, 10);
            delay = TimeSpan.FromSeconds(nextDelaySeconds);
        }
    }

    throw new InvalidOperationException("Unable to apply database migrations after multiple attempts.");
}

public partial class Program
{
}

internal sealed record BootstrapTenantRequest(string Name);
