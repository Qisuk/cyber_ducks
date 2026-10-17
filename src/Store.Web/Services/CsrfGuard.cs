using CyberDucks.Shared.Defences;
using Microsoft.AspNetCore.Antiforgery;

namespace Store.Web.Services;

/// <summary>
/// Anti-forgery validation for state-changing form posts, behind VULN-CSRF-01. The forms always
/// include an &lt;AntiforgeryToken /&gt;; this decides whether the token is actually checked.
/// Vulnerable: no check, so a cross-site form can act as the logged-in user (threat CT5).
/// Secure: a missing or invalid token is rejected.
/// </summary>
public sealed class CsrfGuard(IAntiforgery antiforgery, IDefenceFlags flags)
{
    /// <summary>Returns true if the request may proceed; false if it must be rejected.</summary>
    public async Task<bool> ValidateAsync(HttpContext http)
    {
        if (!flags.IsSecure("VULN-CSRF-01"))
        {
            return true;
        }

        try
        {
            await antiforgery.ValidateRequestAsync(http);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }
}
