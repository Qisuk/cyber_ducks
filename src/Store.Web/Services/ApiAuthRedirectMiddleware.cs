using System.Net;

namespace Store.Web.Services;

/// <summary>
/// When a call to store-api comes back 401 (typically the forwarded session JWT has expired while
/// the auth cookie is still valid), a page would otherwise throw unhandled and render the error
/// page. Catch that 401 here and send the user to /login with a returnUrl so they re-authenticate
/// and land back where they were. Covers server-rendered (SSR) document requests; interactive
/// circuit calls are out of scope (no HTTP response to redirect).
/// </summary>
public sealed class ApiAuthRedirectMiddleware(RequestDelegate next)
{
    public async Task Invoke(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (HttpRequestException ex)
            when (ex.StatusCode == HttpStatusCode.Unauthorized && !context.Response.HasStarted)
        {
            var returnUrl = context.Request.Path + context.Request.QueryString;
            context.Response.Redirect($"/login?returnUrl={Uri.EscapeDataString(returnUrl)}");
        }
    }
}
