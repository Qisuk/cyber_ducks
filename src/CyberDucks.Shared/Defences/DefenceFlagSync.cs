using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using OpenTelemetry;

namespace CyberDucks.Shared.Defences;

/// <summary>
/// Keeps <see cref="DefenceFlagStore"/> in step with range_control.defence_flags.
/// Loads everything on start, then LISTENs for the 'defence_flags' notification sent by the
/// on_flag_change trigger (04-range-control.sql). A periodic full reload covers missed
/// notifications, and the loop reconnects if the database goes away.
/// Connects as flags_reader, which can only SELECT the flags table.
/// </summary>
public sealed class DefenceFlagSync(
    DefenceFlagStore store,
    DefenceFlagOptions options,
    ILogger<DefenceFlagSync> logger) : BackgroundService
{
    private static readonly TimeSpan FullReloadInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(5);

    private bool _loaded;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var conn = new NpgsqlConnection(options.ConnectionString);
                using (Untraced())
                {
                    await conn.OpenAsync(stoppingToken);
                }

                var reload = false;
                conn.Notification += (_, _) => reload = true;

                await ReloadAsync(conn, stoppingToken);
                using (Untraced())
                await using (var listen = new NpgsqlCommand("LISTEN defence_flags", conn))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                }

                var nextFullReload = DateTime.UtcNow + FullReloadInterval;
                while (!stoppingToken.IsCancellationRequested)
                {
                    // Returns early when a notification arrives.
                    await conn.WaitAsync(TimeSpan.FromSeconds(1), stoppingToken);
                    if (reload || DateTime.UtcNow >= nextFullReload)
                    {
                        reload = false;
                        await ReloadAsync(conn, stoppingToken);
                        nextFullReload = DateTime.UtcNow + FullReloadInterval;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Defence flag sync failed; unknown flags default to Vulnerable. Retrying in {Delay}", RetryDelay);
                await Task.Delay(RetryDelay, stoppingToken);
            }
        }
    }

    private async Task ReloadAsync(NpgsqlConnection conn, CancellationToken ct)
    {
        var flags = new Dictionary<string, DefenceMode>(StringComparer.Ordinal);
        using (Untraced())
        await using (var cmd = new NpgsqlCommand("SELECT vuln_id, mode FROM defence_flags", conn))
        await using (var reader = await cmd.ExecuteReaderAsync(ct))
        {
            while (await reader.ReadAsync(ct))
            {
                flags[reader.GetString(0)] =
                    reader.GetString(1) == "secure" ? DefenceMode.Secure : DefenceMode.Vulnerable;
            }
        }

        var changed = store.Replace(flags);
        if (!_loaded)
        {
            // First load: one summary line rather than one per flag.
            _loaded = true;
            logger.LogInformation("Loaded {FlagCount} defence flags ({SecureCount} secure)",
                flags.Count, flags.Values.Count(m => m == DefenceMode.Secure));
            return;
        }

        foreach (var (vulnId, mode) in changed)
        {
            logger.LogInformation("Defence flag {VulnId} is now {DefenceMode}", vulnId, mode);
        }
    }

    /// <summary>
    /// Background polling is not user activity, so its queries are kept out of traces. The scope also
    /// drops OpenTelemetry log records, so it must wrap only the database calls, never the logging.
    /// </summary>
    private static IDisposable Untraced() => SuppressInstrumentationScope.Begin();
}

public sealed record DefenceFlagOptions(string ConnectionString);
