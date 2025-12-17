using System.Net;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using ReliableWebhookDeliveryHub.IntegrationTests.Infrastructure;
using Xunit;

namespace ReliableWebhookDeliveryHub.IntegrationTests;

public class HealthEndpointTests : IClassFixture<IntegrationTestFixture>
{
    private readonly IntegrationTestFixture _fixture;

    public HealthEndpointTests(IntegrationTestFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Health_endpoint_returns_ok_and_applies_migrations()
    {
        await using var factory = new CustomWebApplicationFactory(_fixture.SqlConnectionString, _fixture.RedisConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(content);
        var root = json.RootElement;

        Assert.Equal("ok", root.GetProperty("status").GetString());
        Assert.Equal("ok", root.GetProperty("sqlServer").GetString());
        Assert.Equal("ok", root.GetProperty("redis").GetString());

        await using var connection = new SqlConnection(_fixture.SqlConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM [__EFMigrationsHistory]";
        var result = (int?)await command.ExecuteScalarAsync();

        Assert.True(result > 0);
    }
}
