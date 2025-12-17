namespace ReliableWebhookDeliveryHub.Domain.Tenants;

public class Tenant
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public byte[] ApiKeyHash { get; set; } = Array.Empty<byte>();

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
