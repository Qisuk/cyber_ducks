using Npgsql;

namespace RangeControl.Wargame;

public sealed record CustomScenarioRow(string Id, string Yaml, DateTimeOffset UpdatedAt, string? UpdatedBy);

/// <summary>
/// Facilitator-authored scenarios stored in range_control.custom_scenarios (the scenario editor).
/// Keeps a parsed in-memory cache (refreshed at startup and after every write) so the catalogue can
/// merge them with the file scenarios synchronously.
/// </summary>
public sealed class CustomScenarioStore(NpgsqlDataSource controlDb, ILogger<CustomScenarioStore> log) : IHostedService
{
    private volatile IReadOnlyList<Scenario> _cache = [];
    public IReadOnlyList<Scenario> Cached => _cache;

    public Task StartAsync(CancellationToken ct) => RefreshAsync(ct);
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;

    public async Task RefreshAsync(CancellationToken ct = default)
    {
        try
        {
            var list = new List<Scenario>();
            await using var cmd = controlDb.CreateCommand("SELECT id, yaml FROM custom_scenarios ORDER BY id");
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                try
                {
                    var s = ScenarioCatalog.Parse(r.GetString(1));
                    if (!string.IsNullOrWhiteSpace(s.Id)) list.Add(s);
                }
                catch (Exception ex)
                {
                    log.LogWarning(ex, "Skipping unparseable custom scenario {Id}", r.GetString(0));
                }
            }
            _cache = list;
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not load custom scenarios.");
        }
    }

    public async Task<IReadOnlyList<CustomScenarioRow>> ListAsync(CancellationToken ct = default)
    {
        var list = new List<CustomScenarioRow>();
        await using var cmd = controlDb.CreateCommand("SELECT id, yaml, updated_at, updated_by FROM custom_scenarios ORDER BY id");
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
            list.Add(new CustomScenarioRow(r.GetString(0), r.GetString(1),
                r.GetFieldValue<DateTimeOffset>(2), r.IsDBNull(3) ? null : r.GetString(3)));
        return list;
    }

    public async Task<string?> GetYamlAsync(string id, CancellationToken ct = default)
    {
        await using var cmd = controlDb.CreateCommand("SELECT yaml FROM custom_scenarios WHERE id = @id");
        cmd.Parameters.AddWithValue("id", id);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    /// <summary>Validates the YAML (must parse and its id must match) and upserts it, then refreshes.</summary>
    public async Task SaveAsync(string yaml, string by, CancellationToken ct = default)
    {
        var scenario = ScenarioCatalog.Parse(yaml); // throws on invalid YAML
        if (string.IsNullOrWhiteSpace(scenario.Id))
            throw new InvalidOperationException("The scenario needs an `id:` field.");

        await using (var cmd = controlDb.CreateCommand("""
            INSERT INTO custom_scenarios (id, yaml, updated_by, updated_at) VALUES (@id, @yaml, @by, now())
            ON CONFLICT (id) DO UPDATE SET yaml = EXCLUDED.yaml, updated_by = EXCLUDED.updated_by, updated_at = now()
            """))
        {
            cmd.Parameters.AddWithValue("id", scenario.Id);
            cmd.Parameters.AddWithValue("yaml", yaml);
            cmd.Parameters.AddWithValue("by", by);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await RefreshAsync(ct);
    }

    public async Task DeleteAsync(string id, CancellationToken ct = default)
    {
        await using (var cmd = controlDb.CreateCommand("DELETE FROM custom_scenarios WHERE id = @id"))
        {
            cmd.Parameters.AddWithValue("id", id);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await RefreshAsync(ct);
    }
}
