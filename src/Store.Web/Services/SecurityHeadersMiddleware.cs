using CyberDucks.Shared.Defences;

namespace Store.Web.Services;

/// <summary>
/// VULN-HEADERS-01 — response security headers (defence in depth).
/// <para>
/// When the toggle is <b>vulnerable</b> the store sends no security headers, so a browser applies
/// none of its built-in protections: injected markup from VULN-XSS-01 (stored review bodies) or
/// VULN-LLM-05 (un-encoded agent replies) runs with nothing standing in its way, the pages can be
/// framed for clickjacking, and the full URL leaks in the <c>Referer</c>.
/// </para>
/// <para>
/// When <b>secure</b> the store sends a Content-Security-Policy and the usual hardening headers.
/// The key teaching point is <c>script-src 'self'</c>: even if output encoding (VULN-XSS-01) is
/// still off, the browser refuses to run injected inline scripts — so the headers are a second
/// line of defence, not a replacement for encoding. Styles keep <c>'unsafe-inline'</c> because the
/// Blazor component styles are inline; scripts are same-origin only (<c>blazor.web.js</c>).
/// </para>
/// Runtime toggle (no restart): the decision is read per request from <see cref="IDefenceFlags"/>,
/// which also tags the current trace so the choice is visible in Tempo.
/// </summary>
public sealed class SecurityHeadersMiddleware(RequestDelegate next)
{
    private const string Csp =
        "default-src 'self'; " +
        "img-src 'self' data:; " +
        "style-src 'self' 'unsafe-inline'; " +
        "script-src 'self'; " +
        "object-src 'none'; " +
        "base-uri 'self'; " +
        "frame-ancestors 'none'; " +
        "form-action 'self'";

    public async Task InvokeAsync(HttpContext context, IDefenceFlags flags)
    {
        if (flags.IsSecure("VULN-HEADERS-01", "store-web response"))
        {
            // Set before the response starts; headers added after that are ignored.
            context.Response.OnStarting(static state =>
            {
                var headers = ((HttpContext)state).Response.Headers;
                headers["Content-Security-Policy"] = Csp;
                headers["X-Content-Type-Options"] = "nosniff";
                headers["X-Frame-Options"] = "DENY";
                headers["Referrer-Policy"] = "no-referrer";
                return Task.CompletedTask;
            }, context);
        }

        await next(context);
    }
}
