using System.Globalization;
using Aw.Kernel;
using Aw.Kernel.Services;
using Aw.Meta.Core.Coordination;
using Aw.Meta.Core.Logs;
using Aw.Meta.Core.Obligations;
using Aw.Meta.Core.Ports;

namespace Aw.Meta.Features.MergeRun;

/// <summary>
/// Merges a pull request once every check holds: "gh pr merge --merge", a fetch, the merge recorded with main's new sha
/// (which opens the announce-main obligation), a running-log line, and the text to send every lane.
/// </summary>
/// <param name="readiness">Checks the pull request.</param>
/// <param name="repo">The git and gh calls.</param>
/// <param name="gates">Stores the merge.</param>
/// <param name="log">The running log.</param>
/// <param name="clock">Dates the merge.</param>
internal sealed class MergeRunService(MergeReadiness readiness, RepoCommands repo, IGateStore gates, IRunningLog log, TimeProvider clock)
    : IService<MergeRunRequest, MergeRunResponse>
{
    public async ValueTask<Result<MergeRunResponse>> Handle(MergeRunRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var assessed = await readiness.Assess(request.Pr, cancellationToken).ConfigureAwait(false);
        if (assessed.IsError || !assessed.Value.Ready || assessed.Value.Pr is not { } pull)
        {
            return assessed.IsError ? assessed.Error : new MergeRunResponse(request.Pr, Merged: false, assessed.Value.Checks, Sha: null, Announce: null);
        }

        var number = request.Pr.ToString(CultureInfo.InvariantCulture);
        var repoDirectory = assessed.Value.Team.Repo;
        var merged = await repo.Gh(repoDirectory, "pr", "merge", number, "--merge").ConfigureAwait(false);
        var fetched = merged.IsError ? Results.Failure<Unit>(merged.Error) : await repo.Fetch(repoDirectory, pr: null).ConfigureAwait(false);
        var sha = fetched.IsError ? Results.Failure<string>(fetched.Error) : await repo.Sha(repoDirectory, "origin/main").ConfigureAwait(false);
        if (sha.IsError)
        {
            return sha.Error;
        }

        var now = clock.GetUtcNow();
        var record = new MergeRecord(request.Pr, sha.Value, now);
        var recorded = await gates.RecordMerge(record, cancellationToken).ConfigureAwait(false);
        var logged = recorded.IsError ? Results.Failure<Unit>(recorded.Error) : await log.Append(LogLine.Event(LogKind.Merge, now, $"#{number} {pull.Title} merged; main is now {record.ShortSha}"), cancellationToken).ConfigureAwait(false);
        return logged.IsError
            ? logged.Error
            : new MergeRunResponse(request.Pr, Merged: true, assessed.Value.Checks, sha.Value, $"main moved to {record.ShortSha}: #{number} {pull.Title} merged. Merge main into your branches.");
    }
}
