extern alias rangecontrol;

using Npgsql;
using rangecontrol::RangeControl.Wargame;

namespace CyberDucks.Tests;

/// <summary>
/// The wargame / Ops Console basics (ADR-0008): the real topology and scenario files, the map
/// layout, and the exercise lifecycle against the real range_control schema.
/// </summary>
public sealed class WargameTests(PostgresFixture db)
{
    [Fact]
    public void Topology_file_parses_and_every_flow_joins_known_services()
    {
        var topology = Topology.Load(RepoFile("infra", "topology.yaml"));
        var services = topology.Services.Select(s => s.Name).ToHashSet();
        var zones = topology.Zones.Select(z => z.Id).ToHashSet();

        Assert.Contains("store-api", services);
        Assert.All(topology.Services, s => Assert.Contains(s.Zone, zones));
        Assert.All(topology.Flows, f =>
        {
            Assert.Contains(f.From, services);
            Assert.Contains(f.To, services);
        });
        // F16 lists several sources: one edge each.
        Assert.Equal(5, topology.Flows.Count(f => f.Id == "F16"));
        Assert.False(topology.Zones.Single(z => z.Id == "Z9").InAttackScope);
    }

    [Fact]
    public void Map_layout_places_every_service_without_overlap()
    {
        var topology = Topology.Load(RepoFile("infra", "topology.yaml"));
        var layout = MapLayout.For(topology);

        Assert.Equal(topology.Services.Count, layout.Nodes.Count);
        foreach (var a in layout.Nodes)
        {
            foreach (var b in layout.Nodes.Where(b => b != a))
            {
                var overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
                Assert.False(overlap, $"{a.Service.Name} overlaps {b.Service.Name}");
            }
            Assert.InRange(a.X + a.Width, 0, layout.Width);
            Assert.InRange(a.Y + a.Height, 0, layout.Height);
        }
    }

    [Fact]
    public void Scenario_files_are_valid()
    {
        var scenarios = new ScenarioCatalog(RepoFile("wargame", "scenarios")).All();
        Assert.NotEmpty(scenarios);
        var roles = WargameService.Roles.Append("all").ToHashSet();

        Assert.All(scenarios, s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.Id));
            Assert.False(string.IsNullOrWhiteSpace(s.Briefing));
            Assert.NotEmpty(s.Injects);
            Assert.Equal(s.Injects.Count, s.Injects.Select(i => i.Id).Distinct().Count());
            Assert.All(s.Injects, i =>
            {
                Assert.Contains(i.Phase, WargameService.Phases);
                Assert.NotEmpty(i.Audience);
                Assert.All(i.Audience, a => Assert.Contains(a, roles));
                Assert.InRange(i.AtMinute, 0, s.DurationMinutes);
            });
        });
    }

    [Fact]
    public async Task Exercise_lifecycle_records_decisions_map_states_and_a_timeline()
    {
        await using var dataSource = NpgsqlDataSource.Create(db.ConnectionString("range_control_owner", "range_control"));
        var events = new WargameEvents();
        var notified = 0;
        events.Changed += _ => notified++;
        var games = new WargameService(dataSource, events);

        var id = await games.CreateExerciseAsync("customer-data-leak", "Test run", "Fran");
        Assert.Equal("planned", (await games.GetExerciseAsync(id))!.Status);

        await games.StartAsync(id);
        await games.AddParticipantAsync(id, "Ivy", "incident_commander");
        var ivy = (await games.GetParticipantsAsync(id)).Single(p => p.Name == "Ivy");

        await games.ReleaseInjectAsync(id, "I1", "Fran");
        await games.ReleaseInjectAsync(id, "I1", "Fran"); // releasing twice is a no-op
        await games.LogDecisionAsync(id, ivy.Id, "detection_analysis", "Preserve the store-api traces", "Evidence first");
        await games.SetNodeStateAsync(id, "store-api", "detected", "suspected", "odd traffic", "Ivy");
        await games.SetNodeStateAsync(id, "store-api", "detected", "compromised", "", "Ivy");
        await games.SetNodeStateAsync(id, "store-api", "ground_truth", "compromised", "", "Fran");
        await games.EndAsync(id);

        var exercise = (await games.GetExerciseAsync(id))!;
        Assert.Equal("ended", exercise.Status);
        Assert.NotNull(exercise.EndedAt);
        Assert.Single(await games.GetReleasesAsync(id));

        var decision = Assert.Single(await games.GetDecisionsAsync(id));
        Assert.Equal("Ivy", decision.ParticipantName);
        Assert.Equal("incident_commander", decision.ParticipantRole);

        // Current state is the latest change per node and layer.
        var states = await games.GetNodeStatesAsync(id);
        Assert.Equal("compromised", states.Single(s => s.Node == "store-api" && s.Layer == "detected").State);
        Assert.Equal("compromised", states.Single(s => s.Node == "store-api" && s.Layer == "ground_truth").State);

        var timeline = await games.GetTimelineAsync(id);
        Assert.Contains(timeline, e => e.Kind == "inject" && e.Subject == "I1");
        Assert.Contains(timeline, e => e.Kind == "decision" && e.Detail == "Preserve the store-api traces");
        Assert.Equal(2, timeline.Count(e => e.Kind == "map.detected"));
        Assert.True(timeline.Zip(timeline.Skip(1)).All(p => p.First.At <= p.Second.At));
        Assert.True(notified >= 8);
    }

    [Fact]
    public async Task Database_rejects_unknown_roles_phases_and_states()
    {
        await using var dataSource = NpgsqlDataSource.Create(db.ConnectionString("range_control_owner", "range_control"));
        var games = new WargameService(dataSource, new WargameEvents());
        var id = await games.CreateExerciseAsync("customer-data-leak", "Constraint test", "Fran");

        await Assert.ThrowsAsync<PostgresException>(() => games.AddParticipantAsync(id, "X", "ceo"));
        await Assert.ThrowsAsync<PostgresException>(() => games.LogDecisionAsync(id, null, "panic", "x", ""));
        await Assert.ThrowsAsync<PostgresException>(() => games.SetNodeStateAsync(id, "store-api", "detected", "on-fire", "", "X"));
    }

    [Fact]
    public async Task Store_roles_cannot_read_wargame_tables()
    {
        // The facilitator's ground truth must be out of reach of the attack surface (ADR-0008).
        await using var conn = new NpgsqlConnection(db.ConnectionString("flags_reader", "range_control"));
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand("SELECT count(*) FROM node_state_changes", conn);
        var ex = await Assert.ThrowsAsync<PostgresException>(() => cmd.ExecuteScalarAsync());
        Assert.Equal("42501", ex.SqlState); // insufficient_privilege
    }

    private static string RepoFile(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CyberDucks.slnx")))
            {
                return Path.Combine([dir.FullName, .. parts]);
            }
        }
        throw new InvalidOperationException("Could not find the repository root (CyberDucks.slnx).");
    }
}
