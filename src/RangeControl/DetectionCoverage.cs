using System.Net.Http.Json;
using System.Text.Json;

namespace RangeControl;

public sealed record DetectionRule(string Name, string Severity, string Node);

/// <summary>
/// Reads the provisioned Grafana alert rules and maps each `range_detection` (the vuln it detects) to
/// its severity and the node it raises. Powers the detection-coverage view: which vulnerabilities have
/// an alert, and which are silent gaps the blue team must catch by hand.
/// </summary>
public sealed class DetectionCoverageService(IHttpClientFactory http, ILogger<DetectionCoverageService> log)
{
    public async Task<IReadOnlyDictionary<string, DetectionRule>> GetAsync(CancellationToken ct = default)
    {
        var map = new Dictionary<string, DetectionRule>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var client = http.CreateClient("grafana");
            using var resp = await client.GetAsync("/api/prometheus/grafana/api/v1/rules", ct);
            if (!resp.IsSuccessStatusCode) return map;
            var doc = await resp.Content.ReadFromJsonAsync<JsonElement>(ct);
            if (!doc.TryGetProperty("data", out var data) || !data.TryGetProperty("groups", out var groups))
                return map;

            foreach (var group in groups.EnumerateArray())
            {
                if (!group.TryGetProperty("rules", out var rules)) continue;
                foreach (var rule in rules.EnumerateArray())
                {
                    if (!rule.TryGetProperty("labels", out var labels)) continue;
                    var detection = Str(labels, "range_detection");
                    if (string.IsNullOrEmpty(detection)) continue;
                    var name = Str(rule, "name");
                    map[detection] = new DetectionRule(name, Str(labels, "severity"), Str(labels, "range_node"));
                }
            }
        }
        catch (Exception ex)
        {
            log.LogWarning(ex, "Could not read Grafana rules for detection coverage.");
        }
        return map;
    }

    private static string Str(JsonElement obj, string prop) =>
        obj.TryGetProperty(prop, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";
}
