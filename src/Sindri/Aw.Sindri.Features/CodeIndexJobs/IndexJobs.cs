using System.Text.Json;
using Aw.CodeIndex.Core.Planning;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;

namespace Aw.Sindri.Features.CodeIndexJobs;

/// <summary>What the code index service, the outlooks and the build job agree on: the job's kind, its per-repo key and how to read a finished job's result.</summary>
internal static class IndexJobs
{
    internal static JobKind BuildKind { get; } = JobKind.From("BuildCodeIndex");

    internal static JobKind EmbedKind { get; } = JobKind.From("EmbedCodeIndex");

    /// <summary>'embed:&lt;repo&gt;' (lower case, like <see cref="KeyOf"/>).</summary>
    internal static JobKey EmbedKeyOf(string repo) => JobKey.From("embed:" + repo.ToLowerInvariant());

    internal static bool IsEmbedOf(JobRow job, string repo) => job.Kind == EmbedKind && job.Key == EmbedKeyOf(repo);

    /// <summary>The result's 'embedded' count, or null when the job has no readable result.</summary>
    internal static int? EmbeddedOf(JobRow job) =>
        job.Result is { ValueKind: JsonValueKind.Object } result
        && result.EnumerateObject().FirstOrDefault(static p => string.Equals(p.Name, "embedded", StringComparison.OrdinalIgnoreCase)) is { Value: { ValueKind: JsonValueKind.Number } count }
        && count.TryGetInt32(out var embedded)
            ? embedded
            : null;

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

    /// <summary>What an ended build job says about continuing the repo's layers (code index spec §6.3).</summary>
    internal static IndexJobOutcome OutcomeFor(JobRow job) => new(job.EndedAt ?? default, job.State == JobState.Failed, Deferred(job), PendingLayers(job), EmbeddedOf(job), CommitOf(job));

    private static bool PendingLayers(JobRow job) =>
        job.State == JobState.Succeeded && Property(job, "layersPending") is { ValueKind: JsonValueKind.Array } pending && pending.GetArrayLength() > 0;

    private static string? CommitOf(JobRow job) => Property(job, "commit") is { ValueKind: JsonValueKind.String } commit ? commit.GetString() : null;

    private static int EmbeddedOf(JobRow job) => Property(job, "embedded") is { ValueKind: JsonValueKind.Number } n && n.TryGetInt32(out var count) ? count : 0;

    private static JsonElement? Property(JobRow job, string name) =>
        job.Result is { ValueKind: JsonValueKind.Object } result
        && result.EnumerateObject().FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: not JsonValueKind.Undefined } found
            ? found.Value
            : null;
}
