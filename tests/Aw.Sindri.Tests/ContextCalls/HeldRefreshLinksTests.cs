using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Jobs;
using Aw.Sindri.Core.Views;
using Aw.Sindri.Features.ContextCalls;
using Aw.Sindri.Features.GetJob;
using Aw.Sindri.Features.ListJobs;
using Aw.Sindri.Host.Commands;

namespace Aw.Sindri.Tests.ContextCalls;

/// <summary>'jobs show' on a refresh that ended held: it points at the publish job that finished the map, or says none has (code index spec 6.4).</summary>
public sealed class HeldRefreshLinksTests
{
    private static readonly DateTimeOffset _at = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private static JobView Refresh(string kind = "RefreshContextMap", JobState state = JobState.Succeeded, string? summary = "held: map v14 waits for the code index") =>
        new(Job(1), JobKind.From(kind), JobKey.From("refresh"), Item: null, Repo: null, Service: null, state, "service:context-map-refresh", _at, _at.AddSeconds(30),
            Progress: [], Rounds: 0, summary, Code: null, Result: null, CancelledBy: null, Actions: [], Cli: null, AsOf: new AsOf(0, 0, _at, Rebuilding: false, Stalled: false));

    private static JobId Job(int n) => JobId.From($"JOB-{n:x32}");

    private static JobSummary Publish(int id, string summary, int minutes = 5) =>
        new(Job(id), JobKind.From("PublishContextMap"), JobKey.From("map-publish"), Item: null, Repo: null, Service: null, JobState.Succeeded, "service:context-map-refresh",
            _at.AddMinutes(minutes), _at.AddMinutes(minutes).AddSeconds(5), Rounds: 2, summary, Code: null);

    [Fact, Trait("Lane", "unit")]
    public void The_summaries_the_jobs_write_are_the_ones_the_link_reads()
    {
        var link = HeldRefreshLinks.Publish(Refresh(summary: MapJobSummaries.Held(14)), HeldRefreshLinks.HeldVersion(Refresh(summary: MapJobSummaries.Held(14))) ?? -1, [Publish(2, MapJobSummaries.Promoted(14, "IndexReady"), minutes: 6)]);

        Assert.Equal($"publish: {Job(2).Value} map v14 promoted · IndexReady (sindri jobs show {Job(2).Value})\n", link);
    }

    [Fact, Trait("Lane", "unit")]
    public void Only_a_succeeded_refresh_that_ended_held_has_a_held_version()
    {
        Assert.Equal(14, HeldRefreshLinks.HeldVersion(Refresh()));
        Assert.Null(HeldRefreshLinks.HeldVersion(Refresh(summary: "map v14: 3 kept, 0 removed")));
        Assert.Null(HeldRefreshLinks.HeldVersion(Refresh(state: JobState.Failed)));
        Assert.Null(HeldRefreshLinks.HeldVersion(Refresh(kind: "BuildContextMap")));
    }

    [Fact, Trait("Lane", "unit")]
    public void A_held_refresh_points_at_the_publish_job_that_promoted_its_map()
    {
        var text = HeldRefreshLinks.Publish(Refresh(), 14, [Publish(2, "map v14 promoted · IndexReady", minutes: 6), Publish(1, "map v14 still waits for the code index", minutes: 2)]);

        Assert.Equal($"publish: {Job(2).Value} map v14 promoted · IndexReady (sindri jobs show {Job(2).Value})\n", text);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_held_refresh_with_no_publish_yet_says_the_map_is_still_held_and_where_to_look()
    {
        var text = HeldRefreshLinks.Publish(Refresh(), 14, [Publish(0, "map v13 promoted · IndexReady", minutes: 6)]);

        Assert.Equal("publish: not yet, map v14 is held for the code index (sindri context map status)\n", text);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_publish_before_the_refresh_ended_is_not_its_publish()
    {
        var text = HeldRefreshLinks.Publish(Refresh(), 14, [Publish(9, "map v14 promoted · IndexReady", minutes: -10)]);

        Assert.StartsWith("publish: not yet", text, StringComparison.Ordinal);
    }
}
