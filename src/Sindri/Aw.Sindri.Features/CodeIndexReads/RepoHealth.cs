using System.Globalization;
using Aw.CodeIndex.Core.Facade;
using Aw.CodeIndex.Core.Model;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Scheduling;
using IndexRepoHead = Aw.CodeIndex.Core.Model.RepoHead;

namespace Aw.Sindri.Features.CodeIndexReads;

/// <summary>
/// A repo's index state as the Health card words it (code index spec §7.3): the facade's file state with 'stale', 'building' and 'waiting' laid over it from the live head and the build jobs, and the embeddings
/// layer's progress as a percent. Counts and words only.
/// </summary>
/// <param name="Repo">The repo.</param>
/// <param name="State">missing, damaged, ok, stale (the head moved past the index), building (a build job runs) or waiting (a deferred or failed build holds the next one).</param>
/// <param name="EmbeddingsPercent">The share of embeddable symbols that have a vector, or null when the layer states no count.</param>
/// <param name="Waiting">'waiting for a free build slot' or 'retrying in N min' while the state is waiting, else null.</param>
public sealed record RepoHealth(string Repo, string State, int? EmbeddingsPercent, string? Waiting)
{
    /// <summary>Lays the heads and jobs over one repo's status.</summary>
    /// <param name="status">The repo's index status, from the files.</param>
    /// <param name="live">The repo's live head, or null when unread.</param>
    /// <param name="builds">The repo's build jobs.</param>
    /// <param name="now">The time.</param>
    /// <returns>The health.</returns>
    public static RepoHealth Of(RepoStatus status, IndexRepoHead? live, IReadOnlyList<JobRow> builds, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(status);
        ArgumentNullException.ThrowIfNull(builds);
        var percent = Percent(status.Layers.FirstOrDefault(static l => l.Layer == LayerName.Embeddings)?.Detail);
        var (state, waiting) = Overlay(status, live, builds, now);
        return new RepoHealth(status.Repo.Value, state, percent, waiting);
    }

    private static (string State, string? Waiting) Overlay(RepoStatus status, IndexRepoHead? live, IReadOnlyList<JobRow> builds, DateTimeOffset now)
    {
        if (status.State == IndexState.Damaged)
        {
            return ("damaged", null);
        }

        if (builds.Any(static j => j.State == JobState.Running))
        {
            return ("building", null);
        }

        var missing = status.State == IndexState.Missing;
        var behind = missing || Moved(status, live);
        var waiting = behind || status.Layers.Any(static l => l.State == LayerState.Pending) ? WaitingWords(builds, now) : null;
        return (Word(waiting, missing, behind), waiting);
    }

    private static bool Moved(RepoStatus status, IndexRepoHead? live) => live?.Head is { } head && head != status.Commit;

    private static string Word(string? waiting, bool missing, bool behind) => waiting is not null ? "waiting" : missing ? "missing" : behind ? "stale" : "ok";

    private static string? WaitingWords(IReadOnlyList<JobRow> builds, DateTimeOffset now) =>
        IndexBuildWords.Waiting([.. builds.Where(static j => j.EndedAt is not null).OrderByDescending(static j => j.EndedAt)], now);

    // The embeddings layer's detail is 'N of M embedded' (spec §5.5): anything else states no count.
    private static int? Percent(string? detail)
    {
        var parts = detail?.Split(separator: ' ');
        return parts is [var done, "of", var all, "embedded"] && int.TryParse(done, style: NumberStyles.None, provider: CultureInfo.InvariantCulture, result: out var n) && int.TryParse(all, style: NumberStyles.None, provider: CultureInfo.InvariantCulture, result: out var m)
            ? m == 0 ? 100 : (int)Math.Min(100L, 100L * n / m)
            : null;
    }
}
