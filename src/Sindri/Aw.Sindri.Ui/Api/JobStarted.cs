namespace Aw.Sindri.Ui.Api;

/// <summary>The 202 a job's start answers: the command that was accepted, and the job to follow.</summary>
/// <param name="Accepted">The command's id and ledger sequence.</param>
/// <param name="JobId">The job.</param>
internal sealed record JobStarted(CommandAcceptedView Accepted, string JobId);
