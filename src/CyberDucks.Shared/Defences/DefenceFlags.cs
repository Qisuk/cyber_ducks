using System.Collections.Frozen;

namespace CyberDucks.Shared.Defences;

/// <summary>Whether a defence is off (the range default) or on (ADR-0011).</summary>
public enum DefenceMode
{
    Vulnerable,
    Secure,
}

/// <summary>Current state of every defence flag in the range_control database.</summary>
public interface IDefenceFlags
{
    /// <summary>Mode for a vulnerability ID. Unknown IDs are <see cref="DefenceMode.Vulnerable"/>.</summary>
    DefenceMode Get(string vulnId);

    bool IsSecure(string vulnId) => Get(vulnId) == DefenceMode.Secure;

    IReadOnlyDictionary<string, DefenceMode> Snapshot { get; }
}

/// <summary>
/// In-memory copy of the flags, kept current by <see cref="DefenceFlagSync"/>. Reads are lock-free:
/// the whole snapshot is swapped atomically on each refresh.
/// </summary>
public sealed class DefenceFlagStore : IDefenceFlags
{
    private FrozenDictionary<string, DefenceMode> _flags = FrozenDictionary<string, DefenceMode>.Empty;

    public IReadOnlyDictionary<string, DefenceMode> Snapshot => _flags;

    public DefenceMode Get(string vulnId) =>
        _flags.TryGetValue(vulnId, out var mode) ? mode : DefenceMode.Vulnerable;

    /// <summary>Replaces the snapshot and returns the flags whose mode changed.</summary>
    public IReadOnlyList<(string VulnId, DefenceMode Mode)> Replace(IDictionary<string, DefenceMode> flags)
    {
        var previous = _flags;
        _flags = flags.ToFrozenDictionary(StringComparer.Ordinal);
        return _flags
            .Where(f => !previous.TryGetValue(f.Key, out var old) || old != f.Value)
            .Select(f => (f.Key, f.Value))
            .ToList();
    }
}
