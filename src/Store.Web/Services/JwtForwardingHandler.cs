using System.Net.Http.Headers;
using Microsoft.AspNetCore.Authentication;

namespace Store.Web.Services;

/// <summary>
/// Attaches the logged-in user's JWT to every store-api call (flow F4). The token is kept in the
/// auth cookie's token store (encrypted by Data Protection), so the browser never sees it.
/// </summary>
public sealed class JwtForwardingHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var http = accessor.HttpContext;
        if (http is not null && request.Headers.Authorization is null)
        {
            var token = await http.GetTokenAsync("jwt");
            if (!string.IsNullOrEmpty(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
        return await base.SendAsync(request, ct);
    }
}
