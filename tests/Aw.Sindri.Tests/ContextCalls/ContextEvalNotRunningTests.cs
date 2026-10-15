using Aw.Context.Core;
using Aw.Context.Core.Eval;
using Aw.Context.Testing.Engine;
using Aw.Sindri.Host.Commands;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'context eval probes' with no control plane: every probe's query fails, so the run is incomplete (exit 2), never the attention exit of a graph verb.</summary>
public sealed class ContextEvalNotRunningTests
{
    [Fact, Trait("Lane", "unit")]
    public async Task Probes_with_no_control_plane_exit_2_and_say_the_run_is_incomplete()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var set = new ProbeSet([new ProbeCase(ProbeId.From("one"), "what breaks", new ProbeExpect())]);
        using var client = new ControlPlaneClient(new FakeControlPlaneProbe(), SliceHarness.Json);
        var verbs = new ContextEvalVerbs(client, new Output(stdout, stderr, SliceHarness.Json), FakeContextEngine.WithProbes(set));

        var code = await verbs.Run(["probes"], TestContext.Current.CancellationToken);

        Assert.Equal(2, code);
        Assert.Contains("INCOMPLETE", stdout.ToString() + stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("one: SND-API-001", stdout.ToString() + stderr.ToString(), StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task An_engine_error_is_attention_exit_1_through_the_verb_s_failure_path()
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        using var client = new ControlPlaneClient(new FakeControlPlaneProbe(), SliceHarness.Json);
        var verbs = new ContextEvalVerbs(client, new Output(stdout, stderr, SliceHarness.Json), FakeContextEngine.Instance);

        var code = await verbs.Run(["probes"], TestContext.Current.CancellationToken);

        Assert.Equal(1, code);
        Assert.Contains(ContextErrors.NoProbeFile.Code, stdout.ToString() + stderr.ToString(), StringComparison.Ordinal);
    }
}
