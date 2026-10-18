using Npgsql;

namespace RangeControl.Wargame;

public sealed record Exercise(
    int Id, string ScenarioId, string Title, string Status,
    DateTimeOffset CreatedAt, DateTimeOffset? StartedAt, DateTimeOffset? EndedAt,
    double Speed, bool Autoplay);
public sealed record Participant(int Id, string Name, string Role);
public sealed record InjectRelease(string InjectId, DateTimeOffset ReleasedAt, string ReleasedBy);
/// <summary>A facilitator-authored timeline message / ad-hoc inject (not from the scenario file).</summary>
public sealed record TimelineMessage(
    int Id, string Phase, IReadOnlyList<string> Audience, string Sender, string Title, string Body,
    DateTimeOffset CreatedAt, string CreatedBy);
public sealed record Decision(
    int Id, DateTimeOffset MadeAt, string Phase, string Text, string Rationale, string? ParticipantName, string? ParticipantRole);
public sealed record NodeState(string Node, string Layer, string State, string Note, DateTimeOffset ChangedAt, string ChangedBy);

/// <summary>One line of the exercise timeline: injects, decisions, map changes and defence-flag changes.</summary>
public sealed record TimelineEntry(DateTimeOffset At, string Kind, string Subject, string Detail, string Actor);

/// <summary>
/// Raised after every write, so all open Ops Console circuits (one per participant browser)
/// refresh their view of the exercise. In-process only: fine for a single range-control instance.
/// </summary>
public sealed class WargameEvents
{
    public event Action<int>? Changed;

    public void Notify(int exerciseId) => Changed?.Invoke(exerciseId);
}

/// <summary>Exercise state in the range_control database (06-wargame.sql), as range_control_owner.</summary>
public sealed class WargameService(NpgsqlDataSource db, WargameEvents events)
{
    public static readonly string[] Roles =
        ["facilitator", "incident_commander", "tech_lead", "comms", "legal_dpo", "exec"];

    public static readonly string[] Phases =
        ["preparation", "detection_analysis", "containment_eradication_recovery", "post_incident"];

    public static readonly string[] States = ["normal", "suspected", "compromised", "contained", "recovered"];

    public async Task<IReadOnlyList<Exercise>> GetExercisesAsync(CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand($"{ExerciseSelect} ORDER BY id DESC");
        return await ReadExercisesAsync(cmd, ct);
    }

    public async Task<Exercise?> GetExerciseAsync(int id, CancellationToken ct = default)
    {
        await using var cmd = db.CreateCommand($"{ExerciseSelect} WHERE id = @id");
        cmd.Parameters.AddWithValue("id", id);
        return (await ReadExercisesAsync(cmd, ct)).SingleOrDefault();
    }

    /// <summary>Creates an exercise with its facilitator as the first participant.</summary>
    public async Task<int> CreateExerciseAsync(string scenarioId, string title, string facilitator, CancellationToken ct = default)
    {
        await using var conn = await db.OpenConnectionAsync(ct);
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using var insert = new NpgsqlCommand(
            "INSERT INTO exercises (scenario_id, title) VALUES (@scenario, @title) RETURNING id", conn, tx);
        insert.Parameters.AddWithValue("scenario", scenarioId);
        insert.Parameters.AddWithValue("title", title);
        var id = (int)(await insert.ExecuteScalarAsync(ct))!;

        await using var participant = new NpgsqlCommand(
            "INSERT INTO participants (exercise_id, name, role) VALUES (@id, @name, 'facilitator')", conn, tx);
        participant.Parameters.AddWithValue("id", id);
        participant.Parameters.AddWithValue("name", facilitator);
        await participant.ExecuteNonQueryAsync(ct);

        await tx.CommitAsync(ct);
        return id;
    }

    public Task StartAsync(int id, CancellationToken ct = default) => ExecuteAsync(id,
        "UPDATE exercises SET status = 'running', started_at = now() WHERE id = @id AND status = 'planned'", ct);

    public Task EndAsync(int id, CancellationToken ct = default) => ExecuteAsync(id,
        "UPDATE exercises SET status = 'ended', ended_at = now() WHERE id = @id AND status = 'running'", ct);

    /// <summary>Clock speed multiplier (1 = real time). Clamped to a sane range.</summary>
    public Task SetSpeedAsync(int id, double speed, CancellationToken ct = default) => ExecuteAsync(id,
        "UPDATE exercises SET speed = @speed WHERE id = @id", ct,
        ("speed", Math.Clamp(speed, 1, 100)));

    public Task SetAutoplayAsync(int id, bool autoplay, CancellationToken ct = default) => ExecuteAsync(id,
        "UPDATE exercises SET autoplay = @autoplay WHERE id = @id", ct, ("autoplay", autoplay));

    public async Task<IReadOnlyList<Participant>> GetParticipantsAsync(int exerciseId, CancellationToken ct = default)
    {
        var list = new List<Participant>();
        await using var cmd = db.CreateCommand(
            "SELECT id, name, role FROM participants WHERE exercise_id = @id ORDER BY id");
        cmd.Parameters.AddWithValue("id", exerciseId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Participant(reader.GetInt32(0), reader.GetString(1), reader.GetString(2)));
        }
        return list;
    }

    public Task AddParticipantAsync(int exerciseId, string name, string role, CancellationToken ct = default) =>
        ExecuteAsync(exerciseId,
            "INSERT INTO participants (exercise_id, name, role) VALUES (@id, @name, @role) ON CONFLICT DO NOTHING", ct,
            ("name", name), ("role", role));

    public async Task<IReadOnlyList<InjectRelease>> GetReleasesAsync(int exerciseId, CancellationToken ct = default)
    {
        var list = new List<InjectRelease>();
        await using var cmd = db.CreateCommand(
            "SELECT inject_id, released_at, released_by FROM inject_releases WHERE exercise_id = @id ORDER BY released_at");
        cmd.Parameters.AddWithValue("id", exerciseId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new InjectRelease(reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2)));
        }
        return list;
    }

    public Task ReleaseInjectAsync(int exerciseId, string injectId, string by, CancellationToken ct = default) =>
        ExecuteAsync(exerciseId,
            "INSERT INTO inject_releases (exercise_id, inject_id, released_by) VALUES (@id, @inject, @by) ON CONFLICT DO NOTHING", ct,
            ("inject", injectId), ("by", by));

    public async Task<IReadOnlyList<Decision>> GetDecisionsAsync(int exerciseId, CancellationToken ct = default)
    {
        var list = new List<Decision>();
        await using var cmd = db.CreateCommand(
            """
            SELECT d.id, d.made_at, d.phase, d.decision, d.rationale, p.name, p.role
            FROM decisions d LEFT JOIN participants p ON p.id = d.participant_id
            WHERE d.exercise_id = @id ORDER BY d.made_at
            """);
        cmd.Parameters.AddWithValue("id", exerciseId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Decision(reader.GetInt32(0), reader.GetFieldValue<DateTimeOffset>(1), reader.GetString(2),
                reader.GetString(3), reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5), reader.IsDBNull(6) ? null : reader.GetString(6)));
        }
        return list;
    }

    public Task LogDecisionAsync(int exerciseId, int? participantId, string phase, string decision, string rationale,
        CancellationToken ct = default) =>
        ExecuteAsync(exerciseId,
            """
            INSERT INTO decisions (exercise_id, participant_id, phase, decision, rationale)
            VALUES (@id, @participant, @phase, @decision, @rationale)
            """, ct,
            ("participant", (object?)participantId ?? DBNull.Value), ("phase", phase),
            ("decision", decision), ("rationale", rationale));

    public async Task<IReadOnlyList<TimelineMessage>> GetMessagesAsync(int exerciseId, CancellationToken ct = default)
    {
        var list = new List<TimelineMessage>();
        await using var cmd = db.CreateCommand(
            "SELECT id, phase, audience, sender, title, body, created_at, created_by " +
            "FROM timeline_messages WHERE exercise_id = @id ORDER BY created_at");
        cmd.Parameters.AddWithValue("id", exerciseId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new TimelineMessage(reader.GetInt32(0), reader.GetString(1),
                reader.GetFieldValue<string[]>(2), reader.GetString(3), reader.GetString(4), reader.GetString(5),
                reader.GetFieldValue<DateTimeOffset>(6), reader.GetString(7)));
        }
        return list;
    }

    public Task AddMessageAsync(int exerciseId, string phase, IReadOnlyList<string> audience, string sender,
        string title, string body, string by, CancellationToken ct = default) =>
        ExecuteAsync(exerciseId,
            """
            INSERT INTO timeline_messages (exercise_id, phase, audience, sender, title, body, created_by)
            VALUES (@id, @phase, @audience, @sender, @title, @body, @by)
            """, ct,
            ("phase", phase), ("audience", audience.ToArray()), ("sender", sender),
            ("title", title), ("body", body), ("by", by));

    public Task EditMessageAsync(int exerciseId, int messageId, string phase, IReadOnlyList<string> audience,
        string sender, string title, string body, CancellationToken ct = default) =>
        ExecuteAsync(exerciseId,
            """
            UPDATE timeline_messages SET phase = @phase, audience = @audience, sender = @sender,
                   title = @title, body = @body
            WHERE id = @msg AND exercise_id = @id
            """, ct,
            ("msg", messageId), ("phase", phase), ("audience", audience.ToArray()),
            ("sender", sender), ("title", title), ("body", body));

    public Task DeleteMessageAsync(int exerciseId, int messageId, CancellationToken ct = default) =>
        ExecuteAsync(exerciseId,
            "DELETE FROM timeline_messages WHERE id = @msg AND exercise_id = @id", ct, ("msg", messageId));

    /// <summary>The latest state of every node on each layer. Nodes never set are "normal".</summary>
    public async Task<IReadOnlyList<NodeState>> GetNodeStatesAsync(int exerciseId, CancellationToken ct = default)
    {
        var list = new List<NodeState>();
        await using var cmd = db.CreateCommand(
            """
            SELECT DISTINCT ON (node, layer) node, layer, state, note, changed_at, changed_by
            FROM node_state_changes WHERE exercise_id = @id
            ORDER BY node, layer, id DESC
            """);
        cmd.Parameters.AddWithValue("id", exerciseId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new NodeState(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4), reader.GetString(5)));
        }
        return list;
    }

    /// <summary>Applies a scenario's declared ground-truth map to the ground_truth layer.</summary>
    public async Task ApplyGroundTruthAsync(int exerciseId, Scenario scenario, string by, CancellationToken ct = default)
    {
        foreach (var n in scenario.GroundTruth)
        {
            if (!string.IsNullOrWhiteSpace(n.Node))
            {
                await SetNodeStateAsync(exerciseId, n.Node, "ground_truth", n.State, n.Note, by, ct);
            }
        }
    }

    public Task SetNodeStateAsync(int exerciseId, string node, string layer, string state, string note, string by,
        CancellationToken ct = default) =>
        ExecuteAsync(exerciseId,
            """
            INSERT INTO node_state_changes (exercise_id, node, layer, state, note, changed_by)
            VALUES (@id, @node, @layer, @state, @note, @by)
            """, ct,
            ("node", node), ("layer", layer), ("state", state), ("note", note), ("by", by));

    /// <summary>
    /// Everything that happened during the exercise, oldest first: injects released, decisions,
    /// map changes (both layers), and defence-flag changes made while it was running.
    /// </summary>
    public async Task<IReadOnlyList<TimelineEntry>> GetTimelineAsync(int exerciseId, CancellationToken ct = default)
    {
        var list = new List<TimelineEntry>();
        await using var cmd = db.CreateCommand(
            """
            WITH x AS (SELECT coalesce(started_at, created_at) AS t0, coalesce(ended_at, now()) AS t1
                       FROM exercises WHERE id = @id)
            SELECT released_at, 'inject', inject_id, '', released_by FROM inject_releases WHERE exercise_id = @id
            UNION ALL
            SELECT d.made_at, 'decision', d.phase, d.decision, coalesce(p.name || ' (' || p.role || ')', '')
              FROM decisions d LEFT JOIN participants p ON p.id = d.participant_id WHERE d.exercise_id = @id
            UNION ALL
            SELECT changed_at, 'map.' || layer, node, state || CASE WHEN note <> '' THEN ': ' || note ELSE '' END, changed_by
              FROM node_state_changes WHERE exercise_id = @id
            UNION ALL
            SELECT c.changed_at, 'defence', c.vuln_id, c.old_mode || ' -> ' || c.new_mode, c.changed_by
              FROM flag_changes c, x WHERE c.changed_at BETWEEN x.t0 AND x.t1
            UNION ALL
            SELECT created_at, 'message', sender, title, created_by
              FROM timeline_messages WHERE exercise_id = @id
            ORDER BY 1
            """);
        cmd.Parameters.AddWithValue("id", exerciseId);
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new TimelineEntry(reader.GetFieldValue<DateTimeOffset>(0), reader.GetString(1),
                reader.GetString(2), reader.GetString(3), reader.GetString(4)));
        }
        return list;
    }

    private const string ExerciseSelect =
        "SELECT id, scenario_id, title, status, created_at, started_at, ended_at, speed, autoplay FROM exercises";

    private static async Task<List<Exercise>> ReadExercisesAsync(NpgsqlCommand cmd, CancellationToken ct)
    {
        var list = new List<Exercise>();
        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            list.Add(new Exercise(reader.GetInt32(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
                reader.GetFieldValue<DateTimeOffset>(4),
                reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTimeOffset>(5),
                reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTimeOffset>(6),
                (double)reader.GetDecimal(7), reader.GetBoolean(8)));
        }
        return list;
    }

    // Runs one parameterised write for an exercise (@id), then tells every open console.
    private async Task ExecuteAsync(int exerciseId, string sql, CancellationToken ct, params (string Name, object Value)[] args)
    {
        await using var cmd = db.CreateCommand(sql);
        cmd.Parameters.AddWithValue("id", exerciseId);
        foreach (var (name, value) in args)
        {
            cmd.Parameters.AddWithValue(name, value);
        }
        await cmd.ExecuteNonQueryAsync(ct);
        events.Notify(exerciseId);
    }
}
