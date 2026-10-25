namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>Approve-all's own narrow read of GET /sindri/jobs/{id}: where the job is, its last line and, once it ended, its result. The general jobs client is the jobs unit's; the two are merged after both land.</summary>
/// <param name="Id">The job.</param>
/// <param name="State">'running', 'waiting', 'succeeded', 'failed' or 'cancelled'.</param>
/// <param name="Summary">Served words for the end; null before.</param>
/// <param name="Code">The refusal or failure code, when there is one.</param>
/// <param name="Progress">The job's lines so far (the workflow in hand).</param>
/// <param name="Result">What the approval did, once it ended.</param>
internal sealed record ApproveAllJob(string Id, string State, string? Summary, string? Code, IReadOnlyList<ApproveAllProgress>? Progress, ApproveAllResult? Result);
