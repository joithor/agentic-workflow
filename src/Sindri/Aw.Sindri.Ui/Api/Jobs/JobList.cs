namespace Aw.Sindri.Ui.Api.Jobs;

/// <summary>GET /sindri/jobs: the jobs, newest first, with the served empty sentence.</summary>
/// <param name="Jobs">The rows.</param>
/// <param name="Empty">The sentence for an empty list ("No running jobs."), or null.</param>
/// <param name="AsOf">How fresh the answer is.</param>
internal sealed record JobList(IReadOnlyList<JobRowView> Jobs, string? Empty, AsOfView AsOf);
