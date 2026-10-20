namespace Aw.Sindri.Ui.Api.Workflow;

/// <summary>The 202 the approval answers (JobStarted): the approval runs as a job.</summary>
/// <param name="Accepted">The command's id and sequence.</param>
/// <param name="JobId">The job to follow.</param>
internal sealed record ApproveAllStarted(ApproveAllAccepted Accepted, string JobId);
