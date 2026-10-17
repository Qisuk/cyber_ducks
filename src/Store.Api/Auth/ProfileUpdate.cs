using CyberDucks.Shared.Contracts;
using Store.Api.Data;

namespace Store.Api.Auth;

/// <summary>Applies a profile update, deciding which fields may be bound — VULN-MASS-01.</summary>
public interface IProfileUpdate
{
    void Apply(User user, UpdateProfileRequest request);
}

/// <summary>
/// VULNERABLE (VULN-MASS-01): binds every field in the request, including <c>role</c>. A customer
/// can set their own role to admin (mass assignment, threat TH7, asset A17).
/// </summary>
public sealed class VulnerableProfileUpdate : IProfileUpdate
{
    public void Apply(User user, UpdateProfileRequest request)
    {
        user.DisplayName = request.DisplayName;
        if (request.Role is not null)
        {
            user.Role = request.Role;
        }
    }
}

/// <summary>SECURE (VULN-MASS-01): only the display name is bound; role is never taken from the request.</summary>
public sealed class SecureProfileUpdate : IProfileUpdate
{
    public void Apply(User user, UpdateProfileRequest request) => user.DisplayName = request.DisplayName;
}
