using Aw.Sindri.Core.Items;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Features.StreamEvents;
using Aw.Sindri.Testing;

namespace Aw.Sindri.Tests.StreamEvents;

/// <summary>The stream service over a slice harness, with no scrubbing and the item repos read from the harness's ledger.</summary>
internal static class StreamEventsServices
{
    public static StreamEventsService Over(SliceHarness h) => Over(h, new PassThroughScrubber());

    public static StreamEventsService Over(SliceHarness h, ITextScrubber scrubber) =>
        new(h.Store, scrubber, h.Callers, new LedgerItemRepos(h.Store, h.Events), SliceHarness.Json);
}
