namespace Aw.Context.Core.Lenses;

/// <summary>The sync job that is filling a gap in a 'searched' row (Sindri adds it; the engine never does): the job and where it is.</summary>
/// <param name="State">The job's state word ('running').</param>
/// <param name="JobId">The id GET /sindri/jobs/{jobId} serves.</param>
public sealed record SearchedSync(string State, SyncJobId JobId);
