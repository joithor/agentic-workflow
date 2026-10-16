using Aw.Sindri.Core.Sessions;

namespace Aw.Sindri.Features.Sessions;

/// <summary>One pass's discovery.</summary>
/// <param name="Candidates">The live sessions found.</param>
/// <param name="Problems">One row per source that failed.</param>
/// <param name="Good">False when the process table itself failed: the pass keeps the previous snapshot and ledgers no 'gone'.</param>
internal sealed record Discovered(IReadOnlyList<Candidate> Candidates, IReadOnlyList<SessionProblem> Problems, bool Good);
