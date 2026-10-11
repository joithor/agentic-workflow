using Aw.Evidence.Core.Guard;

namespace Aw.Evidence.Core.Publishing;

/// <summary>How a run's evidence would be published; the agent asks <see cref="Question"/> before any write.</summary>
/// <param name="Provenance">The effective provenance (a non-local host is never seeded).</param>
/// <param name="Question">The ask-first question.</param>
/// <param name="LinearUploadAllowed">Whether screenshots may go to the Linear issue.</param>
/// <param name="Files">The screenshots to upload when allowed (absolute paths).</param>
/// <param name="Note">Why the evidence stays local, when it does.</param>
/// <param name="PrComment">The PR comment body.</param>
public sealed record RunPublishPlan(DbProvenance Provenance, string Question, bool LinearUploadAllowed, IReadOnlyList<string> Files, string? Note, string PrComment);
