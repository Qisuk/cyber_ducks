namespace CyberDucks.Shared.Contracts;

// The chat contract between store-web and ai-agent (flows F5/F9).

/// <summary>A customer chat message plus the conversation so far (oldest first).</summary>
public sealed record AgentChatRequest(string Message, IReadOnlyList<AgentChatTurn> History);

/// <summary>One prior turn. <see cref="Role"/> is "user" or "assistant".</summary>
public sealed record AgentChatTurn(string Role, string Content);

/// <summary>The agent's reply and which tools it called (for the UI and the wargame timeline).</summary>
public sealed record AgentChatReply(string Reply, IReadOnlyList<string> ToolsUsed);
