using System.Net.Http.Headers;

namespace AgentTools;

/// <summary>
/// Secure behaviour (ADR-0009): every call to store-api is made <b>as the customer</b>. The agent
/// (via its MCP client) attaches the customer's JWT to each MCP request; this handler copies that
/// Authorization header onto the outgoing store-api call (flow F22), so store-api scopes the data to
/// that customer. The vulnerable variant (VULN-AGENT-01) swaps this for a shared service account.
/// </summary>
public sealed class CustomerTokenHandler(IHttpContextAccessor accessor) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();
        if (request.Headers.Authorization is null
            && !string.IsNullOrEmpty(incoming)
            && AuthenticationHeaderValue.TryParse(incoming, out var auth))
        {
            request.Headers.Authorization = auth;
        }
        return await base.SendAsync(request, ct);
    }
}
