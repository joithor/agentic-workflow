using System.Globalization;
using Aw.Sindri.Core.Events;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Host;
using Aw.Sindri.Tests.Jobs;
using static Aw.Sindri.Tests.Callers.CallerRequests;

namespace Aw.Sindri.Tests.ApiGoldens;

/// <summary>
/// API P6's session-scope goldens (review B1): the events and the ledger read of a repo 'demo' job's whole life (start, a line, a cancelled and a finished
/// job) as a session in 'other' (sees none of it) and as a session in 'demo' (sees all of it). Every event carries the repo the job started with.
/// </summary>
internal static class JobScopeScenarios
{
    /// <summary>The (slug, scenario) pairs of this class.</summary>
    public static IReadOnlyList<(string Slug, string Scenario)> All { get; } =
    [
        ("events", "session-other-repo-job"), ("events", "session-own-repo-job"), ("ledger", "session-other-repo-job"), ("ledger", "session-own-repo-job"),
    ];

    /// <summary>Whether <paramref name="slug"/> and <paramref name="scenario"/> belong to this class.</summary>
    public static bool Handles(string slug, string scenario) => All.Contains((slug, scenario));

    /// <summary>Runs the scenario against <paramref name="host"/> (started by <see cref="JobHost.Start"/>) and returns the body to compare.</summary>
    public static async Task<string> Body(GoldenHost host, string slug, string scenario, CancellationToken ct)
    {
        await ViewScenarios.Approve(host, ct);
        var repo = RepoName.From("demo");
        var (first, second) = (Job(1), Job(2));
        await ViewScenarios.Append(host, EventTypes.JobStarted, Started(first, "echo", repo), $"job.started:{first.Value}", ct);
        await ViewScenarios.Append(host, EventTypes.JobProgress, new JobProgress(Command(1), first, 1, "echo 1", Item: null, repo), $"job.progress:{first.Value}:1", ct);
        await ViewScenarios.Append(host, EventTypes.JobFinished, new JobFinished(Command(1), first, JobOutcome.Succeeded, "echoed 1", Code: null, Result: null, Item: null, repo), $"job.finished:{first.Value}", ct);
        await ViewScenarios.Append(host, EventTypes.JobStarted, Started(second, "echo2", repo), $"job.started:{second.Value}", ct);
        await ViewScenarios.Append(host, EventTypes.JobCancelled, new JobCancelled(Command(2), second, "human:cli", Item: null, repo), $"job.cancelled:{second.Value}", ct);
        await ViewScenarios.Append(host, "service.heartbeat", new Heartbeat(host.Now(), host.Now()), "service.heartbeat:1:1", ct);   // a frame every caller gets, so the stream is not empty
        await ViewScenarios.CatchUp(host, ct);
        (string Name, string Value)[] headers = [(SindriHeaders.Caller, "session"), (SindriHeaders.Client, "cli"), (SindriHeaders.Repo, scenario == "session-own-repo-job" ? "demo" : "other")];
        return slug == "events"
            ? await StreamScenarios.Frames(host, "/sindri/events?types=job.*,service.heartbeat", headers, ct)
            : (await host.Http.Ask(Get("/sindri/ledger?type=job.*", headers))).Body;
    }

    private static JobStarted Started(JobId job, string key, RepoName repo) => new(Command(1), job, JobKind.From("EchoJob"), JobKey.From(key), Item: null, repo, Service: null);

    private static JobId Job(int n) => JobId.From($"JOB-{n.ToString("x32", CultureInfo.InvariantCulture)}");

    private static CommandId Command(int n) => CommandId.From(new Guid(0xc0, 0, 0, 0, 0, 0, 0, 0, 0, 0, (byte)n));
}
