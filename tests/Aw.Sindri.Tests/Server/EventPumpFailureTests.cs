using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Host.Endpoints;
using Microsoft.AspNetCore.Http;

namespace Aw.Sindri.Tests.Server;

/// <summary>What the event stream does when its loop fails in a way nobody planned for (API spec §5.5).</summary>
public sealed class EventPumpFailureTests : IDisposable
{
    private readonly SliceHarness _h = new();
    private readonly ListLogger<EventPump> _log = new();
    private readonly MemoryStream _body = new();

    public void Dispose() => _body.Dispose();

    [Fact, Trait("Lane", "unit")]
    public async Task An_unexpected_exception_is_logged_told_as_an_internal_error_frame_and_frees_the_slot()
    {
        var slot = new CountingSlot();
        var context = new DefaultHttpContext { Response = { Body = _body } };
        var pump = new EventPump(_h.Store, new RecordingSignal(_h.Clock), _h.Clock, SliceHarness.Json, _log);

        await new SseResult(pump, new ThrowingRead(), new StreamEventsRequest(0), slot, CancellationToken.None).ExecuteAsync(context);

        var frames = System.Text.Encoding.UTF8.GetString(_body.ToArray());
        Assert.Equal("event: error\ndata: {\"code\":\"SND-API-005\",\"detail\":\"Internal error in the control plane\"}\n\n", frames);   // no exception text leaks to the client
        Assert.Equal(1, slot.Disposed);
        _ = Assert.Single(_log.Lines);
    }

    private sealed class CountingSlot : IDisposable
    {
        private int _disposed;

        public int Disposed => Volatile.Read(ref _disposed);

        public void Dispose() => _ = Interlocked.Increment(ref _disposed);
    }

    private sealed class ThrowingRead : IService<StreamEventsRequest, StreamEventsResponse>
    {
        public ValueTask<Result<StreamEventsResponse>> Handle(StreamEventsRequest request, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("boom");
    }
}
