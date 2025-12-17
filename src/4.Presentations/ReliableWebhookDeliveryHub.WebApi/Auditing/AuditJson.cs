using System.Text.Json;
using ReliableWebhookDeliveryHub.Domain.Destinations;

namespace ReliableWebhookDeliveryHub.WebApi.Auditing;

internal static class AuditJson
{
    public static string ForDestination(Destination destination, Dictionary<string, string>? headers)
    {
        var payload = new
        {
            destination.Id,
            destination.Name,
            destination.Url,
            destination.IsActive,
            Headers = headers,
            HasSecret = !string.IsNullOrEmpty(destination.SecretProtected),
            destination.CreatedAt,
            destination.UpdatedAt
        };

        return JsonSerializer.Serialize(payload);
    }
}
