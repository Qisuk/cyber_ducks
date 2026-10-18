namespace CyberDucks.Shared.Contracts;

// The chat contract between store-web and ai-agent (flows F5/F9).

/// <summary>A customer chat message plus the conversation so far (oldest first).</summary>
public sealed record AgentChatRequest(string Message, IReadOnlyList<AgentChatTurn> History);

/// <summary>One prior turn. <see cref="Role"/> is "user" or "assistant".</summary>
public sealed record AgentChatTurn(string Role, string Content);

/// <summary>
/// The agent's reply, which tools it called (for the UI and the wargame timeline), and any
/// state-changing actions waiting for the customer to confirm (VULN-LLM-06).
/// </summary>
public sealed record AgentChatReply(string Reply, IReadOnlyList<string> ToolsUsed, IReadOnlyList<PendingAction> PendingActions);

/// <summary>
/// An action the agent asked for but did not perform. The customer confirms it with a real button
/// in the store, outside the conversation, so the model can't confirm on their behalf.
/// </summary>
public sealed record PendingAction(string Kind, int OrderId);
