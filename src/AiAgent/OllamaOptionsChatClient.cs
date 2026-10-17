using Microsoft.Extensions.AI;
using OllamaSharp.Models;
using OllamaSharp.Models.Chat;

namespace AiAgent;

/// <summary>
/// Ollama-only tweaks, kept out of the agent code:
/// - Thinking off by default: on a CPU the hidden reasoning tokens make each turn take minutes.
///   Set <c>Llm:Think=true</c> (with a thinking model) to compare behaviour with reasoning on.
/// - Thread count matched to the container's CPU limit. llama.cpp otherwise starts one thread per
///   host core, and the CFS quota throttles them: measured ~3 tok/s at 8 threads vs ~12 at 4.
/// </summary>
public sealed class OllamaOptionsChatClient(IChatClient inner, bool think, int? threads) : DelegatingChatClient(inner)
{
    public override Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
        base.GetResponseAsync(messages, WithThink(options), ct);

    public override IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken ct = default) =>
        base.GetStreamingResponseAsync(messages, WithThink(options), ct);

    private ChatOptions WithThink(ChatOptions? options)
    {
        var result = options?.Clone() ?? new ChatOptions();
        result.RawRepresentationFactory ??= _ => new ChatRequest
        {
            Think = think,
            Options = threads is null ? null : new RequestOptions { NumThread = threads },
        };
        return result;
    }
}
