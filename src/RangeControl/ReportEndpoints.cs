using System.Text;
using RangeControl.Wargame;

namespace RangeControl;

/// <summary>Markdown export of an exercise after-action report (facilitator-only).</summary>
public static class ReportEndpoints
{
    public static IEndpointRouteBuilder MapReportExport(this IEndpointRouteBuilder app)
    {
        app.MapGet("/wargame/{id:int}/report.md", async Task<IResult> (
            int id, WargameService games, ScenarioCatalog catalog, CancellationToken ct) =>
        {
            var ex = await games.GetExerciseAsync(id, ct);
            if (ex is null) return Results.NotFound();

            var scenario = catalog.Find(ex.ScenarioId);
            var decisions = await games.GetDecisionsAsync(id, ct);
            var states = await games.GetNodeStatesAsync(id, ct);
            var timeline = await games.GetTimelineAsync(id, ct);
            var released = (await games.GetReleasesAsync(id, ct)).Count;

            var sb = new StringBuilder();
            sb.AppendLine($"# After-action report: {ex.Title}").AppendLine();
            sb.AppendLine($"- **Scenario:** {scenario?.Title ?? ex.ScenarioId}");
            sb.AppendLine($"- **Started:** {(ex.StartedAt is { } s ? s.UtcDateTime.ToString("d MMM yyyy HH:mm 'UTC'") : "not started")}");
            if (ex.StartedAt is { } st)
            {
                var mins = (int)((ex.EndedAt ?? DateTimeOffset.UtcNow) - st).TotalMinutes;
                sb.AppendLine($"- **Duration:** {mins} min{(ex.EndedAt is null ? " (still running)" : "")}");
            }
            sb.AppendLine($"- **Injects released:** {released}/{scenario?.Injects.Count ?? 0}");
            sb.AppendLine($"- **Decisions logged:** {decisions.Count}").AppendLine();

            sb.AppendLine("## Detected vs ground truth").AppendLine();
            string Of(string node, string layer) => states.FirstOrDefault(x => x.Node == node && x.Layer == layer)?.State ?? "normal";
            var nodes = states.Select(x => x.Node).Distinct().Order().ToList();
            if (nodes.Count == 0)
            {
                sb.AppendLine("_No map states recorded._").AppendLine();
            }
            else
            {
                sb.AppendLine("| Node | Team detected | Ground truth | |");
                sb.AppendLine("|---|---|---|---|");
                foreach (var n in nodes)
                {
                    var d = Of(n, "detected");
                    var t = Of(n, "ground_truth");
                    sb.AppendLine($"| {n} | {d} | {t} | {(d == t ? "match" : "**gap**")} |");
                }
                sb.AppendLine();
            }

            sb.AppendLine("## Decisions by phase (NIST SP 800-61)").AppendLine();
            foreach (var phase in WargameService.Phases)
            {
                var inPhase = decisions.Where(d => d.Phase == phase).ToList();
                sb.AppendLine($"### {phase} ({inPhase.Count})");
                if (inPhase.Count == 0) { sb.AppendLine("_None logged._").AppendLine(); continue; }
                foreach (var d in inPhase)
                {
                    sb.AppendLine($"- **{d.MadeAt.UtcDateTime:HH:mm}** {d.Text}" +
                        (string.IsNullOrWhiteSpace(d.Rationale) ? "" : $" — _{d.Rationale}_") +
                        (d.ParticipantName is { } p ? $" ({p})" : ""));
                }
                sb.AppendLine();
            }

            sb.AppendLine("## Timeline").AppendLine();
            foreach (var e in timeline)
            {
                sb.AppendLine($"- **{e.At.UtcDateTime:HH:mm}** `[{e.Kind}]` {e.Subject}" +
                    (string.IsNullOrWhiteSpace(e.Detail) ? "" : $" — {e.Detail.Replace("\n", " ")}"));
            }
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"_Generated {DateTimeOffset.UtcNow.UtcDateTime:d MMM yyyy HH:mm 'UTC'} by Cyber Ducks Range Control._");

            return Results.File(Encoding.UTF8.GetBytes(sb.ToString()), "text/markdown; charset=utf-8", $"report-exercise-{id}.md");
        }).RequireAuthorization();

        return app;
    }
}
