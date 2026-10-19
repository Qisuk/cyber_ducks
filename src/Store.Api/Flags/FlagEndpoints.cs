using System.Text.RegularExpressions;
using Npgsql;

namespace Store.Api.Flags;

public sealed record FlagSubmitRequest(string? Flag, string? Note, string? How);

/// <summary>
/// Checks a submitted flag string against the per-deployment flag guids (range_control.ctf_flags,
/// via flags_reader). Returns the matched slug, or null if it isn't a known flag.
/// </summary>
public sealed partial class CtfFlagValidator(string connectionString) : IAsyncDisposable
{
    private readonly NpgsqlDataSource _flagsDb = NpgsqlDataSource.Create(connectionString);

    [GeneratedRegex(@"flag\{([0-9a-fA-F\-]{36})\}")]
    private static partial Regex FlagPattern();

    public async Task<string?> MatchAsync(string? flagText, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(flagText)) return null;
        var m = FlagPattern().Match(flagText);
        if (!m.Success || !Guid.TryParse(m.Groups[1].Value, out var guid)) return null;
        await using var cmd = _flagsDb.CreateCommand("SELECT slug FROM ctf_flags WHERE guid = @g");
        cmd.Parameters.AddWithValue("g", guid);
        return await cmd.ExecuteScalarAsync(ct) as string;
    }

    public ValueTask DisposeAsync() => _flagsDb.DisposeAsync();
}

public static class FlagEndpoints
{
    public static IEndpointRouteBuilder MapFlags(this IEndpointRouteBuilder app)
    {
        // Public "post a flag": an attacker submits a captured flag plus a note on how they got it —
        // for flags grabbed without tripping a log. Always records (that is the point) and tells them
        // whether the flag was recognised. The facilitator sees it on the Range Control scoreboard.
        app.MapPost("/api/flags/submit", async (
            FlagSubmitRequest req, HttpContext http, NpgsqlDataSource db, CtfFlagValidator validator, CancellationToken ct) =>
        {
            var slug = await validator.MatchAsync(req.Flag, ct);
            var ip = http.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            await using var cmd = db.CreateCommand(
                "INSERT INTO flag_submissions (flag_text, note, how, source_ip, recognised, matched_slug) VALUES (@f, @n, @h, @ip, @r, @s)");
            cmd.Parameters.AddWithValue("f", req.Flag ?? "");
            cmd.Parameters.AddWithValue("n", (object?)req.Note ?? DBNull.Value);
            cmd.Parameters.AddWithValue("h", (object?)req.How ?? DBNull.Value);
            cmd.Parameters.AddWithValue("ip", ip);
            cmd.Parameters.AddWithValue("r", slug is not null);
            cmd.Parameters.AddWithValue("s", (object?)slug ?? DBNull.Value);
            await cmd.ExecuteNonQueryAsync(ct);
            return Results.Ok(new
            {
                recognised = slug is not null,
                message = slug is not null
                    ? "Flag accepted — recognised! Logged for the facilitator."
                    : "Submitted. The facilitator will verify it.",
            });
        });
        return app;
    }
}
