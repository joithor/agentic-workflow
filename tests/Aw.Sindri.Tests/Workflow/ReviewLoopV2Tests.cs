using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow;

/// <summary>workflow.review-loop version 2 (line spec DTC4; plan LN-Y2 U4): the lens frame, one lens per run, a recorded review packet.</summary>
public sealed class ReviewLoopV2Tests
{
    private const string Path = "sindri/workflow.review-loop.yaml";

    private static Recipe Load() => RecipeComposition.ReadClean(ShippedRecipes.Read(Path), "workflow.review-loop.yaml").Recipe;

    [Fact, Trait("Lane", "unit")]
    public void The_recipe_is_version_3_and_validates_against_the_shipped_catalog()
    {
        var document = RecipeComposition.ReadClean(ShippedRecipes.Read(Path), "workflow.review-loop.yaml");

        Assert.Equal(3, document.Recipe.Version);
        Assert.Empty(RecipeComposition.Errors(document, FixtureCatalog.Shipped(), new InMemoryChildRecipes()));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_review_output_is_a_packet_written_by_the_record_state()
    {
        var output = Assert.Single(Load().Outputs);

        Assert.Equal("fact:review", output.Key.Value);
        Assert.Equal("packet", output.Type);
        Assert.Equal("record", output.From);
    }

    [Fact, Trait("Lane", "unit")]
    public void Both_inputs_stay_required_and_the_change_carries_the_branch()
        => Assert.Equal(["fact:change", "fact:phasePacket"], Load().Inputs.Select(static i => i.Key.Value), StringComparer.Ordinal);

    [Fact, Trait("Lane", "unit")]
    public void The_graph_runs_round_lens_log_and_ends_clean_through_record()
    {
        var recipe = Load();

        Assert.Equal("round", recipe.Initial);
        Assert.Equal(
            ["clean", "fix", "lens", "lens-log", "recheck", "record", "round", "stuck", "wrong"],
            recipe.States.Keys.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Assert.Equal("step.review.round", recipe.States["round"].Step!.Use.Value);
        Assert.Equal("step.lens.run", recipe.States["lens"].Step!.Use.Value);
        Assert.Equal("step.review.log", recipe.States["lens-log"].Step!.Use.Value);
        Assert.Equal("step.review.record", recipe.States["record"].Step!.Use.Value);
        Assert.Equal("step.review.fix", recipe.States["fix"].Step!.Use.Value);
        Assert.Equal("step.review.recheck", recipe.States["recheck"].Step!.Use.Value);
        Assert.Equal("rounds", recipe.States["round"].Loop);
        Assert.Equal(5, recipe.Loops["rounds"].Cap);
        Assert.Equal("change", recipe.States["lens"].Step!.With["subject"].Literal?.GetString());
        Assert.Equal("lenses", recipe.States["lens"].Step!.With["bundle"].Literal?.GetString());
    }

    [Fact, Trait("Lane", "unit")]
    public void Clean_is_reached_only_through_record_so_the_packet_is_always_frozen()
    {
        var into = Load().Transitions.Where(static t => t.To == "clean").ToList();

        Assert.All(into, static t => Assert.Equal(["record"], t.From));
        Assert.DoesNotContain(Load().Transitions, static t => t.To == "record" && t.From.Contains("round", StringComparer.Ordinal));
    }

    [Fact, Trait("Lane", "unit")]
    public void Every_path_into_failed_writes_the_review_so_the_parent_can_take_it()
    {
        var into = Load().Transitions.Where(static t => t.To == "wrong").ToList();

        Assert.NotEmpty(into);
        Assert.All(into, static t => Assert.Contains(t.Produces, static p => p.Fact.Value == "fact:review"));
    }

    [Fact, Trait("Lane", "unit")]
    public void No_step_id_is_in_a_reserved_family()
    {
        var ids = Load().States.Values.Where(static s => s.Step is not null).Select(static s => s.Step!.Use.Value);

        Assert.All(ids, static id => Assert.DoesNotMatch(@"^(step\.judge\.|step\.desk\.|input\.)", id));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_lens_frame_takes_a_subject_and_a_bundle_and_reports_one_lens_per_run()
    {
        var run = FixtureCatalog.Shipped().Find(BlockId.From("step.lens.run"))!;

        Assert.Equal(BlockRuntime.Agent, run.Runtime);
        Assert.Equal(["reviewed"], run.Outcomes.Select(static o => o.Value), StringComparer.Ordinal);
        Assert.Equal(["subject"], run.Parameters.Where(static p => p.Required).Select(static p => p.Name), StringComparer.Ordinal);
        Assert.Contains(run.Parameters, static p => p.Name == "bundle");
    }

    [Fact, Trait("Lane", "unit")]
    public void The_replaced_review_run_step_is_gone_and_the_new_review_steps_are_shipped()
    {
        var catalog = FixtureCatalog.Shipped();

        Assert.Null(catalog.Find(BlockId.From("step.review.run")));
        Assert.All(["step.review.round", "step.review.log", "step.review.record", "step.lens.run"], id => Assert.NotNull(catalog.Find(BlockId.From(id))));
    }

    [Fact, Trait("Lane", "unit")]
    public void The_eval_names_of_version_1_are_kept_and_one_lens_per_run_is_added()
    {
        var names = Load().Evals.Select(static e => System.IO.Path.GetFileNameWithoutExtension(e));

        Assert.Equal(["behaviour-wrong", "cap-then-recheck", "happy", "one-lens-per-run", "recheck-clean", "recheck-wrong"], names.Order(StringComparer.Ordinal), StringComparer.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void No_step_names_a_step_input_so_approval_cannot_refuse_it_with_SND_WF_016() =>
        Assert.All(Load().States.Values.Where(static s => s.Step is not null), static s => Assert.Empty(s.Step!.Inputs));
}
