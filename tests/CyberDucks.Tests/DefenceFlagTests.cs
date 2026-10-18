using CyberDucks.Shared.Defences;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;

namespace CyberDucks.Tests;

public sealed class DefenceFlagTests(PostgresFixture db)
{
    [Fact]
    public async Task Sync_loads_seeded_flags_all_vulnerable()
    {
        var (store, sync) = await StartSyncAsync();
        try
        {
            Assert.Equal(29, store.Snapshot.Count);
            Assert.All(store.Snapshot.Values, mode => Assert.Equal(DefenceMode.Vulnerable, mode));
        }
        finally
        {
            await sync.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task Sync_picks_up_a_flag_change_via_notify()
    {
        var (store, sync) = await StartSyncAsync();
        try
        {
            await SetFlagAsync("VULN-IDOR-01", "secure");
            await WaitUntilAsync(() => store.Get("VULN-IDOR-01") == DefenceMode.Secure, TimeSpan.FromSeconds(5));
            Assert.True(((IDefenceFlags)store).IsSecure("VULN-IDOR-01"));
        }
        finally
        {
            await SetFlagAsync("VULN-IDOR-01", "vulnerable");
            await sync.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void Unknown_vuln_id_defaults_to_vulnerable()
    {
        Assert.Equal(DefenceMode.Vulnerable, new DefenceFlagStore().Get("VULN-DOES-NOT-EXIST"));
    }

    [Theory]
    [InlineData(DefenceMode.Vulnerable, typeof(VulnerableGreeter))]
    [InlineData(DefenceMode.Secure, typeof(SecureGreeter))]
    public void AddDefence_resolves_implementation_from_flag(DefenceMode mode, Type expected)
    {
        var store = new DefenceFlagStore();
        store.Replace(new Dictionary<string, DefenceMode> { ["VULN-TEST-01"] = mode });

        var services = new ServiceCollection();
        services.AddSingleton<IDefenceFlags>(store);
        services.AddDefence<IGreeter, VulnerableGreeter, SecureGreeter>("VULN-TEST-01");

        using var provider = services.BuildServiceProvider();
        using var scope = provider.CreateScope();
        Assert.IsType(expected, scope.ServiceProvider.GetRequiredService<IGreeter>());
    }

    private async Task<(DefenceFlagStore Store, DefenceFlagSync Sync)> StartSyncAsync()
    {
        var store = new DefenceFlagStore();
        var sync = new DefenceFlagSync(store,
            new DefenceFlagOptions(db.ConnectionString("flags_reader", "range_control")),
            NullLogger<DefenceFlagSync>.Instance);
        await sync.StartAsync(CancellationToken.None);
        await WaitUntilAsync(() => store.Snapshot.Count > 0, TimeSpan.FromSeconds(10));
        return (store, sync);
    }

    // Flags are changed the way range-control will change them: as range_control_owner.
    private async Task SetFlagAsync(string vulnId, string mode)
    {
        await using var conn = new NpgsqlConnection(db.ConnectionString("range_control_owner", "range_control"));
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            "UPDATE defence_flags SET mode = @mode, updated_by = 'test' WHERE vuln_id = @id", conn);
        cmd.Parameters.AddWithValue("mode", mode);
        cmd.Parameters.AddWithValue("id", vulnId);
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;
        while (!condition())
        {
            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException($"Condition not met within {timeout}.");
            }
            await Task.Delay(50);
        }
    }

    public interface IGreeter;
    public sealed class VulnerableGreeter : IGreeter;
    public sealed class SecureGreeter : IGreeter;
}
