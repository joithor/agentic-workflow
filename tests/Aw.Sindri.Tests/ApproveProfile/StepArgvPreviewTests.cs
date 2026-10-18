using Aw.Context.Testing.Engine;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Features.ApproveProfile;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ApproveProfile;

/// <summary>The approve preview shows the argv behind each added or changed step hash (launcher L-16).</summary>
[Lane(TestLane.Unit)]
public sealed class StepArgvPreviewTests
{
    private static SindriProfile Profile(string id, params string[] hashes) =>
        new(1, "thor", Agents: new AgentSettings(Targets: new Dictionary<string, AgentLimits>(StringComparer.Ordinal) { [id] = new(StepArgvHash: hashes) }));

    private static ProfileChecks Checks(params AgentEntry[] entries) => new(FakeContextEngine.Instance, new FixedAgentCatalog(entries));

    private static AgentEntry Aider() =>
        new(AgentId.From("aider"), "Aider", "aider", ProcessName: null, Transcripts: null, Resume: ["{bin}", "--restore"]) { Open = ["{bin}", "{seed}"], Source = CatalogSource.Added, ArgvHash = "sha256:aaa" };

    [Fact]
    public void Approve_preview_shows_argv_behind_hash()
    {
        var lines = Checks(Aider()).StepArgv(Profile("aider", "sha256:aaa"), ["agents.aider.stepArgvHash"]);

        var line = Assert.Single(lines);
        Assert.Equal(("aider", "sha256:aaa"), (line.Agent, line.Hash));
        Assert.Equal(("aider", CatalogSource.Added), (line.Entry!.Bin, line.Entry.Source));
        Assert.Equal(["{bin}", "{seed}"], line.Entry.Open);
        Assert.Equal(["{bin}", "--restore"], line.Entry.Resume);
    }

    [Fact]
    public void Approve_preview_says_no_entry_on_this_box()
    {
        var lines = Checks(Aider()).StepArgv(Profile("aider", "sha256:zzz"), ["agents.aider.stepArgvHash"]);

        Assert.Null(Assert.Single(lines).Entry);
    }

    [Fact]
    public void An_unchanged_hash_is_not_previewed()
    {
        Assert.Empty(Checks(Aider()).StepArgv(Profile("aider", "sha256:aaa"), ["api.port"]));
    }
}
