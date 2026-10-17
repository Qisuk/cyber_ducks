using System.Security.Claims;

namespace Store.Api.Staff;

/// <summary>Whether the caller may use staff-only functions — function-level authorisation, VULN-AUTHZ-01.</summary>
public interface IStaffGuard
{
    bool IsStaff(ClaimsPrincipal user);
}

/// <summary>
/// VULNERABLE (VULN-AUTHZ-01): any authenticated user passes. A customer who discovers the staff
/// endpoints can list every order and ship them (broken function-level authorisation, threat CT13).
/// </summary>
public sealed class VulnerableStaffGuard : IStaffGuard
{
    public bool IsStaff(ClaimsPrincipal user) => user.Identity?.IsAuthenticated == true;
}

/// <summary>SECURE (VULN-AUTHZ-01): only the employee and admin roles.</summary>
public sealed class SecureStaffGuard : IStaffGuard
{
    public bool IsStaff(ClaimsPrincipal user) => user.IsInRole("employee") || user.IsInRole("admin");
}
