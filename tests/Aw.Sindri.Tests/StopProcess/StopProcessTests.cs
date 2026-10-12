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

        Assert.Equal(new StopProcessResponse(grant.Holder.Pid, grant.Epoch), stopped);
        Assert.Equal(EventTypes.ProcessStopped, Assert.Single(_h.Store.Events).Envelope.Type);
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
    public async Task StopProcess_removes_the_endpoint_record_before_it_releases_the_lease()
    {
        var grant = await _h.HoldLease();
        var endpoints = new InMemoryEndpointFile();
        _ = await endpoints.Write(new ControlEndpoint(new Uri("http://127.0.0.1:7470"), grant.Holder.Pid, grant.Epoch), Token);
        var lease = new ScriptedLease(_h.Store);
        ControlEndpoint? recordedAtRelease = null;
        lease.BeforeRelease = () => recordedAtRelease = endpoints.Read(Token).AsTask().GetAwaiter().GetResult().Value;   // the fake completes synchronously

        var stopped = await new StopProcessService(_h.Held, lease, _h.Store, _h.Events, _h.Shutdown, endpoints).Handle(new StopProcessRequest(StopReason.Stop), Token);

        Assert.True(stopped.IsSuccess);
        Assert.True((await _h.Store.Read(Token)).Value!.Released);   // why: the lease row exists once HoldLease took it
        Assert.Null(recordedAtRelease);   // why: a client that finds control.json must never find the lease already free
    }

    [Fact, Trait("Lane", "unit")]
    public async Task StopProcess_without_a_lease_still_shuts_down_and_writes_nothing()
    {
        var stopped = (await Service().Handle(new StopProcessRequest(StopReason.Signal), Token)).Value;

        Assert.Equal(new StopProcessResponse(0, 0), stopped);
        Assert.Empty(_h.Store.Events);
        Assert.True(_h.Shutdown.Requested);
    }
}
