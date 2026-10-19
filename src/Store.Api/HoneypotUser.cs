namespace Store.Api;

/// <summary>
/// VULN-HONEYPOT-02: a decoy "administrator" account. No legitimate user ever signs in as it or
/// reads its data, so any login attempt against it, or any read of its order, is high-fidelity
/// attacker activity (credential stuffing, admin enumeration, or IDOR/data-walking). Seeded with a
/// NULL password hash in 05-seed.sql, so an attempt can never actually authenticate — it only
/// alarms. Detection is always on; SIEM-01 gates whether it reaches the wargame map.
/// </summary>
public static class HoneypotUser
{
    public const int Id = 99;
    public const string Email = "administrator@duckstore.test";
    public const int CanaryOrderId = 1099;

    public static bool IsEmail(string? email) =>
        string.Equals(email?.Trim(), Email, StringComparison.OrdinalIgnoreCase);
}
