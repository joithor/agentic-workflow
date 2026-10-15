using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>The challenge and the preview the human reads before typing the action (engine spec §4.2). Exactly one preview is set.</summary>
/// <param name="Challenge">The challenge's id.</param>
/// <param name="Action">The action text to type back.</param>
/// <param name="ExpiresAt">When it stops being valid.</param>
/// <param name="Approve">An approval's preview, or null.</param>
/// <param name="Revoke">A revoke's preview, or null.</param>
public sealed record IssueChallengeResponse(ChallengeId Challenge, string Action, DateTimeOffset ExpiresAt, ApprovalPreview? Approve, RevokePreview? Revoke);
