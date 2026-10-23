using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine.Runtime;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Tests.GetInstance;

/// <summary>What an open desk has its person review (q65): the block's descriptor declares the facts and their kinds, the engine fills the path or url from the fact's stored value, and a state that is no desk says nothing.</summary>
[Lane(TestLane.Unit)]
public sealed class DeskReviewsTests
{
    internal static readonly ReviewSpec[] Declared =
    [
        new(FactKey.From("fact:design"), DeskReviewKind.Design),
        new(FactKey.From("fact:prStatus"), DeskReviewKind.Pr),
    ];

    private static readonly InstanceDetailContext _context = new("Title", Parent: null, new ViewCounts(0, 0, 0, Context: null), StatusCases.Now);

    [Fact]
    public void A_desks_declared_reviews_are_served_on_its_form_without_a_path_or_url_until_the_values_are_read()
    {
        var detail = DetailOf("desk-task", _context);

        Assert.Equal(
            [new DeskReview("fact:design", DeskReviewKind.Design), new DeskReview("fact:prStatus", DeskReviewKind.Pr)],
            detail.Current.Desk!.Reviews);   // why: the desk-task case has an open desk
    }

    [Fact]
    public void The_path_and_url_are_filled_from_the_facts_stored_values_and_stay_null_for_a_fact_with_no_target()
    {
        var targets = new Dictionary<string, DeskReviewTarget>(StringComparer.Ordinal)
        {
            ["fact:design"] = new("/work/design/1", Url: null),
            ["fact:prStatus"] = new(Path: null, "https://github.com/o/r/pull/7"),
        };

        var reviews = DetailOf("desk-task", _context with { ReviewTargets = targets }).Current.Desk!.Reviews;

        Assert.Equal(
            [new DeskReview("fact:design", DeskReviewKind.Design, Path: "/work/design/1"), new DeskReview("fact:prStatus", DeskReviewKind.Pr, Url: "https://github.com/o/r/pull/7")],
            reviews);
        Assert.Equal(
            [new DeskReview("fact:design", DeskReviewKind.Design), new DeskReview("fact:prStatus", DeskReviewKind.Pr)],
            DetailOf("desk-task", _context with { ReviewTargets = new Dictionary<string, DeskReviewTarget>(StringComparer.Ordinal) { ["fact:other"] = new("/x", Url: null) } }).Current.Desk!.Reviews);
    }

    [Fact]
    public void A_block_that_declares_no_reviews_serves_none()
    {
        var view = InstanceViews.Of(StatusCases.Instance("desk-task"), StatusCases.Recipe, StatusCases.Now, timeoutAt: null, new InstanceViewContext());

        Assert.Null(view.Desk!.Reviews);
    }

    [Fact]
    public void A_state_with_no_open_desk_has_no_form_and_so_no_reviews()
    {
        var detail = DetailOf("parked-needs-answer-step", _context);

        Assert.Null(detail.Current.Desk);
    }

    [Theory]
    [InlineData("""{"hash":"sha256:ab","dir":"/packets/scope/3"}""", "/packets/scope/3", null)]
    [InlineData("""{"pr":7,"url":"https://github.com/o/r/pull/7","checks":{"passed":3}}""", null, "https://github.com/o/r/pull/7")]
    [InlineData("""{"sections":[]}""", null, null)]
    [InlineData("""{"dir":7,"url":false}""", null, null)]
    [InlineData("not json", null, null)]
    [InlineData("", null, null)]
    public void A_target_is_read_only_from_the_shapes_the_engine_writes(string stored, string? path, string? url) =>
        Assert.Equal(new DeskReviewTarget(path, url), DeskReviewTargets.Of(stored));

    private static InstanceDetail DetailOf(string name, InstanceDetailContext context)
    {
        var view = InstanceViews.Of(StatusCases.Instance(name), Reviewed(), StatusCases.Now, timeoutAt: null, new InstanceViewContext());
        return InstanceViews.Detail(view, context);
    }

    /// <summary>The shipped workflow.step with its produce Step's block declaring two reviews (the descriptor alone: a recipe never names them).</summary>
    internal static InterpretedRecipe Reviewed()
    {
        var shipped = StatusCases.Recipe.Catalog;
        var blocks = shipped.Blocks.Select(b => b.Id.Value == "step.produce" ? b with { Reviews = Declared } : b);
        return StatusCases.Recipe with { Catalog = new BlockCatalog(blocks, shipped.Events) };
    }
}
