namespace ReliableWebhookDeliveryHub.WebApi.Security;

public sealed class TenantContext : ITenantContext
{
    public Guid TenantId { get; private set; }

    public bool IsAuthenticated { get; private set; }

    public void SetTenant(Guid tenantId)
    {
        TenantId = tenantId;
        IsAuthenticated = true;
    }
}
