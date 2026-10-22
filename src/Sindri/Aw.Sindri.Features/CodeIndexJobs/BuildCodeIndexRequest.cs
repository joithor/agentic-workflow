using Aw.Sindri.Core.Commands;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// Builds or updates one repo's code index at a commit (code index spec §8): a job-class command the 'code-index-main-move' service starts as itself when the repo's trunk moved.
/// Keyed 'index:&lt;repo&gt;', so a second start for the same repo is SND-JOB-409. It has no route or verb of its own.
/// </summary>
/// <param name="Repo">The registered repo.</param>
/// <param name="Commit">The commit to index (40 lower-case hex digits); it must still be on the default branch when the build runs.</param>
public sealed record BuildCodeIndexRequest(string Repo, string Commit) : IJob<BuildCodeIndexResult>;
