using Store.Api.Data;

namespace Store.Api.Auth;

/// <summary>Checks a password against the column chosen by VULN-PWD-01.</summary>
public interface IPasswordVerifier
{
    bool Verify(User user, string password);
}

/// <summary>VULNERABLE (VULN-PWD-01): verify against the weak, unsalted legacy hash.</summary>
public sealed class VulnerablePasswordVerifier : IPasswordVerifier
{
    public bool Verify(User user, string password) =>
        PasswordHashing.VerifyLegacy(user.PasswordHashLegacy, password);
}

/// <summary>SECURE (VULN-PWD-01): verify against the salted, iterated hash.</summary>
public sealed class SecurePasswordVerifier : IPasswordVerifier
{
    public bool Verify(User user, string password) =>
        PasswordHashing.VerifySecure(user.PasswordHash, password);
}
