using System.Text.Json;
using Aw.Sindri.Core.Workflow.Blocks;
using Aw.Sindri.Core.Workflow.Engine;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Rules;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Recipes;
using Aw.Sindri.Features.Blocks.Gate;
using Aw.Sindri.Infrastructure.Workflows;
using Aw.Sindri.Testing.Workflow;
using Microsoft.Extensions.Logging.Abstractions;

namespace Aw.Sindri.Tests.Workflows;

/// <summary>'EvaluateGates' (engine P7 plan D7): the matched transition's gates run over the registered runtimes, one answer per gate that has one.</summary>
public sealed class GateActivitiesTests
{
    private static readonly BlockId _budget = BlockId.From("gate.budget.available");
    private static readonly BlockId _route = BlockId.From("gate.route.startable");

    private static GateUse Use(BlockId id, IReadOnlyDictionary<string, WithValue>? with = null) => new(id, Threshold: null, with ?? new Dictionary<string, WithValue>(StringComparer.Ordinal));

    private static GateRun Run(int? tokensLeft, params GateUse[] gates) =>
        new("T-3/item/1", gates, new Dictionary<FactKey, FactValue>(), tokensLeft);

    private static GateActivities Activities(GateDispatch dispatch, InMemoryFactStore? contents = null) =>
        new(dispatch, contents ?? new InMemoryFactStore(), NullLogger<GateActivities>.Instance);

    private static GateDispatch Budget { get; } = async (gate, context, ct) =>
        gate == _budget ? await new BudgetAvailableGate().Evaluate(context, ct) : null;

    [Fact, Trait("Lane", "unit")]
    public async Task A_transition_with_gates_decides_with_their_answers()
    {
        var answers = await Activities(Budget).Evaluate(Run(tokensLeft: 5, Use(_budget)), TestContext.Current.CancellationToken);

        var answer = Assert.Single(answers);
        Assert.Equal((_budget, GateVerdict.Pass), (answer.Gate, answer.Result.Verdict));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_without_a_runtime_refuses_as_unavailable()
    {
        var answers = await Activities(Budget).Evaluate(Run(tokensLeft: 5, Use(_route), Use(_budget)), TestContext.Current.CancellationToken);

        Assert.Equal([_budget], answers.Select(static a => a.Gate));   // no answer for the gate with no runtime
        var record = RefusalRules.Classify(Use(_route), answer: null, RouteStartableGate.Descriptor);
        Assert.Equal(GateOutcome.Unavailable, record.Outcome);   // so the decision refuses it as unavailable (W5)
    }

    [Fact, Trait("Lane", "unit")]
    public async Task Budget_available_fails_when_the_items_tokens_are_spent()
    {
        var spent = await Activities(Budget).Evaluate(Run(tokensLeft: 0, Use(_budget)), TestContext.Current.CancellationToken);
        var none = await Activities(Budget).Evaluate(Run(tokensLeft: null, Use(_budget)), TestContext.Current.CancellationToken);

        Assert.Equal(GateVerdict.Fail, Assert.Single(spent).Result.Verdict);
        Assert.Contains("token budget is spent", Assert.Single(Assert.Single(spent).Result.Reasons), StringComparison.Ordinal);
        Assert.Equal(GateVerdict.Pass, Assert.Single(none).Result.Verdict);   // no budget applies
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_gate_that_throws_has_no_answer_and_the_others_still_run()
    {
        var dispatch = new GateDispatch((gate, context, ct) => gate == _route ? throw new InvalidOperationException("boom") : Budget(gate, context, ct));

        var answers = await Activities(dispatch).Evaluate(Run(tokensLeft: 5, Use(_route), Use(_budget)), TestContext.Current.CancellationToken);

        Assert.Equal([_budget], answers.Select(static a => a.Gate));
    }

    [Fact, Trait("Lane", "unit")]
    public async Task A_fact_argument_is_read_into_the_gates_context()
    {
        var contents = new InMemoryFactStore();
        var stored = (await contents.Put("the spec"u8.ToArray(), TestContext.Current.CancellationToken)).Value;
        GateContext? seen = null;
        var dispatch = new GateDispatch((_, context, _) =>
        {
            seen = context;
            return ValueTask.FromResult<GateResult?>(new GateResult(GateVerdict.Pass, Score: null, []));
        });
        var key = FactKey.From("fact:spec");
        var use = Use(_budget, new Dictionary<string, WithValue>(StringComparer.Ordinal) { ["spec"] = new(Literal: null, key, Profile: null), ["limit"] = new(JsonSerializer.SerializeToElement(3), Fact: null, Profile: null) });
        var run = new GateRun("T-3/item/1", [use], new Dictionary<FactKey, FactValue> { [key] = stored }, TokensLeft: 9);

        _ = await Activities(dispatch, contents).Evaluate(run, TestContext.Current.CancellationToken);

        Assert.Equal("the spec", seen!.Arguments["spec"].GetString());
        Assert.Equal(3, seen.Arguments["limit"].GetInt32());
        Assert.Equal("the spec", seen.Facts[key].GetString());
        Assert.Equal(9, seen.TokensLeft);
    }
}
