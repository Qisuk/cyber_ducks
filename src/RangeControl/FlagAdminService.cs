using Npgsql;

namespace RangeControl;

public sealed record DefenceFlag(string VulnId, string Title, string Kind, string Mode, bool RequiresRestart);
public sealed record FlagChange(DateTimeOffset ChangedAt, string VulnId, string OldMode, string NewMode, string ChangedBy);
public sealed record GuardState(string Guard, bool Enabled);

/// <summary>
/// Reads and writes the defence flags (range_control DB) and the database guards (duckstore),
/// both as range_control_owner. Writing a flag fires the LISTEN/NOTIFY the services watch, so
/// changes reach the range within seconds.
/// </summary>
public sealed class FlagAdminService(NpgsqlDataSource controlDb, NpgsqlDataSource storeDb)
{
    public async Task<IReadOnlyList<DefenceFlag>> GetFlagsAsync(CancellationToken ct = default)
    {
        var flags = new List<DefenceFlag>();
        await using var cmd = controlDb.CreateCommand(
            "SELECT vuln_id, title, kind, mode, requires_restart FROM defence_flags ORDER BY kind, vuln_id");
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            flags.Add(new DefenceFlag(reader.GetString(0), reader.GetString(1), reader.GetString(2),
                reader.GetString(3), reader.GetBoolean(4)));
        }
        return flags;
    }

    /// <summary>True when a flag is in secure mode (or treated as secure when the row is missing-safe default).</summary>
    public async Task<bool> IsSecureAsync(string vulnId, bool defaultIfMissing = false, CancellationToken ct = default)
    {
        await using var cmd = controlDb.CreateCommand("SELECT mode FROM defence_flags WHERE vuln_id = @id");
        cmd.Parameters.AddWithValue("id", vulnId);
        var mode = (string?)await cmd.ExecuteScalarAsync(ct);
        return mode is null ? defaultIfMissing : mode == "secure";
    }

    public async Task SetFlagAsync(string vulnId, string mode, string changedBy, CancellationToken ct = default)
    {
        await using var cmd = controlDb.CreateCommand(
            "UPDATE defence_flags SET mode = @mode, updated_by = @by, updated_at = now() WHERE vuln_id = @id");
        cmd.Parameters.AddWithValue("mode", mode);
        cmd.Parameters.AddWithValue("by", changedBy);
        cmd.Parameters.AddWithValue("id", vulnId);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Applies a named preset (e.g. "All vulnerable"), returning how many flags changed.</summary>
    public async Task<int> ApplyPresetAsync(string presetName, string changedBy, CancellationToken ct = default)
    {
        await using var cmd = controlDb.CreateCommand(
            """
            UPDATE defence_flags f SET mode = pf.mode, updated_by = @by, updated_at = now()
            FROM preset_flags pf JOIN presets p ON p.id = pf.preset_id
            WHERE pf.vuln_id = f.vuln_id AND p.name = @name AND f.mode <> pf.mode
            """);
        cmd.Parameters.AddWithValue("by", changedBy);
        cmd.Parameters.AddWithValue("name", presetName);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<IReadOnlyList<FlagChange>> GetRecentChangesAsync(int limit = 20, CancellationToken ct = default)
    {
        var changes = new List<FlagChange>();
        await using var cmd = controlDb.CreateCommand(
            "SELECT changed_at, vuln_id, old_mode, new_mode, changed_by FROM flag_changes ORDER BY changed_at DESC LIMIT @n");
        cmd.Parameters.AddWithValue("n", limit);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            changes.Add(new FlagChange(reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        }
        return changes;
    }

    public async Task<IReadOnlyList<GuardState>> GetGuardsAsync(CancellationToken ct = default)
    {
        var guards = new List<GuardState>();
        await using var cmd = storeDb.CreateCommand("SELECT guard, enabled FROM guard_status ORDER BY guard");
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            guards.Add(new GuardState(reader.GetString(0), reader.GetBoolean(1)));
        }
        return guards;
    }

    public async Task SetGuardAsync(string guard, bool enabled, CancellationToken ct = default)
    {
        await using var cmd = storeDb.CreateCommand("SELECT set_guard(@g, @e)");
        cmd.Parameters.AddWithValue("g", guard);
        cmd.Parameters.AddWithValue("e", enabled);
        await cmd.ExecuteNonQueryAsync(ct);
    }
}
