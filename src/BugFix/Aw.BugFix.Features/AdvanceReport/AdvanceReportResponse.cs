using Aw.BugFix.Core.Ids;
using Aw.BugFix.Core.Model;

namespace Aw.BugFix.Features.AdvanceReport;

/// <summary>The result of advance report.</summary>
/// <param name="Phase">The new phase.</param>
/// <param name="Status">The final status.</param>
/// <param name="Candidate">The resolving candidate.</param>
public sealed record AdvanceReportResponse(
    Phase Phase,
    BugFixStatus Status,
    CandidateId? Candidate);
