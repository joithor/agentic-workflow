using System.Text;
using Aw.Kernel.Json;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;

namespace Aw.Sindri.Tests.Profile;

/// <summary>The profile's 'workflow.agent.target' (engine P8, launcher L-14): any agent id by pattern, and invisible to a profile that doesn't set it.</summary>
public sealed class WorkflowAgentTargetTests
{
    private static readonly SindriProfile _valid = new(1, "thor");

    [Fact, Trait("Lane", "integration")]
    public async Task Workflow_agent_target_takes_any_agent_id_and_refuses_a_bad_pattern()
    {
        using var files = new ProfileFiles();
        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  agent:\n    target: no-such-agent-on-this-box\n");
        var loaded = await files.Source.Load(TestContext.Current.CancellationToken);
        Assert.False(loaded.IsError, loaded.IsError ? loaded.Error.Message : null);
        Assert.Equal("no-such-agent-on-this-box", loaded.Value.Profile.Workflow?.Agent?.Target?.Value);

        files.Write("schemaVersion: 1\nuser: thor\nworkflow:\n  agent:\n    target: Not_An_Id\n");
        Assert.True((await files.Source.Load(TestContext.Current.CancellationToken)).IsError);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_profile_without_agent_keeps_its_hash()
    {
        var plain = Encoding.UTF8.GetString(AwJson.Serialize(ProfileRules.Check(_valid).Value, SliceHarness.Json));
        var withOthers = Encoding.UTF8.GetString(AwJson.Serialize(ProfileRules.Check(_valid with { Workflow = new(DeskTimeout: "1m") }).Value, SliceHarness.Json));

        Assert.DoesNotContain("agent", plain, StringComparison.Ordinal);
        Assert.DoesNotContain("agent", withOthers, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void The_target_is_part_of_the_diff_the_human_approves()
    {
        var target = AgentId.From("claude-code");

        var changed = ProfileDiff.Between(_valid, _valid with { Workflow = new(Agent: new(target)) });

        Assert.Equal(["workflow.agent.target"], changed);
    }
}
