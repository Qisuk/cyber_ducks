using CyberDucks.Shared.Defences;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace CyberDucks.Tests;

/// <summary>
/// Hosts store-api against the test database with the defence flags set in code. Replacing
/// <see cref="IDefenceFlags"/> (and dropping the background sync) gives each test its own flag
/// state with no timing or cross-test interference; NOTIFY propagation is covered separately.
/// </summary>
public sealed class AuthTestFactory(PostgresFixture db) : WebApplicationFactory<Program>
{
    public const string WebhookSecret = "test-webhook-secret";

    private readonly DefenceFlagStore _flags = new();

    public void SetFlag(string vulnId, DefenceMode mode) =>
        _flags.Replace(new Dictionary<string, DefenceMode>(_flags.Snapshot) { [vulnId] = mode });

    /// <summary>Mints a JWT for a seeded user (so tests can act as a persona without a password).</summary>
    public async Task<string> TokenForAsync(int userId)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<Store.Api.Data.StoreDbContext>();
        var user = await db.Users.FindAsync(userId)
            ?? throw new InvalidOperationException($"User {userId} not found.");
        return scope.ServiceProvider.GetRequiredService<Store.Api.Auth.JwtIssuer>().Issue(user).Token;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Store", db.ConnectionString("store_owner", "duckstore"));
        builder.UseSetting("ConnectionStrings:Flags", db.ConnectionString("flags_reader", "range_control"));
        builder.UseSetting("Jwt:Secret", "test-signing-secret-at-least-32-bytes-long!!");
        builder.UseSetting("Webhook:Secret", WebhookSecret);
        builder.UseSetting("PaymentStub:BaseUrl", "http://payment-stub.invalid/"); // not called by webhook tests
        // Personas already have passwords from the SQL+seeder path; skip re-seeding in tests.

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IHostedService>();
            services.RemoveAll<DefenceFlagStore>();
            services.RemoveAll<IDefenceFlags>();
            services.AddSingleton(_flags);
            services.AddSingleton<IDefenceFlags>(_flags);
        });
    }
}
