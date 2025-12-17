using System.Text.Json;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ReliableWebhookDeliveryHub.Domain.Destinations;
using ReliableWebhookDeliveryHub.Infrastructure.Persistence;
using ReliableWebhookDeliveryHub.WebApi.Auditing;
using ReliableWebhookDeliveryHub.WebApi.Security;

namespace ReliableWebhookDeliveryHub.WebApi.Destinations;

internal static class DestinationsEndpoints
{
    private const string SecretPurpose = "Destinations.Secret.v1";

    public static IEndpointRouteBuilder MapDestinationsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/v1/destinations");

        group.MapPost(string.Empty, CreateDestinationAsync);
        group.MapGet("/{id:guid}", GetDestinationAsync);
        group.MapGet(string.Empty, ListDestinationsAsync);
        group.MapPut("/{id:guid}", UpdateDestinationAsync);
        group.MapDelete("/{id:guid}", DeleteDestinationAsync);

        return endpoints;
    }

    private static async Task<IResult> CreateDestinationAsync(
        [FromBody] CreateDestinationRequest request,
        AppDbContext dbContext,
        ITenantContext tenantContext,
        IDataProtectionProvider dataProtectionProvider,
        AuditWriter auditWriter,
        CancellationToken ct)
    {
        if (ValidateCreateRequest(request) is { } validationError)
        {
            return validationError;
        }

        var headersJson = SerializeHeaders(request.Headers);
        if (headersJson is string serialized && serialized.Length > 8000)
        {
            return ValidationProblem("Headers are too large. Maximum serialized length is 8000 characters.");
        }

        var now = DateTimeOffset.UtcNow;
        var tenantId = tenantContext.TenantId;
        var destination = new Destination
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            Name = request.Name.Trim(),
            Url = request.Url.Trim(),
            IsActive = request.IsActive,
            HeadersJson = headersJson,
            CreatedAt = now,
            UpdatedAt = now
        };

        if (!string.IsNullOrEmpty(request.Secret))
        {
            var protector = dataProtectionProvider.CreateProtector(SecretPurpose);
            destination.SecretProtected = protector.Protect(request.Secret);
        }

        dbContext.Destinations.Add(destination);

        var afterJson = AuditJson.ForDestination(destination, request.Headers);
        auditWriter.AddDestinationAudit(dbContext, tenantId, destination.Id, "DestinationCreated", null, afterJson);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return ConflictProblem("Destination name already exists.");
        }

        var response = MapToResponse(destination);
        return Results.Json(response, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> GetDestinationAsync(
        Guid id,
        AppDbContext dbContext,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var destination = await dbContext.Destinations
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.Id == id && d.TenantId == tenantContext.TenantId, ct);

        if (destination is null)
        {
            return NotFoundProblem();
        }

        return Results.Json(MapToResponse(destination));
    }

    private static async Task<IResult> ListDestinationsAsync(
        [FromQuery] int page,
        [FromQuery] int pageSize,
        AppDbContext dbContext,
        ITenantContext tenantContext,
        CancellationToken ct)
    {
        var validatedPage = page <= 0 ? 1 : page;
        var validatedPageSize = pageSize <= 0 ? 50 : Math.Min(pageSize, 200);

        var query = dbContext.Destinations
            .AsNoTracking()
            .Where(d => d.TenantId == tenantContext.TenantId);

        var totalCount = await query.LongCountAsync(ct);

        var items = await query
            .OrderByDescending(d => d.CreatedAt)
            .Skip((validatedPage - 1) * validatedPageSize)
            .Take(validatedPageSize)
            .ToListAsync(ct);

        var response = new PagedResponse<DestinationResponse>(
            items.Select(MapToResponse).ToList(),
            validatedPage,
            validatedPageSize,
            totalCount);

        return Results.Json(response);
    }

    private static async Task<IResult> UpdateDestinationAsync(
        Guid id,
        [FromBody] UpdateDestinationRequest request,
        AppDbContext dbContext,
        ITenantContext tenantContext,
        IDataProtectionProvider dataProtectionProvider,
        AuditWriter auditWriter,
        CancellationToken ct)
    {
        if (ValidateUpdateRequest(request) is { } validationError)
        {
            return validationError;
        }

        byte[] rowVersionBytes;
        try
        {
            rowVersionBytes = Convert.FromBase64String(request.RowVersion);
        }
        catch (FormatException)
        {
            return ValidationProblem("RowVersion must be a valid base64 string.");
        }

        var destination = await dbContext.Destinations
            .SingleOrDefaultAsync(d => d.Id == id && d.TenantId == tenantContext.TenantId, ct);

        if (destination is null)
        {
            return NotFoundProblem();
        }

        if (!destination.RowVersion.SequenceEqual(rowVersionBytes))
        {
            return ConflictProblem("RowVersion mismatch.");
        }

        var headersJson = SerializeHeaders(request.Headers);
        if (headersJson is string serialized && serialized.Length > 8000)
        {
            return ValidationProblem("Headers are too large. Maximum serialized length is 8000 characters.");
        }

        var beforeHeaders = DeserializeHeaders(destination.HeadersJson);
        var beforeJson = AuditJson.ForDestination(destination, beforeHeaders);

        destination.Name = request.Name.Trim();
        destination.Url = request.Url.Trim();
        destination.IsActive = request.IsActive;
        destination.HeadersJson = headersJson;
        destination.UpdatedAt = DateTimeOffset.UtcNow;

        var secretChanged = false;
        if (request.Secret is not null)
        {
            secretChanged = true;
            if (request.Secret.Length == 0)
            {
                destination.SecretProtected = null;
            }
            else
            {
                var protector = dataProtectionProvider.CreateProtector(SecretPurpose);
                destination.SecretProtected = protector.Protect(request.Secret);
            }
        }

        var afterJson = AuditJson.ForDestination(destination, request.Headers);
        var action = secretChanged ? "DestinationSecretUpdated" : "DestinationUpdated";
        auditWriter.AddDestinationAudit(dbContext, tenantContext.TenantId, destination.Id, action, beforeJson, afterJson);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConflictProblem("RowVersion mismatch.");
        }
        catch (DbUpdateException ex) when (IsUniqueConstraintViolation(ex))
        {
            return ConflictProblem("Destination name already exists.");
        }

        return Results.Json(MapToResponse(destination));
    }

    private static async Task<IResult> DeleteDestinationAsync(
        Guid id,
        AppDbContext dbContext,
        ITenantContext tenantContext,
        AuditWriter auditWriter,
        CancellationToken ct)
    {
        var destination = await dbContext.Destinations
            .SingleOrDefaultAsync(d => d.Id == id && d.TenantId == tenantContext.TenantId, ct);

        if (destination is null)
        {
            return NotFoundProblem();
        }

        var beforeHeaders = DeserializeHeaders(destination.HeadersJson);
        var beforeJson = AuditJson.ForDestination(destination, beforeHeaders);

        dbContext.Destinations.Remove(destination);
        auditWriter.AddDestinationAudit(dbContext, tenantContext.TenantId, destination.Id, "DestinationDeleted", beforeJson, null);

        try
        {
            await dbContext.SaveChangesAsync(ct);
        }
        catch (DbUpdateConcurrencyException)
        {
            return ConflictProblem("RowVersion mismatch.");
        }

        return Results.StatusCode(StatusCodes.Status204NoContent);
    }

    private static IResult? ValidateCreateRequest(CreateDestinationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
        {
            return ValidationProblem("Name is required and must be 200 characters or fewer.");
        }

        if (!IsValidUrl(request.Url))
        {
            return ValidationProblem("Url must be an absolute http or https URL and 2048 characters or fewer.");
        }

        if (!AreHeadersValid(request.Headers))
        {
            return ValidationProblem("Headers keys and values must be non-empty strings.");
        }

        if (request.Secret is { Length: > 2000 })
        {
            return ValidationProblem("Secret must be 2000 characters or fewer.");
        }

        return null;
    }

    private static IResult? ValidateUpdateRequest(UpdateDestinationRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RowVersion))
        {
            return ValidationProblem("RowVersion is required.");
        }

        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 200)
        {
            return ValidationProblem("Name is required and must be 200 characters or fewer.");
        }

        if (!IsValidUrl(request.Url))
        {
            return ValidationProblem("Url must be an absolute http or https URL and 2048 characters or fewer.");
        }

        if (!AreHeadersValid(request.Headers))
        {
            return ValidationProblem("Headers keys and values must be non-empty strings.");
        }

        if (request.Secret is { Length: > 2000 })
        {
            return ValidationProblem("Secret must be 2000 characters or fewer.");
        }

        return null;
    }

    private static bool IsValidUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url) || url.Length > 2048)
        {
            return false;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
    }

    private static bool AreHeadersValid(Dictionary<string, string>? headers)
    {
        if (headers is null)
        {
            return true;
        }

        foreach (var (key, value) in headers)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(value))
            {
                return false;
            }
        }

        return true;
    }

    private static string? SerializeHeaders(Dictionary<string, string>? headers)
    {
        return headers is null ? null : JsonSerializer.Serialize(headers);
    }

    private static Dictionary<string, string>? DeserializeHeaders(string? headersJson)
    {
        if (string.IsNullOrWhiteSpace(headersJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string>>(headersJson);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DestinationResponse MapToResponse(Destination destination)
    {
        var headers = DeserializeHeaders(destination.HeadersJson);

        return new DestinationResponse(
            destination.Id,
            destination.Name,
            destination.Url,
            destination.IsActive,
            headers,
            !string.IsNullOrEmpty(destination.SecretProtected),
            destination.CreatedAt,
            destination.UpdatedAt,
            Convert.ToBase64String(destination.RowVersion));
    }

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        return exception.InnerException is SqlException { Number: 2601 or 2627 };
    }

    private static IResult NotFoundProblem()
    {
        return Results.Problem(
            title: "Not Found",
            detail: "Destination was not found.",
            statusCode: StatusCodes.Status404NotFound);
    }

    private static IResult ValidationProblem(string detail)
    {
        return Results.Problem(
            title: "Invalid request",
            detail: detail,
            statusCode: StatusCodes.Status400BadRequest);
    }

    private static IResult ConflictProblem(string detail)
    {
        return Results.Problem(
            title: "Conflict",
            detail: detail,
            statusCode: StatusCodes.Status409Conflict);
    }
}
