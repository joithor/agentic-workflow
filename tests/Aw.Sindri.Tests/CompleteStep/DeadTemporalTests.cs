using Aw.Kernel;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Features.CompleteStep;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Aw.Sindri.Tests.ApplyCommand;
using Aw.Sindri.Tests.Ledger;
using Aw.Sindri.Tests.Workflows;
using Microsoft.Extensions.Time.Testing;

namespace Aw.Sindri.Tests.CompleteStep;

/// <summary>
/// A dev server that died under a live serve (review run 2, code-reviewer-r2-1): every Update and Signal on the instance
/// is SND-API-001 (Sindri is not running), not a bare RPC failure that surfaces as a 500, and the Kind (Unavailable) keeps
/// a fresh nonce claim so a retry with the same nonce resends the same Update id.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class DeadTemporalTests
{
    private static readonly InstanceAddress _address = InstanceAddress.From("T-90/step/1");
    private static readonly CommandNonce _nonce = CommandNonce.From(new string('e', 32));

    [Fact(Timeout = 120_000)]
    public async Task Updates_and_signals_on_a_Temporal_that_died_are_SND_API_001()
    {
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempHome();
        var environment = await WorkflowsHarness.StartDevServer(ct: TestContext.Current.CancellationToken);
        var settings = new TemporalSettings(home.Path, PinnedTemporal.Path());
        using var frontend = new TemporalFrontend(new LiveEndpoint(WorkflowsHarness.PortOf(environment)), settings);
        var runtime = new TemporalInstanceRuntime(frontend, new TemporalDeskReports(frontend), new TemporalCommands(frontend), settings, new KnownInstances());
        Assert.Equal("SND-WF-001", (await runtime.Get(_address, ct)).Error.Code);   // connected, and the instance isn't there
        await environment.ShutdownAsync();

        var command = new CommandFired("human:test", CommandVerb.From("ship"), new CommandStamp(_nonce, new string('d', 64)));
        var report = new StepCompleted("human:test", RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, FactValue>(), Tokens: 0, StepRuntime.Desk, new string('c', 64));
        var routed = new WaitSatisfied("service:workflow.events", new EventWaitSource(EventKind.From("item.routed"), 1), OutcomeName.From("startable"), new Dictionary<FactKey, FactValue>());

        var codes = new[]
        {
            (await runtime.Send(_address, _nonce, command, ct)).Error.Code,
            (await runtime.Send(_address, _nonce, report, ct)).Error.Code,
            (await runtime.Signal(_address, routed, ct)).Error.Code,
        };

        Assert.Equal(["SND-API-001", "SND-API-001", "SND-API-001"], codes);
    }

    [Fact(Timeout = 120_000)]
    public async Task A_command_or_report_on_a_Temporal_that_died_names_the_nonce_to_rerun_with_and_a_start_does_not()
    {
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempHome();
        var environment = await WorkflowsHarness.StartDevServer(ct: ct);
        var settings = new TemporalSettings(home.Path, PinnedTemporal.Path());
        using var frontend = new TemporalFrontend(new LiveEndpoint(WorkflowsHarness.PortOf(environment)), settings);
        var runtime = new TemporalInstanceRuntime(frontend, new TemporalDeskReports(frontend), new TemporalCommands(frontend), settings, new KnownInstances());
        Assert.Equal("SND-WF-001", (await runtime.Get(_address, ct)).Error.Code);
        await environment.ShutdownAsync();

        var command = new CommandFired("human:test", CommandVerb.From("ship"), new CommandStamp(_nonce, new string('d', 64)));
        var report = new StepCompleted("human:test", RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, FactValue>(), Tokens: 0, StepRuntime.Desk, new string('c', 64));
        var routed = new WaitSatisfied("service:workflow.events", new EventWaitSource(EventKind.From("item.routed"), 1), OutcomeName.From("startable"), new Dictionary<FactKey, FactValue>());

        var sent = (await runtime.Send(_address, _nonce, command, ct)).Error.Message;
        var completed = (await runtime.Send(_address, _nonce, report, ct)).Error.Message;
        var signalled = (await runtime.Signal(_address, routed, ct)).Error.Message;

        Assert.All([sent, completed], m => Assert.Contains($"--nonce {_nonce.Value}", m, StringComparison.Ordinal));
        Assert.All([sent, completed], m => Assert.Contains("sindri workflow status", m, StringComparison.Ordinal));
        Assert.All([sent, completed, signalled], m => Assert.DoesNotContain("start sindri serve", m, StringComparison.Ordinal));
        Assert.All([sent, completed, signalled], m => Assert.Contains("sindri doctor", m, StringComparison.Ordinal));
        Assert.DoesNotContain("--nonce", signalled, StringComparison.Ordinal);
    }

    [Fact(Timeout = 120_000)]
    public async Task Reads_start_and_a_report_on_a_Temporal_that_died_are_SND_API_001_and_a_fresh_claim_is_released()
    {
        var ct = TestContext.Current.CancellationToken;
        using var home = new TempHome();
        var environment = await WorkflowsHarness.StartDevServer(ct: ct);
        var settings = new TemporalSettings(home.Path, PinnedTemporal.Path());
        using var frontend = new TemporalFrontend(new LiveEndpoint(WorkflowsHarness.PortOf(environment)), settings);
        var known = new KnownInstances();
        var runtime = new TemporalInstanceRuntime(frontend, new TemporalDeskReports(frontend), new TemporalCommands(frontend), settings, known);
        var directory = new TemporalInstanceDirectory(frontend, known);
        var like = new TemporalLikeRuntime();   // why: the approval check and the caller, which are not Temporal's
        var nonces = new InMemoryCommandNonces(new FakeTimeProvider());
        var service = new CompleteStepService(directory, runtime, like, new InMemoryFactStore(), nonces, like);
        Assert.Equal("SND-WF-001", (await runtime.Get(_address, ct)).Error.Code);
        await environment.ShutdownAsync();

        var request = new CompleteStepRequest(_address.Value, RunToken.For(1, 1), OutcomeName.From("done"), new Dictionary<FactKey, ReadOnlyMemory<byte>>(), _nonce);
        var reported = await service.Handle(request, ct);
        var released = await nonces.Claim(_nonce, "another command", ct);   // fresh again: the report sent nothing
        var start = new InstanceStart(new RecipeVersion(RecipeId.From("workflow.step"), 1, RecipeHash.From("sha256:" + new string('a', 64))), ItemKey.From("T-90"), Attempt: 1, new Dictionary<FactKey, InputFact>(), Tenant: "local", Ring: 0);

        Assert.Equal("SND-API-001", reported.Error.Code);
        Assert.Equal(NonceClaim.Fresh, released.Value);
        Assert.Equal("SND-API-001", (await runtime.Start(start, ct)).Error.Code);
        Assert.Equal("SND-API-001", (await directory.Latest(ItemKey.From("T-90"), RecipeId.From("workflow.step"), ct)).Error.Code);
        Assert.Equal("SND-API-001", (await directory.Resolve("T-90", ct)).Error.Code);
        Assert.Equal("SND-API-001", (await directory.Resolve(WorkflowInstanceId.Of(InstanceAddress.From("T-91/step/1")).Value, ct)).Error.Code);
    }

    private sealed class LiveEndpoint(int port) : IEndpointFile
    {
        public ValueTask<Result<ControlEndpoint?>> Read(CancellationToken cancellationct) =>
            ValueTask.FromResult<Result<ControlEndpoint?>>(new ControlEndpoint(new Uri("http://127.0.0.1:1"), Pid: 1, Epoch: 1) { Temporal = new TemporalEndpoint(Pid: 1, port, UiPort: 0) });

        public ValueTask<Result<Unit>> Write(ControlEndpoint endpoint, CancellationToken cancellationct) => throw new NotSupportedException("read only");

        public ValueTask<Result<Unit>> Remove(int pid, CancellationToken cancellationct) => throw new NotSupportedException("read only");
    }
}
