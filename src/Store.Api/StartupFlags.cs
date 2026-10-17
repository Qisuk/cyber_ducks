using Npgsql;

namespace Store.Api;

/// <summary>
/// One-off read of a defence flag at startup, for restart-required toggles (e.g. VULN-DBROLE-01)
/// whose choice is fixed when the process starts. Reads as flags_reader on range_control; if the
/// flag can't be read, defaults to vulnerable (the range default).
/// </summary>
public static class StartupFlags
{
    public static async Task<bool> IsSecureAsync(IConfiguration configuration, string vulnId)
    {
        var connectionString = configuration.GetConnectionString("Flags");
        if (string.IsNullOrEmpty(connectionString))
        {
            return false;
        }
        try
        {
            await using var conn = new NpgsqlConnection(connectionString);
            await conn.OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT mode FROM defence_flags WHERE vuln_id = @id", conn);
            cmd.Parameters.AddWithValue("id", vulnId);
            return await cmd.ExecuteScalarAsync() as string == "secure";
        }
        catch
        {
            return false;
        }
    }
}
