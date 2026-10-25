using Aw.Sindri.Core.Workflow.Engine.Decisions;
using Aw.Sindri.Core.Workflow.Engine.Events;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Recipes;

namespace Aw.Sindri.Core.Workflow.Engine.Rules;

/// <summary>A new instance (spec §5.1, W17): the initial state entered as entry 1, and its workflow.started event.</summary>
public static partial class TransitionDecision
{
    private const string StartKind = "start";

    /// <summary><see cref="Start(StartRequest, Recipe, DecisionContext)"/> through <paramref name="rules"/>.</summary>
    internal static Decision Start(StartRequest request, Recipe recipe, DecisionContext context, IEngineRules rules)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(recipe);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rules);
        if (context.Approval != ApprovalCheck.Holds)
        {
            return new Rejected(request.Key, ApprovalError(context.Approval, recipe));
        }

        if (request.Recipe.Id != recipe.Id)
        {
            return new Rejected(request.Key, WorkflowErrors.RecipeNotFound.With(
                $"the start pins {request.Recipe.Id.Value}, not {recipe.Id.Value} (fix: start it from the recipe it pins)"));
        }

        var entering = Enter(new Scope(rules, context, recipe, request.Actor), Seed(request, recipe, context.PooledBudget), recipe.Initial);
        if (entering.IsError)
        {
            return new Rejected(request.Key, entering.Error);
        }

        var entered = entering.Value;
        var taking = request.TakenOver ? rules.TakeOver(entered.After, EngineActor.Name) : entered.After;
        if (taking.IsError)
        {
            return new Rejected(request.Key, taking.Error);
        }

        var row = taking.Value;
        var started = new WorkflowEvent(
            WorkflowEventType.Started, row.Id, new TriggerRecord(StartKind, request.Key, request.Actor, Outcome: null),
            row, rules.Delta(before: null, row), [], [], Grant: null, Override: null, Refusal: null, Effects: []);
        return new Applied(
            request.Key,
            [new InstanceChange(row, started), .. entered.Children],
            entered.Effects,
            request.TakenOver ? [.. entered.FollowUps.Where(f => f.Instance != row.Id)] : entered.FollowUps);
    }

    /// <summary>The instance before its first entry: entry 0 in the initial state, so the entry makes it entry 1.</summary>
    private static WorkflowInstance Seed(StartRequest request, Recipe recipe, bool pooledBudget) => new(
        request.Id,
        request.Address,
        request.Tenant,
        request.Ring,
        request.Recipe,
        request.Item,
        request.Attempt,
        request.Parent,
        new Frame(recipe.Initial, Entry: 0, Run: null, Child: null),
        Suspension: null,
        PendingPark: null,
        new Dictionary<string, LoopState>(StringComparer.Ordinal),
        pooledBudget ? TokenSpend.None : TokenSpend.PerRound,
        request.Inputs,
        Outcome: null);
}
