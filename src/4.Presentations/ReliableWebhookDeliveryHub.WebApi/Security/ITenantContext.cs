namespace ReliableWebhookDeliveryHub.WebApi.Security;

public interface ITenantContext
{
    Guid TenantId { get; }

    bool IsAuthenticated { get; }
}
