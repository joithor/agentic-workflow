using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Features.PublishPlan;

/// <summary>Plans publishing a run's evidence: the ask-first question, the Linear decision and the PR comment.</summary>
/// <param name="RunDirectory">The run directory holding summary.json (absolute).</param>
/// <param name="Provenance">The database provenance the caller determined (a non-local host is always unknown).</param>
/// <param name="LinearIssue">The Linear issue to upload to, when there is one.</param>
public sealed record PublishPlanRequest(string RunDirectory, DbProvenance Provenance, string? LinearIssue = null);
