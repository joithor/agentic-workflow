namespace Aw.Sindri.Core.Workflow.ApproveAll;

/// <summary>The result of approve-all, kept on the job (setup-once spec §7.2): the job carries on after a refusal with every recipe that does not wait on it.</summary>
/// <param name="Approved">The recipes approved.</param>
/// <param name="Refused">The recipes the checks refused.</param>
/// <param name="Skipped">The recipes left because one they wait on was refused or skipped.</param>
public sealed record ApproveAllResult(IReadOnlyList<ApproveAllApproved> Approved, IReadOnlyList<ApproveAllRefused> Refused, IReadOnlyList<ApproveAllSkipped> Skipped)
{
    /// <summary>Nothing approved, refused or skipped: the answer for an empty plan.</summary>
    public static ApproveAllResult Nothing { get; } = new([], [], []);
}
