using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Ports;
using Aw.CodeIndex.Core.Values;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Sindri.Core;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using Microsoft.Extensions.Logging;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// The 'code-index-main-move' background service (code index spec §6.1): when a registered repo's checkout moved along its default branch past the commit its index describes, it starts a 'BuildCodeIndex'
/// job of key 'index:&lt;repo&gt;' as itself, through the command chain. Each repo is decided on its own by <see cref="MainMoveDecision"/> (a running build first, then nothing when the index is at the head, then the
/// backoff after failures in a row); a build that ended deferred (heavy-busy) holds the next start for <see cref="IndexOutlookRules.DeferredSuppress"/>. A repo on a feature branch starts nothing and is named in the summary.
/// Three failed builds in a row for a repo that still needs one fail the run (needs-you 'failure:service:code-index-main-move'). A tick reads the heads, the stored index heads and the jobs once.
/// </summary>
/// <param name="scopes">The profile's scope and the caller.</param>
/// <param name="index">The code index: its stored heads.</param>
/// <param name="heads">The repos' live heads.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="start">The BuildCodeIndex command chain.</param>
/// <param name="clock">Dates the backoff.</param>
/// <param name="logger">Logs a 409 that is not "the build is already running".</param>
internal sealed partial class CodeIndexMainMoveService(CodeIndexJobScopes scopes, ICodeIndex index, IRepoHeads heads, IJobViews jobs, IService<BuildCodeIndexRequest, JobStartedResponse> start, TimeProvider clock, ILogger<CodeIndexMainMoveService> logger) : IScheduledService
{
    private const int RecentRuns = 8;

    internal static ServiceName ServiceId { get; } = ServiceName.From("code-index-main-move");

    /// <summary>Runs without a profile entry, every minute: a tick is one heads read, one stored-heads read and one jobs read.</summary>
    internal static BuiltInService BuiltIn { get; } = new(ServiceId, new Cadence(TimeSpan.FromMinutes(1)));

    public ServiceName Identity() => ServiceId;

    public async ValueTask<Result<ScheduledResult>> Run(ScheduledRun run, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(run);
        var scope = await scopes.Current(cancellationToken).ConfigureAwait(false);
        if (scope.IsError || scope.Value.Repos.Count == 0)
        {
            // no approved profile, or no repo, is nothing to index, not a failed run (a failed run would land in needs-you every tick)
            return scope.IsError && scope.Error.Code != SindriErrors.ProfileNotApproved.Code ? scope.Error : ScheduledResult.Quiet;
        }

        var read = await Read(scope.Value, cancellationToken).ConfigureAwait(false);
        if (read.IsError)
        {
            return read.Error;
        }

        var (live, recorded, builds) = read.Value;
        var tally = new Tally();
        foreach (var head in live)
        {
            var step = await Step(head, recorded, builds, cancellationToken).ConfigureAwait(false);
            if (step.IsError)
            {
                return step.Error;
            }

            tally.Add(head.Repo.Value, step.Value);
        }

        return tally.Failing.Count > 0
            ? SindriErrors.ServiceFailed.With($"{ServiceId.Value}: the last builds of {string.Join(", ", tally.Failing)} failed {IndexOutlookRules.FailuresToBroken} times in a row; the next try backs off")
            : new ScheduledResult(tally.Started.Count == 0, tally.Summary());
    }

    private async ValueTask<Result<Reads>> Read(CodeIndexScope scope, CancellationToken cancellationToken)
    {
        var live = await heads.Heads(scope.Repos, cancellationToken).ConfigureAwait(false);
        var recorded = await index.Heads(scope, cancellationToken).ConfigureAwait(false);
        var builds = await jobs.List(state: null, IndexJobs.BuildKind, cancellationToken).ConfigureAwait(false);
        if (live.IsError || recorded.IsError || builds.IsError)
        {
            return live.IsError ? live.Error : recorded.IsError ? recorded.Error : builds.Error;
        }

        return new Reads(live.Value, recorded.Value, builds.Value);
    }

    private async ValueTask<Result<RepoStep>> Step(IndexRepoHead head, IReadOnlyList<RecordedHead> recorded, IReadOnlyList<JobRow> builds, CancellationToken cancellationToken)
    {
        if (head.Head is not { } commit || !head.OnDefaultBranch)
        {
            return new RepoStep(Elsewhere: true);
        }

        var repo = head.Repo.Value;
        var (action, failing) = Decide(repo, commit, recorded.FirstOrDefault(h => h.Repo == head.Repo)?.Commit, [.. builds.Where(j => IndexJobs.IsOf(j, repo))]);
        if (action != MainMoveAction.Start)
        {
            return new RepoStep(Failing: failing);
        }

        var response = await start.Handle(new BuildCodeIndexRequest(repo, commit.Value), cancellationToken).ConfigureAwait(false);
        if (response.IsError)
        {
            return response.Error.Code != SindriErrors.JobConflict.Code ? response.Error : Conflict(response.Error);
        }

        return new RepoStep(Started: true, Failing: failing);
    }

    // The repo's own decision: the stored index against the head, the key's running and ended builds, and the deferred hold on top.
    private (MainMoveAction Action, bool Failing) Decide(string repo, CommitSha commit, CommitSha? stored, List<JobRow> mine)
    {
        var finished = mine.Where(static j => j.State != JobState.Running && j.EndedAt is not null).OrderByDescending(static j => j.EndedAt).Take(RecentRuns).ToList();
        var now = clock.GetUtcNow();
        var onDefault = new Dictionary<string, string>(StringComparer.Ordinal) { [repo] = commit.Value };
        IReadOnlyDictionary<string, string>? last = stored is { } sha ? new Dictionary<string, string>(StringComparer.Ordinal) { [repo] = sha.Value } : null;
        var attempts = finished.Select(static j => new SyncAttempt(j.State == JobState.Failed, j.EndedAt ?? default)).ToList();
        var action = MainMoveDecision.Decide(onDefault, last, mine.Exists(static j => j.State == JobState.Running), attempts, now);
        if (action == MainMoveAction.Start && finished is [var newest, ..] && IndexJobs.Deferred(newest) && now < newest.EndedAt + IndexOutlookRules.DeferredSuppress)
        {
            action = MainMoveAction.BackoffNotDue;
        }

        return (action, action != MainMoveAction.Nothing && MainMoveDecision.FailedInARow(attempts) >= IndexOutlookRules.FailuresToBroken);
    }

    // 409 with 'runningJob' is the same build running: nothing to say. Any other 409 is logged, and not retried this tick.
    private RepoStep Conflict(Error error)
    {
        if (error.Extensions is null || !error.Extensions.Any(static e => e.Key == "runningJob"))
        {
            LogConflict(logger, error.Code, error.Message);
        }

        return default;
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "code-index-main-move: the build was refused with {Code} and is not retried this tick: {Detail}")]
    private static partial void LogConflict(ILogger logger, string code, string? detail);

    private sealed record Reads(IReadOnlyList<IndexRepoHead> Live, IReadOnlyList<RecordedHead> Recorded, IReadOnlyList<JobRow> Builds);

    private readonly record struct RepoStep(bool Started = false, bool Elsewhere = false, bool Failing = false);

    private sealed class Tally
    {
        internal List<string> Started { get; } = [];

        internal List<string> Elsewhere { get; } = [];

        internal List<string> Failing { get; } = [];

        internal void Add(string repo, RepoStep step)
        {
            if (step.Started)
            {
                Started.Add(repo);
            }

            if (step.Elsewhere)
            {
                Elsewhere.Add(repo);
            }

            if (step.Failing)
            {
                Failing.Add(repo);
            }
        }

        internal string? Summary()
        {
            var notes = new List<string>();
            if (Started.Count > 0)
            {
                notes.Add("ok: index build started for " + string.Join(", ", Started));
            }

            if (Elsewhere.Count > 0)
            {
                notes.Add("not on trunk, left out: " + string.Join(", ", Elsewhere));
            }

            return notes.Count == 0 ? null : string.Join("; ", notes);
        }
    }
}
