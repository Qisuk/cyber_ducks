using System.Net;
using CyberDucks.Shared.Defences;

namespace AgentTools;

/// <summary>
/// VULN-EGRESS-01 — the toggleable external-access control. The agent's tool layer can be asked to
/// fetch a URL (see <c>fetch_reference</c>); this handler decides whether that fetch may leave the
/// store. Secure mode allows only an allowlist of in-range store hosts, so a prompt-injected agent
/// cannot <b>exfiltrate</b> data to, or <b>ingest</b> data from, anything outside — the honeypot, a
/// pastebin, an attacker host. Vulnerable mode lets the fetch reach any host, which is how exfil and
/// ingest are demonstrated on the range.
///
/// Defence in depth with the sealed <c>agent</c> network: the network <c>internal: true</c> seal is
/// the static control (ai-agent/ollama have no route off-host at all); this flag is the runtime one
/// defenders flip in Range Control to watch the behaviour change. agent-tools itself bridges to the
/// service tier, so without this app-layer control it <i>could</i> reach a decoy/external host.
/// </summary>
public sealed class EgressGuardHandler(IDefenceFlags flags, IConfiguration config) : DelegatingHandler
{
    private readonly HashSet<string> _allowed =
        (config["Egress:AllowedHosts"] ?? "store-api,idp,payment-stub")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var host = request.RequestUri?.Host ?? "";
        if (flags.IsSecure("VULN-EGRESS-01", "agent-tools egress") && !_allowed.Contains(host))
        {
            // Short-circuit with a clean refusal so the tool reports "blocked" rather than throwing.
            return new HttpResponseMessage(HttpStatusCode.Forbidden)
            {
                ReasonPhrase = "Blocked by egress policy",
                RequestMessage = request,
                Content = new StringContent(
                    $"Egress to '{host}' is blocked by policy (VULN-EGRESS-01 secure)."),
            };
        }
        return await base.SendAsync(request, ct);
    }
}
