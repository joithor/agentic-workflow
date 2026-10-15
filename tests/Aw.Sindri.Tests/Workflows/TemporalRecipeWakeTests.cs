using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Triggers;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Registry;
using Aw.Sindri.Infrastructure.Workflows;
using Microsoft.Extensions.DependencyInjection;
using Temporalio.Api.Enums.V1;
using Temporalio.Client;
using Temporalio.Testing;
using Temporalio.Worker;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>
/// The registry's wake over a real Temporal dev server (engine spec §4.1, W11; engine P6 integration): it finds the open
/// instances whose 'RecipeHash' is the notice's and signals only those, finds nothing when no serve records a Temporal,
/// skips an instance that ended between the list and the signal, and tries every instance before it reports a failure.
/// </summary>
[Lane(TestLane.Integration)]
public sealed class TemporalRecipeWakeTests : IDisposable
{
    private readonly WorkflowsHarness _harness = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task Running_lists_the_open_instances_of_a_hash_and_a_wake_signals_only_those()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: Token);
        var notify = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        var timeout = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = Sindri(env);
        await WorkflowsHarness.HoldLease(sindri, Token);
        _ = await WorkflowsHarness.AsServe(sindri, env, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var (a, b) = await Started(sindri, env, "T-201", "T-202");
            var wake = new TemporalRecipeWake(sindri.GetRequiredService<TemporalFrontend>());
            var running = await Listed(wake, notify.Hash);

            var woken = await wake.Wake(new RecipeWakeNotice(notify.Hash, RecipeWakeKind.Drifted, Sequence: 5, "human:cli"), Token);

            Assert.Equal([InstanceIdOf(a)], running);
            Assert.Equal([InstanceIdOf(b)], await Listed(wake, timeout.Hash));
            Assert.True(woken.IsSuccess);
            Assert.Equal((1, 0), (await Signals(a), await Signals(b)));   // only the matching hash was woken
            await a.TerminateAsync("test over");
            await b.TerminateAsync("test over");
        }, Token);
    }

    [Fact]
    public async Task With_no_serve_recording_a_temporal_there_is_nobody_to_wake_and_the_wake_succeeds()
    {
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        await using var sindri = _harness.Sindri();   // no control.json: the connect is refused
        var wake = new TemporalRecipeWake(sindri.GetRequiredService<TemporalFrontend>());
        var hash = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml")).Hash;

        var running = await wake.Running(hash, Token);
        var woken = await wake.Wake(new RecipeWakeNotice(hash, RecipeWakeKind.Revoked, Sequence: 6, "human:cli"), Token);

        Assert.True(running.IsSuccess);
        Assert.Empty(running.Value);
        Assert.True(woken.IsSuccess);
    }

    /// <summary>An instance listed as open that ended before its signal (Temporal: not found) is skipped; the others still wake.</summary>
    [Fact]
    public async Task An_instance_that_ended_between_the_list_and_the_signal_is_skipped_and_the_rest_are_woken()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: Token);
        var notify = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = Sindri(env);
        await WorkflowsHarness.HoldLease(sindri, Token);
        _ = await WorkflowsHarness.AsServe(sindri, env, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());

        await worker.ExecuteAsync(async () =>
        {
            var (a, _) = await Started(sindri, env, "T-203", "T-204");
            var gone = InstanceAddress.Root(ItemKey.From("T-999"), RecipeId.From("workflow.dev-notify"), 1);
            var wake = new ListingWake(sindri.GetRequiredService<TemporalFrontend>(), extra: gone);

            var woken = await wake.Wake(new RecipeWakeNotice(notify.Hash, RecipeWakeKind.Drifted, Sequence: 7, "human:cli"), Token);

            Assert.True(woken.IsSuccess, woken.IsError ? woken.Error.Message : null);
            Assert.Equal(1, await Signals(a));
            await a.TerminateAsync("test over");
        }, Token);
    }

    /// <summary>A signal that fails for any other reason doesn't abandon the instances after it: all are tried, then one error names the failures.</summary>
    [Fact]
    public async Task A_failing_signal_is_reported_after_every_instance_was_tried()
    {
        await using var env = await WorkflowsHarness.StartDevServer(ct: Token);
        var notify = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-notify.yaml"));
        _ = _harness.Approve(WorkflowsHarness.Shipped("dev/workflow.dev-timeout.yaml"));
        await using var sindri = Sindri(env);
        await WorkflowsHarness.HoldLease(sindri, Token);
        _ = await WorkflowsHarness.AsServe(sindri, env, Token);
        using var worker = new TemporalWorker(env.Client, sindri.GetRequiredService<TemporalWorkerOptions>());
        List<InstanceAddress> open = [];

        await worker.ExecuteAsync(async () =>
        {
            var (a, b) = await Started(sindri, env, "T-205", "T-206");
            open = [InstanceAddress.From(a.Id), InstanceAddress.From(b.Id)];
        }, Token);
        var wake = new ListingWake(sindri.GetRequiredService<TemporalFrontend>(), extra: null, listed: open, afterList: () => env.ShutdownAsync());   // the server goes away once the list is read

        var woken = await wake.Wake(new RecipeWakeNotice(notify.Hash, RecipeWakeKind.Drifted, Sequence: 8, "human:cli"), Token);

        Assert.True(woken.IsError);
        Assert.Contains(open[0].Value, woken.Error.Message, StringComparison.Ordinal);
        Assert.Contains(open[1].Value, woken.Error.Message, StringComparison.Ordinal);
    }

    private ServiceProvider Sindri(WorkflowEnvironment env) => _harness.Sindri(
        static _ => { },
        name => name == "AW_SINDRI_TEMPORAL_NAMESPACE" ? env.Client.Options.Namespace : null);

    /// <summary>Starts 'workflow.dev-notify' on <paramref name="itemA"/> and 'workflow.dev-timeout' on <paramref name="itemB"/>, and waits until the worker has published each one's 'RecipeHash'.</summary>
    private static async Task<(WorkflowHandle<RecipeWorkflow, RecipeResult> A, WorkflowHandle<RecipeWorkflow, RecipeResult> B)> Started(
        IServiceProvider sindri, WorkflowEnvironment env, string itemA, string itemB)
    {
        var a = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-notify", itemA, Token));
        var b = await WorkflowsHarness.Start(env.Client, await WorkflowsHarness.RecipeStart(sindri, "workflow.dev-timeout", itemB, Token));
        _ = await WorkflowsHarness.Until(a, static v => v.Task is not null);
        _ = await WorkflowsHarness.Until(b, static v => v.Task is not null);
        return (a, b);
    }

    /// <summary>The ids <see cref="IRecipeWake.Running"/> lists, polled until the visibility store has them.</summary>
    private static async Task<string[]> Listed(TemporalRecipeWake wake, RecipeHash hash)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(20);
        while (true)
        {
            var running = await wake.Running(hash, Token);
            Assert.True(running.IsSuccess);
            if (running.Value.Count > 0 || DateTime.UtcNow > deadline)
            {
                return [.. running.Value.Select(static i => i.Value)];
            }

            await Task.Delay(100, Token);
        }
    }

    private static string InstanceIdOf(WorkflowHandle handle) => WorkflowInstanceId.Of(InstanceAddress.From(handle.Id)).Value;

    private static async Task<int> Signals(WorkflowHandle handle) =>
        (await handle.FetchHistoryAsync()).Events.Count(static e => e.EventType == EventType.WorkflowExecutionSignaled && e.WorkflowExecutionSignaledEventAttributes.SignalName == "wake");

    /// <summary>The wake with a list the test controls: the real one plus a stale address, or a fixed one, and a hook that runs once the list is read.</summary>
    private sealed class ListingWake(TemporalFrontend frontend, InstanceAddress? extra, IReadOnlyList<InstanceAddress>? listed = null, Func<Task>? afterList = null) : TemporalRecipeWake(frontend)
    {
        internal override async ValueTask<IReadOnlyList<InstanceAddress>> Open(RecipeHash hash, CancellationToken cancellationToken)
        {
            var open = listed ?? await base.Open(hash, cancellationToken);
            if (afterList is not null)
            {
                await afterList();
            }

            return extra is { } stale ? [stale, .. open] : open;
        }
    }
}
