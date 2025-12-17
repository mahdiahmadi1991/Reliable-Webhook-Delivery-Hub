using ReliableWebhookDeliveryHub.Domain.Audit;
using ReliableWebhookDeliveryHub.Infrastructure.Persistence;

namespace ReliableWebhookDeliveryHub.WebApi.Auditing;

internal sealed class AuditWriter
{
    public void AddDestinationAudit(AppDbContext dbContext, Guid tenantId, Guid destinationId, string action, string? beforeJson, string? afterJson)
    {
        var auditLog = new AuditLog
        {
            Id = Guid.NewGuid(),
            TenantId = tenantId,
            ActorType = "apiKey",
            ActorId = tenantId.ToString(),
            Action = action,
            EntityType = "Destination",
            EntityId = destinationId,
            BeforeJson = beforeJson,
            AfterJson = afterJson,
            CreatedAt = DateTimeOffset.UtcNow
        };

        dbContext.AuditLogs.Add(auditLog);
    }
}
