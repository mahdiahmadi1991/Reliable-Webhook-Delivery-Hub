using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using ReliableWebhookDeliveryHub.Application.Security;
using ReliableWebhookDeliveryHub.IntegrationTests.Infrastructure;
using Xunit;

namespace ReliableWebhookDeliveryHub.IntegrationTests;

public class TenantAuthTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public TenantAuthTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Ping_without_api_key_returns_problem_details_401()
    {
        await using var factory = new CustomWebApplicationFactory(_fixture.SqlConnectionString, _fixture.RedisConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/v1/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":401", content);
    }

    [Fact]
    public async Task Ping_with_invalid_api_key_returns_problem_details_401()
    {
        await using var factory = new CustomWebApplicationFactory(_fixture.SqlConnectionString, _fixture.RedisConnectionString);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Api-Key", "invalid");

        var response = await client.GetAsync("/v1/ping");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.ToString());

        var content = await response.Content.ReadAsStringAsync();
        Assert.Contains("\"status\":401", content);
    }

    [Fact]
    public async Task Bootstrap_returns_api_key_and_ping_succeeds()
    {
        await using var factory = new CustomWebApplicationFactory(_fixture.SqlConnectionString, _fixture.RedisConnectionString);
        using var client = factory.CreateClient();

        var (_, apiKey) = await BootstrapTenantAsync(client, $"tenant-{Guid.NewGuid():N}");

        client.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
        var response = await client.GetAsync("/v1/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", content.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Bootstrap_stores_hash_only_and_matches_sha256()
    {
        await using var factory = new CustomWebApplicationFactory(_fixture.SqlConnectionString, _fixture.RedisConnectionString);
        using var client = factory.CreateClient();

        var (tenantId, apiKey) = await BootstrapTenantAsync(client, $"tenant-{Guid.NewGuid():N}");

        var apiKeyService = new ApiKeyService();
        Assert.True(apiKeyService.TryParse(apiKey, out var parsedApiKey));

        var expectedHash = ComputeSha256(parsedApiKey.SecretBytes);

        await using var connection = new SqlConnection(_fixture.SqlConnectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT [ApiKeyHash] FROM [Tenants] WHERE [Id] = @id";
        command.Parameters.AddWithValue("@id", tenantId);

        var result = await command.ExecuteScalarAsync();
        var storedHash = Assert.IsType<byte[]>(result);

        Assert.Equal(32, storedHash.Length);
        Assert.True(storedHash.SequenceEqual(expectedHash));
    }

    [Fact]
    public async Task Bootstrap_route_is_not_mapped_in_production()
    {
        await using var factory = new CustomWebApplicationFactory(
            _fixture.SqlConnectionString,
            _fixture.RedisConnectionString,
            environmentName: "Production");

        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/dev/bootstrap/tenants", new { name = "prod-tenant" });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static byte[] ComputeSha256(byte[] data)
    {
        return SHA256.HashData(data);
    }

    private static async Task<(Guid TenantId, string ApiKey)> BootstrapTenantAsync(HttpClient client, string name)
    {
        var response = await client.PostAsJsonAsync("/dev/bootstrap/tenants", new { name });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        await using var stream = await response.Content.ReadAsStreamAsync();
        using var json = await JsonDocument.ParseAsync(stream);
        var root = json.RootElement;

        var tenantId = root.GetProperty("tenantId").GetGuid();
        var apiKey = root.GetProperty("apiKey").GetString();

        Assert.False(string.IsNullOrWhiteSpace(apiKey));

        return (tenantId, apiKey!);
    }
}
