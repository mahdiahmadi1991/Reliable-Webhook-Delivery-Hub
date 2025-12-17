namespace ReliableWebhookDeliveryHub.Domain.Audit;

public class AuditLog
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string ActorType { get; set; } = string.Empty;

    public string ActorId { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string? BeforeJson { get; set; }

    public string? AfterJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }
}
