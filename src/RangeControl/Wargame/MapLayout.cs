namespace RangeControl.Wargame;

public sealed record ZoneBox(Zone Zone, double X, double Y, double Width, double Height);
public sealed record NodeBox(ServiceNode Service, double X, double Y, double Width, double Height)
{
    public double CenterX => X + Width / 2;
    public double CenterY => Y + Height / 2;
}

/// <summary>
/// Places the topology on a fixed grid for the infra map: each trust zone is a box in a column,
/// read left to right roughly in the order a request travels (clients, edge, application, agent,
/// data, then support zones), with its services stacked inside. Zones not listed go in a last column.
/// </summary>
public sealed record MapLayout(IReadOnlyList<ZoneBox> Zones, IReadOnlyList<NodeBox> Nodes, double Width, double Height)
{
    public const double ZoneWidth = 176;
    public const double NodeWidth = 156;
    public const double NodeHeight = 30;
    private const double ColumnGap = 34;
    private const double ZoneGap = 18;
    private const double Header = 26;
    private const double NodeGap = 8;
    private const double Padding = 10;

    // Column per zone, top to bottom within a column.
    private static readonly string[][] Columns =
    [
        ["Z1"],
        ["Z2"],
        ["Z3", "Z3a", "Z3b"],
        ["Z4", "Z7"],
        ["Z5", "Z6"],
        ["Z8", "Z9"],
    ];

    public static MapLayout For(Topology topology)
    {
        var known = Columns.SelectMany(c => c).ToHashSet();
        var columns = Columns.Select(c => c.ToList()).ToList();
        var extra = topology.Zones.Select(z => z.Id).Where(id => !known.Contains(id)).ToList();
        if (extra.Count > 0)
        {
            columns.Add(extra);
        }

        var zoneBoxes = new List<ZoneBox>();
        var nodeBoxes = new List<NodeBox>();
        double height = 0;
        for (var col = 0; col < columns.Count; col++)
        {
            var x = Padding + col * (ZoneWidth + ColumnGap);
            var y = Padding;
            foreach (var zoneId in columns[col])
            {
                var zone = topology.Zones.FirstOrDefault(z => z.Id == zoneId);
                var services = topology.Services.Where(s => s.Zone == zoneId).ToList();
                if (zone is null || services.Count == 0)
                {
                    continue;
                }
                var zoneHeight = Header + services.Count * (NodeHeight + NodeGap) + NodeGap;
                zoneBoxes.Add(new ZoneBox(zone, x, y, ZoneWidth, zoneHeight));
                for (var i = 0; i < services.Count; i++)
                {
                    nodeBoxes.Add(new NodeBox(services[i],
                        x + (ZoneWidth - NodeWidth) / 2, y + Header + i * (NodeHeight + NodeGap), NodeWidth, NodeHeight));
                }
                y += zoneHeight + ZoneGap;
            }
            height = Math.Max(height, y);
        }

        var width = Padding * 2 + columns.Count * ZoneWidth + (columns.Count - 1) * ColumnGap;
        return new MapLayout(zoneBoxes, nodeBoxes, width, height + Padding);
    }
}
