using System.Net.Http.Headers;
using CyberDucks.Shared.Defences;

namespace AgentTools;

/// <summary>
/// Decides which identity agent-tools uses when it calls store-api (flow F22) — the VULN-MCP-01
/// toggle.
/// <para>
/// <b>Secure</b> (ADR-0009): every call is made <b>as the customer</b>. The agent attaches the
/// customer's JWT to each MCP request; this handler copies that Authorization header onto the
/// outgoing store-api call, so store-api's own authorization (IDOR/AUTHZ) scopes the data to that
/// customer.
/// </para>
/// <para>
/// <b>Vulnerable</b>: the handler drops the customer's token and calls store-api as a single shared
/// <b>service account</b> instead. The tool gateway becomes a confused deputy — tool results then
/// reflect that account rather than the signed-in customer, and prompt injection can pull data
/// across accounts.
/// </para>
/// </summary>
public sealed class CustomerTokenHandler(
    IHttpContextAccessor accessor, IDefenceFlags flags, ServiceTokenProvider serviceToken) : DelegatingHandler
{
    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        if (request.Headers.Authorization is null)
        {
            if (flags.IsSecure("VULN-MCP-01", "agent-tools->store-api"))
            {
                // Secure: act as the signed-in customer by forwarding their token.
                var incoming = accessor.HttpContext?.Request.Headers.Authorization.ToString();
                if (!string.IsNullOrEmpty(incoming) && AuthenticationHeaderValue.TryParse(incoming, out var auth))
                {
                    request.Headers.Authorization = auth;
                }
            }
            else
            {
                // Vulnerable: act as the shared service account for everyone.
                var token = await serviceToken.GetTokenAsync(ct);
                if (!string.IsNullOrEmpty(token))
                {
                    request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                }
            }
        }

        return await base.SendAsync(request, ct);
    }
}
