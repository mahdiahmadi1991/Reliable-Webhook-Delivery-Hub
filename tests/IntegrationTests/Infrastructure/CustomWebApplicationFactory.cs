using System.Collections.Generic;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace ReliableWebhookDeliveryHub.IntegrationTests.Infrastructure;

public sealed class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _sqlConnectionString;
    private readonly string _redisConnectionString;
    private readonly string _environmentName;

    public CustomWebApplicationFactory(string sqlConnectionString, string redisConnectionString, string environmentName = "Development")
    {
        _sqlConnectionString = sqlConnectionString;
        _redisConnectionString = redisConnectionString;
        _environmentName = environmentName;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environmentName);
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:SqlServer"] = _sqlConnectionString,
                ["Redis:ConnectionString"] = _redisConnectionString,
                ["OpenTelemetry:Otlp:Endpoint"] = string.Empty
            };

            configurationBuilder.AddInMemoryCollection(overrides);
        });
    }
}
