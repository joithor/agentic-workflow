using Aw.Context.Core.Lenses;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.ContextCalls;

/// <summary>
/// Adds the running sync job to the 'not-synced' rows of an answer (the engine reads no jobs; ARCH-CQRS-001). The job comes from the 'jobs' view, the same views snapshot the
/// rest of the answer is held to (X-Sindri-After, SND-API-011), never from the runner or the table live. A gap with no job running carries no 'sync': the 'context-sync-gaps' service starts one.
/// </summary>
/// <param name="jobs">The 'jobs' view.</param>
internal sealed class SyncGaps(IJobViews jobs)
{
    internal const string NotSynced = "not-synced";

    internal static JobKind SyncKind { get; } = JobKind.From("SyncSources");

    /// <summary>The searched object with its not-synced rows marked by the running sync, or unchanged when there is no gap or no job.</summary>
    /// <param name="searched">What the answer searched.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The searched object.</returns>
    internal async ValueTask<LensSearched> Mark(LensSearched searched, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(searched);
        if (!searched.Sources.Any(static s => s.Reason == NotSynced))
        {
            return searched;
        }

        var running = await jobs.List(JobState.Running, SyncKind, cancellationToken).ConfigureAwait(false);
        if (running.IsError || running.Value.Count == 0)
        {
            return searched;
        }

        var sync = new SearchedSync("running", SyncJobId.From(running.Value[0].Id.Value));
        return searched with { Sources = [.. searched.Sources.Select(s => s.Reason == NotSynced ? s with { Sync = sync } : s)] };
    }
}
