using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using static Aw.Sindri.Tests.Workflow.Engine.EngineFixture;

namespace Aw.Sindri.Tests.Workflow.Engine;

/// <summary>
/// The closed 'when' (spec §3.6): each form selects the first transition when it holds, and lets the match fall to the
/// next transition when it doesn't. Transition 0 goes to 'dropped' under the form; transition 1 goes to 'done' always.
/// </summary>
public sealed class WhenFormTests
{
    private static readonly WhenClause _outputPresent = Clause(fact: "fact:output", @is: FactPresence.Present);
    private static readonly WhenClause _findingsAbsent = Clause(fact: "fact:findings", @is: FactPresence.Absent);

    [Fact, Trait("Lane", "unit")]
    public void Outcome_form_tests_the_reported_outcome()
    {
        Assert.Equal(0, Matched(Clause(outcome: "done")));
        Assert.Equal(1, Matched(Clause(outcome: "accepted")));
    }

    [Fact, Trait("Lane", "unit")]
    public void Fact_present_form_tests_the_facts_and_the_report()
    {
        Assert.Equal(0, Matched(_outputPresent));
        Assert.Equal(1, Matched(Clause(fact: "fact:findings", @is: FactPresence.Present)));
        Assert.Equal(0, Matched(Clause(fact: "fact:findings", @is: FactPresence.Present), withFindings: true));
    }

    [Fact, Trait("Lane", "unit")]
    public void Fact_absent_form_tests_the_facts_and_the_report()
    {
        Assert.Equal(0, Matched(_findingsAbsent));
        Assert.Equal(1, Matched(_findingsAbsent, withFindings: true));
    }

    [Fact, Trait("Lane", "unit")]
    public void Loop_below_cap_form_tests_the_rounds_allowed()
    {
        var below = Clause(loop: "revisions");

        Assert.Equal(0, Matched(below, round: 2));
        Assert.Equal(1, Matched(below, round: 3));
        Assert.Equal(0, Matched(below, round: 3, allowed: 5));
    }

    [Fact, Trait("Lane", "unit")]
    public void All_form_holds_when_every_part_holds()
    {
        Assert.Equal(0, Matched(Clause(all: [Clause(outcome: "done"), _outputPresent])));
        Assert.Equal(1, Matched(Clause(all: [Clause(outcome: "done"), _findingsAbsent]), withFindings: true));
    }

    [Fact, Trait("Lane", "unit")]
    public void Any_form_holds_when_one_part_holds()
    {
        Assert.Equal(0, Matched(Clause(any: [Clause(outcome: "accepted"), _outputPresent])));
        Assert.Equal(1, Matched(Clause(any: [Clause(outcome: "accepted"), _findingsAbsent]), withFindings: true));
    }

    [Fact, Trait("Lane", "unit")]
    public void Not_form_holds_when_its_part_does_not()
    {
        Assert.Equal(0, Matched(Clause(not: Clause(outcome: "accepted"))));
        Assert.Equal(1, Matched(Clause(not: _outputPresent)));
    }

    private static int? Matched(WhenClause when, bool withFindings = false, int round = 1, int allowed = 3)
    {
        var recipe = Minimal([From("dropped", when), From("done")], new Dictionary<string, LoopSpec>(StringComparer.Ordinal)
        {
            ["revisions"] = new(3, LoopSpec.DefaultNoProgress),
        }, loop: "revisions");
        var instance = At(recipe, "produce") with
        {
            Loops = new Dictionary<string, LoopState>(StringComparer.Ordinal) { ["revisions"] = new(round, allowed, Last: null) },
            Facts = withFindings ? new Dictionary<FactKey, FactValue> { [FactKey.From("fact:findings")] = Fact("found") } : [],
        };

        var match = TransitionDecision.Match(instance, recipe, Completed(1, "done", ("fact:output", "x")));

        Assert.Equal(MatchMiss.None, match.Miss);
        return match.Transition;
    }

    private static WhenClause Clause(
        string? outcome = null, string? fact = null, FactPresence? @is = null, string? loop = null,
        IReadOnlyList<WhenClause>? all = null, IReadOnlyList<WhenClause>? any = null, WhenClause? not = null) => new(
        outcome is null ? null : OutcomeName.From(outcome), fact is null ? null : FactKey.From(fact), @is, loop,
        loop is null ? null : LoopBound.Cap, all, any, not);
}
