using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.StreamEvents;

/// <summary>The stream service over a slice harness, with an empty items view and no scrubbing.</summary>
internal static class StreamEventsServices
{
    public static StreamEventsService Over(SliceHarness h) => Over(h, new InMemoryViewStore(SliceHarness.Json));

    public static StreamEventsService Over(SliceHarness h, InMemoryViewStore views) =>
        new(h.Store, h.Events, new PassThroughScrubber(), views, h.Callers, SliceHarness.Json);
}
