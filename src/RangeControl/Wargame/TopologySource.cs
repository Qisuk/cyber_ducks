namespace RangeControl.Wargame;

/// <summary>
/// Where the topology file lives (mounted read-only from infra/topology.yaml). Read on each use,
/// so edits to the topology show up without restarting range-control.
/// </summary>
public sealed class TopologySource(string path)
{
    public Topology Load() => Topology.Load(path);
}
