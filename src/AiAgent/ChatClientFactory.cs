using System.ClientModel;
using Anthropic.SDK;
using Microsoft.Extensions.AI;
using OllamaSharp;
using OpenAI;

namespace AiAgent;

/// <summary>
/// Builds the single <see cref="IChatClient"/> the agent talks to, choosing the backend from
/// configuration at startup (ADR-0010). The agent code never names a provider; switching between
/// local Ollama and a cloud model is config only. Function-invocation middleware is layered on so
/// the model can call the MCP tools automatically.
/// </summary>
public static class ChatClientFactory
{
    public static IChatClient Create(IConfiguration config, ILoggerFactory loggerFactory)
    {
        // Compose passes unset values as empty strings, so treat blank as "not set".
        var provider = Setting(config, "Llm:Provider") ?? "Ollama";
        var model = Setting(config, "Llm:Model") ?? "qwen3:4b-instruct";
        var endpoint = Setting(config, "Llm:Endpoint");
        var apiKey = Setting(config, "Llm:ApiKey");
        var timeout = TimeSpan.FromSeconds(int.TryParse(Setting(config, "Llm:TimeoutSeconds"), out var s) ? s : 300);

        IChatClient inner = provider switch
        {
            // CPU inference is slow, so the HTTP timeout is much longer than the 100 s default.
            "Ollama" => new OllamaOptionsChatClient(
                new OllamaApiClient(new HttpClient
                {
                    BaseAddress = new Uri(endpoint ?? "http://ollama:11434"),
                    Timeout = timeout,
                }, model),
                think: string.Equals(Setting(config, "Llm:Think"), "true", StringComparison.OrdinalIgnoreCase),
                threads: int.TryParse(Setting(config, "Llm:Threads"), out var t) ? t : null),

            // Claude API. The key is a real secret and lives only in .env (ADR-0010).
            "Anthropic" => new AnthropicClient(apiKey
                    ?? throw new InvalidOperationException("Llm:ApiKey is required for the Anthropic provider."))
                .Messages,

            // Any OpenAI-compatible endpoint (OpenAI, Azure, LM Studio, vLLM).
            "OpenAICompatible" => new OpenAIClient(
                    new ApiKeyCredential(apiKey ?? "not-needed"),
                    new OpenAIClientOptions
                    {
                        Endpoint = new Uri(endpoint
                            ?? throw new InvalidOperationException("Llm:Endpoint is required for the OpenAICompatible provider.")),
                    })
                .GetChatClient(model).AsIChatClient(),

            _ => throw new InvalidOperationException($"Unknown Llm:Provider '{provider}'."),
        };

        return new ChatClientBuilder(inner)
            .UseFunctionInvocation(loggerFactory)
            .Build();
    }

    private static string? Setting(IConfiguration config, string key) =>
        string.IsNullOrWhiteSpace(config[key]) ? null : config[key];
}
