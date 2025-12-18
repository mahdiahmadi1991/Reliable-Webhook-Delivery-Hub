using System.Data.Common;
using Microsoft.Data.SqlClient;
using Testcontainers.MsSql;
using Testcontainers.Redis;
using StackExchange.Redis;
using Xunit;

namespace ReliableWebhookDeliveryHub.IntegrationTests.Infrastructure;

public class IntegrationTestFixture : IAsyncLifetime
{
    private const string SqlSaPassword = "P@ssword123456!";
    private const string DefaultDatabaseName = "hub";
    private const string SqlConnectionStringEnvVar = "ConnectionStrings__SqlServer";
    private const string RedisConnectionStringEnvVar = "Redis__ConnectionString";

    private MsSqlContainer? _sqlContainer;
    private RedisContainer? _redisContainer;
    private bool _ownsSqlContainer;
    private bool _ownsRedisContainer;

    public string SqlConnectionString { get; private set; } = string.Empty;

    public string RedisConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var providedSqlConnectionString = Environment.GetEnvironmentVariable(SqlConnectionStringEnvVar);
        var providedRedisConnectionString = Environment.GetEnvironmentVariable(RedisConnectionStringEnvVar);

        if (!string.IsNullOrWhiteSpace(providedSqlConnectionString) &&
            !string.IsNullOrWhiteSpace(providedRedisConnectionString))
        {
            SqlConnectionString = providedSqlConnectionString;
            RedisConnectionString = providedRedisConnectionString;

            await EnsureSqlReadyAsync(SqlConnectionString, DefaultDatabaseName);
            await EnsureRedisReadyAsync(RedisConnectionString);

            return;
        }

        _ownsSqlContainer = true;
        _sqlContainer = new MsSqlBuilder()
            .WithPassword(SqlSaPassword)
            .WithPortBinding(1433, true)
            .Build();

        _ownsRedisContainer = true;
        _redisContainer = new RedisBuilder()
            .WithImage("redis:7-alpine")
            .WithPortBinding(6379, true)
            .Build();

        await Task.WhenAll(_sqlContainer.StartAsync(), _redisContainer.StartAsync());

        var sqlHost = _sqlContainer.Hostname;
        var sqlPort = _sqlContainer.GetMappedPublicPort(1433);
        var redisHost = _redisContainer.Hostname;
        var redisPort = _redisContainer.GetMappedPublicPort(6379);

        SqlConnectionString = BuildSqlConnectionString(sqlHost, sqlPort, DefaultDatabaseName);
        RedisConnectionString = $"{redisHost}:{redisPort}";

        await EnsureSqlReadyAsync(SqlConnectionString, DefaultDatabaseName);
        await EnsureRedisReadyAsync(RedisConnectionString);
    }

    public async Task DisposeAsync()
    {
        if (_ownsSqlContainer && _sqlContainer is not null)
        {
            await _sqlContainer.DisposeAsync();
        }

        if (_ownsRedisContainer && _redisContainer is not null)
        {
            await _redisContainer.DisposeAsync();
        }
    }

    private static async Task EnsureSqlReadyAsync(string connectionString, string databaseName)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var builder = new SqlConnectionStringBuilder(connectionString);

        if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
        {
            builder.InitialCatalog = databaseName;
        }

        var targetDatabaseConnectionString = builder.ConnectionString;
        var masterConnectionString = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master"
        }.ConnectionString;

        while (!cts.IsCancellationRequested)
        {
            try
            {
                await using var connection = new SqlConnection(masterConnectionString);
                await connection.OpenAsync(cts.Token);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT 1";
                await command.ExecuteScalarAsync(cts.Token);

                await EnsureDatabaseExistsAsync(connection, databaseName, cts.Token);
                await VerifyDatabaseConnectionAsync(targetDatabaseConnectionString, cts.Token);
                return;
            }
            catch (Exception) when (!cts.IsCancellationRequested)
            {
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        throw new TimeoutException("SQL Server container failed to become ready in time.");
    }

    private static async Task EnsureDatabaseExistsAsync(DbConnection connection, string databaseName, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID('{databaseName}') IS NULL CREATE DATABASE [{databaseName}];";
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task VerifyDatabaseConnectionAsync(string connectionString, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static async Task EnsureRedisReadyAsync(string connectionString)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));

        while (!cts.IsCancellationRequested)
        {
            try
            {
                using var multiplexer = await ConnectionMultiplexer.ConnectAsync(connectionString);
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
