using CyberDucks.Shared.Contracts;

namespace Store.Web.Services;

/// <summary>
/// Typed client for ai-agent (flow F5). The JWT-forwarding handler attaches the signed-in
/// customer's token, which the agent passes on to agent-tools, so every tool call is scoped to them.
/// </summary>
public sealed class AgentClient(HttpClient http, ILogger<AgentClient> log)
{
    /// <summary>Returns the agent's reply, or an error message the page can show.</summary>
    public async Task<(AgentChatReply? Reply, string? Error)> ChatAsync(AgentChatRequest request, CancellationToken ct = default)
    {
        try
        {
            using var response = await http.PostAsJsonAsync("chat", request, ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("ai-agent returned {Status}", (int)response.StatusCode);
                return (null, "The duck assistant couldn't answer just now. Please try again.");
            }
            return (await response.Content.ReadFromJsonAsync<AgentChatReply>(ct), null);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            log.LogWarning(ex, "ai-agent call failed");
            return (null, "The duck assistant is unavailable or took too long. Please try again.");
        }
    }
}
