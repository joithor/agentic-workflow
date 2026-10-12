using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Evals;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Testing.Workflow;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// A 'step.failed' eval line's reason (spec §3.7 'sameFailure', §9.2): an optional 'reason', a constant without a line
/// number when left out, so an eval can pin a no-progress park on a repeated failure, and '--record' writes the reason
/// each failure reported, so the recorded case replays it.
/// </summary>
public sealed class EvalStepFailedTests
{
    private const string Start = """{"start":{"inputs":{}}}""";
    private const string Producing = """{"state":"produce"}""";
    private const string NoProgress = """{"state":"parked","park":"no-progress"}""";

    [Fact, Trait("Lane", "unit")]
    public void Two_step_failed_lines_with_the_default_reason_park_no_progress()
    {
        var verdict = Run(Line("""{"kind":"step.failed"}""", Producing), Line("""{"kind":"step.failed"}""", NoProgress));

        Assert.Null(verdict.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_step_failed_line_names_its_reason_and_only_a_repeated_one_parks()
    {
        var verdict = Run(
            Line("""{"kind":"step.failed","reason":"the build broke"}""", Producing),
            Line("""{"kind":"step.failed","reason":"a test timed out"}""", Producing),
            Line("""{"kind":"step.failed","reason":"a test timed out"}""", NoProgress));

        Assert.Null(verdict.Detail);
    }

    [Fact, Trait("Lane", "unit")]
    public void A_reason_on_another_kind_fails_its_line()
    {
        var verdict = Run(Line("""{"kind":"step.completed","outcome":"done","reason":"the build broke"}""", """{"state":"done"}"""));

        Assert.Equal(2, verdict.Line);
        Assert.Contains("'reason' belongs on a step.failed line only", verdict.Detail, StringComparison.Ordinal);
    }

    [Fact, Trait("Lane", "unit")]
    public void Recorded_step_failed_lines_carry_their_reason_and_replay()
    {
        var original = Case(
            Line("""{"kind":"step.failed","reason":"the build broke"}""", Producing),
            Line("""{"kind":"step.failed","reason":"a test timed out"}""", Producing),
            Line("""{"kind":"step.failed","reason":"a test timed out"}""", NoProgress));
        var (verdict, history) = EvalRunner.Replay(original, Looped(), EngineFixture.Catalog, InMemoryChildRecipes.None);
        Assert.Null(verdict.Detail);

        var recorded = EvalRecorder.Record("recorded", history, new Dictionary<TriggerKey, IReadOnlyDictionary<FactKey, FactValue>>(), new Dictionary<string, System.Text.Json.JsonElement>(StringComparer.Ordinal)).Value;

        var written = EvalLines.Write(recorded, EvalComposition.Options).Split('\n');
        Assert.Contains("\"reason\":\"the build broke\"", written[1], StringComparison.Ordinal);
        Assert.Contains("\"reason\":\"a test timed out\"", written[2], StringComparison.Ordinal);
        Assert.Null(EvalRunner.Run(recorded, Looped(), EngineFixture.Catalog, InMemoryChildRecipes.None).Detail);
    }

    /// <summary>The case's verdict as 'recipe eval' gives it: a line out of the format fails there, otherwise the run's verdict.</summary>
    private static EvalVerdict Run(params string[] lines)
    {
        var read = Read(lines);
        return read.Case is { } evalCase ? EvalRunner.Run(evalCase, Looped(), EngineFixture.Catalog, InMemoryChildRecipes.None) : read.Invalid!; // why: a reading has a case or why it has none
    }

    private static EvalCase Case(params string[] lines)
    {
        var read = Read(lines);
        Assert.Null(read.Invalid?.Detail);
        return Assert.IsType<EvalCase>(read.Case);
    }

    private static EvalReading Read(string[] lines) => EvalCaseReader.Read("case", string.Join('\n', [Start, .. lines]), EvalComposition.Options);

    private static string Line(string trigger, string expect) => "{\"trigger\":" + trigger + ",\"expect\":" + expect + "}";

    /// <summary>'produce' (loop 'revisions', cap 3) goes back into itself on step.failed and to 'done' on step.completed.</summary>
    private static Recipe Looped() => EngineFixture.Minimal(
        [new RecipeTransition(["produce"], "produce", "step.failed", When: null, [], [], [], []), EngineFixture.From("done")],
        new Dictionary<string, LoopSpec>(StringComparer.Ordinal) { ["revisions"] = new(3, LoopSpec.DefaultNoProgress) },
        loop: "revisions");
}
