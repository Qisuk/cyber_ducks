using Microsoft.EntityFrameworkCore;
using Store.Api.Data;

namespace Store.Api.Auth;

/// <summary>
/// Sets passwords for the seeded personas on first start (the SQL seed leaves them NULL, because
/// computing both stored forms is application logic behind a toggle). Idempotent: only fills
/// personas whose password columns are still empty, so it never clobbers a changed password and a
/// volume reset re-seeds cleanly. Passwords come from the Personas:* config (fake range values).
/// </summary>
public sealed class PersonaPasswordSeeder(
    IServiceProvider services, IConfiguration config, ILogger<PersonaPasswordSeeder> log) : IHostedService
{
    // Local-part of each persona's @duckstore.test email. Passwords are read from Personas:<local>.
    private static readonly string[] Personas = ["alice", "bob", "carol", "dave", "eve", "mallory", "trent"];

    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<StoreDbContext>();

        var filled = 0;
        foreach (var local in Personas)
        {
            var password = config[$"Personas:{local}"];
            if (string.IsNullOrEmpty(password))
            {
                continue;
            }

            var email = $"{local}@duckstore.test";
            var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, ct);
            if (user is null || user.PasswordHash is not null || user.PasswordHashLegacy is not null)
            {
                continue; // unknown persona, or already has a password
            }

            user.PasswordHashLegacy = PasswordHashing.LegacyHash(password);
            user.PasswordHash = PasswordHashing.SecureHash(password);
            filled++;
        }

        if (filled > 0)
        {
            await db.SaveChangesAsync(ct);
            log.LogInformation("Set passwords for {Count} personas", filled);
        }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
