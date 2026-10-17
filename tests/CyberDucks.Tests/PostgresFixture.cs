using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Npgsql;
using Testcontainers.PostgreSql;

[assembly: AssemblyFixture(typeof(CyberDucks.Tests.PostgresFixture))]

namespace CyberDucks.Tests;

/// <summary>
/// A throwaway Postgres built from the range's own init scripts (infra/postgres/init), so tests
/// run against the real schema, roles, guards and seed. One container is shared by all tests.
/// Passwords are fake test values.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private const string Password = "test-password";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder("postgres:18")
        .WithDatabase("duckstore")
        .WithUsername("duckadmin")
        .WithPassword(Password)
        .WithEnvironment(new Dictionary<string, string>
        {
            ["STORE_OWNER_PASSWORD"] = Password,
            ["STORE_APP_PASSWORD"] = Password,
            ["RANGE_CONTROL_PASSWORD"] = Password,
            ["FLAGS_READER_PASSWORD"] = Password,
            ["BACKUP_READER_PASSWORD"] = Password,
            ["METRICS_READER_PASSWORD"] = Password,
        })
        .WithBindMount(Path.Combine(RepoRoot(), "infra", "postgres", "init"), "/docker-entrypoint-initdb.d", AccessMode.ReadOnly)
        // During init Postgres only listens on a local socket; TCP readiness means init has finished.
        .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("pg_isready", "-h", "127.0.0.1", "-U", "duckadmin"))
        .Build();

    public ValueTask InitializeAsync() => new(_container.StartAsync());

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Connection string for one of the range's roles.</summary>
    public string ConnectionString(string role, string database) =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString())
        {
            Username = role,
            Password = Password,
            Database = database,
        }.ConnectionString;

    private static string RepoRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CyberDucks.slnx")))
            {
                return dir.FullName;
            }
        }
        throw new InvalidOperationException("Could not find the repository root (CyberDucks.slnx).");
    }
}
