using System.Text.RegularExpressions;
using CyberDucks.Shared.Ctf;
using Npgsql;

namespace Store.Api.Flags;

public sealed record FlagSubmitRequest(string? Flag, string? Note, string? How, string? Team);
public sealed record LeaderboardRow(string Team, int Flags, int Points, DateTimeOffset LastCapture);

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
            var team = string.IsNullOrWhiteSpace(req.Team) ? null : req.Team.Trim()[..Math.Min(req.Team.Trim().Length, 60)];
            await using var cmd = db.CreateCommand(
                "INSERT INTO flag_submissions (flag_text, note, how, team, source_ip, recognised, matched_slug) VALUES (@f, @n, @h, @t, @ip, @r, @s)");
            cmd.Parameters.AddWithValue("f", req.Flag ?? "");
            cmd.Parameters.AddWithValue("n", (object?)req.Note ?? DBNull.Value);
            cmd.Parameters.AddWithValue("h", (object?)req.How ?? DBNull.Value);
            cmd.Parameters.AddWithValue("t", (object?)team ?? DBNull.Value);
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

        // Public leaderboard: recognised flags grouped by team, scored by the flag's points. Powers
        // the store's /leaderboard page so the CTF self-drives without a facilitator tallying.
        app.MapGet("/api/flags/leaderboard", async (NpgsqlDataSource db, CtfFlagReader flags, CancellationToken ct) =>
        {
            var points = await flags.GetPointsAsync(ct);
            var caps = new List<(string Team, string Slug, DateTimeOffset At)>();
            await using (var cmd = db.CreateCommand(
                "SELECT team, matched_slug, min(submitted_at) FROM flag_submissions WHERE recognised AND team IS NOT NULL AND matched_slug IS NOT NULL GROUP BY team, matched_slug"))
            await using (var r = await cmd.ExecuteReaderAsync(ct))
                while (await r.ReadAsync(ct))
                    caps.Add((r.GetString(0), r.GetString(1), r.GetFieldValue<DateTimeOffset>(2)));

            var board = caps.GroupBy(c => c.Team).Select(g => new LeaderboardRow(
                g.Key, g.Count(), g.Sum(c => points.GetValueOrDefault(c.Slug, 0)), g.Max(c => c.At)))
                .OrderByDescending(t => t.Points).ThenBy(t => t.LastCapture).ToList();
            return Results.Ok(board);
        });

        // CTF flag `admin-view`: an admin-only overview. Reached by escalating your role to admin
        // (VULN-MASS-01 mass assignment on the profile) and signing in again, or taking over an admin.
        app.MapGet("/api/admin/overview", async (System.Security.Claims.ClaimsPrincipal user, CtfFlagReader flags, CancellationToken ct) =>
            user.IsInRole("admin")
                ? Results.Ok(new { view = "admin-overview", token = await flags.GetAsync("admin-view", ct) })
                : Results.Forbid()).RequireAuthorization();

        // CTF flag `hidden-endpoint`: an undocumented diagnostics endpoint store-web never calls.
        // Found by API enumeration / fuzzing. Unauthenticated, like a left-in debug route.
        app.MapGet("/api/diagnostics", async (CtfFlagReader flags, CancellationToken ct) =>
            Results.Ok(new
            {
                status = "ok",
                service = "store-api",
                build = "dev",
                note = "internal diagnostics — do not expose",
                token = await flags.GetAsync("hidden-endpoint", ct),
            }));

        return app;
    }
}
