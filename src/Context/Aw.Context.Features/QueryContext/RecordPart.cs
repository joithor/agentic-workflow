using Aw.Context.Core.Results;
using Aw.Context.Core.Sources;

namespace Aw.Context.Features.QueryContext;

/// <summary>What the sources gave an answer: the fetched records, their version checks and the sources that didn't answer.</summary>
/// <param name="Fetched">The records, best first.</param>
/// <param name="Versions">Each cited reference's version check: whether it ran, and the current version when it differs.</param>
/// <param name="Degraded">Sources that didn't answer.</param>
internal sealed record RecordPart(IReadOnlyList<FetchedRecord> Fetched, IReadOnlyDictionary<SourceRef, (bool Ran, SourceVersion? Current)> Versions,
    IReadOnlyList<DegradedSource> Degraded);
