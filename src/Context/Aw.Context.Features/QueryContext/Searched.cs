using Aw.Context.Core.Ranking;
using Aw.Context.Core.Results;

namespace Aw.Context.Features.QueryContext;

/// <summary>What the sources answered: each answering source's hits, and the ones that didn't answer.</summary>
/// <param name="Answers">Hits per answering source.</param>
/// <param name="Degraded">Sources that didn't answer.</param>
internal sealed record Searched(IReadOnlyList<SourceAnswer> Answers, IReadOnlyList<DegradedSource> Degraded);
