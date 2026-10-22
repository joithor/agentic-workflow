using System.Globalization;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.CodeIndex.Core.Ports;
using Aw.Sindri.Core.Doctor;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Profile;
using Aw.Sindri.Core.Scheduling;
using Aw.Sindri.Core.Views;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;
using IndexRepoStatus = Aw.CodeIndex.Core.Facade.RepoStatus;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>
/// The code index's doctor rows (code index spec §8): 'code-index:&lt;repo&gt;' per repo (ok, warn when stale, first build queued, waiting or retrying, fail when damaged) and 'code-index:graphify',
/// 'code-index:ollama' and 'code-index:lock', read from the layer statuses the last builds stored, so doctor runs no tool. A layer that is unavailable is a warning, never a failure.
/// </summary>
/// <param name="index">The code index: its status, from the files only.</param>
/// <param name="heads">The repos' live heads.</param>
/// <param name="jobs">The 'jobs' view.</param>
/// <param name="clock">Dates the retry words.</param>
internal sealed class CodeIndexDoctor(ICodeIndex index, IRepoHeads heads, IJobViews jobs, TimeProvider clock) : ICodeIndexChecks
{
    private const string GraphCheck = "code-index:graphify";
    private const string OllamaCheck = "code-index:ollama";
    private const string LockCheck = "code-index:lock";
    private const string Setup = "./setup.sh";

    /// <summary>The checks, in the order doctor prints them.</summary>
    /// <param name="approved">The approved profile, or null.</param>
    /// <param name="cancellationToken">Cancels the reads.</param>
    /// <returns>The repo rows, then graphify, ollama and the lock.</returns>
    public async ValueTask<DoctorCheck[]> Check(LoadedProfile? approved, CancellationToken cancellationToken)
    {
        if (approved is null)
        {
            return [new DoctorCheck("code-index", CheckState.Ok, "not configured: no approved profile", Fix: null)];
        }

        var scope = CodeIndexScopes.For(approved.Profile, caller: null);
        if (scope.IsError)
        {
            return [new DoctorCheck("code-index", CheckState.Fail, $"{scope.Error.Code}: {scope.Error.Message}", "edit the profile, then sindri profile validate")];
        }

        var status = await index.Status(scope.Value, cancellationToken).ConfigureAwait(false);
        var live = await heads.Heads(scope.Value.Repos, cancellationToken).ConfigureAwait(false);
        var builds = await jobs.List(state: null, IndexJobs.BuildKind, cancellationToken).ConfigureAwait(false);
        if (status.IsError)
        {
            return [new DoctorCheck("code-index", CheckState.Fail, $"{status.Error.Code}: {status.Error.Message}", "check $AW_STATE_DIR/code-index")];
        }

        return Rows(status.Value.Repos, live.IsError ? [] : live.Value, builds.IsError ? [] : builds.Value, clock.GetUtcNow());
    }

    /// <summary>The rows for what was read; pure, so the words are tested without a box.</summary>
    /// <param name="repos">Each repo's index status.</param>
    /// <param name="live">The repos' live heads (empty when unreadable).</param>
    /// <param name="builds">The build jobs of every repo.</param>
    /// <param name="now">The time.</param>
    /// <returns>The rows.</returns>
    internal static DoctorCheck[] Rows(IReadOnlyList<IndexRepoStatus> repos, IReadOnlyList<IndexRepoHead> live, IReadOnlyList<JobRow> builds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(repos);
        ArgumentNullException.ThrowIfNull(live);
        ArgumentNullException.ThrowIfNull(builds);
        if (repos.Count == 0)
        {
            return [new DoctorCheck("code-index", CheckState.Ok, "no repos in the profile", Fix: null)];
        }

        var layers = repos.SelectMany(static r => r.Layers).ToList();
        return
        [
            .. repos.Select(r => Repo(r, live.FirstOrDefault(h => h.Repo == r.Repo), [.. builds.Where(j => IndexJobs.IsOf(j, r.Repo.Value))], now)),
            Layer(GraphCheck, "graph", layers, LayerName.Graph),
            Layer(OllamaCheck, "embeddings", layers, LayerName.Embeddings),
            Lock(layers),
        ];
    }

    private static DoctorCheck Repo(IndexRepoStatus repo, IndexRepoHead? live, List<JobRow> mine, DateTimeOffset now)
    {
        var name = "code-index:" + repo.Repo.Value;
        var running = mine.Exists(static j => j.State == JobState.Running);
        var finished = mine.Where(static j => j.State != JobState.Running && j.EndedAt is not null).OrderByDescending(static j => j.EndedAt).ToList();
        var buildWords = running ? "building" : Waiting(finished, now) ?? "build queued";
        return repo.State switch
        {
            IndexState.Damaged => new DoctorCheck(name, CheckState.Fail, "index damaged, rebuilding (SND-CODEIDX-010)", "none needed: the next tick deletes and rebuilds it; sindri jobs shows the build"),
            IndexState.Missing => new DoctorCheck(name, CheckState.Warn, running ? "building the first index" : Waiting(finished, now) ?? "first build queued", Fix: null),
            IndexState.Ok => Readable(name, repo, live, buildWords, now),
            _ => Readable(name, repo, live, buildWords, now),
        };
    }

    private static DoctorCheck Readable(string name, IndexRepoStatus repo, IndexRepoHead? live, string buildWords, DateTimeOffset now)
    {
        var commit = repo.Commit?.Value ?? string.Empty;
        var short7 = commit.Length >= 7 ? commit[..7] : commit;
        if (live?.OnDefaultBranch == false)
        {
            return new DoctorCheck(name, CheckState.Warn, $"checkout is not on main; the index at {short7} is served", "switch the repo's checkout to its default branch");
        }

        return live?.Head is { } head && head != repo.Commit
            ? new DoctorCheck(name, CheckState.Warn, $"stale: index at {short7}, head at {head.Value[..7]}; {buildWords}", "none needed: the next tick builds it")
            : new DoctorCheck(name, CheckState.Ok, $"{short7}, {Age(repo.BuiltAt, now)}, {string.Join(" · ", repo.Layers.Select(static l => $"{Word(l.Layer)} {Word(l.State)}"))}", Fix: null);
    }

    // "retrying in N min" after failures in a row, "waiting for a free build slot" after a deferred build; null when neither holds.
    private static string? Waiting(List<JobRow> finished, DateTimeOffset now)
    {
        if (finished is not [var newest, ..] || newest.EndedAt is not { } ended)
        {
            return null;
        }

        if (IndexJobs.Deferred(newest))
        {
            return "waiting for a free build slot";
        }

        var failures = finished.TakeWhile(static j => j.State == JobState.Failed).Count();
        var due = ended + SyncGapDecision.Wait(failures);
        return failures > 0 && now < due ? string.Create(CultureInfo.InvariantCulture, $"retrying in {Math.Ceiling((due - now).TotalMinutes)} min") : null;
    }

    private static DoctorCheck Layer(string name, string word, List<LayerStatus> layers, LayerName which)
    {
        var mine = layers.Where(l => l.Layer == which).ToList();
        if (mine.Find(static l => l.State == LayerState.Unavailable) is { } down)
        {
            return new DoctorCheck(name, CheckState.Warn, $"{word} unavailable ({down.Code}): {down.Detail}", Fix(down.Code));
        }

        return new DoctorCheck(name, CheckState.Ok, mine.Count == 0 ? "no index built yet" : $"{word} {Word(mine[0].State)}", Fix: null);
    }

    private static DoctorCheck Lock(List<LayerStatus> layers) => layers.Find(static l => l.Code == "SND-CODEIDX-009") is { } mismatch
        ? new DoctorCheck(LockCheck, CheckState.Warn, $"a tool or model does not match context/code-index.lock ({mismatch.Layer.ToString().ToLowerInvariant()})", Setup + " re-installs the pin")
        : new DoctorCheck(LockCheck, CheckState.Ok, "every built layer matches context/code-index.lock", Fix: null);

    private static string? Fix(string? code) => code switch
    {
        "SND-CODEIDX-007" => "install bubblewrap (" + Setup + "), or set index.graph.enabled: false",
        "SND-CODEIDX-006" => "start Ollama (the pin is in context/code-index.lock)",
        _ => Setup + " installs the pin",
    };

    private static string Age(DateTimeOffset? builtAt, DateTimeOffset now)
    {
        if (builtAt is not { } at)
        {
            return "age unknown";
        }

        var age = now - at;
        return age.TotalMinutes < 1 ? "just built"
            : age.TotalHours < 1 ? $"{((int)age.TotalMinutes).ToString(CultureInfo.InvariantCulture)} min old"
            : age.TotalDays < 1 ? $"{((int)age.TotalHours).ToString(CultureInfo.InvariantCulture)} h old"
            : $"{((int)age.TotalDays).ToString(CultureInfo.InvariantCulture)} d old";
    }

    private static string Word(LayerName layer) => layer.ToString().ToLowerInvariant();

    private static string Word(LayerState state) => state.ToString().ToLowerInvariant();
}
