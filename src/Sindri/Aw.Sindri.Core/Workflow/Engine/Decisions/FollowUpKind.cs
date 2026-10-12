namespace Aw.Sindri.Core.Workflow.Engine.Decisions;

/// <summary>Work the interpreter does once a decision is in history (spec §2, §5.3); a replay never repeats it.</summary>
public enum FollowUpKind
{
    /// <summary>Start the run the decision opened, as the RunStep activity (its id carries the run token).</summary>
    StartRun = 1,

    /// <summary>Decide the withheld transition as a system trigger: the answer recorded, its gates and evidence evaluate afresh under the answer's key.</summary>
    Reevaluate = 2,

    /// <summary>Cancel a child (a parent's cancel cascades).</summary>
    CancelChild = 3,
}
