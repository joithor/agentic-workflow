namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>
/// The approval check's answer for the instance's recipe (spec §5.2, RCP-CAP-003), from a snapshot the registry
/// events invalidate. A mismatch on a human trigger is SND-RECIPE-005 (revoked) or SND-RECIPE-006 (drift) and records
/// nothing; on a system trigger it turns the decision into a spec-conflict park.
/// </summary>
public enum ApprovalCheck
{
    /// <summary>Approved, not revoked, and its pins match the build.</summary>
    Holds = 1,

    /// <summary>The hash was revoked.</summary>
    Revoked = 2,

    /// <summary>A pinned block's descriptor hash differs from this build's.</summary>
    Drifted = 3,
}
