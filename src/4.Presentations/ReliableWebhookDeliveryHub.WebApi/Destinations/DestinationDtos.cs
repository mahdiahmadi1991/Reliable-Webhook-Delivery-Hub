using System.Text.Json.Serialization;

namespace ReliableWebhookDeliveryHub.WebApi.Destinations;

public sealed record CreateDestinationRequest
{
    public string Name { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;

    public Dictionary<string, string>? Headers { get; init; }

    public string? Secret { get; init; }
}

public sealed record UpdateDestinationRequest
{
    public string Name { get; init; } = string.Empty;

    public string Url { get; init; } = string.Empty;

    public bool IsActive { get; init; }

    public Dictionary<string, string>? Headers { get; init; }

    public string? Secret { get; init; }

    public string RowVersion { get; init; } = string.Empty;
}

public sealed record DestinationResponse(
    Guid Id,
    string Name,
    string Url,
    bool IsActive,
    Dictionary<string, string>? Headers,
    bool HasSecret,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string RowVersion);

public sealed record PagedResponse<T>(
    [property: JsonPropertyName("items")] IReadOnlyList<T> Items,
    [property: JsonPropertyName("page")] int Page,
    [property: JsonPropertyName("pageSize")] int PageSize,
    [property: JsonPropertyName("totalCount")] long TotalCount);
