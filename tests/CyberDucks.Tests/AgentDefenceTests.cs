extern alias agenttools;
extern alias aiagent;

using System.Net;
using System.Text.Json;
using CyberDucks.Shared.Contracts;
using CyberDucks.Shared.Defences;
using Microsoft.Extensions.AI;
using agenttools::AgentTools;
using PendingActions = aiagent::AiAgent.PendingActions;

namespace CyberDucks.Tests;

/// <summary>
/// The agent defence toggles (Phase 6d): VULN-LLM-02 (minimised tool results) and VULN-LLM-06
/// (customer confirms state-changing actions). The tools run against a fake store-api.
/// </summary>
public sealed class AgentDefenceTests
{
    private static readonly OrderDetail Order = new(
        1014, "placed", "Leave by the shed", "1 Pond Lane, Duckford", DateTimeOffset.Parse("2026-10-17T20:44:53Z"), null,
        6.99m, [new OrderLine(1, "Classic Yellow Duck", 1, 6.99m)], null);

    [Fact]
    public async Task Secure_get_order_status_returns_only_the_fields_needed()
    {
        var (store, _) = FakeStore();
        var result = await OrderTools.GetOrderStatus(store, Flags("VULN-LLM-02", DefenceMode.Secure), 1014, default);

        var status = Assert.IsType<OrderStatus>(result);
        Assert.Equal("placed", status.Status);
        var json = JsonSerializer.Serialize(result);
        Assert.DoesNotContain("Leave by the shed", json);
        Assert.DoesNotContain("Pond Lane", json);
    }

    [Fact]
    public async Task Vulnerable_get_order_status_returns_the_whole_record()
    {
        var (store, _) = FakeStore();
        var result = await OrderTools.GetOrderStatus(store, Flags("VULN-LLM-02", DefenceMode.Vulnerable), 1014, default);

        var detail = Assert.IsType<OrderDetail>(result);
        Assert.Equal("Leave by the shed", detail.Notes);
        Assert.Equal("1 Pond Lane, Duckford", detail.ShippingAddress);
    }

    [Fact]
    public async Task Secure_cancel_order_waits_for_the_customer_and_never_calls_the_store()
    {
        var (store, requests) = FakeStore();
        var outcome = await OrderTools.CancelOrder(store, Flags("VULN-LLM-06", DefenceMode.Secure), 1014, default);

        Assert.Equal("confirmation_required", outcome.Status);
        Assert.DoesNotContain(requests, r => r.Method == HttpMethod.Post);
    }

    [Fact]
    public async Task Vulnerable_cancel_order_acts_immediately()
    {
        var (store, requests) = FakeStore();
        var outcome = await OrderTools.CancelOrder(store, Flags("VULN-LLM-06", DefenceMode.Vulnerable), 1014, default);

        Assert.Equal("cancelled", outcome.Status);
        Assert.Contains(requests, r => r.Method == HttpMethod.Post && r.RequestUri!.AbsolutePath == "/api/orders/1014/cancel");
    }

    [Fact]
    public void Pending_actions_come_from_held_back_cancel_calls_only()
    {
        ChatMessage[] messages =
        [
            new(ChatRole.Assistant, [
                new FunctionCallContent("c1", "cancel_order", new Dictionary<string, object?> { ["orderId"] = JsonDocument.Parse("1014").RootElement }),
                new FunctionCallContent("c2", "cancel_order", new Dictionary<string, object?> { ["orderId"] = 1011 }),
                new FunctionCallContent("c3", "get_order_status", new Dictionary<string, object?> { ["orderId"] = 1009 }),
            ]),
            new(ChatRole.Tool, [
                new FunctionResultContent("c1", JsonDocument.Parse("""{"orderId":1014,"status":"confirmation_required"}""").RootElement),
                new FunctionResultContent("c2", JsonDocument.Parse("""{"orderId":1011,"status":"cancelled"}""").RootElement),
                new FunctionResultContent("c3", JsonDocument.Parse("""{"status":"confirmation_required"}""").RootElement),
            ]),
        ];

        var pending = PendingActions.From(messages);

        var action = Assert.Single(pending);
        Assert.Equal(new PendingAction("cancel_order", 1014), action);
    }

    private static IDefenceFlags Flags(string vulnId, DefenceMode mode)
    {
        var store = new DefenceFlagStore();
        store.Replace(new Dictionary<string, DefenceMode> { [vulnId] = mode });
        return store;
    }

    private static (StoreApiClient Store, List<HttpRequestMessage> Requests) FakeStore()
    {
        var requests = new List<HttpRequestMessage>();
        var handler = new FakeHandler(request =>
        {
            requests.Add(request);
            return request.Method == HttpMethod.Post
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}") }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(Order, JsonSerializerOptions.Web), System.Text.Encoding.UTF8, "application/json"),
                };
        });
        return (new StoreApiClient(new HttpClient(handler) { BaseAddress = new Uri("http://store-api/") }), requests);
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
            Task.FromResult(respond(request));
    }
}
