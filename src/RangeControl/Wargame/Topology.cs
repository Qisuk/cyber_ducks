using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RangeControl.Wargame;

public sealed record Zone(string Id, string Name, string? Parent, bool InAttackScope);
public sealed record ServiceNode(string Name, string Zone, string Kind, string? Tech, string Status, string? Note);
public sealed record Flow(string Id, string From, string To, string What, string? Protocol, string? Boundary);

/// <summary>
/// The range topology from <c>infra/topology.yaml</c> (the single source of truth for zones,
/// services and flows). Drives the Ops Console infra map. Flows listing several endpoints are
/// expanded into one edge per pair.
/// </summary>
public sealed record Topology(IReadOnlyList<Zone> Zones, IReadOnlyList<ServiceNode> Services, IReadOnlyList<Flow> Flows)
{
    public static Topology Load(string path) => Parse(File.ReadAllText(path));

    public static Topology Parse(string yaml)
    {
        var file = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build()
            .Deserialize<TopologyFile>(yaml);

        var zones = file.Zones.Select(z => new Zone(z.Key, z.Value.Name, z.Value.Parent, z.Value.AttackScope ?? true)).ToList();
        var services = file.Services
            .Select(s => new ServiceNode(s.Key, s.Value.Zone, s.Value.Kind, s.Value.Tech, s.Value.Status ?? "implemented", s.Value.Note))
            .ToList();
        var flows = (
            from f in file.Flows
            from source in Endpoints(f.From)
            from target in Endpoints(f.To)
            select new Flow(f.Id, source, target, f.What, f.Protocol, f.Boundary)).ToList();
        return new Topology(zones, services, flows);
    }

    // YAML `from`/`to` is either a single service name or a list of them.
    private static IEnumerable<string> Endpoints(object value) => value switch
    {
        string one => [one],
        IEnumerable<object> many => many.Select(m => m.ToString()!),
        _ => [],
    };

    private sealed class TopologyFile
    {
        public Dictionary<string, ZoneDto> Zones { get; set; } = [];
        public Dictionary<string, ServiceDto> Services { get; set; } = [];
        public List<FlowDto> Flows { get; set; } = [];
    }

    private sealed class ZoneDto
    {
        public string Name { get; set; } = "";
        public string? Parent { get; set; }
        public bool? AttackScope { get; set; }
    }

    private sealed class ServiceDto
    {
        public string Zone { get; set; } = "";
        public string Kind { get; set; } = "";
        public string? Tech { get; set; }
        public string? Status { get; set; }
        public string? Note { get; set; }
    }

    private sealed class FlowDto
    {
        public string Id { get; set; } = "";
        public object From { get; set; } = "";
        public object To { get; set; } = "";
        public string What { get; set; } = "";
        public string? Protocol { get; set; }
        public string? Boundary { get; set; }
    }
}
