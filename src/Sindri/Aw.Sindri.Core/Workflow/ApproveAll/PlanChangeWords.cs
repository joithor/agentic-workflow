namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>The words a client shows for a <see cref="PlanChange"/> (setup-once spec §5.3.1): served, so no client invents its own.</summary>
public static class PlanChangeWords
{
    /// <summary>The change word.</summary>
    /// <param name="change">The change.</param>
    /// <returns>'New', 'Updated' or 'Needs a second look'.</returns>
    public static string For(PlanChange change) => change switch
    {
        PlanChange.New => "New",
        PlanChange.Updated => "Updated",
        PlanChange.SecondLook => "Needs a second look",
        _ => throw new ArgumentOutOfRangeException(nameof(change), change, "no such change"),
    };
}
