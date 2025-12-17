namespace ReliableWebhookDeliveryHub.Domain.Destinations;

public class Destination
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public string? HeadersJson { get; set; }

    public string? SecretProtected { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public byte[] RowVersion { get; set; } = Array.Empty<byte>();
}
