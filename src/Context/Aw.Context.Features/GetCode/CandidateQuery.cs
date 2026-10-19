using Aw.Context.Core.Sources;

namespace Aw.Context.Features.GetCode;

/// <summary>One candidate search: the terms, the paths to stay within, the most files to take and the source whose row a failure belongs to.</summary>
/// <param name="Terms">The terms.</param>
/// <param name="Within">The paths searched.</param>
/// <param name="Max">The most files to take.</param>
/// <param name="Source">The source the search serves.</param>
internal sealed record CandidateQuery(IReadOnlyList<string> Terms, IReadOnlyList<string> Within, int Max, SourceId Source);
