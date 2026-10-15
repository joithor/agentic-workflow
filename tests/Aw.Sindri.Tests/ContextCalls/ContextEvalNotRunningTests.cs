using System.Reflection;
using Aw.Context.Core.Eval;
using Aw.Context.Core.Facade;
using Aw.Kernel;
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
        var probe = new FakeControlPlaneProbe();
        using var client = new ControlPlaneClient(probe, SliceHarness.Json);
        var engine = DispatchProxy.Create<IContextEngine, ProbeOnlyEngine>();
        var verbs = new ContextEvalVerbs(client, new Output(stdout, stderr, SliceHarness.Json), engine);

        var code = await verbs.Run(["probes"], TestContext.Current.CancellationToken);

        Assert.Equal(2, code);
        Assert.Contains("INCOMPLETE", stdout.ToString() + stderr.ToString(), StringComparison.Ordinal);
        Assert.Contains("one: SND-API-001", stdout.ToString() + stderr.ToString(), StringComparison.Ordinal);
    }

    /// <summary>An engine that runs the real probe runner over one probe and nothing else.</summary>
    public class ProbeOnlyEngine : DispatchProxy
    {
        protected override object? Invoke(MethodInfo? targetMethod, object?[]? args)
        {
            Assert.Equal(nameof(IContextEngine.EvalProbes), targetMethod?.Name);
            var ask = (Func<ProbeCase, CancellationToken, ValueTask<Result<Aw.Context.Core.Results.ContextResult>>>)args![1]!;
            return new ValueTask<Result<ProbeRun>>(Run(ask));
        }

        private static async Task<Result<ProbeRun>> Run(Func<ProbeCase, CancellationToken, ValueTask<Result<Aw.Context.Core.Results.ContextResult>>> ask)
        {
            var set = new ProbeSet([new ProbeCase(ProbeId.From("one"), "what breaks", new ProbeExpect())]);
            var report = await ProbeRunner.Run(new ProbeFile("/p/probes.json", "h", set), ask, baseline: null, DateTimeOffset.UnixEpoch, TestContext.Current.CancellationToken);
            return report.IsError ? report.Error : new ProbeRun(report.Value, Path: null);
        }
    }
}
