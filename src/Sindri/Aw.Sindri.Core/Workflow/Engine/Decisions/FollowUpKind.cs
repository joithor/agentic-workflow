namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>Work the post-commit loop does after a Commit (spec §2, §5.3); each is keyed so a re-run after a crash is a no-op.</summary>
public enum FollowUpKind
{
    /// <summary>Call IStep.Start for the run the Commit opened (keyed by the run token).</summary>
    StartRun = 1,

    /// <summary>Decide the withheld transition as a system trigger: the answer recorded, its gates and evidence evaluate afresh under the answer's key.</summary>
    Reevaluate = 2,

    /// <summary>Cancel a child (a parent's cancel cascades).</summary>
    CancelChild = 3,
}
