using Npgsql;

namespace CyberDucks.Shared.Ctf;

/// <summary>
/// Reads CTF flag values by slug from range_control.ctf_flags (the flags_reader connection), for
/// code-served flags (a hidden endpoint, an unlinked page, a staff banner, a secret agent tool, …).
/// Values are generated per deployment, so they are never in Git.
/// </summary>
public sealed class CtfFlagReader(string? connectionString)
{
    private NpgsqlDataSource? _db;
    private NpgsqlDataSource? Db =>
        _db ??= string.IsNullOrWhiteSpace(connectionString) ? null : NpgsqlDataSource.Create(connectionString);

    public async Task<string> GetAsync(string slug, CancellationToken ct = default)
    {
        try
        {
            if (Db is not { } db) return "flag{unavailable}";
            await using var cmd = db.CreateCommand("SELECT guid FROM ctf_flags WHERE slug = @s AND guid IS NOT NULL");
            cmd.Parameters.AddWithValue("s", slug);
            return await cmd.ExecuteScalarAsync(ct) is Guid g ? $"flag{{{g}}}" : "flag{not-generated}";
        }
        catch
        {
            return "flag{unavailable}";
        }
    }

    /// <summary>slug → points, for scoring the public leaderboard.</summary>
    public async Task<IReadOnlyDictionary<string, int>> GetPointsAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, int>();
        try
        {
            if (Db is not { } db) return map;
            await using var cmd = db.CreateCommand("SELECT slug, points FROM ctf_flags");
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) map[r.GetString(0)] = r.GetInt32(1);
        }
        catch { /* leaderboard degrades to zero points */ }
        return map;
    }
}
