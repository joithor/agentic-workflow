namespace Aw.Sindri.Core.Jobs;

/// <summary>Where a started job can be followed (API P6): paths under the control plane's origin.</summary>
/// <param name="Job">'/sindri/jobs/JOB-…': the job's view.</param>
/// <param name="Cancel">'/sindri/jobs/JOB-…/cancel': POST to cancel.</param>
/// <param name="Events">'/sindri/events?after=&lt;sequence&gt;&amp;types=job.*': the stream from the start (filter on payload.job).</param>
public sealed record JobLinks(string Job, string Cancel, string Events);
