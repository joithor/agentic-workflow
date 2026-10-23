using System.Text.Json;
using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Stats;
using Aw.Sindri.Tests.ApiGoldens;

namespace Aw.Sindri.Tests.PrBabysitter;

/// <summary>
/// The 'babysitter' section of GET /sindri/workflow/recipes/workflow.pr-babysitter/stats as the API's serializer writes it (API19), for the app's typed record: one answer with every
/// figure set and one with nothing counted yet. The rest of the body is the served 'workflow-item' golden's, read back through the same serializer.
/// </summary>
[Lane(TestLane.Unit)]
public sealed class BabysitterStatsGoldenTests
{
    private const string Slug = "workflow-recipes-id-stats";

    /// <summary>Every figure set: two comment types (a judge type and 'none'), two docs outcomes, a CI-fix rate and one unreadable record line.</summary>
    private static BabysitterStats Full { get; } = new(
        Pulls: 4,
        SinceStartToReadySeconds: new BabysitterSpread(Median: 5400, P90: 14400),
        SinceStartToMergeSeconds: new BabysitterSpread(Median: 9000, P90: 21600),
        TokensPerPull: new BabysitterSpread(Median: 42000, P90: 88000),
        Comments: new BabysitterCommentStats(Total: 9, Held: 2, WithoutOwner: 7,
            [new CommentTypeStats("nit", Fixed: 3, Replied: 1, Reacted: 1, Held: 1, Skipped: 0), new CommentTypeStats("none", Fixed: 0, Replied: 0, Reacted: 0, Held: 1, Skipped: 2)]),
        CiFix: new CiFixStats(Attempts: 4, Succeeded: 3, StillRed: 1, Rate: 0.75),
        Rechecks: new RecheckStats(Runs: 2, Resolved: 1, Unresolved: 1),
        OwnerActionsAtReady: new OwnerActionStats(Merge: 3, Rework: 1, Comments: 2, ResyncDocs: 1, Check: 1),
        Docs: new BabysitterDocsStats(WithMerge: 2, OwnerDesk: 1, [new ReasonCount("merged", 2), new ReasonCount("pending", 1)]),
        RecordErrors: 1);

    /// <summary>Nothing counted yet: no pulls, every spread and the rate null, no rows.</summary>
    private static BabysitterStats Empty { get; } = new(
        Pulls: 0,
        SinceStartToReadySeconds: new BabysitterSpread(Median: null, P90: null),
        SinceStartToMergeSeconds: new BabysitterSpread(Median: null, P90: null),
        TokensPerPull: new BabysitterSpread(Median: null, P90: null),
        Comments: new BabysitterCommentStats(Total: 0, Held: 0, WithoutOwner: 0, []),
        CiFix: new CiFixStats(Attempts: 0, Succeeded: 0, StillRed: 0, Rate: null),
        Rechecks: new RecheckStats(Runs: 0, Resolved: 0, Unresolved: 0),
        OwnerActionsAtReady: new OwnerActionStats(Merge: 0, Rework: 0, Comments: 0, ResyncDocs: 0, Check: 0),
        Docs: new BabysitterDocsStats(WithMerge: 0, OwnerDesk: 0, []),
        RecordErrors: 0);

    [Fact]
    public void A_babysitter_answer_with_every_figure_matches_its_golden() => ApiGolden.Match(Body(Full), "<no home>", Slug, "pr-babysitter");

    [Fact]
    public void A_babysitter_answer_with_nothing_counted_matches_its_golden() => ApiGolden.Match(Body(Empty), "<no home>", Slug, "pr-babysitter-empty");

    private static string Body(BabysitterStats babysitter)
    {
        var served = File.ReadAllText(Golden.SourcePath(typeof(BabysitterStatsGoldenTests).Assembly, ApiGolden.PathOf(Slug, "workflow-item")));
        var stats = JsonSerializer.Deserialize<RecipeStats>(served, SliceHarness.Json)! with { Recipe = BabysitterNames.Recipe, Babysitter = babysitter };
        return JsonSerializer.Serialize(stats, SliceHarness.Json);
    }
}
