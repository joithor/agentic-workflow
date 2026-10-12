using Aw.Sindri.Core.Control;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Features.StopProcess;
using Aw.Sindri.Testing;
using Aw.Sindri.Tests.Server;

namespace Aw.Sindri.Tests.StopProcess;

public sealed class StopProcessTests
{
    private readonly SliceHarness _h = new();

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    private StopProcessService Service() => new(_h.Held, _h.Store, _h.Store, _h.Events, _h.Shutdown, new InMemoryEndpointFile());

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

        _ = await new StopProcessService(_h.Held, lease, _h.Store, _h.Events, _h.Shutdown, new InMemoryEndpointFile()).Handle(new StopProcessRequest(StopReason.Stop), Token);

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

        var stopped = await new StopProcessService(_h.Held, lease, _h.Store, _h.Events, _h.Shutdown, endpoints).Handle(new StopProcessRequest(StopReason.Stop), Token);

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

        _ = await new StopProcessService(_h.Held, lease, _h.Store, _h.Events, _h.Shutdown, record).Handle(new StopProcessRequest(StopReason.Stop), caller.Token);

        Assert.False(record.RemovedWith?.CanBeCanceled ?? true);   // why: a cancelled removal would skip the clear, the shutdown and the release
        Assert.False(lease.ReleasedWith?.CanBeCanceled ?? true);   // why: a client that hangs up mid-stop must not leave the lease held
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_without_a_lease_still_shuts_down_and_writes_nothing()
    {
        var stopped = (await Service().Handle(new StopProcessRequest(StopReason.Signal), Token)).Value;

        Assert.Equal(new StopProcessResponse(0, 0, Sequence: null), stopped);
        Assert.Empty(_h.Store.Events);
        Assert.True(_h.Shutdown.Requested);
    }
}
