using Aw.Kernel;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Auth;
using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.StopProcess;

public sealed class StopProcessTests
{
    private readonly SliceHarness _h = new();
    private readonly FakeTemporalServer _temporal = new();
    private readonly TemporalRuntime _runtime;

    public StopProcessTests()
    {
        _runtime = new TemporalRuntime(_temporal, TimeProvider.System);
    }

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private static ServeRecords Records(IEndpointFile endpoints, HeldHumanToken? token = null, IHumanTokenStore? tokens = null) =>
        new(endpoints, token ?? new HeldHumanToken(), tokens ?? new InMemoryHumanTokenStore());

    private StopProcessService Service() => new(_h.Held, _h.Store, _h.Store, _h.Events, _h.Shutdown, Records(new InMemoryEndpointFile()), _runtime);

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_records_the_stop_releases_the_lease_and_shuts_down()
    {
        var grant = await _h.HoldLease();

        var stopped = (await Service().Handle(new StopProcessRequest(StopReason.Stop), Token)).Value;

        var recorded = Assert.Single(_h.Store.Events);
        Assert.Equal(new StopProcessResponse(grant.Holder.Pid, grant.Epoch, recorded.Sequence), stopped);
        Assert.Equal(EventTypes.ProcessStopped, recorded.Envelope.Type);
        Assert.True((await _h.Store.Read(Token)).Value!.Released);
        Assert.Null(_h.Held.Current);
        Assert.True(_h.Shutdown.Requested);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_requests_the_shutdown_before_it_releases_the_lease()
    {
        _ = await _h.HoldLease();
        var lease = new ScriptedLease(_h.Store);
        bool? requestedAtRelease = null;
        lease.BeforeRelease = () => requestedAtRelease = _h.Shutdown.Requested;

        _ = await new StopProcessService(_h.Held, lease, _h.Store, _h.Events, _h.Shutdown, Records(new InMemoryEndpointFile()), _runtime).Handle(new StopProcessRequest(StopReason.Stop), Token);

        Assert.True(requestedAtRelease);   // why: a renewal that runs after the release must see a stop, not a lost lease
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_removes_the_endpoint_record_before_it_clears_the_grant_and_releases_the_lease()
    {
        var grant = await _h.HoldLease();
        var record = new InMemoryEndpointFile();
        _ = await record.Write(new ControlEndpoint(new Uri("http://127.0.0.1:7470"), grant.Holder.Pid, grant.Epoch), Token);
        var endpoints = new ObservedEndpointFile(record);
        var lease = new ScriptedLease(_h.Store);
        bool? heldAtRemove = null;
        endpoints.BeforeRemove = () => heldAtRemove = _h.Held.Current is not null;
        ControlEndpoint? recordedAtShutdown = null;
        await using var atShutdown = _h.Shutdown.Stopping.Register(() => recordedAtShutdown = Recorded(record));
        ControlEndpoint? recordedAtRelease = null;
        lease.BeforeRelease = () => recordedAtRelease = Recorded(record);

        var stopped = await new StopProcessService(_h.Held, lease, _h.Store, _h.Events, _h.Shutdown, Records(endpoints), _runtime).Handle(new StopProcessRequest(StopReason.Stop), Token);

        Assert.True(stopped.IsSuccess);
        Assert.True((await _h.Store.Read(Token)).Value!.Released);   // why: the lease row exists once HoldLease took it
        Assert.True(heldAtRemove);        // why: ping answers epoch 0 once the grant is cleared; control.json must be gone by then
        Assert.Null(recordedAtShutdown);
        Assert.Null(recordedAtRelease);   // why: a client that finds control.json must never find the lease already free
    }

    /// <summary>The record the in-memory file holds now (it completes synchronously).</summary>
    private static ControlEndpoint? Recorded(InMemoryEndpointFile record) => record.Read(Token).AsTask().GetAwaiter().GetResult().Value;

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_removes_and_releases_whatever_the_caller_cancels_once_the_stop_is_recorded()
    {
        _ = await _h.HoldLease();
        var lease = new ScriptedLease(_h.Store);
        var record = new ObservedEndpointFile(new InMemoryEndpointFile());
        using var caller = CancellationTokenSource.CreateLinkedTokenSource(Token);   // the request's RequestAborted

        _ = await new StopProcessService(_h.Held, lease, _h.Store, _h.Events, _h.Shutdown, Records(record), _runtime).Handle(new StopProcessRequest(StopReason.Stop), caller.Token);

        Assert.False(record.RemovedWith?.CanBeCanceled ?? true);   // why: a cancelled removal would skip the clear, the shutdown and the release
        Assert.False(lease.ReleasedWith?.CanBeCanceled ?? true);   // why: a client that hangs up mid-stop must not leave the lease held
        Assert.False(_temporal.StoppedWith?.CanBeCanceled ?? true);   // why: a cancelled server stop would skip the release too
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_removes_this_starts_human_token_beside_the_endpoint_record()
    {
        _ = await _h.HoldLease();
        var tokens = new InMemoryHumanTokenStore();
        var held = new HeldHumanToken();
        held.Hold((await tokens.Mint(Token)).Value);

        var stopped = await new StopProcessService(_h.Held, _h.Store, _h.Store, _h.Events, _h.Shutdown, Records(new InMemoryEndpointFile(), held, tokens), _runtime)
            .Handle(new StopProcessRequest(StopReason.Stop), Token);

        Assert.True(stopped.IsSuccess);
        Assert.Null((await tokens.Read(Token)).Value);
        Assert.Null(held.Current);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_failed_token_removal_keeps_the_token_held_so_a_second_try_removes_it()
    {
        var tokens = new InMemoryHumanTokenStore();
        var held = new HeldHumanToken();
        held.Hold((await tokens.Mint(Token)).Value);
        var records = Records(new InMemoryEndpointFile(), held, new FailsOnce(tokens));

        var first = await records.RemoveToken(Token);

        Assert.True(first.IsError);
        Assert.NotNull(held.Current);   // not forgotten: the file is still there
        Assert.True((await records.RemoveToken(Token)).IsSuccess);
        Assert.Null(held.Current);
        Assert.Null((await tokens.Read(Token)).Value);
    }

    private sealed class FailsOnce(InMemoryHumanTokenStore inner) : IHumanTokenStore
    {
        private bool _failed;

        public ValueTask<Result<HumanToken>> Mint(CancellationToken cancellationToken) => inner.Mint(cancellationToken);

        public ValueTask<Result<HumanToken?>> Read(CancellationToken cancellationToken) => inner.Read(cancellationToken);

        public ValueTask<Result<Unit>> Remove(HumanToken mine, CancellationToken cancellationToken)
        {
            if (_failed)
            {
                return inner.Remove(mine, cancellationToken);
            }

            _failed = true;
            return ValueTask.FromResult<Result<Unit>>(SindriErrors.ViewsStalled.With("the token file can't be removed (fixture)"));
        }
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_leaves_a_successors_human_token_alone()
    {
        _ = await _h.HoldLease();
        var tokens = new InMemoryHumanTokenStore();
        var held = new HeldHumanToken();
        held.Hold((await tokens.Mint(Token)).Value);
        var successors = (await tokens.Mint(Token)).Value;   // a later start replaced the file

        _ = await new StopProcessService(_h.Held, _h.Store, _h.Store, _h.Events, _h.Shutdown, Records(new InMemoryEndpointFile(), held, tokens), _runtime)
            .Handle(new StopProcessRequest(StopReason.Stop), Token);

        Assert.Equal(successors, (await tokens.Read(Token)).Value);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_without_a_lease_still_shuts_down_and_writes_nothing()
    {
        var stopped = (await Service().Handle(new StopProcessRequest(StopReason.Signal), Token)).Value;

        Assert.Equal(new StopProcessResponse(0, 0, Sequence: null), stopped);
        Assert.Empty(_h.Store.Events);
        Assert.True(_h.Shutdown.Requested);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_stops_the_temporal_dev_server_before_it_releases_the_lease()
    {
        _ = await _h.HoldLease();
        _ = await _temporal.Ensure(Token);
        var lease = new ScriptedLease(_h.Store);
        bool? runningAtRelease = null;
        lease.BeforeRelease = () => runningAtRelease = _temporal.Running is not null;

        _ = await new StopProcessService(_h.Held, lease, _h.Store, _h.Events, _h.Shutdown, Records(new InMemoryEndpointFile()), _runtime).Handle(new StopProcessRequest(StopReason.Stop), Token);

        Assert.Null(_temporal.Running);
        Assert.False(runningAtRelease);   // why: the next serve takes the lease and must not adopt a server that is stopping
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_without_a_lease_leaves_the_server_alone()
    {
        _ = await _temporal.Ensure(Token);   // another serve's, as far as this process knows

        _ = await Service().Handle(new StopProcessRequest(StopReason.Signal), Token);

        Assert.Equal(0, _temporal.Stops);
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_waits_for_the_worker_to_stop_before_it_stops_the_dev_server()
    {
        _ = await _h.HoldLease();
        _ = await _temporal.Ensure(Token);
        var worker = new TaskCompletionSource();
        _runtime.Track(worker.Task);
        bool? serverAtWorkerStop = null;
        await using var stopping = _h.Shutdown.Stopping.Register(() => _ = Task.Run(async () =>
        {
            await Task.Delay(50, Token);   // the SDK worker drains its pollers before Run completes
            serverAtWorkerStop = _temporal.Running is not null;
            worker.SetResult();
        }, Token));
        var lease = new ScriptedLease(_h.Store);
        bool? workerStoppedAtRelease = null;
        lease.BeforeRelease = () => workerStoppedAtRelease = worker.Task.IsCompleted;

        _ = await new StopProcessService(_h.Held, lease, _h.Store, _h.Events, _h.Shutdown, Records(new InMemoryEndpointFile()), _runtime).Handle(new StopProcessRequest(StopReason.Stop), Token);

        Assert.True(serverAtWorkerStop);        // why: a worker whose server goes first fails its pollers instead of stopping
        Assert.True(workerStoppedAtRelease);
        Assert.Null(_temporal.Running);
    }
}
