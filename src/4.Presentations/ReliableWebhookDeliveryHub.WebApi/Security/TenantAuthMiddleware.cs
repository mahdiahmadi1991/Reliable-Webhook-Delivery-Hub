using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReliableWebhookDeliveryHub.Application.Security;
using ReliableWebhookDeliveryHub.Infrastructure.Persistence;

namespace ReliableWebhookDeliveryHub.WebApi.Security;

public sealed class TenantAuthMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(
        HttpContext context,
        ApiKeyService apiKeyService,
        AppDbContext dbContext,
        ITenantContext tenantContext,
        ILogger<TenantAuthMiddleware> logger)
    {
        var path = context.Request.Path;

        if (ShouldSkip(path))
        {
            await next(context);
            return;
        }

        if (!path.StartsWithSegments("/v1", StringComparison.OrdinalIgnoreCase))
        {
            await next(context);
            return;
        }

        var apiKeyHeader = context.Request.Headers["X-Api-Key"].FirstOrDefault();
        if (string.IsNullOrWhiteSpace(apiKeyHeader))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "API key is missing.");
            return;
        }

        if (!apiKeyService.TryParse(apiKeyHeader, out var parsedApiKey))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "API key is invalid.");
            return;
        }

        var tenant = await dbContext.Tenants.AsNoTracking().SingleOrDefaultAsync(t => t.Id == parsedApiKey.TenantId);
        if (tenant is null)
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "API key is invalid.");
            return;
        }

        if (!tenant.IsActive)
        {
            await WriteProblemAsync(context, StatusCodes.Status403Forbidden, "Tenant is disabled.");
            return;
        }

        var computedHash = SHA256.HashData(parsedApiKey.SecretBytes);
        if (!CryptographicOperations.FixedTimeEquals(computedHash, tenant.ApiKeyHash))
        {
            await WriteProblemAsync(context, StatusCodes.Status401Unauthorized, "API key is invalid.");
            return;
        }

        tenantContext.SetTenant(tenant.Id);

        using (logger.BeginScope(new Dictionary<string, object?>
               {
                   ["TenantId"] = tenant.Id
               }))
        {
            await next(context);
        }
    }

    private static bool ShouldSkip(PathString path)
    {
        if (path.StartsWithSegments("/health", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWithSegments("/swagger", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (path.StartsWithSegments("/dev", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static Task WriteProblemAsync(HttpContext context, int statusCode, string detail)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = statusCode switch
            {
                StatusCodes.Status401Unauthorized => "Unauthorized",
                StatusCodes.Status403Forbidden => "Forbidden",
                _ => "Error"
            },
            Detail = detail
        };

        return context.Response.WriteAsJsonAsync(problem);
    }
}
