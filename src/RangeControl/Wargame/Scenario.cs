using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace RangeControl.Wargame;

/// <summary>A wargame scenario, loaded from <c>wargame/scenarios/*.yaml</c> (ADR-0008).</summary>
public sealed class Scenario
{
    public string Id { get; set; } = "";
    public string Title { get; set; } = "";
    public string Summary { get; set; } = "";
    public int DurationMinutes { get; set; }
    public string Briefing { get; set; } = "";
    public List<Inject> Injects { get; set; } = [];
    /// <summary>Facilitator-only: never shown to players.</summary>
    public string FacilitatorNotes { get; set; } = "";
    public List<string> DebriefQuestions { get; set; } = [];
}

/// <summary>A timed message released to some roles during the exercise.</summary>
public sealed class Inject
{
    public string Id { get; set; } = "";
    public int AtMinute { get; set; }
    public string Phase { get; set; } = "";
    public List<string> Audience { get; set; } = [];
    public string From { get; set; } = "";
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
}

/// <summary>
/// Reads the scenario files on demand, so a facilitator can edit a scenario without a rebuild
/// (the folder is mounted read-only into the container).
/// </summary>
public sealed class ScenarioCatalog(string directory)
{
    private static readonly IDeserializer Yaml = new DeserializerBuilder()
        .WithNamingConvention(UnderscoredNamingConvention.Instance)
        .IgnoreUnmatchedProperties()
        .Build();

    public IReadOnlyList<Scenario> All() =>
        Directory.Exists(directory)
            ? Directory.EnumerateFiles(directory, "*.yaml").Order().Select(Load).ToList()
            : [];

    public Scenario? Find(string id) => All().FirstOrDefault(s => s.Id == id);

    public static Scenario Parse(string yaml) => Yaml.Deserialize<Scenario>(yaml);

    private static Scenario Load(string path) => Parse(File.ReadAllText(path));
}
