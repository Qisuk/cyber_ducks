using System.Text.Json;
using CyberDucks.Shared.Contracts;
using Microsoft.Extensions.AI;

namespace AiAgent;

/// <summary>
/// Finds state-changing tool calls that agent-tools held back for the customer to confirm
/// (VULN-LLM-06 secure mode), so store-web can show a real confirm button for each.
/// The order id comes from the tool call's own arguments, not from the model's reply text.
/// </summary>
public static class PendingActions
{
    public static IReadOnlyList<PendingAction> From(IEnumerable<ChatMessage> messages)
    {
        var contents = messages.SelectMany(m => m.Contents).ToList();
        var calls = contents.OfType<FunctionCallContent>()
            .Where(c => c.Name == "cancel_order")
            .ToDictionary(c => c.CallId);

        return contents.OfType<FunctionResultContent>()
            .Where(r => calls.ContainsKey(r.CallId)
                && JsonSerializer.Serialize(r.Result).Contains("confirmation_required", StringComparison.Ordinal))
            .Select(r => OrderId(calls[r.CallId]))
            .OfType<int>()
            .Distinct()
            .Select(id => new PendingAction("cancel_order", id))
            .ToList();
    }

    private static int? OrderId(FunctionCallContent call) =>
        call.Arguments?.TryGetValue("orderId", out var value) == true
            ? value switch
            {
                int i => i,
                long l => (int)l,
                JsonElement { ValueKind: JsonValueKind.Number } e when e.TryGetInt32(out var n) => n,
                JsonElement { ValueKind: JsonValueKind.String } e when int.TryParse(e.GetString(), out var n) => n,
                string s when int.TryParse(s, out var n) => n,
                _ => null,
            }
            : null;
}
