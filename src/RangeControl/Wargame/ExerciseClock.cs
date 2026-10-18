namespace RangeControl.Wargame;

/// <summary>
/// Drives autoplay for running exercises: when an exercise has autoplay on, its scenario injects are
/// released automatically as their (speed-scaled) <c>at_minute</c> arrives, so a facilitator can run
/// a scenario hands-free for a demo or a quick solo runthrough. Scaled elapsed minutes =
/// real elapsed seconds × speed ÷ 60, so speed 60 releases a minute of scenario every real second.
///
/// Manual release still works at any time (Ops Console); autoplay only adds the ones that come due.
/// Releases are idempotent (unique per exercise+inject), so a tick never double-releases. Runs in
/// range-control alongside the alert bridge; writes go through WargameService, so every open console
/// refreshes live.
/// </summary>
public sealed class ExerciseClock(WargameService games, ScenarioCatalog scenarios, ILogger<ExerciseClock> log)
    : BackgroundService
{
    private static readonly TimeSpan Tick = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Tick);
        do
        {
            try
            {
                await ReleaseDueInjectsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                log.LogWarning(ex, "Exercise autoplay tick failed; will retry.");
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

    private async Task ReleaseDueInjectsAsync(CancellationToken ct)
    {
        var playing = (await games.GetExercisesAsync(ct))
            .Where(e => e is { Status: "running", Autoplay: true, StartedAt: not null })
            .ToList();
        if (playing.Count == 0)
        {
            return;
        }

        foreach (var exercise in playing)
        {
            var scenario = scenarios.Find(exercise.ScenarioId);
            if (scenario is null || scenario.Injects.Count == 0)
            {
                continue;
            }
            var scaledMinutes = (DateTimeOffset.UtcNow - exercise.StartedAt!.Value).TotalSeconds * exercise.Speed / 60.0;
            var released = (await games.GetReleasesAsync(exercise.Id, ct)).Select(r => r.InjectId).ToHashSet();

            foreach (var inject in scenario.Injects)
            {
                if (inject.AtMinute <= scaledMinutes && released.Add(inject.Id))
                {
                    await games.ReleaseInjectAsync(exercise.Id, inject.Id, "auto", ct);
                    log.LogInformation("Autoplay released inject {Inject} on exercise {Exercise} (T+{Minute}, {Speed}x).",
                        inject.Id, exercise.Id, inject.AtMinute, exercise.Speed);
                }
            }
        }
    }
}
