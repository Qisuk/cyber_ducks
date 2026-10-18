using System.Net.Http.Json;
using System.Text.Json;

namespace RangeControl.Wargame;

/// <summary>
/// Pulls firing Grafana alerts (the Phase 8a detection rules) and raises the matching node to
/// "suspected" on the wargame's DETECTED layer, for every running exercise. This is flow F19
/// (range-control → lgtm, "alert state queries"): the range's own telemetry drives the infra map
/// the blue team watches, instead of the map only moving when a facilitator clicks.
///
/// The alert → node mapping lives on the alert rules themselves — a rule tagged
/// <c>range_node: store-api</c> lights up the store-api node (infra/grafana/alerting/range-alerts.yaml).
/// Raise-only from "normal": once a human (or an earlier tick) has moved a node, the team owns it and
/// the bridge never downgrades or clears it. Alerts with no <c>range_node</c> label, or naming a node
/// that isn't on the map, are ignored. A critical-severity rule asserts "compromised"; everything else
/// is "suspected" — a detection is a lead, not a confirmed breach.
/// </summary>
public sealed class GrafanaAlertBridge(
    IHttpClientFactory http, WargameService games, TopologySource topology,
    IConfiguration config, ILogger<GrafanaAlertBridge> log) : BackgroundService
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!config.GetValue("Wargame:AlertBridge", true))
        {
            log.LogInformation("Grafana alert bridge disabled (Wargame:AlertBridge=false).");
            return;
        }

        var period = TimeSpan.FromSeconds(Math.Max(5, config.GetValue("Grafana:AlertSyncSeconds", 15)));
        using var timer = new PeriodicTimer(period);
        do
        {
            try
            {
                await SyncOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // Grafana may be down, anonymous access off, or no rules yet — degrade to a manual map.
                log.LogWarning(ex, "Grafana alert sync failed; the detected layer stays manual this tick.");
            }
        }
        while (await WaitAsync(timer, stoppingToken));
    }

    private static async Task<bool> WaitAsync(PeriodicTimer timer, CancellationToken ct)
    {
        try
        {
            return await timer.WaitForNextTickAsync(ct);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }

    private async Task SyncOnceAsync(CancellationToken ct)
    {
        var running = (await games.GetExercisesAsync(ct)).Where(e => e.Status == "running").ToList();
        if (running.Count == 0)
        {
            return; // nothing to drive — don't even call Grafana
        }

        var firing = await FetchFiringAsync(ct);
        if (firing.Count == 0)
        {
            return;
        }

        var onMap = topology.Load().Services.Select(s => s.Name).ToHashSet();
        foreach (var exercise in running)
        {
            var states = await games.GetNodeStatesAsync(exercise.Id, ct);
            foreach (var alert in firing)
            {
                if (!onMap.Contains(alert.Node))
                {
                    continue; // rule points at a node that isn't on the map
                }
                var current = states.FirstOrDefault(s => s.Node == alert.Node && s.Layer == "detected")?.State ?? "normal";
                if (current != "normal")
                {
                    continue; // raise-only: a human (or an earlier tick) owns this node now
                }
                var note = string.IsNullOrWhiteSpace(alert.Summary)
                    ? $"Grafana alert {alert.Detection}".Trim()
                    : $"Grafana alert {alert.Detection}: {alert.Summary}";
                await games.SetNodeStateAsync(exercise.Id, alert.Node, "detected", alert.State, Clamp(note, 500), "grafana", ct);
                log.LogInformation("Alert {Detection} raised {Node} to {State} on exercise {Exercise}.",
                    alert.Detection, alert.Node, alert.State, exercise.Id);
            }
        }
    }

    // Grafana's Prometheus-compatible endpoint: firing/pending/normal per alert instance, with labels.
    private async Task<IReadOnlyList<FiringAlert>> FetchFiringAsync(CancellationToken ct)
    {
        var client = http.CreateClient("grafana");
        var payload = await client.GetFromJsonAsync<AlertsResponse>("/api/prometheus/grafana/api/v1/alerts", Json, ct);
        var alerts = payload?.Data?.Alerts ?? [];

        var list = new List<FiringAlert>();
        foreach (var a in alerts)
        {
            if (!IsFiring(a.State))
            {
                continue;
            }
            var labels = a.Labels ?? [];
            if (!labels.TryGetValue("range_node", out var node) || string.IsNullOrWhiteSpace(node))
            {
                continue; // only rules that opt in with a node mapping drive the map
            }
            labels.TryGetValue("severity", out var severity);
            labels.TryGetValue("range_detection", out var detection);
            var summary = a.Annotations is { } ann && ann.TryGetValue("summary", out var s) ? s : "";
            list.Add(new FiringAlert(node.Trim(), StateFor(severity), (detection ?? "alert").Trim(), summary.Trim()));
        }
        return list;
    }

    // Grafana's Prometheus-compatible endpoint reports instance state as "Alerting" / "Pending" /
    // "Normal" (sometimes suffixed, e.g. "Normal (NoData)"). Only a fully active rule counts.
    private static bool IsFiring(string? state) =>
        state is not null &&
        (state.Trim().StartsWith("Alerting", StringComparison.OrdinalIgnoreCase)
            || state.Trim().Equals("firing", StringComparison.OrdinalIgnoreCase));

    private static string StateFor(string? severity) =>
        string.Equals(severity, "critical", StringComparison.OrdinalIgnoreCase) ? "compromised" : "suspected";

    private static string Clamp(string s, int max) => s.Length <= max ? s : s[..max];

    private sealed record FiringAlert(string Node, string State, string Detection, string Summary);

    private sealed class AlertsResponse
    {
        public AlertsData? Data { get; set; }
    }

    private sealed class AlertsData
    {
        public List<AlertDto>? Alerts { get; set; }
    }

    private sealed class AlertDto
    {
        public Dictionary<string, string>? Labels { get; set; }
        public Dictionary<string, string>? Annotations { get; set; }
        public string? State { get; set; }
    }
}
