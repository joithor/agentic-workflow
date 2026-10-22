using Aw.Kernel.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Launch;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing;
using Aw.Sindri.Testing.Launch;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>The agent runtime is on only when a launcher is registered AND the approved profile sets 'workflow.agent.target' (engine P8 D6, D7).</summary>
[Lane(TestLane.Unit)]
public sealed class AgentStepSelectionTests
{
    private static readonly BlockDescriptor _agent = TestBlocks.Step("step.produce") with { Runtime = BlockRuntime.Agent, DeskCapable = true };
    private static readonly BlockDescriptor _desk = TestBlocks.Step("step.checks");

    [Fact]
    public async Task A_launcher_and_a_target_select_the_agent_steps()
    {
        var selection = new AgentStepSelection(await Profiles(target: "claude-code"), [new FakeLauncher()]);

        var selected = await selection.Selected([_desk, _agent], TestContext.Current.CancellationToken);

        Assert.Equal(["step.produce"], selected.Value.Select(static id => id.Value), StringComparer.Ordinal);
    }

    [Fact]
    public async Task No_launcher_selects_nothing_even_with_a_target()
    {
        var selection = new AgentStepSelection(await Profiles(target: "claude-code"), []);

        Assert.Empty((await selection.Selected([_agent], TestContext.Current.CancellationToken)).Value);
    }

    [Fact]
    public async Task No_target_selects_nothing_even_with_a_launcher()
    {
        var selection = new AgentStepSelection(await Profiles(target: null), [new FakeLauncher()]);

        Assert.Empty((await selection.Selected([_agent], TestContext.Current.CancellationToken)).Value);
    }

    private static async Task<InMemoryProfiles> Profiles(string? target)
    {
        var declared = new SindriProfile(SindriProfile.CurrentSchemaVersion, "tester", Workflow: target is null ? null : new WorkflowSettings(Agent: new WorkflowAgentSettings(AgentId.From(target))));
        var profile = ProfileRules.Check(declared).Value;
        var profiles = new InMemoryProfiles();
        _ = await profiles.Save(new LoadedProfile(profile, ProfileHashing.Of(AwJson.Serialize(profile, SliceHarness.Json)), "/home/profile.yaml"), TestContext.Current.CancellationToken);
        return profiles;
    }
}
