using Aw.Context.Core.ConceptScan;
using Aw.Context.Core.Map;
using Aw.Context.Core.Models;
using Aw.Context.Core.Trust;
using Aw.Kernel;
using Aw.Kernel.Services;

namespace Aw.Context.Features.ScanConcepts;

/// <summary>The concept scan (owner, q110): two model passes with the map search between them, hints stored beside the scanned version.</summary>
internal sealed class ScanConceptsService(IGraphStore store, IScrubberFactory scrubbers, IModelPort model, IConceptScanSearch search, IConceptHintStore hintStore, TimeProvider clock)
    : IService<ScanConceptsRequest, ConceptScanReport>
{
    public ValueTask<Result<ConceptScanReport>> Handle(ScanConceptsRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException(string.Join(',', store, scrubbers, model, search, hintStore, clock));
}
