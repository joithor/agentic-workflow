using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.NeedsYou;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Text;
using Aw.Sindri.Core.Views;

namespace Aw.Sindri.Features.GetNeedsYou;

/// <summary>
/// The setup rows the needs-you list adds to the views' (setup-once spec §7.3). 'setup:repo' stands while the profile names no repo and the last AddRepo job failed; it is cleared when a new AddRepo job runs
/// or succeeds. The sentence is the job's own, the fix in it.
/// </summary>
/// <param name="jobs">The jobs view.</param>
/// <param name="live">The live profile: a repo in it means the step is done.</param>
/// <param name="scrubber">Scrubs the text.</param>
internal sealed class SetupNotices(IJobViews jobs, IProfileSource live, ITextScrubber scrubber)
{
    private static readonly JobKind _addRepo = JobKind.From("AddRepo");

    internal async ValueTask<NeedsYouRow?> Repo(CancellationToken cancellationToken)
    {
        if (await LastFailed(cancellationToken).ConfigureAwait(false) is not { } failed || await HasRepo(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        var row = NeedsYouRows.ForSetupRepo(failed.Summary ?? "The last folder you picked could not be added. Pick the repo's top folder.", failed.EndedAt ?? failed.StartedAt, scrubber);
        return row.IsSuccess ? row.Value : null;
    }

    // The newest AddRepo job when it failed and none is running now; else null.
    private async ValueTask<JobRow?> LastFailed(CancellationToken cancellationToken)
    {
        var running = await jobs.Running(cancellationToken).ConfigureAwait(false);
        var latest = await jobs.List(state: null, _addRepo, cancellationToken).ConfigureAwait(false);
        if (running.IsError || latest.IsError || running.Value.Any(static j => j.Kind == _addRepo))
        {
            return null;
        }

        return latest.Value.Count > 0 && latest.Value[0].State == JobState.Failed ? latest.Value[0] : null;
    }

    private async ValueTask<bool> HasRepo(CancellationToken cancellationToken)
    {
        var profile = await live.Load(cancellationToken).ConfigureAwait(false);
        return profile.IsSuccess && profile.Value.Profile.Repos is { Count: > 0 };
    }
}
