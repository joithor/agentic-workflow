using Aw.Sindri.Core.Challenges;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Registry;

namespace Aw.Sindri.Features.IssueChallenge;

/// <summary>The challenge and the preview the human reads before typing the action (engine spec §4.2). At most one preview is set (none for the profile).</summary>
/// <param name="Challenge">The challenge's id.</param>
/// <param name="Action">The action text to type back.</param>
/// <param name="ExpiresAt">When it stops being valid.</param>
/// <param name="Approve">An approval's preview, or null.</param>
/// <param name="Revoke">A revoke's preview, or null.</param>
public sealed record IssueChallengeResponse(ChallengeId Challenge, string Action, DateTimeOffset ExpiresAt, ApprovalPreview? Approve, RevokePreview? Revoke)
{
    /// <summary>A protected answer's preview: the instance as it was when the challenge was issued (its question, if parked), or null.</summary>
    public InstanceView? Instance { get; init; }
}
