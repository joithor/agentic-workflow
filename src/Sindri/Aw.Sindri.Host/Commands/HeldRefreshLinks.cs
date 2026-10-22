using System.Globalization;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.ListJobs;

namespace Aw.Sindri.Host.Commands;

/// <summary>
/// The link 'jobs show' draws from a map refresh that ended held for the code index to the publish job that finished it (code index spec 6.4): the refresh's
/// summary says 'held: map v14 …', and the publish's says 'map v14 promoted · IndexReady' (or why it did not). Read from the two summaries, so no job stores the other's id.
/// </summary>
internal static class HeldRefreshLinks
{
    private const string RefreshKind = "RefreshContextMap";
    private const string PublishKind = "PublishContextMap";
    private const string HeldPrefix = "held: map v";

    /// <summary>The held map's number when <paramref name="job"/> is a refresh that ended held; otherwise null.</summary>
    /// <param name="job">The job.</param>
    /// <returns>The version, or null.</returns>
    internal static int? HeldVersion(JobView job)
    {
        ArgumentNullException.ThrowIfNull(job);
        if (job.Kind.Value != RefreshKind || job.State != JobState.Succeeded || job.Summary is not { } summary || !summary.StartsWith(HeldPrefix, StringComparison.Ordinal))
        {
            return null;
        }

        var digits = new string([.. summary[HeldPrefix.Length..].TakeWhile(char.IsAsciiDigit)]);
        return int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var version) ? version : null;
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
        var tag = string.Create(CultureInfo.InvariantCulture, $"map v{version} ");
        var about = publishes.Where(j => j.StartedAt >= started && (j.Summary?.Contains(tag, StringComparison.Ordinal) ?? false)).OrderByDescending(static j => j.StartedAt).ToList();
        var found = about.Find(static j => j.Summary?.Contains("promoted", StringComparison.Ordinal) == true) ?? about.FirstOrDefault();
        return found is null
            ? string.Create(CultureInfo.InvariantCulture, $"publish: not yet, map v{version} is held for the code index (sindri context map status)\n")
            : $"publish: {found.Id.Value} {found.Summary} (sindri jobs show {found.Id.Value})\n";
    }
}
