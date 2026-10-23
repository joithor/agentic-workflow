using System.Globalization;
using Aw.Sindri.Core.Ids;
using Aw.Sindri.Core.Workflow.Engine.Instances;
using Aw.Sindri.Core.Workflow.Ids;
using Aw.Sindri.Core.Workflow.Views;

namespace Aw.Sindri.Core.Workflow.Engine.Runtime;

/// <summary>The active state's step declaration, the gates on its exits and the desk form (API spec §3.8 'current').</summary>
internal static class CurrentStates
{
    /// <summary>The step: 'active' for a state that runs a Step block, 'wait' for one waiting on a child or a person; null for a terminal state.</summary>
    public static StepDeclaration? Step(WorkflowInstance instance, InterpretedRecipe interpreted)
    {
        if (!interpreted.Recipe.States.TryGetValue(instance.Frame.State, out var state))
        {
            return null;
        }

        return state switch
        {
            { Step: { } step } => new StepDeclaration("active", step.Use.Value, step.Use.Value),
            { WaitFor.Child: { } child } => new StepDeclaration("wait", child.Recipe.Value, "one child per packet task"),
            { WaitFor: not null } => new StepDeclaration("wait", instance.Frame.State, "waits for an answer"),
            _ => null,
        };
    }

    /// <summary>
    /// What each Step state reads (the Step block descriptor's inputs) and produces (the facts of the transitions leaving the state, once each, as the
    /// instance already holds them in <paramref name="facts"/>; a fact not recorded yet carries only its key), in the recipe's state order.
    /// </summary>
    public static IReadOnlyList<StateStep> Steps(InterpretedRecipe interpreted, IReadOnlyList<FactRow> facts) =>
    [
        .. interpreted.Recipe.States
            .Where(static s => s.Value.Step is not null)
            .Select(s => new StateStep(
                s.Key,
                [.. (interpreted.Catalog.Find(s.Value.Step!.Use)?.Inputs ?? []).Select(static k => k.Value)],   // why: the Where above keeps only states with a Step
                [.. interpreted.Recipe.Transitions
                    .Where(t => t.From.Contains(s.Key, StringComparer.Ordinal))
                    .SelectMany(static t => t.Produces)
                    .Select(static p => p.Fact.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Select(key => facts.FirstOrDefault(f => string.Equals(f.Key, key, StringComparison.Ordinal))?.Ref is { } held ? held with { FactKey = key } : new FactRef(Name: null, Size: null, Hash: null, Path: null, ItemId: null, Url: null, Host: null, Title: null, FactKey: key))])),
    ];

    /// <summary>The gates on the transitions leaving the active state, once each.</summary>
    public static IReadOnlyList<GateLine> Gates(WorkflowInstance instance, InterpretedRecipe interpreted) =>
    [
        .. interpreted.Recipe.Transitions
            .Where(t => t.From.Contains(instance.Frame.State, StringComparer.Ordinal))
            .SelectMany(static t => t.Gates)
            .DistinctBy(static g => g.Use)
            .Select(static g => new GateLine(g.Use, "exit", g.Threshold, g.Threshold is { } at ? string.Create(CultureInfo.InvariantCulture, $"{g.Use.Value} passes at {at} or above.") : $"{g.Use.Value} passes.")),
    ];

    /// <summary>The desk form of the open task: its run, the outputs required on the outcome the form's command reports (API spec §3.8 'Desk runtime until P8'), the checks the engine runs on a report and the action that completes it.</summary>
    public static DeskForm? Desk(DeskTask? task, InterpretedRecipe interpreted, string? recommendation = null) => task is null
        ? null
        : new DeskForm(
            task.Run,
            [.. task.Outputs.Where(o => DeskChecks.Applies(task, o)).Select(o => new DeskOutput(o.Fact.Value, FactTypes.Parse(TypeOf(interpreted, o.Fact)), Required: true))],
            [.. task.Outputs.Where(o => DeskChecks.Applies(task, o)).Select(o => DeskChecks.Id(interpreted, o.Fact))],
            ActionId.From("CompleteStep"),
            Recommendations.Of(recommendation, task.Outcomes),
            Reviews(task, interpreted));

    /// <summary>What the Step's block declares its desk reviews, as the form lists it (path and url are filled at read time); null when the block declares none.</summary>
    private static List<DeskReview>? Reviews(DeskTask task, InterpretedRecipe interpreted) =>
        interpreted.Catalog.Find(task.Step)?.Reviews is { Count: > 0 } declared
            ? [.. declared.Select(static r => new DeskReview(r.Fact.Value, r.Kind))]
            : null;

    private static string? TypeOf(InterpretedRecipe interpreted, FactKey fact) => interpreted.Recipe.Outputs.FirstOrDefault(r => r.Key == fact)?.Type ?? Recipes.FactTypes.Default;
}
