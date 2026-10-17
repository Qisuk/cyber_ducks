using System.Collections.Concurrent;
using Store.Api.Data;

namespace Store.Api.Auth;

/// <summary>Why a login attempt was refused before the password was even checked.</summary>
public enum LoginBlock
{
    None,
    AccountLocked,
    TooManyAttempts,
}

/// <summary>
/// Login hardening (VULN-AUTH-01): per-account lockout, per-IP rate limiting, and whether failures
/// reveal which field was wrong (user enumeration). The login endpoint calls into this around the
/// password check.
/// </summary>
public interface ILoginGuard
{
    /// <summary>If true, the API may say "no such account" vs "wrong password" (leaks valid emails).</summary>
    bool RevealsReason { get; }

    ValueTask<LoginBlock> CheckAsync(User? user, string clientIp, CancellationToken ct);

    ValueTask RecordFailureAsync(User? user, string clientIp, CancellationToken ct);

    ValueTask RecordSuccessAsync(User user, CancellationToken ct);
}

/// <summary>VULNERABLE: unlimited attempts, no lockout, and failures reveal which field was wrong.</summary>
public sealed class VulnerableLoginGuard : ILoginGuard
{
    public bool RevealsReason => true;

    public ValueTask<LoginBlock> CheckAsync(User? user, string clientIp, CancellationToken ct) =>
        ValueTask.FromResult(LoginBlock.None);

    public ValueTask RecordFailureAsync(User? user, string clientIp, CancellationToken ct) => ValueTask.CompletedTask;

    public ValueTask RecordSuccessAsync(User user, CancellationToken ct) => ValueTask.CompletedTask;
}

/// <summary>
/// SECURE: locks an account for a cool-off period after too many failures (persisted on the user
/// row so it survives restarts), rate-limits each client IP, and never reveals which field was wrong.
/// </summary>
public sealed class SecureLoginGuard(StoreDbContext db, TimeProvider clock) : ILoginGuard
{
    private const int AccountFailureLimit = 5;
    private const int IpAttemptLimit = 20;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan IpWindow = TimeSpan.FromMinutes(5);

    // Per-IP attempt timestamps. In-memory is fine for the range; a real multi-instance deploy
    // would use a shared store.
    private static readonly ConcurrentDictionary<string, List<DateTimeOffset>> IpAttempts = new();

    public bool RevealsReason => false;

    public ValueTask<LoginBlock> CheckAsync(User? user, string clientIp, CancellationToken ct)
    {
        if (CountRecentIpAttempts(clientIp) >= IpAttemptLimit)
        {
            return ValueTask.FromResult(LoginBlock.TooManyAttempts);
        }

        if (user?.LockedUntil is { } until && until > clock.GetUtcNow())
        {
            return ValueTask.FromResult(LoginBlock.AccountLocked);
        }

        return ValueTask.FromResult(LoginBlock.None);
    }

    public async ValueTask RecordFailureAsync(User? user, string clientIp, CancellationToken ct)
    {
        RecordIpAttempt(clientIp);

        if (user is null)
        {
            return;
        }

        user.FailedLoginCount++;
        if (user.FailedLoginCount >= AccountFailureLimit)
        {
            user.LockedUntil = clock.GetUtcNow().Add(LockoutDuration);
            user.FailedLoginCount = 0;
        }
        await db.SaveChangesAsync(ct);
    }

    public async ValueTask RecordSuccessAsync(User user, CancellationToken ct)
    {
        if (user.FailedLoginCount != 0 || user.LockedUntil is not null)
        {
            user.FailedLoginCount = 0;
            user.LockedUntil = null;
            await db.SaveChangesAsync(ct);
        }
    }

    private int CountRecentIpAttempts(string clientIp)
    {
        if (!IpAttempts.TryGetValue(clientIp, out var attempts))
        {
            return 0;
        }
        var cutoff = clock.GetUtcNow() - IpWindow;
        lock (attempts)
        {
            attempts.RemoveAll(t => t < cutoff);
            return attempts.Count;
        }
    }

    private void RecordIpAttempt(string clientIp)
    {
        var attempts = IpAttempts.GetOrAdd(clientIp, _ => []);
        lock (attempts)
        {
            attempts.Add(clock.GetUtcNow());
        }
    }
}
