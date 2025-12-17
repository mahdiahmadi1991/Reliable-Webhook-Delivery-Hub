using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using ReliableWebhookDeliveryHub.IntegrationTests.Infrastructure;
using System.Linq;
using Xunit;

namespace ReliableWebhookDeliveryHub.IntegrationTests;

public class DestinationsCrudTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public DestinationsCrudTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Create_destination_list_and_write_audit_log()
    {
        await using var factory = new CustomWebApplicationFactory(_fixture.SqlConnectionString, _fixture.RedisConnectionString);
        using var client = factory.CreateClient();

        var (tenantId, apiKey) = await BootstrapTenantAsync(client);

        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);

        var secret = "plain-secret-value";
        var createResponse = await client.PostAsJsonAsync(
            "/v1/destinations",
            new
            {
                name = "Orders Webhook",
                url = "https://example.com/webhook",
                isActive = true,
                headers = new Dictionary<string, string> { { "X-Test", "value" } },
                secret
            });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        using var createJson = await JsonDocument.ParseAsync(await createResponse.Content.ReadAsStreamAsync());
        var createRoot = createJson.RootElement;

        Assert.True(createRoot.TryGetProperty("id", out var idProperty));
        var destinationId = idProperty.GetGuid();

        Assert.True(createRoot.TryGetProperty("hasSecret", out var hasSecretProperty));
        Assert.True(hasSecretProperty.GetBoolean());

        Assert.True(createRoot.TryGetProperty("rowVersion", out var rowVersionProperty));
        Assert.False(string.IsNullOrWhiteSpace(rowVersionProperty.GetString()));

        foreach (var property in createRoot.EnumerateObject())
        {
            Assert.NotEqual("secret", property.Name, StringComparer.OrdinalIgnoreCase);
        }

        var listResponse = await client.GetAsync("/v1/destinations?page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        using var listJson = await JsonDocument.ParseAsync(await listResponse.Content.ReadAsStreamAsync());
        var listRoot = listJson.RootElement;
        Assert.True(listRoot.TryGetProperty("totalCount", out var totalCountProperty));
        Assert.True(totalCountProperty.GetInt64() >= 1);

        var items = listRoot.GetProperty("items").EnumerateArray().ToList();
        Assert.Contains(items, item => item.GetProperty("id").GetGuid() == destinationId);

        await using var connection = new SqlConnection(_fixture.SqlConnectionString);
        await connection.OpenAsync();

        await using (var secretCommand = connection.CreateCommand())
        {
            secretCommand.CommandText = "SELECT [SecretProtected] FROM [Destinations] WHERE [Id] = @id";
            secretCommand.Parameters.AddWithValue("@id", destinationId);

            var storedSecret = (string?)await secretCommand.ExecuteScalarAsync();
            Assert.False(string.IsNullOrWhiteSpace(storedSecret));
            Assert.NotEqual(secret, storedSecret);
        }

        await using (var auditCommand = connection.CreateCommand())
        {
            auditCommand.CommandText = """
                SELECT COUNT(*)
                FROM [AuditLogs]
                WHERE [TenantId] = @tenantId AND [EntityType] = 'Destination' AND [EntityId] = @entityId AND [Action] = 'DestinationCreated'
                """;
            auditCommand.Parameters.AddWithValue("@tenantId", tenantId);
            auditCommand.Parameters.AddWithValue("@entityId", destinationId);

            var auditCount = (int)await auditCommand.ExecuteScalarAsync();
            Assert.Equal(1, auditCount);
        }
    }

    private static async Task<(Guid TenantId, string ApiKey)> BootstrapTenantAsync(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/dev/bootstrap/tenants", new { name = $"tenant-{Guid.NewGuid():N}" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);
        var root = json.RootElement;

        var tenantId = root.GetProperty("tenantId").GetGuid();
        var apiKey = root.GetProperty("apiKey").GetString();

        return (tenantId, apiKey!);
    }
}
