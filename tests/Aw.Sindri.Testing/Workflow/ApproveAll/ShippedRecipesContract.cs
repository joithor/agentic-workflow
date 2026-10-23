using Aw.Kernel.Testing;
using Aw.Sindri.Core.Workflow.ApproveAll;
using Aw.Sindri.Core.Workflow.Ids;
using Xunit;

namespace Aw.Sindri.Testing.Workflow.ApproveAll;

/// <summary>
/// The contract every <see cref="IShippedRecipes"/> honours (setup-once spec §5.3): the plan lists what is not approved at its shipped hash, children first, with a digest
/// that moves when a row does; applying a redeemed plan approves each recipe in order, refuses what the checks refuse, skips what waits on a refused or skipped recipe and carries on
/// with the rest, and reports one line per recipe. Run against the fake and, when the engine lands, its implementation.
/// </summary>
public abstract class ShippedRecipesContract
{
    private static readonly RecipeId _step = RecipeId.From("workflow.step");
    private static readonly RecipeId _item = RecipeId.From("workflow.item");
    private static readonly RecipeId _note = RecipeId.From("workflow.note");
    private static readonly RecipeId _review = RecipeId.From("workflow.review");
    private static readonly CapabilityLine _agent = new(CapabilityId.From("step.produce"), "Starts an AI agent session in a repo you added");

    /// <summary>The implementation over exactly the recipes <paramref name="seeds"/> describe.</summary>
    /// <param name="seeds">The shipped set.</param>
    /// <returns>The port.</returns>
    protected abstract IShippedRecipes Create(IReadOnlyList<ShippedSeed> seeds);

    /// <summary>A seed with a hash made from <paramref name="digit"/>.</summary>
    /// <param name="id">The recipe.</param>
    /// <param name="name">Its display name.</param>
    /// <param name="digit">The hex digit its hash repeats.</param>
    /// <param name="children">The recipes it uses.</param>
    /// <param name="refused">Whether the approval checks refuse it.</param>
    /// <returns>The seed.</returns>
    protected static ShippedSeed Seed(RecipeId id, string name, char digit, RecipeId[]? children = null, bool refused = false) =>
        new(id, name, RecipeHash.From("sha256:" + new string(digit, 64)), children ?? [], [_agent],
            Refusal: refused ? new SeededRefusal("SND-RECIPE-008", name + " declares no checks.", Cli: null) : null);

    /// <summary>An empty set has an empty plan with the empty digest.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task An_empty_set_has_an_empty_plan()
    {
        var plan = (await Create([]).Plan(TestContext.Current.CancellationToken)).Value;

        Expect.True(plan.IsEmpty);
        Expect.Equal(ApproveAllDigest.Empty, plan.Digest);
    }

    /// <summary>A recipe comes after the recipes it uses, whatever order the set lists them in.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_plan_lists_children_first()
    {
        var port = Create([Seed(_item, "Work item", 'b', [_step]), Seed(_step, "One step", 'a')]);

        var plan = (await port.Plan(TestContext.Current.CancellationToken)).Value;

        Expect.Sequence([_step, _item], plan.Recipes.Select(static r => r.Id));
        Expect.Equal(PlanChange.New, plan.Recipes[0].Change);
        Expect.Equal("New", plan.Recipes[0].ChangeWord);
    }

    /// <summary>The digest is the one the pure function computes over the rows, and it moves when a row's hash does.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task The_digest_covers_the_rows()
    {
        var first = (await Create([Seed(_step, "One step", 'a')]).Plan(TestContext.Current.CancellationToken)).Value;
        var moved = (await Create([Seed(_step, "One step", 'c')]).Plan(TestContext.Current.CancellationToken)).Value;

        Expect.Equal(ApproveAllDigest.Of(first.Recipes), first.Digest);
        Expect.True(first.Digest != moved.Digest);
    }

    /// <summary>Applying approves every recipe in order, reports each by its display name, and the next plan is empty.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task Applying_approves_in_order_and_empties_the_plan()
    {
        var port = Create([Seed(_item, "Work item", 'b', [_step]), Seed(_step, "One step", 'a')]);
        var progress = new RecordedProgress();

        var result = (await port.Approve(await Redeemed(port), progress, TestContext.Current.CancellationToken)).Value;

        Expect.Sequence([_step, _item], result.Approved.Select(static a => a.Id));
        Expect.Sequence(["One step", "Work item"], progress.Lines);
        Expect.True(result.Refused.Count == 0 && result.Skipped.Count == 0);
        Expect.True((await port.Plan(TestContext.Current.CancellationToken)).Value.IsEmpty);
    }

    /// <summary>A refusal skips the recipes that wait on it, carries on with the rest, and names the recipe each skipped one waits on.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_refusal_skips_its_dependents_and_carries_on()
    {
        var port = Create([Seed(_step, "One step", 'a'), Seed(_item, "Work item", 'b', [_step], refused: true), Seed(_review, "Review loop", 'd', [_item]), Seed(_note, "Note", 'c')]);

        var result = (await port.Approve(await Redeemed(port), new RecordedProgress(), TestContext.Current.CancellationToken)).Value;

        Expect.Sequence([_step, _note], result.Approved.Select(static a => a.Id));
        Expect.Sequence([_item], result.Refused.Select(static r => r.Id));
        Expect.Sequence([(_review, _item)], result.Skipped.Select(static s => (s.Id, s.Because)));
    }

    /// <summary>A row the plan names but the set does not ship is refused SND-RECIPE-011, and one whose hash is not the shipped one SND-RECIPE-009; both carry the one-recipe approval to run instead, and the rest still go.</summary>
    /// <returns>The test.</returns>
    [Fact]
    public async Task A_row_that_is_not_shipped_or_moved_is_refused_by_its_own_code_and_the_rest_go()
    {
        var port = Create([Seed(_step, "One step", 'a'), Seed(_note, "Note", 'c')]);
        var plan = (await port.Plan(TestContext.Current.CancellationToken)).Value;
        var stray = plan.Recipes[0] with { Id = _review, DisplayName = "Review loop", Children = [] };
        var moved = plan.Recipes[1] with { Hash = RecipeHash.From("sha256:" + new string('f', 64)) };
        var edited = new ShippedRecipePlan([stray, moved, plan.Recipes[0]], []);

        var result = (await port.Approve(await Redeemed(port, edited), new RecordedProgress(), TestContext.Current.CancellationToken)).Value;

        Expect.Sequence([(_review, "SND-RECIPE-011"), (_note, "SND-RECIPE-009")], result.Refused.Select(static r => (r.Id, r.Code)));
        Expect.Sequence([_step], result.Approved.Select(static a => a.Id));
        Expect.True(result.Refused.All(static r => r.Cli is { } cli && cli.Contains("recipe approve", StringComparison.Ordinal)));
    }

    private static readonly Core.Ids.CommandId _batch = Core.Ids.CommandId.From(new Guid(192, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0));

    private static async Task<RedeemedPlan> Redeemed(IShippedRecipes port) =>
        await Redeemed(port, (await port.Plan(TestContext.Current.CancellationToken)).Value);

    private static Task<RedeemedPlan> Redeemed(IShippedRecipes port, ShippedRecipePlan plan)
    {
        ArgumentNullException.ThrowIfNull(port);
        return Task.FromResult(RedeemedPlan.For(plan, _batch));
    }

    private sealed class RecordedProgress : IProgress<string>
    {
        public List<string> Lines { get; } = [];

        public void Report(string value) => Lines.Add(value);
    }
}
