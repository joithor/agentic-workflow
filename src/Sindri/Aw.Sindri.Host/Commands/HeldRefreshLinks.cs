using System.Globalization;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.ListJobs;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The link 'jobs show' draws from a map refresh that ended held for the code index to the publish job that finished it (code index spec 6.4): the refresh's
/// summary and the publish's are written and read by <see cref="MapJobSummaries"/>, so no job stores the other's id and the two sides can't drift.
/// </summary>
internal static class HeldRefreshLinks
{
    private const string RefreshKind = "RefreshContextMap";
    private const string PublishKind = "PublishContextMap";

    /// <summary>The held map's number when <paramref name="job"/> is a refresh that ended held; otherwise null.</summary>
    /// <param name="job">The job.</param>
    /// <returns>The version, or null.</returns>
    internal static int? HeldVersion(JobView job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return job.Kind.Value == RefreshKind && job.State == JobState.Succeeded ? MapJobSummaries.HeldVersion(job.Summary) : null;
    }

    /// <summary>The kind the publish jobs have, for the list read.</summary>
    internal static string PublishJobKind => PublishKind;

    /// <summary>The lines under a held refresh: the publish job that finished its map, or that none has yet.</summary>
    /// <param name="refresh">The held refresh.</param>
    /// <param name="version">Its held map's number.</param>
    /// <param name="publishes">The publish jobs, newest first.</param>
    /// <returns>The text, ending in a newline.</returns>
    internal static string Publish(JobView refresh, int version, IReadOnlyList<JobSummary> publishes)
    {
        ArgumentNullException.ThrowIfNull(refresh);
        ArgumentNullException.ThrowIfNull(publishes);
        var started = refresh.EndedAt ?? refresh.StartedAt;
        var about = publishes.Where(j => j.StartedAt >= started && MapJobSummaries.Mentions(j.Summary, version)).OrderByDescending(static j => j.StartedAt).ToList();
        var found = about.Find(static j => MapJobSummaries.IsPromotion(j.Summary)) ?? about.FirstOrDefault();
        return found is null
            ? string.Create(CultureInfo.InvariantCulture, $"publish: not yet, map v{version} is held for the code index (sindri context map status)\n")
            : $"publish: {found.Id.Value} {found.Summary} (sindri jobs show {found.Id.Value})\n";
    }
}
