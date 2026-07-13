using Microsoft.Data.SqlClient;

namespace DriftWatch.Core.IntegrationTests;

/// <summary>
/// Spins up the test state on the dockerized SQL Server from
/// docker-compose.yml (never a real instance): creates two uniquely named
/// databases — one seeded with a view, procedure, scalar/table function,
/// trigger and one encrypted module, one containing only a table — and
/// drops them again on dispose.
///
/// IMPORTANT: all CREATE/DROP DATABASE and seed DDL lives HERE, in the
/// test fixture. DriftWatch.Core itself stays strictly read-only; this
/// fixture only exists to give the read-only code something to read.
///
/// If no server is reachable (docker not running), <see cref="IsAvailable"/>
/// stays false and the tests skip instead of failing.
/// </summary>
public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string DefaultSaPassword = "DriftW4tch!LocalTest";
    private static readonly TimeSpan ConnectDeadline = TimeSpan.FromSeconds(60);

    private readonly string _masterConnectionString;
    private readonly string _seededDbName = "DriftWatchTest_" + Guid.NewGuid().ToString("N");
    private readonly string _emptyDbName = "DriftWatchTestEmpty_" + Guid.NewGuid().ToString("N");

    public bool IsAvailable { get; private set; }
    public string SkipReason { get; private set; } =
        "SQL Server test container is not reachable (run 'docker compose up -d' first).";

    public string SeededConnectionString { get; private set; } = "";
    public string EmptyConnectionString { get; private set; } = "";

    public SqlServerFixture()
    {
        _masterConnectionString =
            Environment.GetEnvironmentVariable("DRIFTWATCH_TEST_CONNECTION_STRING")
            ?? BuildDefaultMasterConnectionString();
    }

    private static string BuildDefaultMasterConnectionString()
    {
        var password = Environment.GetEnvironmentVariable("DRIFTWATCH_TEST_SA_PASSWORD")
            ?? DefaultSaPassword;

        return new SqlConnectionStringBuilder
        {
            DataSource = "localhost,14333",
            UserID = "sa",
            Password = password,
            TrustServerCertificate = true,
            ConnectTimeout = 5,
        }.ConnectionString;
    }

    public async Task InitializeAsync()
    {
        if (!await WaitForServerAsync())
        {
            return;
        }

        await ExecuteOnMasterAsync($"CREATE DATABASE [{_seededDbName}]");
        await ExecuteOnMasterAsync($"CREATE DATABASE [{_emptyDbName}]");

        SeededConnectionString = WithDatabase(_seededDbName);
        EmptyConnectionString = WithDatabase(_emptyDbName);

        // Each CREATE <module> must be the only statement in its batch,
        // so every seed step runs as its own command.
        string[] seedBatches =
        [
            "CREATE TABLE dbo.Orders (Id INT NOT NULL PRIMARY KEY)",
            "CREATE VIEW dbo.OrdersView AS SELECT Id FROM dbo.Orders",
            "CREATE PROCEDURE dbo.GetOrders AS SELECT Id FROM dbo.Orders",
            "CREATE FUNCTION dbo.AddOne (@x INT) RETURNS INT AS BEGIN RETURN @x + 1 END",
            "CREATE FUNCTION dbo.OrdersTvf () RETURNS TABLE AS RETURN (SELECT Id FROM dbo.Orders)",
            "CREATE TRIGGER dbo.OrdersAudit ON dbo.Orders AFTER INSERT AS SET NOCOUNT ON",
            "CREATE PROCEDURE dbo.SecretProc WITH ENCRYPTION AS SELECT 1 AS X",
        ];

        await using (var connection = new SqlConnection(SeededConnectionString))
        {
            await connection.OpenAsync();
            foreach (var batch in seedBatches)
            {
                await using var command = new SqlCommand(batch, connection);
                await command.ExecuteNonQueryAsync();
            }
        }

        await using (var connection = new SqlConnection(EmptyConnectionString))
        {
            await connection.OpenAsync();
            await using var command = new SqlCommand(
                "CREATE TABLE dbo.JustATable (Id INT NOT NULL)", connection);
            await command.ExecuteNonQueryAsync();
        }

        IsAvailable = true;
    }

    public async Task DisposeAsync()
    {
        if (!IsAvailable)
        {
            return;
        }

        await DropDatabaseAsync(_seededDbName);
        await DropDatabaseAsync(_emptyDbName);
    }

    private async Task<bool> WaitForServerAsync()
    {
        var deadline = DateTime.UtcNow + ConnectDeadline;
        Exception? lastError = null;

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                await using var connection = new SqlConnection(_masterConnectionString);
                await connection.OpenAsync();
                return true;
            }
            catch (Exception ex) when (ex is SqlException or InvalidOperationException)
            {
                lastError = ex;
                await Task.Delay(TimeSpan.FromSeconds(2));
            }
        }

        SkipReason =
            $"SQL Server test container is not reachable after {ConnectDeadline.TotalSeconds:F0}s " +
            $"(run 'docker compose up -d' first). Last error: {lastError?.Message}";
        return false;
    }

    private string WithDatabase(string database) =>
        new SqlConnectionStringBuilder(_masterConnectionString)
        {
            InitialCatalog = database,
        }.ConnectionString;

    private async Task ExecuteOnMasterAsync(string sql)
    {
        await using var connection = new SqlConnection(_masterConnectionString);
        await connection.OpenAsync();
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private async Task DropDatabaseAsync(string database)
    {
        try
        {
            await ExecuteOnMasterAsync(
                $"""
                IF DB_ID(N'{database}') IS NOT NULL
                BEGIN
                    ALTER DATABASE [{database}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
                    DROP DATABASE [{database}];
                END
                """);
        }
        catch (SqlException)
        {
            // Cleanup is best-effort; a leaked DriftWatchTest_* database on
            // the throwaway container is harmless.
        }
    }
}
