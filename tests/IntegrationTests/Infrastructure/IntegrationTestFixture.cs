using System.Data.Common;
using Microsoft.Data.SqlClient;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using StackExchange.Redis;
using Xunit;

namespace ReliableWebhookDeliveryHub.IntegrationTests.Infrastructure;

public class IntegrationTestFixture : IAsyncLifetime
{
    private const string SqlSaPassword = "P@ssword123456!";

    private IContainer? _sqlContainer;
    private IContainer? _redisContainer;

    public string SqlConnectionString { get; private set; } = string.Empty;

    public string RedisConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        _sqlContainer = BuildSqlContainer();
        _redisContainer = BuildRedisContainer();

        await Task.WhenAll(_sqlContainer.StartAsync(), _redisContainer.StartAsync());

        var sqlHost = _sqlContainer.Hostname;
        var sqlPort = _sqlContainer.GetMappedPublicPort(1433);
        var redisHost = _redisContainer.Hostname;
        var redisPort = _redisContainer.GetMappedPublicPort(6379);

        await EnsureSqlReadyAsync(sqlHost, sqlPort);
        await EnsureRedisReadyAsync(redisHost, redisPort);

        SqlConnectionString = BuildSqlConnectionString(sqlHost, sqlPort, "hub");
        RedisConnectionString = $"{redisHost}:{redisPort}";
    }

    public async Task DisposeAsync()
    {
        if (_sqlContainer is not null)
        {
            await _sqlContainer.DisposeAsync();
        }

        if (_redisContainer is not null)
        {
            await _redisContainer.DisposeAsync();
        }
    }

    private static IContainer BuildSqlContainer()
    {
        return new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .WithEnvironment("ACCEPT_EULA", "Y")
            .WithEnvironment("MSSQL_PID", "Developer")
            .WithEnvironment("MSSQL_SA_PASSWORD", SqlSaPassword)
            .WithPortBinding(1433, true)
            .WithCreateContainerParametersModifier(parameters => parameters.Platform = "linux/amd64")
            .Build();
    }

    private static IContainer BuildRedisContainer()
    {
        return new TestcontainersBuilder<TestcontainersContainer>()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .Build();
    }

    private static async Task EnsureSqlReadyAsync(string host, int port)
    {
        var connectionString = BuildSqlConnectionString(host, port, null);
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));

        while (!cts.IsCancellationRequested)
        {
            try
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cts.Token);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cts.Token);

                await EnsureDatabaseExistsAsync(connection, cts.Token);
                return;
            }
            catch (Exception) when (!cts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new TimeoutException("SQL Server container failed to become ready in time.");
    }

    private static async Task EnsureDatabaseExistsAsync(DbConnection connection, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "IF DB_ID('hub') IS NULL CREATE DATABASE hub;";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task EnsureRedisReadyAsync(string host, int port)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        while (!cts.IsCancellationRequested)
        {
            try
            {
                using var multiplexer = await ConnectionMultiplexer.ConnectAsync($"{host}:{port}");
                await multiplexer.GetDatabase().PingAsync();
                return;
            }
            catch (Exception) when (!cts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
            }
        }

        throw new TimeoutException("Redis container failed to become ready in time.");
    }

    private static string BuildSqlConnectionString(string host, int port, string? database)
    {
        var builder = new SqlConnectionStringBuilder
        {
            DataSource = $"{host},{port}",
            UserID = "sa",
            Password = SqlSaPassword,
            Encrypt = true,
            TrustServerCertificate = true
        };

        if (!string.IsNullOrWhiteSpace(database))
        {
            builder.InitialCatalog = database;
        }

        return builder.ConnectionString;
    }
}
