using System.Text.Json;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>What the code index service, the outlooks and the build job agree on: the job's kind, its per-repo key and how to read a finished job's result.</summary>
internal static class IndexJobs
{
    internal static JobKind BuildKind { get; } = JobKind.From("BuildCodeIndex");

    /// <summary>'index:&lt;repo&gt;' (a job key is lower case, so two repos that differ only by case share a key).</summary>
    internal static JobKey KeyOf(string repo) => JobKey.From("index:" + repo.ToLowerInvariant());

    internal static bool IsOf(JobRow job, string repo) => job.Kind == BuildKind && job.Key == KeyOf(repo);

    /// <summary>The result's outcome word, or null when the job has no readable result (failed, cancelled, running).</summary>
    internal static string? OutcomeOf(JobRow job) =>
        job.Result is { ValueKind: JsonValueKind.Object } result
        && result.EnumerateObject().FirstOrDefault(static p => string.Equals(p.Name, "outcome", StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: JsonValueKind.String } outcome
            ? outcome.Value.GetString()
            : null;

    internal static bool Deferred(JobRow job) => job.State == JobState.Succeeded && OutcomeOf(job) == BuildCodeIndexResult.Deferred;

    internal static bool Published(JobRow job) => job.State == JobState.Succeeded && OutcomeOf(job) == "published";
}
