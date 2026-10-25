using System.Text.Json.Nodes;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Scoping;
using Aw.Sindri.Infrastructure.Scoping;
using Aw.Sindri.Testing.Scoping;

namespace Aw.Sindri.Tests.Scoping;

/// <summary>The file tracker source honours the tracker contract over an export written to a temp directory, and reads the scoping fixture's real export.</summary>
[Lane(TestLane.Integration)]
public sealed class FileTrackerSourceContractTests : TrackerSourceContract, IDisposable
{
    private readonly string _base = Path.Combine(Path.GetTempPath(), "aw-tracker-" + Path.GetRandomFileName());

    public void Dispose()
    {
        if (Directory.Exists(_base))
        {
            Directory.Delete(_base, recursive: true);
        }
    }

    protected override ITrackerSource Create(TrackerSeed seed)
    {
        ArgumentNullException.ThrowIfNull(seed);
        _ = Directory.CreateDirectory(_base);
        var projects = seed.Projects.Select(static (p, i) => new JsonObject { ["id"] = "p" + i.ToString(System.Globalization.CultureInfo.InvariantCulture), ["name"] = p.Name, ["description"] = p.Text, ["updatedAt"] = TrackerSeed.Updated });
        var issues = seed.Issues.Select(i => new JsonObject
        {
            ["identifier"] = i.Key,
            ["title"] = i.Title,
            ["description"] = i.Text,
            ["updatedAt"] = TrackerSeed.Updated,
            ["project"] = i.Project is null ? null : new JsonObject { ["id"] = "p" + seed.Projects.ToList().FindIndex(p => string.Equals(p.Name, i.Project, StringComparison.Ordinal)).ToString(System.Globalization.CultureInfo.InvariantCulture) },
            ["comments"] = new JsonObject { ["nodes"] = new JsonArray([.. i.Comments.Select(static c => (JsonNode)new JsonObject { ["body"] = c.Text, ["user"] = new JsonObject { ["name"] = c.Author } })]) },
        });
        File.WriteAllText(Path.Combine(_base, "projects.json"), Wrap("projects", projects));
        File.WriteAllText(Path.Combine(_base, "issues.json"), Wrap("issues", issues));
        return new FileTrackerSource(_base);
    }

    protected override ITrackerSource CreateUnconfigured() => new FileTrackerSource(directory: null);

    private static string Wrap(string name, IEnumerable<JsonObject> nodes) =>
        new JsonObject { ["data"] = new JsonObject { [name] = new JsonObject { ["nodes"] = new JsonArray([.. nodes]) } } }.ToJsonString();

    [Fact]
    public async Task A_directory_that_does_not_exist_is_no_tracker()
    {
        var read = await new FileTrackerSource(Path.Combine(_base, "absent")).Read("NTS-1", TrackerRead.Scoping, TestContext.Current.CancellationToken);

        Assert.Equal(ScopingErrors.NoTrackerSource.Code, read.Error.Code);
    }
}
