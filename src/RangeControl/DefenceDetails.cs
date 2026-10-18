using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RangeControl;

/// <summary>Teaching detail shown under a defence toggle: how to simulate the attack, how the fix works.</summary>
public sealed class DefenceDetail
{
    public string Simulate { get; set; } = "";
    public string Mitigation { get; set; } = "";
}

/// <summary>
/// Reads <c>infra/defence-details.yaml</c> (mounted read-only into range-control, like the topology
/// and scenarios) on each use, so the attack/mitigation text can be edited without a rebuild. Keyed
/// by vuln id. A flag with no entry simply shows no detail.
/// </summary>
public sealed class DefenceDetailSource(string path)
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public IReadOnlyDictionary<string, DefenceDetail> Load()
    {
        if (!File.Exists(path))
        {
            return new Dictionary<string, DefenceDetail>();
        }
        return Yaml.Deserialize<Dictionary<string, DefenceDetail>>(File.ReadAllText(path))
            ?? new Dictionary<string, DefenceDetail>();
    }
}
